using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Intelligence.Decision;

// Activates prepared corpus documents on the Disambiguate & Answer path. Upload persists documents in a
// prepare-only state (extracted text, no derived propositions). This service enriches the pending
// versions (Stage 1 semantic interpretation) and runs Continuous Decision Integrity for each, one
// bounded batch per call so the UI can poll and render observable progress.
public sealed class LegalMatterCorpusActivationService(
    ILegalDocumentCorpusRepository corpusRepository,
    ILegalDocumentSemanticInterpreter semanticInterpreter,
    IMatterChangeProcessor matterChangeProcessor,
    IDecisionIntegrityRepository integrityRepository,
    ILegalDecisionRepository decisionRepository,
    IDomainPackResolver domainPackResolver,
    IAiProviderRouter aiRouter,
    ILogger<LegalMatterCorpusActivationService> logger) : ILegalMatterCorpusActivationService
{
    // Feature code that resolves the EMBEDDING route/model deployment (seeded in migration 0354).
    private const string EmbeddingFeatureCode = "LEGAL_DOCUMENT_PASSAGE_EMBEDDING";
    private const string EmbeddingModelCode = "text-embedding-3-small";
    // Bounded per-call embedding work so activation stays observable and cost-controlled.
    private const int EmbeddingPassageBatchLimit = 32;
    public Task<LegalMatterCorpusActivationStatus> GetStatusAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
        => corpusRepository.GetMatterActivationStatusAsync(tenantId, matterId, cancellationToken);

    public async Task<LegalMatterCorpusActivationStatus> ActivateAsync(Guid tenantId, Guid userId, Guid matterId, string? modelCode, int batchSize, CancellationToken cancellationToken = default)
    {
        var settings = await corpusRepository.GetRetrievalArchitectureSettingsAsync(cancellationToken);
        var pending = await corpusRepository.GetPendingCorpusVersionsAsync(
            tenantId, matterId, Math.Clamp(batchSize, 1, 25), cancellationToken);

        var activated = 0;
        // Per-document real-time detail for the batch, so the UI can name the actual file and describe
        // exactly what was done to it instead of a bare "N of M" count.
        var batchDetails = new List<LegalCorpusActivationDocumentDetail>(pending.Count);
        var correlationId = Guid.NewGuid().ToString("N");
        // Domain-pack concepts are resolved once per distinct pack code and reused across versions. When a
        // document carries a Domain Pack, its concepts are passed to the interpreter so extracted evidence is
        // concept-bound (dimension + verification profile) exactly like the decision path. Documents without a
        // pack activate with unbound evidence (the interpreter only enforces concept binding when concepts exist).
        var conceptsByPack = new Dictionary<string, IReadOnlyCollection<DecisionDomainConceptDto>>(StringComparer.OrdinalIgnoreCase);
        // Continuous Decision Integrity only has meaning once a prior decision snapshot exists — its job is
        // to detect whether a changed document disturbs an already-made decision. On first-time preparation
        // (no snapshot yet) it would be a pure no-op, so skip it entirely and defer CDI to the document
        // change/upload path that runs after a decision has been produced.
        var hasDecisionSnapshot = await integrityRepository.GetLatestMatterSnapshotAsync(tenantId, matterId, cancellationToken) is not null;
        foreach (var version in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var passagesRead = 0;
            var evidenceExtracted = 0;
            var conceptBound = false;
            try
            {
                if (settings.Stage1SemanticEnrichmentEnabled)
                {
                    var passages = await corpusRepository.GetDocumentPassagesAsync(
                        tenantId, version.LegalDocumentVersionId, cancellationToken);
                    passagesRead = passages.Count;
                    if (passages.Count > 0)
                    {
                        var domainConcepts = await ResolveDomainConceptsAsync(
                            tenantId, version.DomainPackCode, conceptsByPack, cancellationToken);
                        conceptBound = domainConcepts.Count > 0;
                        var resolvedPack = await domainPackResolver.ResolveAsync(tenantId, version.DomainPackCode, cancellationToken);
                        var proposal = await semanticInterpreter.InterpretAsync(
                            tenantId, matterId, version.LegalDocumentId, version.LegalDocumentVersionId,
                            version.DomainPackCode, domainConcepts, passages, correlationId, modelCode, resolvedPack, cancellationToken);
                        evidenceExtracted = proposal.EvidenceItems.Count;
                        await corpusRepository.SaveSemanticProposalAsync(
                            tenantId, userId, matterId, version.LegalDocumentId,
                            version.LegalDocumentVersionId, proposal, cancellationToken);

                        if (proposal.DomainEntities.Count > 0 || proposal.DomainEvents.Count > 0)
                        {
                            var entities = proposal.DomainEntities
                                .Select(entity => new DocumentDomainEntityPersistence(
                                    Guid.NewGuid(), matterId, version.LegalDocumentId, version.LegalDocumentVersionId,
                                    entity.PassageId, resolvedPack.PackCode, entity.EntityTypeCode, entity.DimensionCode,
                                    entity.EntityText, entity.NormalizedValue, entity.Confidence,
                                    modelCode, correlationId, tenantId, userId))
                                .ToArray();
                            var events = proposal.DomainEvents
                                .Select(ev => new DocumentDomainEventPersistence(
                                    Guid.NewGuid(), matterId, version.LegalDocumentId, version.LegalDocumentVersionId,
                                    ev.PassageId, resolvedPack.PackCode, ev.EventTypeCode, ev.DimensionCode,
                                    ev.Summary, ev.EventDateUtc, ev.Confidence,
                                    modelCode, correlationId, tenantId, userId))
                                .ToArray();
                            await corpusRepository.SaveDocumentDomainExtractionAsync(
                                tenantId, userId, matterId, version.LegalDocumentId,
                                version.LegalDocumentVersionId,
                                string.IsNullOrWhiteSpace(resolvedPack.PackCode) ? version.DomainPackCode : resolvedPack.PackCode,
                                entities, events, cancellationToken);
                        }
                    }
                }

                // Continuous Decision Integrity — evaluate the newly activated document for material impact
                // on existing conclusions. Only meaningful when a decision snapshot already exists; on the
                // first-ever preparation there is nothing to impact, so it is skipped.
                if (hasDecisionSnapshot)
                {
                    await matterChangeProcessor.ProcessDocumentChangeAsync(
                        tenantId, userId, matterId, version.LegalDocumentId, version.LegalDocumentVersionId,
                        version.Sha256Hash, version.FileName, DateTime.UtcNow, cancellationToken);
                }

                // Hybrid semantic scoring — generate and persist passage embeddings so retrieval can match
                // by vector similarity in addition to exact-hash/keyword overlap. Gated by a DB-backed flag
                // and best-effort: an embedding failure never blocks activation (keyword/exact scoring
                // remains fully functional without embeddings).
                if (settings.HybridSemanticScoringEnabled)
                {
                    await GeneratePassageEmbeddingsAsync(tenantId, version.LegalDocumentVersionId, correlationId, cancellationToken);
                }

                activated++;
                batchDetails.Add(new LegalCorpusActivationDocumentDetail(
                    version.LegalDocumentId, version.LegalDocumentVersionId, version.FileName,
                    "ENRICHED", "Extracting evidence", passagesRead, evidenceExtracted,
                    conceptBound, version.DomainPackCode, Succeeded: true));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Corpus activation failed for document version {VersionId}; will retry on next activation pass.", version.LegalDocumentVersionId);
                batchDetails.Add(new LegalCorpusActivationDocumentDetail(
                    version.LegalDocumentId, version.LegalDocumentVersionId, version.FileName,
                    "FAILED", "Activation failed", passagesRead, evidenceExtracted,
                    conceptBound, version.DomainPackCode, Succeeded: false));
            }
        }

        var status = await corpusRepository.GetMatterActivationStatusAsync(tenantId, matterId, cancellationToken);
        return status with { ActivatedThisCall = activated, BatchDetails = batchDetails };
    }

    // Resolves (and caches per pack code) the Domain Pack concepts for a document. Returns an empty set when
    // the document has no pack or the pack cannot be loaded, in which case the interpreter admits evidence
    // without requiring concept binding. Best-effort: a lookup failure never blocks activation.
    private async Task<IReadOnlyCollection<DecisionDomainConceptDto>> ResolveDomainConceptsAsync(
        Guid tenantId,
        string? domainPackCode,
        Dictionary<string, IReadOnlyCollection<DecisionDomainConceptDto>> cache,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(domainPackCode))
            return [];
        if (cache.TryGetValue(domainPackCode, out var cached))
            return cached;

        IReadOnlyCollection<DecisionDomainConceptDto> concepts = [];
        try
        {
            var pack = await decisionRepository.GetDomainPackAsync(tenantId, domainPackCode, cancellationToken);
            concepts = pack?.Concepts ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to resolve Domain Pack {DomainPackCode}; activating without concept binding.", domainPackCode);
        }

        cache[domainPackCode] = concepts;
        return concepts;
    }

    // Generates embeddings for up to EmbeddingPassageBatchLimit passages of a version that still lack one,
    // then persists each vector as JSON. One embedding request per passage keeps result-to-passage mapping
    // unambiguous. Best-effort: any failure is logged and swallowed so activation is never blocked.
    private async Task GeneratePassageEmbeddingsAsync(Guid tenantId, Guid documentVersionId, string correlationId, CancellationToken cancellationToken)
    {
        var candidates = await corpusRepository.GetPassagesMissingEmbeddingAsync(tenantId, documentVersionId, cancellationToken);
        if (candidates.Count == 0)
        {
            return;
        }

        foreach (var candidate in candidates.Take(EmbeddingPassageBatchLimit))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await aiRouter.CreateEmbeddingAsync(
                    tenantId, EmbeddingFeatureCode, [candidate.PassageText], correlationId, cancellationToken);
                var vector = result.Embeddings.FirstOrDefault();
                if (vector.Length == 0)
                {
                    continue;
                }

                var embeddingJson = JsonSerializer.Serialize(vector.ToArray());
                await corpusRepository.SavePassageEmbeddingAsync(
                    tenantId, candidate.LegalDocumentPassageId, embeddingJson, EmbeddingModelCode, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Passage embedding generation failed for passage {PassageId}; retrieval will fall back to keyword scoring until the next activation pass.", candidate.LegalDocumentPassageId);
            }
        }
    }
}
