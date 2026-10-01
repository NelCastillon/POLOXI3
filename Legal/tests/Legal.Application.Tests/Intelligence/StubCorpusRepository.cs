using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Tests.Intelligence;

// Throwing stub for the large ILegalDocumentCorpusRepository surface. Tests override only the members
// they exercise (GetMatterEvidenceGraphAsync); every other member throws so accidental use is obvious.
public abstract class StubCorpusRepository : ILegalDocumentCorpusRepository
{
    private static NotSupportedException NotUsed([System.Runtime.CompilerServices.CallerMemberName] string member = "")
        => new($"{member} is not used by these tests.");

    public virtual Task<LegalMatterEvidenceGraphDto> GetMatterEvidenceGraphAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw NotUsed();

    public Task<DecisionRetrievalArchitectureSettings> GetRetrievalArchitectureSettingsAsync(CancellationToken cancellationToken = default) => throw NotUsed();
    public Task MarkProcessingFailedAsync(Guid tenantId, Guid userId, Guid documentVersionId, string errorCode, string errorMessage, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<Guid?> GetDocumentMatterIdAsync(Guid tenantId, Guid documentVersionId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<IReadOnlyCollection<DecisionRetrievalTelemetryDto>> GetRetrievalTelemetryAsync(Guid tenantId, Guid? matterId, Guid? decisionSessionId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<IReadOnlyCollection<LegalMatterContextItem>> SearchRoutedMatterContextAsync(Guid tenantId, Guid matterId, string query, IReadOnlyCollection<string> documentTypeCodes, int maximumItems, IReadOnlyCollection<float>? queryEmbedding = null, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<IReadOnlyCollection<LegalDocumentDto>> GetMatterDocumentsAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<IReadOnlyCollection<LegalDocumentPassageDto>> GetDocumentPassagesAsync(Guid tenantId, Guid documentVersionId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<IReadOnlyCollection<LegalPassageEmbeddingCandidate>> GetPassagesMissingEmbeddingAsync(Guid tenantId, Guid documentVersionId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task SavePassageEmbeddingAsync(Guid tenantId, Guid legalDocumentPassageId, string embeddingJson, string embeddingModelCode, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<LegalMatterDocumentPurgeResult> PurgeMatterDocumentEvidenceAsync(Guid tenantId, Guid userId, Guid matterId, LegalMatterDocumentPurgeRequest request, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<Guid> AppendSourceAssertionAsync(Guid tenantId, Guid userId, LegalSourceAssertionCreateRequest request, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<LegalMatterCorpusActivationStatus> GetMatterActivationStatusAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<IReadOnlyCollection<LegalPendingCorpusVersion>> GetPendingCorpusVersionsAsync(Guid tenantId, Guid matterId, int maximumVersions, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<int> GenerateRandomTestCorpusAsync(Guid tenantId, Guid userId, Guid matterId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<IReadOnlyCollection<LegalMatterContextItem>> SearchMatterContextAsync(Guid tenantId, Guid userId, Guid matterId, string query, int maximumItems, int maximumCharacters, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<IReadOnlyCollection<LegalMatterContextItem>> SearchLegacyProjectionAsync(Guid tenantId, Guid userId, Guid matterId, string query, int maximumItems, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<Guid> CreateDocumentAsync(Guid documentId, LegalDocumentIntakeRequest request, string sha256Hash, string storageReference, string malwareStatusCode, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<Guid?> FindActiveDocumentByHashAsync(Guid tenantId, Guid matterId, string sha256Hash, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task PurgeDocumentAsync(Guid tenantId, Guid userId, Guid documentId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task SaveExtractionAsync(Guid tenantId, Guid userId, Guid documentVersionId, string correlationId, DocumentExtractionResult extraction, IReadOnlyCollection<LegalDocumentPassageDto> passages, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task SaveSemanticProposalAsync(Guid tenantId, Guid userId, Guid matterId, Guid documentId, Guid documentVersionId, LegalDocumentSemanticProposal proposal, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task SaveDocumentDomainExtractionAsync(Guid tenantId, Guid userId, Guid matterId, Guid documentId, Guid documentVersionId, string? domainPackCode, IReadOnlyCollection<DocumentDomainEntityPersistence> entities, IReadOnlyCollection<DocumentDomainEventPersistence> events, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<IReadOnlyCollection<DocumentDomainEntityDto>> GetMatterDomainEntitiesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<IReadOnlyCollection<DocumentDomainEventDto>> GetMatterDomainEventsAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task PersistRetrievalTelemetryAsync(Guid tenantId, Guid userId, DecisionRetrievalTelemetry telemetry, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<Guid> CreateUploadBatchAsync(Guid tenantId, Guid userId, Guid matterId, StartUploadBatchCommand command, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<IReadOnlyCollection<LegalUploadBatchDto>> GetUploadBatchesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<LegalUploadOperationLookup?> FindUploadOperationAsync(Guid tenantId, Guid matterId, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<Guid> RecordUploadOperationAsync(Guid tenantId, Guid userId, Guid matterId, Guid? batchId, string idempotencyKey, string requestFingerprint, string fileName, string? declaredContentType, long expectedLength, Guid resultDocumentId, Guid resultDocumentVersionId, bool contentReused, string sha256Hash, string? correlationId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<Guid> CreateEvidenceOccurrenceAsync(Guid tenantId, Guid userId, Guid matterId, Guid documentId, Guid documentVersionId, Guid? batchId, Guid? uploadOperationId, bool contentReused, EvidenceSourceDescriptor source, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<IReadOnlyCollection<LegalEvidenceOccurrenceDto>> GetEvidenceOccurrencesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task IncrementUploadBatchCountersAsync(Guid tenantId, Guid batchId, UploadBatchCounterDelta delta, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task SetUploadBatchDiscoveredAsync(Guid tenantId, Guid batchId, int filesDiscovered, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task CloseUploadBatchAsync(Guid tenantId, Guid userId, Guid batchId, string statusCode, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<LegalProcessingOperationLookup> AcquireProcessingOperationAsync(Guid tenantId, Guid userId, LegalProcessingOperationRequest request, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task CompleteProcessingOperationAsync(Guid tenantId, Guid userId, Guid processingOperationId, string? resultEntityTypeCode, Guid? resultEntityId, string? resultHash, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task FailProcessingOperationAsync(Guid tenantId, Guid userId, Guid processingOperationId, string errorCode, string errorMessage, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<Guid> CreateDocumentFamilyAsync(Guid tenantId, Guid userId, Guid matterId, string? familyLabel, string containerTypeCode, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task LinkOccurrenceToFamilyAsync(Guid tenantId, Guid userId, Guid occurrenceId, Guid familyId, Guid? parentOccurrenceId, int familyDepth, int familyOrdinal, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<Guid> CreateEvidenceLineageGroupAsync(Guid tenantId, Guid userId, Guid matterId, string? lineageLabel, string? originDescription, string independenceBasisCode, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<Guid> AddEvidenceLineageMemberAsync(Guid tenantId, Guid userId, Guid lineageGroupId, Guid occurrenceId, string roleCode, string? derivationNote, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<IReadOnlyCollection<LegalEvidenceLineageGroupDto>> GetEvidenceLineageAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw NotUsed();
}
