using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class LegalMatterContextRetrieverTests
{
    [Fact]
    public async Task DisabledStage_DoesNotQueryAnyCorpus()
    {
        var repository = new MatterContextRepositoryFake();
        var retriever = new LegalMatterContextRetriever(repository);

        var result = await retriever.RetrieveAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "query", Settings(enabled: false));

        Assert.False(result.Enabled);
        Assert.Equal("STAGE_DISABLED", result.NotRunReason);
        Assert.Equal(0, repository.AuthoritativeCalls);
        Assert.Equal(0, repository.LegacyCalls);
    }

    [Fact]
    public async Task AuthoritativeCorpusResult_PreventsLegacyFallback()
    {
        var item = Item(DecisionResearchRouteCodes.MatterCorpus);
        var repository = new MatterContextRepositoryFake { Authoritative = [item], Legacy = [Item(DecisionResearchRouteCodes.LegacyProjection)] };
        var retriever = new LegalMatterContextRetriever(repository);

        var result = await retriever.RetrieveAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "query", Settings(enabled: true));

        Assert.Equal(DecisionResearchRouteCodes.MatterCorpus, result.SourceRouteCode);
        Assert.Same(item, Assert.Single(result.Items));
        Assert.Equal(1, repository.AuthoritativeCalls);
        Assert.Equal(0, repository.LegacyCalls);
    }

    [Fact]
    public async Task EmptyAuthoritativeCorpus_UsesConfiguredLegacyFallback()
    {
        var legacy = Item(DecisionResearchRouteCodes.LegacyProjection);
        var repository = new MatterContextRepositoryFake { Legacy = [legacy] };
        var retriever = new LegalMatterContextRetriever(repository);

        var result = await retriever.RetrieveAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "query", Settings(enabled: true));

        Assert.Equal(DecisionResearchRouteCodes.LegacyProjection, result.SourceRouteCode);
        Assert.Same(legacy, Assert.Single(result.Items));
        Assert.Equal(1, repository.AuthoritativeCalls);
        Assert.Equal(1, repository.LegacyCalls);
    }

    private static DecisionRetrievalArchitectureSettings Settings(bool enabled) =>
        new(false, enabled, 12, 18000, true, true, false, true, false);

    private static LegalMatterContextItem Item(string extractionMethod) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null,
        "Document", "Grounded text", "source", 1, extractionMethod,
        LegalEvidenceStates.Proposed, LegalFactStates.Alleged, false, 0.8m, null, null);

    private sealed class MatterContextRepositoryFake : ILegalDocumentCorpusRepository
    {
        public IReadOnlyCollection<LegalMatterContextItem> Authoritative { get; init; } = [];
        public IReadOnlyCollection<LegalMatterContextItem> Legacy { get; init; } = [];
        public int AuthoritativeCalls { get; private set; }
        public int LegacyCalls { get; private set; }

        public Task<IReadOnlyCollection<LegalMatterContextItem>> SearchMatterContextAsync(Guid tenantId, Guid userId, Guid matterId, string query, int maximumItems, int maximumCharacters, CancellationToken cancellationToken = default)
        {
            AuthoritativeCalls++;
            return Task.FromResult(Authoritative);
        }

        public Task<IReadOnlyCollection<LegalMatterContextItem>> SearchLegacyProjectionAsync(Guid tenantId, Guid userId, Guid matterId, string query, int maximumItems, CancellationToken cancellationToken = default)
        {
            LegacyCalls++;
            return Task.FromResult(Legacy);
        }

        public Task<DecisionRetrievalArchitectureSettings> GetRetrievalArchitectureSettingsAsync(CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<IReadOnlyCollection<LegalDocumentDto>> GetMatterDocumentsAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<IReadOnlyCollection<LegalDocumentPassageDto>> GetDocumentPassagesAsync(Guid tenantId, Guid documentVersionId, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<LegalMatterEvidenceGraphDto> GetMatterEvidenceGraphAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<Guid> AppendSourceAssertionAsync(Guid tenantId, Guid userId, LegalSourceAssertionCreateRequest request, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<LegalMatterCorpusActivationStatus> GetMatterActivationStatusAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<IReadOnlyCollection<LegalPendingCorpusVersion>> GetPendingCorpusVersionsAsync(Guid tenantId, Guid matterId, int maximumVersions, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<int> GenerateRandomTestCorpusAsync(Guid tenantId, Guid userId, Guid matterId, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<IReadOnlyCollection<DecisionRetrievalTelemetryDto>> GetRetrievalTelemetryAsync(Guid tenantId, Guid? matterId, Guid? decisionSessionId, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<Guid?> GetDocumentMatterIdAsync(Guid tenantId, Guid documentVersionId, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<IReadOnlyCollection<LegalMatterContextItem>> SearchRoutedMatterContextAsync(Guid tenantId, Guid matterId, string query, IReadOnlyCollection<string> documentTypeCodes, int maximumItems, IReadOnlyCollection<float>? queryEmbedding = null, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<IReadOnlyCollection<LegalPassageEmbeddingCandidate>> GetPassagesMissingEmbeddingAsync(Guid tenantId, Guid documentVersionId, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task SavePassageEmbeddingAsync(Guid tenantId, Guid passageId, string embeddingJson, string embeddingModelCode, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<Guid> CreateDocumentAsync(Guid documentId, LegalDocumentIntakeRequest request, string sha256Hash, string storageReference, string malwareStatusCode, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<Guid?> FindActiveDocumentByHashAsync(Guid tenantId, Guid matterId, string sha256Hash, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task PurgeDocumentAsync(Guid tenantId, Guid userId, Guid documentId, CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<LegalMatterDocumentPurgeResult> PurgeMatterDocumentEvidenceAsync(Guid tenantId, Guid userId, Guid matterId, LegalMatterDocumentPurgeRequest request, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task SaveExtractionAsync(Guid tenantId, Guid userId, Guid documentVersionId, string correlationId, DocumentExtractionResult extraction, IReadOnlyCollection<LegalDocumentPassageDto> passages, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task MarkProcessingFailedAsync(Guid tenantId, Guid userId, Guid documentVersionId, string errorCode, string errorMessage, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task SaveSemanticProposalAsync(Guid tenantId, Guid userId, Guid matterId, Guid documentId, Guid documentVersionId, LegalDocumentSemanticProposal proposal, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task PersistRetrievalTelemetryAsync(Guid tenantId, Guid userId, DecisionRetrievalTelemetry telemetry, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<Guid> CreateUploadBatchAsync(Guid tenantId, Guid userId, Guid matterId, StartUploadBatchCommand command, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<IReadOnlyCollection<LegalUploadBatchDto>> GetUploadBatchesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<LegalUploadOperationLookup?> FindUploadOperationAsync(Guid tenantId, Guid matterId, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<Guid> RecordUploadOperationAsync(Guid tenantId, Guid userId, Guid matterId, Guid? batchId, string idempotencyKey, string requestFingerprint, string fileName, string? declaredContentType, long expectedLength, Guid resultDocumentId, Guid resultDocumentVersionId, bool contentReused, string sha256Hash, string? correlationId, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<Guid> CreateEvidenceOccurrenceAsync(Guid tenantId, Guid userId, Guid matterId, Guid documentId, Guid documentVersionId, Guid? batchId, Guid? uploadOperationId, bool contentReused, EvidenceSourceDescriptor source, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<IReadOnlyCollection<LegalEvidenceOccurrenceDto>> GetEvidenceOccurrencesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task IncrementUploadBatchCountersAsync(Guid tenantId, Guid batchId, UploadBatchCounterDelta delta, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task SetUploadBatchDiscoveredAsync(Guid tenantId, Guid batchId, int filesDiscovered, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task CloseUploadBatchAsync(Guid tenantId, Guid userId, Guid batchId, string statusCode, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<LegalProcessingOperationLookup> AcquireProcessingOperationAsync(Guid tenantId, Guid userId, LegalProcessingOperationRequest request, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task CompleteProcessingOperationAsync(Guid tenantId, Guid userId, Guid processingOperationId, string? resultEntityTypeCode, Guid? resultEntityId, string? resultHash, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task FailProcessingOperationAsync(Guid tenantId, Guid userId, Guid processingOperationId, string errorCode, string errorMessage, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<Guid> CreateDocumentFamilyAsync(Guid tenantId, Guid userId, Guid matterId, string? familyLabel, string containerTypeCode, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task LinkOccurrenceToFamilyAsync(Guid tenantId, Guid userId, Guid occurrenceId, Guid familyId, Guid? parentOccurrenceId, int familyDepth, int familyOrdinal, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<Guid> CreateEvidenceLineageGroupAsync(Guid tenantId, Guid userId, Guid matterId, string? lineageLabel, string? originDescription, string independenceBasisCode, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<Guid> AddEvidenceLineageMemberAsync(Guid tenantId, Guid userId, Guid lineageGroupId, Guid occurrenceId, string roleCode, string? derivationNote, CancellationToken cancellationToken = default) => throw Unexpected();
        public Task<IReadOnlyCollection<LegalEvidenceLineageGroupDto>> GetEvidenceLineageAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw Unexpected();
        private static NotSupportedException Unexpected() => new("Unexpected repository call.");
    }
}
