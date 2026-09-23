using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Intelligence;

public interface IDocumentExtractionProvider
{
    Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILegalDocumentSearchProjectionDispatcher
{
    Task<int> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken = default);
}

public interface ILegalDocumentExtractionRouter
{
    Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILegalDocumentIntakeValidator
{
    Task ValidateAsync(
        LegalDocumentIntakeRequest request,
        Stream content,
        CancellationToken cancellationToken = default);
}

public interface ILegalDocumentBinaryStore
{
    Task<string> StoreImmutableAsync(
        Guid tenantId,
        Guid documentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);
}

public interface ILegalDocumentSecurityScanner
{
    Task<LegalDocumentSecurityScanResult> ScanAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);
}

public interface INativeDocumentTextProvider
{
    Task<DocumentExtractionResult?> TryExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILegalDocumentIntakeService
{
    Task<LegalDocumentDto> IngestAsync(
        LegalDocumentIntakeRequest request,
        Stream content,
        CancellationToken cancellationToken = default);
}

public interface ILegalDocumentSemanticInterpreter
{
    Task<LegalDocumentSemanticProposal> InterpretAsync(
        Guid tenantId,
        Guid matterId,
        Guid documentId,
        Guid documentVersionId,
        string? domainPackCode,
        IReadOnlyCollection<DecisionDomainConceptDto> domainConcepts,
        IReadOnlyCollection<LegalDocumentPassageDto> passages,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public interface ILegalMatterContextRetriever
{
    Task<LegalMatterContextResult> RetrieveAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        string query,
        DecisionRetrievalArchitectureSettings settings,
        CancellationToken cancellationToken = default);
}

public interface IDecisionResearchSourceRouter
{
    DecisionResearchRoute Route(
        DecisionResearchNeedPersistence researchNeed,
        string? jurisdiction,
        DateTime? authorityCutoffDate = null);
}
