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
        var correlationId = Guid.NewGuid().ToString("N");
        // Continuous Decision Integrity only has meaning once a prior decision snapshot exists — its job is
        // to detect whether a changed document disturbs an already-made decision. On first-time preparation
        // (no snapshot yet) it would be a pure no-op, so skip it entirely and defer CDI to the document
        // change/upload path that runs after a decision has been produced.
        var hasDecisionSnapshot = await integrityRepository.GetLatestMatterSnapshotAsync(tenantId, matterId, cancellationToken) is not null;
        foreach (var version in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (settings.Stage1SemanticEnrichmentEnabled)
                {
                    var passages = await corpusRepository.GetDocumentPassagesAsync(
                        tenantId, version.LegalDocumentVersionId, cancellationToken);
                    if (passages.Count > 0)
                    {
                        var proposal = await semanticInterpreter.InterpretAsync(
                            tenantId, matterId, version.LegalDocumentId, version.LegalDocumentVersionId,
                            null, [], passages, correlationId, modelCode, cancellationToken);
                        await corpusRepository.SaveSemanticProposalAsync(
                            tenantId, userId, matterId, version.LegalDocumentId,
                            version.LegalDocumentVersionId, proposal, cancellationToken);
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
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Corpus activation failed for document version {VersionId}; will retry on next activation pass.", version.LegalDocumentVersionId);
            }
        }

        var status = await corpusRepository.GetMatterActivationStatusAsync(tenantId, matterId, cancellationToken);
        return status with { ActivatedThisCall = activated };
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
