using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Persistence;

public interface ILegalDocumentCorpusRepository
{
    Task<DecisionRetrievalArchitectureSettings> GetRetrievalArchitectureSettingsAsync(
        CancellationToken cancellationToken = default);

    Task MarkProcessingFailedAsync(
        Guid tenantId,
        Guid userId,
        Guid documentVersionId,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetDocumentMatterIdAsync(
        Guid tenantId,
        Guid documentVersionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DecisionRetrievalTelemetryDto>> GetRetrievalTelemetryAsync(
        Guid tenantId,
        Guid? matterId,
        Guid? decisionSessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LegalMatterContextItem>> SearchRoutedMatterContextAsync(
        Guid tenantId,
        Guid matterId,
        string query,
        IReadOnlyCollection<string> documentTypeCodes,
        int maximumItems,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LegalDocumentDto>> GetMatterDocumentsAsync(
        Guid tenantId,
        Guid matterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LegalDocumentPassageDto>> GetDocumentPassagesAsync(
        Guid tenantId,
        Guid documentVersionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LegalMatterContextItem>> SearchMatterContextAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        string query,
        int maximumItems,
        int maximumCharacters,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LegalMatterContextItem>> SearchLegacyProjectionAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        string query,
        int maximumItems,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateDocumentAsync(
        Guid documentId,
        LegalDocumentIntakeRequest request,
        string sha256Hash,
        string storageReference,
        string malwareStatusCode,
        CancellationToken cancellationToken = default);

    Task SaveExtractionAsync(
        Guid tenantId,
        Guid userId,
        Guid documentVersionId,
        string correlationId,
        DocumentExtractionResult extraction,
        IReadOnlyCollection<LegalDocumentPassageDto> passages,
        CancellationToken cancellationToken = default);

    Task SaveSemanticProposalAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        Guid documentId,
        Guid documentVersionId,
        LegalDocumentSemanticProposal proposal,
        CancellationToken cancellationToken = default);

    Task PersistRetrievalTelemetryAsync(
        Guid tenantId,
        Guid userId,
        DecisionRetrievalTelemetry telemetry,
        CancellationToken cancellationToken = default);
}
