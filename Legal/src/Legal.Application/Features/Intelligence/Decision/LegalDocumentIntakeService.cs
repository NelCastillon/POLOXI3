using System.Security.Cryptography;
using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Intelligence.Decision;

public sealed class LegalDocumentIntakeService(
    ILegalDocumentBinaryStore binaryStore,
    ILegalDocumentIntakeValidator intakeValidator,
    ILegalDocumentSecurityScanner securityScanner,
    ILegalDocumentExtractionRouter extractionRouter,
    ILegalDocumentSemanticInterpreter semanticInterpreter,
    ILegalDocumentCorpusRepository corpusRepository,
    IDomainPackResolver domainPackResolver,
    IMatterChangeProcessor matterChangeProcessor,
    ILogger<LegalDocumentIntakeService> logger) : ILegalDocumentIntakeService
{
    public async Task<LegalDocumentDto> IngestAsync(LegalDocumentIntakeRequest request, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
            throw new ArgumentException("The document content stream must be readable.", nameof(content));

        await using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, cancellationToken);
        if (buffered.Length != request.FileSizeBytes)
            throw new InvalidDataException("The received document size does not match the intake request.");

        buffered.Position = 0;
        await intakeValidator.ValidateAsync(request, buffered, cancellationToken);

        buffered.Position = 0;
        var sha256Hash = Convert.ToHexString(await SHA256.HashDataAsync(buffered, cancellationToken));
        buffered.Position = 0;

        // Request idempotency: when the client supplies an IdempotencyKey, a retried upload of the same
        // file returns the same result instead of creating a duplicate evidence intake. The request
        // fingerprint is the physical content hash + declared size, so reusing a key for a DIFFERENT file
        // is rejected by the repository (IDEMPOTENCY_KEY_REUSED).
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var fingerprint = $"{sha256Hash}:{request.FileSizeBytes}";
            var existingOperation = await corpusRepository.FindUploadOperationAsync(
                request.TenantId, request.MatterId, request.IdempotencyKey, fingerprint, cancellationToken);
            if (existingOperation is { } completed)
            {
                return (await corpusRepository.GetMatterDocumentsAsync(request.TenantId, request.MatterId, cancellationToken))
                    .Single(item => item.LegalDocumentId == completed.ResultLegalDocumentId);
            }
        }

        // Deduplication: if an identical file (same content hash) already exists for this matter and was
        // not a failed intake, reuse the existing physical content instead of creating a duplicate corpus
        // entry. Legal provenance is NEVER collapsed — a new evidence occurrence is still recorded so the
        // same bytes arriving via a different production/custodian remain a distinct, admissible source.
        var existingDocumentId = await corpusRepository.FindActiveDocumentByHashAsync(
            request.TenantId, request.MatterId, sha256Hash, cancellationToken);
        if (existingDocumentId is { } duplicateId)
        {
            var duplicate = (await corpusRepository.GetMatterDocumentsAsync(request.TenantId, request.MatterId, cancellationToken))
                .Single(item => item.LegalDocumentId == duplicateId);
            var duplicateVersion = duplicate.Versions.OrderByDescending(item => item.VersionNumber).First();
            await RecordProvenanceAsync(request, duplicateId, duplicateVersion.LegalDocumentVersionId,
                sha256Hash, contentReused: true, cancellationToken);
            return duplicate;
        }

        var documentId = Guid.NewGuid();
        var scanResult = await securityScanner.ScanAsync(request.FileName, request.ContentType, buffered, cancellationToken);
        var malwareStatus = scanResult.StatusCode;
        if (!IsClean(malwareStatus))
        {
            await corpusRepository.CreateDocumentAsync(
                documentId, request, sha256Hash,
                scanResult.QuarantineReference ?? "quarantine:reference-unavailable",
                malwareStatus, cancellationToken);
            throw new InvalidDataException($"Document security validation failed with status '{malwareStatus}'.");
        }

        buffered.Position = 0;
        var storageReference = await binaryStore.StoreImmutableAsync(request.TenantId, documentId, request.FileName, buffered, cancellationToken);
        await corpusRepository.CreateDocumentAsync(documentId, request, sha256Hash, storageReference, malwareStatus, cancellationToken);
        var document = (await corpusRepository.GetMatterDocumentsAsync(request.TenantId, request.MatterId, cancellationToken))
            .Single(item => item.LegalDocumentId == documentId);
        var version = document.Versions.Single(item => item.VersionNumber == 1);

        try
        {
            // Deterministic processing-operation ledger for the EXTRACT effect. New content = new input
            // identity, so this normally records a fresh operation; a reprocess of identical input +
            // processor version would reuse it. Fail-soft: any ledger error never blocks extraction.
            Guid? extractOperationId = null;
            try
            {
                var acquired = await corpusRepository.AcquireProcessingOperationAsync(request.TenantId, request.UserId,
                    new LegalProcessingOperationRequest
                    {
                        OperationTypeCode = "EXTRACT",
                        MatterId = request.MatterId,
                        InputEntityTypeCode = "DOCUMENT_VERSION",
                        InputEntityId = version.LegalDocumentVersionId,
                        InputVersion = version.VersionNumber,
                        InputHash = sha256Hash,
                        ProcessorCode = "ILegalDocumentExtractionRouter",
                        ProcessorVersion = "v1",
                        CorrelationId = request.CorrelationId
                    }, cancellationToken);
                extractOperationId = acquired.LegalProcessingOperationId;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Processing-operation ledger unavailable for EXTRACT of {DocumentId}; running effect directly.", documentId);
            }

            buffered.Position = 0;
            var extractionRequest = new DocumentExtractionRequest(
                request.TenantId, documentId, version.LegalDocumentVersionId, request.FileName,
                request.ContentType, buffered, request.CorrelationId);
            var extraction = await extractionRouter.ExtractAsync(extractionRequest, cancellationToken);

            var passages = CreatePassages(version.LegalDocumentVersionId, extraction);
            if (passages.Count == 0)
                throw new InvalidDataException("Document extraction produced no usable text passages.");
            await corpusRepository.SaveExtractionAsync(request.TenantId, request.UserId, version.LegalDocumentVersionId, request.CorrelationId, extraction, passages, cancellationToken);

            if (extractOperationId is { } completedOpId)
            {
                try
                {
                    await corpusRepository.CompleteProcessingOperationAsync(request.TenantId, request.UserId, completedOpId,
                        "DOCUMENT_VERSION", version.LegalDocumentVersionId, sha256Hash, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Failed to mark EXTRACT processing operation complete for {DocumentId}.", documentId);
                }
            }

            var settings = await corpusRepository.GetRetrievalArchitectureSettingsAsync(cancellationToken);
            if (!request.PrepareOnly && settings.Stage1SemanticEnrichmentEnabled)
            {
                // Stage 1 semantic enrichment is advisory: it augments retrieval but must never block or
                // fail the upload itself. If no AI model route is configured (or the model call fails),
                // the extracted document is preserved and enrichment is simply skipped.
                try
                {
                    var resolvedPack = await domainPackResolver.ResolveAsync(request.TenantId, request.DomainPackCode, cancellationToken);
                    var concepts = resolvedPack.Concepts;
                    var proposal = await semanticInterpreter.InterpretAsync(
                        request.TenantId, request.MatterId, documentId, version.LegalDocumentVersionId,
                        request.DomainPackCode, concepts, passages, request.CorrelationId, request.ModelCode, resolvedPack, cancellationToken);
                    await corpusRepository.SaveSemanticProposalAsync(
                        request.TenantId, request.UserId, request.MatterId, documentId,
                        version.LegalDocumentVersionId, proposal, cancellationToken);

                    // Explicit domain-specific entity/event extraction (migration 0373). Persisted as a
                    // separate, advisory store so the Decision Channels can resolve domain semantics by
                    // matter without re-reading the LLM. Fail-soft within the enrichment try/catch.
                    if (proposal.DomainEntities.Count > 0 || proposal.DomainEvents.Count > 0)
                    {
                        var entities = proposal.DomainEntities
                            .Select(entity => new DocumentDomainEntityPersistence(
                                Guid.NewGuid(), request.MatterId, documentId, version.LegalDocumentVersionId,
                                entity.PassageId, resolvedPack.PackCode, entity.EntityTypeCode, entity.DimensionCode,
                                entity.EntityText, entity.NormalizedValue, entity.Confidence,
                                request.ModelCode, request.CorrelationId, request.TenantId, request.UserId))
                            .ToArray();
                        var events = proposal.DomainEvents
                            .Select(ev => new DocumentDomainEventPersistence(
                                Guid.NewGuid(), request.MatterId, documentId, version.LegalDocumentVersionId,
                                ev.PassageId, resolvedPack.PackCode, ev.EventTypeCode, ev.DimensionCode,
                                ev.Summary, ev.EventDateUtc, ev.Confidence,
                                request.ModelCode, request.CorrelationId, request.TenantId, request.UserId))
                            .ToArray();
                        await corpusRepository.SaveDocumentDomainExtractionAsync(
                            request.TenantId, request.UserId, request.MatterId, documentId,
                            version.LegalDocumentVersionId,
                            string.IsNullOrWhiteSpace(resolvedPack.PackCode) ? request.DomainPackCode : resolvedPack.PackCode,
                            entities, events, cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Stage 1 semantic enrichment skipped for document {DocumentId}; extraction preserved.", documentId);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await corpusRepository.MarkProcessingFailedAsync(
                request.TenantId, request.UserId, version.LegalDocumentVersionId,
                ex.GetType().Name, ex.Message, CancellationToken.None);
            // A document that never produced usable content must not inflate the corpus. Remove the
            // orphaned document/version so failed uploads do not appear as retrievable matter documents.
            await corpusRepository.PurgeDocumentAsync(
                request.TenantId, request.UserId, documentId, CancellationToken.None);
            throw;
        }

        // Continuous Decision Integrity — fire the Matter Change Processor so a newly arrived document
        // is evaluated for material impact on existing conclusions. Fail-soft: change awareness must
        // never block or fail the upload itself; the change event is idempotent on the source version.
        // Skipped when PrepareOnly: activation on the Disambiguate & Answer path runs enrichment + CDC.
        if (!request.PrepareOnly)
        {
            try
            {
                await matterChangeProcessor.ProcessDocumentChangeAsync(
                    request.TenantId, request.UserId, request.MatterId, documentId, version.LegalDocumentVersionId,
                    sha256Hash, request.FileName, DateTime.UtcNow, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Continuous Decision Integrity change processing failed for document {DocumentId}; upload preserved.", documentId);
            }
        }

        // Record the legal provenance occurrence (and idempotency ledger) for this new physical content.
        await RecordProvenanceAsync(request, documentId, version.LegalDocumentVersionId, sha256Hash, contentReused: false, cancellationToken);

        return (await corpusRepository.GetMatterDocumentsAsync(request.TenantId, request.MatterId, cancellationToken))
            .Single(item => item.LegalDocumentId == documentId);
    }

    // Records the legal provenance layer for a completed intake: the request-idempotency ledger entry
    // (when a key was supplied) and a distinct EvidenceOccurrence preserving custodian/production/Bates
    // provenance. Fail-soft — provenance recording never blocks or fails the underlying evidence intake.
    private async Task RecordProvenanceAsync(LegalDocumentIntakeRequest request, Guid documentId, Guid documentVersionId, string sha256Hash, bool contentReused, CancellationToken cancellationToken)
    {
        try
        {
            var source = request.Source ?? new EvidenceSourceDescriptor { SourceTypeCode = "USER_UPLOAD" };

            // Record the request-idempotency ledger entry FIRST (when a key was supplied) so the evidence
            // occurrence can be linked to the exact upload operation that produced it (FK integrity).
            Guid? operationId = null;
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var fingerprint = $"{sha256Hash}:{request.FileSizeBytes}";
                operationId = await corpusRepository.RecordUploadOperationAsync(
                    request.TenantId, request.UserId, request.MatterId, request.UploadBatchId,
                    request.IdempotencyKey, fingerprint, request.FileName, request.ContentType,
                    request.FileSizeBytes, documentId, documentVersionId, contentReused, sha256Hash, request.CorrelationId, cancellationToken);
            }

            var occurrenceId = await corpusRepository.CreateEvidenceOccurrenceAsync(
                request.TenantId, request.UserId, request.MatterId, documentId, documentVersionId,
                request.UploadBatchId, operationId, contentReused, source, cancellationToken);

            if (request.UploadBatchId is { } batchId)
            {
                await corpusRepository.IncrementUploadBatchCountersAsync(request.TenantId, batchId, new UploadBatchCounterDelta(
                    FilesAccepted: 1,
                    ExactContentDuplicates: contentReused ? 1 : 0,
                    NewEvidenceOccurrences: 1,
                    ProcessingReused: contentReused ? 1 : 0), cancellationToken);
            }

            _ = occurrenceId;
            _ = operationId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not InvalidOperationException)
        {
            logger.LogWarning(ex, "Evidence provenance recording failed for document {DocumentId}; upload preserved.", documentId);
        }
    }

    private static IReadOnlyCollection<LegalDocumentPassageDto> CreatePassages(Guid versionId, DocumentExtractionResult extraction)
    {
        var passages = new List<LegalDocumentPassageDto>();
        var sequence = 0;
        // Running character offset into the document's extracted text. Every passage is anchored to a
        // traceable source span so evidence extracted from it is admissible. When the extraction provider
        // supplies its own span (e.g. Azure Document Intelligence offsets), that authoritative span is
        // preserved; otherwise a deterministic character-offset span is synthesized from the extracted text.
        var documentOffset = 0;
        foreach (var page in extraction.Pages.OrderBy(item => item.PageNumber))
        {
            if (page.Paragraphs.Count > 0)
            {
                foreach (var paragraph in page.Paragraphs.OrderBy(item => item.SequenceNumber))
                {
                    if (string.IsNullOrWhiteSpace(paragraph.Text))
                        continue;
                    var sourceSpanJson = paragraph.SourceSpanJson ?? BuildSourceSpanJson(documentOffset, paragraph.Text.Length);
                    passages.Add(new LegalDocumentPassageDto(
                        Guid.NewGuid(), versionId, page.PageNumber, paragraph.Role, ++sequence,
                        paragraph.Text, page.ExtractionMethodCode, paragraph.Confidence ?? page.Confidence,
                        paragraph.BoundingRegionJson, sourceSpanJson, LegalEvidenceStates.Proposed));
                    documentOffset += paragraph.Text.Length;
                }
            }
            else if (!string.IsNullOrWhiteSpace(page.Text))
            {
                passages.Add(new LegalDocumentPassageDto(
                    Guid.NewGuid(), versionId, page.PageNumber, null, ++sequence, page.Text,
                    page.ExtractionMethodCode, page.Confidence, null,
                    BuildSourceSpanJson(documentOffset, page.Text.Length), LegalEvidenceStates.Proposed));
                documentOffset += page.Text.Length;
            }
        }
        return passages;
    }

    // Serializes a character-offset source span ({ Offset, Length }) matching the shape emitted by the
    // Azure Document Intelligence provider, so downstream span-presence enforcement can admit the passage.
    private static string BuildSourceSpanJson(int offset, int length)
        => JsonSerializer.Serialize(new { Offset = offset, Length = Math.Max(1, length) });

    private static bool IsClean(string status) =>
        status.Equals("CLEAN", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("NOT_DETECTED", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("PASSED", StringComparison.OrdinalIgnoreCase);

}
