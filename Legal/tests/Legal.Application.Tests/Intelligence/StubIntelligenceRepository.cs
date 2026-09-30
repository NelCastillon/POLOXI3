using Legal.Application.Abstractions.Persistence;
using Legal.Application.Common.Models;
using Legal.Application.Features.Intelligence;

namespace Legal.Application.Tests.Intelligence;

// Throwing stub for the large IIntelligenceRepository surface. Tests override only the single member
// the channel under test uses (SearchFindingsAsync); every other member throws so accidental use is loud.
public abstract class StubIntelligenceRepository : IIntelligenceRepository
{
    public virtual Task<PagedResult<IntelligenceFindingDto>> SearchFindingsAsync(SearchIntelligenceFindingsQuery query, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyCollection<AiProviderDto>> GetProvidersAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<AiModelDeploymentDto>> GetModelsAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<AiFeaturePolicyDto>> GetFeaturePoliciesAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SaveFeaturePolicyAsync(SaveAiFeaturePolicyRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SaveProviderAsync(SaveAiProviderRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SaveModelDeploymentAsync(SaveAiModelDeploymentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteProviderAsync(Guid tenantId, string providerCode, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteModelDeploymentAsync(Guid tenantId, string modelCode, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteFeaturePolicyAsync(Guid tenantId, string featureCode, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PagedResult<AiExecutionSummaryDto>> SearchExecutionsAsync(SearchAiExecutionsQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AiExecutionDetailDto?> GetExecutionAsync(Guid tenantId, Guid executionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SubmitExecutionFeedbackAsync(SubmitAiExecutionFeedbackRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<RecommendationTypeDto>> GetRecommendationTypesAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PagedResult<RecommendationDto>> SearchRecommendationsAsync(SearchRecommendationsQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DecideRecommendationAsync(DecideRecommendationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IntelligenceSearchConfiguration> GetSearchConfigurationAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<IntelligenceSearchIntentPatternDto>> GetSearchIntentPatternsAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task RecordSearchIntentInterpretationAsync(IntelligenceSearchIntentLogRecord record, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IntelligenceSearchResponse> SearchAsync(IntelligenceSearchRequest request, IReadOnlyCollection<SemanticConceptMatchDto> concepts, IReadOnlyCollection<string> expandedTerms, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<IntelligenceSearchResultDto>> GetAuthorizedSearchDocumentsAsync(IntelligenceSearchRequest request, IReadOnlyCollection<IntelligenceSearchEntityKey> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<IntelligenceSearchResultDto>> GetRelatedSearchDocumentsAsync(IntelligenceSearchRequest request, IReadOnlyCollection<IntelligenceSearchEntityKey> sources, int maximumResults, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task CompleteUnifiedSearchAsync(Guid tenantId, Guid userId, Guid searchQueryId, string normalizedQuery, IntelligenceSearchWeightsDto weights, IReadOnlyCollection<IntelligenceSearchResultDto> results, string summaryStatusCode, Guid? summaryExecutionId, long durationMilliseconds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PoloxiConfiguration> GetPoloxiConfigurationAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<PoloxiCapabilityDto>> GetPoloxiCapabilitiesAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PoloxiHierarchyRecord?> GetReusablePoloxiHierarchyAsync(Guid tenantId, string querySignature, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PoloxiHierarchyRecord> SavePoloxiHierarchyAsync(Guid tenantId, Guid userId, string querySignature, string normalizedQuery, PoloxiHierarchyProposal proposal, string? providerCode, string? modelCode, DateTime expiresDateUtc, IReadOnlyCollection<PoloxiBranchRecord> branches, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<PoloxiEvidenceDto>> ExecutePoloxiBranchAsync(PoloxiSearchRequest request, PoloxiBranchRecord branch, PoloxiCapabilityDto capability, int maximumResults, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Guid> StartPoloxiExecutionAsync(PoloxiExecutionStart execution, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task CompletePoloxiExecutionAsync(Guid tenantId, Guid userId, Guid poloxiExecutionId, Guid hierarchyId, IReadOnlyCollection<PoloxiEvidenceDto> evidence, string explanationStatusCode, string? explanation, long durationMilliseconds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PagedResult<AiReviewQueueItemDto>> SearchReviewQueueAsync(SearchAiReviewQueueQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DecideReviewAsync(DecideAiReviewRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<AiEvaluationDefinitionDto>> GetEvaluationDefinitionsAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<AiEvaluationRunDto>> GetEvaluationRunsAsync(Guid tenantId, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Guid> QueueEvaluationAsync(QueueAiEvaluationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IntelligenceDashboardDto> GetDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IntelligencePlatformSummaryDto> GetPlatformSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PlatformArchitectureDto> GetPlatformArchitectureAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<IntelligenceEnginePolicyDto>> GetEnginePoliciesAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SaveEnginePolicyAsync(SaveIntelligenceEnginePolicyRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<IntelligenceSafetyControlDto>> GetSafetyControlsAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SaveSafetyControlAsync(SaveIntelligenceSafetyControlRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<IntelligenceComplianceRequirementDto>> GetComplianceRequirementsAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<IntelligenceSafetyEventDto>> GetSafetyEventsAsync(Guid tenantId, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<IntelligencePromptDefinitionDto>> GetPromptDefinitionsAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SavePromptDefinitionAsync(SaveIntelligencePromptDefinitionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeletePromptDefinitionAsync(Guid tenantId, string promptCode, string versionLabel, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SubmitEvaluationSampleLabelAsync(SubmitEvaluationSampleLabelRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IntelligenceFindingDetailDto?> GetFindingAsync(Guid tenantId, Guid findingId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DecideFindingAsync(DecideIntelligenceFindingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<EntityRelationshipGraphDto> GetRelationshipGraphAsync(RelationshipQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyCollection<EntitySimilarityDto>> GetSimilarEntitiesAsync(SimilarityQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PagedResult<BusinessIntelligenceSignalDto>> SearchBusinessSignalsAsync(SearchBusinessIntelligenceSignalsQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DecideBusinessSignalAsync(DecideBusinessIntelligenceSignalRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<InsuranceReasoningResponse> ExecuteReasoningAsync(InsuranceReasoningRequest request, IReadOnlyCollection<SemanticConceptMatchDto> concepts, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<InsuranceReasoningResponse?> GetReasoningSessionAsync(Guid tenantId, Guid userId, Guid reasoningSessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
