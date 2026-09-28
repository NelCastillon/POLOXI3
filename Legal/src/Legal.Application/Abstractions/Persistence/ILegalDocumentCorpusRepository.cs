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

    Task<LegalMatterEvidenceGraphDto> GetMatterEvidenceGraphAsync(
        Guid tenantId,
        Guid matterId,
        CancellationToken cancellationToken = default);
    // Enrichment/activation state for a matter: how many clean document versions have extracted text,
    // and how many of those still lack any derived evidence (i.e. are prepared but not yet activated).
    Task<LegalMatterCorpusActivationStatus> GetMatterActivationStatusAsync(
        Guid tenantId,
        Guid matterId,
        CancellationToken cancellationToken = default);

    // Returns up to <paramref name="maximumVersions"/> prepared-but-not-activated versions (extracted
    // text present, no evidence rows yet) so activation can enrich them in observable chunks.
    Task<IReadOnlyCollection<LegalPendingCorpusVersion>> GetPendingCorpusVersionsAsync(
        Guid tenantId,
        Guid matterId,
        int maximumVersions,
        CancellationToken cancellationToken = default);

    // evidence items → fact propositions → support edges) for a matter so the Document
    // Intelligence workspace renders real DB-backed content. Idempotent: no-op if documents exist.
    Task<int> GenerateRandomTestCorpusAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
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

    Task<Guid?> FindActiveDocumentByHashAsync(
        Guid tenantId,
        Guid matterId,
        string sha256Hash,
        CancellationToken cancellationToken = default);

    Task PurgeDocumentAsync(
        Guid tenantId,
        Guid userId,
        Guid documentId,
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
