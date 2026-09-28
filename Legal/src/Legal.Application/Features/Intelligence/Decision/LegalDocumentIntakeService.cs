using System.Security.Cryptography;
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
    ILegalDecisionRepository decisionRepository,
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

        // Deduplication: if an identical file (same content hash) already exists for this matter and was
        // not a failed intake, return the existing document instead of creating a duplicate corpus entry.
        var existingDocumentId = await corpusRepository.FindActiveDocumentByHashAsync(
            request.TenantId, request.MatterId, sha256Hash, cancellationToken);
        if (existingDocumentId is { } duplicateId)
        {
            return (await corpusRepository.GetMatterDocumentsAsync(request.TenantId, request.MatterId, cancellationToken))
                .Single(item => item.LegalDocumentId == duplicateId);
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
            buffered.Position = 0;
            var extractionRequest = new DocumentExtractionRequest(
                request.TenantId, documentId, version.LegalDocumentVersionId, request.FileName,
                request.ContentType, buffered, request.CorrelationId);
            var extraction = await extractionRouter.ExtractAsync(extractionRequest, cancellationToken);

            var passages = CreatePassages(version.LegalDocumentVersionId, extraction);
            if (passages.Count == 0)
                throw new InvalidDataException("Document extraction produced no usable text passages.");
            await corpusRepository.SaveExtractionAsync(request.TenantId, request.UserId, version.LegalDocumentVersionId, request.CorrelationId, extraction, passages, cancellationToken);

            var settings = await corpusRepository.GetRetrievalArchitectureSettingsAsync(cancellationToken);
            if (!request.PrepareOnly && settings.Stage1SemanticEnrichmentEnabled)
            {
                // Stage 1 semantic enrichment is advisory: it augments retrieval but must never block or
                // fail the upload itself. If no AI model route is configured (or the model call fails),
                // the extracted document is preserved and enrichment is simply skipped.
                try
                {
                    var pack = string.IsNullOrWhiteSpace(request.DomainPackCode)
                        ? null
                        : await decisionRepository.GetDomainPackAsync(request.TenantId, request.DomainPackCode, cancellationToken);
                    var proposal = await semanticInterpreter.InterpretAsync(
                        request.TenantId, request.MatterId, documentId, version.LegalDocumentVersionId,
                        request.DomainPackCode, pack?.Concepts ?? [], passages, request.CorrelationId, request.ModelCode, cancellationToken);
                    await corpusRepository.SaveSemanticProposalAsync(
                        request.TenantId, request.UserId, request.MatterId, documentId,
                        version.LegalDocumentVersionId, proposal, cancellationToken);
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

        return (await corpusRepository.GetMatterDocumentsAsync(request.TenantId, request.MatterId, cancellationToken))
            .Single(item => item.LegalDocumentId == documentId);
    }

    private static IReadOnlyCollection<LegalDocumentPassageDto> CreatePassages(Guid versionId, DocumentExtractionResult extraction)
    {
        var passages = new List<LegalDocumentPassageDto>();
        var sequence = 0;
        foreach (var page in extraction.Pages.OrderBy(item => item.PageNumber))
        {
            if (page.Paragraphs.Count > 0)
            {
                foreach (var paragraph in page.Paragraphs.OrderBy(item => item.SequenceNumber))
                {
                    if (string.IsNullOrWhiteSpace(paragraph.Text))
                        continue;
                    passages.Add(new LegalDocumentPassageDto(
                        Guid.NewGuid(), versionId, page.PageNumber, paragraph.Role, ++sequence,
                        paragraph.Text, page.ExtractionMethodCode, paragraph.Confidence ?? page.Confidence,
                        paragraph.BoundingRegionJson, paragraph.SourceSpanJson, LegalEvidenceStates.Proposed));
                }
            }
            else if (!string.IsNullOrWhiteSpace(page.Text))
            {
                passages.Add(new LegalDocumentPassageDto(
                    Guid.NewGuid(), versionId, page.PageNumber, null, ++sequence, page.Text,
                    page.ExtractionMethodCode, page.Confidence, null, null, LegalEvidenceStates.Proposed));
            }
        }
        return passages;
    }

    private static bool IsClean(string status) =>
        status.Equals("CLEAN", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("NOT_DETECTED", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("PASSED", StringComparison.OrdinalIgnoreCase);

}
