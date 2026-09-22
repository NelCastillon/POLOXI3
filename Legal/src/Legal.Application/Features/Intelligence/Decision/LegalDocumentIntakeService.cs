using System.Security.Cryptography;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Decision;

public sealed class LegalDocumentIntakeService(
    ILegalDocumentBinaryStore binaryStore,
    ILegalDocumentIntakeValidator intakeValidator,
    ILegalDocumentSecurityScanner securityScanner,
    ILegalDocumentExtractionRouter extractionRouter,
    ILegalDocumentSemanticInterpreter semanticInterpreter,
    ILegalDocumentCorpusRepository corpusRepository,
    ILegalDecisionRepository decisionRepository) : ILegalDocumentIntakeService
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
            if (settings.Stage1SemanticEnrichmentEnabled)
            {
                var pack = string.IsNullOrWhiteSpace(request.DomainPackCode)
                    ? null
                    : await decisionRepository.GetDomainPackAsync(request.TenantId, request.DomainPackCode, cancellationToken);
                var proposal = await semanticInterpreter.InterpretAsync(
                    request.TenantId, request.MatterId, documentId, version.LegalDocumentVersionId,
                    request.DomainPackCode, pack?.Concepts ?? [], passages, request.CorrelationId, cancellationToken);
                await corpusRepository.SaveSemanticProposalAsync(
                    request.TenantId, request.UserId, request.MatterId, documentId,
                    version.LegalDocumentVersionId, proposal, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await corpusRepository.MarkProcessingFailedAsync(
                request.TenantId, request.UserId, version.LegalDocumentVersionId,
                ex.GetType().Name, ex.Message, CancellationToken.None);
            throw;
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
