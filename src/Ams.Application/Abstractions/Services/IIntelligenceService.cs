using Ams.Application.Common.Models;
using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Services;

public interface IIntelligenceService
{
    Task<IReadOnlyCollection<AiProviderDto>> GetProvidersAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<AiModelDeploymentDto>> GetModelsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<AiFeaturePolicyDto>> GetFeaturePoliciesAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task SaveFeaturePolicyAsync(SaveAiFeaturePolicyRequest request,CancellationToken cancellationToken=default);
    Task<PagedResult<AiExecutionSummaryDto>> SearchExecutionsAsync(SearchAiExecutionsQuery query,CancellationToken cancellationToken=default);
    Task<AiExecutionDetailDto?> GetExecutionAsync(Guid tenantId,Guid executionId,CancellationToken cancellationToken=default);
    Task SubmitExecutionFeedbackAsync(SubmitAiExecutionFeedbackRequest request,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<RecommendationTypeDto>> GetRecommendationTypesAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<PagedResult<RecommendationDto>> SearchRecommendationsAsync(SearchRecommendationsQuery query,CancellationToken cancellationToken=default);
    Task QueueRecommendationsAsync(GenerateRecommendationsRequest request,CancellationToken cancellationToken=default);
    Task DecideRecommendationAsync(DecideRecommendationRequest request,CancellationToken cancellationToken=default);
    Task<IntelligenceSearchResponse> SearchAsync(IntelligenceSearchRequest request,CancellationToken cancellationToken=default);
    Task<QuickSearchFastPathResponse> QuickSearchFastPathAsync(QuickSearchRequest request,CancellationToken cancellationToken=default);
    Task<QuickSearchResponse> QuickSearchIntelligentFallbackAsync(QuickSearchRequest request,CancellationToken cancellationToken=default);
    Task<PagedResult<AiReviewQueueItemDto>> SearchReviewQueueAsync(SearchAiReviewQueueQuery query,CancellationToken cancellationToken=default);
    Task DecideReviewAsync(DecideAiReviewRequest request,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<AiEvaluationDefinitionDto>> GetEvaluationDefinitionsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<AiEvaluationRunDto>> GetEvaluationRunsAsync(Guid tenantId,int pageSize,CancellationToken cancellationToken=default);
    Task<Guid> QueueEvaluationAsync(QueueAiEvaluationRequest request,CancellationToken cancellationToken=default);
    Task<IntelligenceDashboardDto> GetDashboardAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<IntelligencePlatformSummaryDto> GetPlatformSummaryAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<PlatformArchitectureDto> GetPlatformArchitectureAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<IntelligenceEnginePolicyDto>> GetEnginePoliciesAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task SaveEnginePolicyAsync(SaveIntelligenceEnginePolicyRequest request,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<IntelligenceSafetyControlDto>> GetSafetyControlsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task SaveSafetyControlAsync(SaveIntelligenceSafetyControlRequest request,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<IntelligenceComplianceRequirementDto>> GetComplianceRequirementsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<IntelligenceSafetyEventDto>> GetSafetyEventsAsync(Guid tenantId,int pageSize,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<IntelligencePromptDefinitionDto>> GetPromptDefinitionsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task SavePromptDefinitionAsync(SaveIntelligencePromptDefinitionRequest request,CancellationToken cancellationToken=default);
    Task SubmitEvaluationSampleLabelAsync(SubmitEvaluationSampleLabelRequest request,CancellationToken cancellationToken=default);
    Task<PagedResult<IntelligenceFindingDto>> SearchFindingsAsync(SearchIntelligenceFindingsQuery query,CancellationToken cancellationToken=default);
    Task<IntelligenceFindingDetailDto?> GetFindingAsync(Guid tenantId,Guid findingId,CancellationToken cancellationToken=default);
    Task DecideFindingAsync(DecideIntelligenceFindingRequest request,CancellationToken cancellationToken=default);
    Task<EntityRelationshipGraphDto> GetRelationshipGraphAsync(RelationshipQuery query,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<EntitySimilarityDto>> GetSimilarEntitiesAsync(SimilarityQuery query,CancellationToken cancellationToken=default);
    Task<PagedResult<BusinessIntelligenceSignalDto>> SearchBusinessSignalsAsync(SearchBusinessIntelligenceSignalsQuery query,CancellationToken cancellationToken=default);
    Task DecideBusinessSignalAsync(DecideBusinessIntelligenceSignalRequest request,CancellationToken cancellationToken=default);
    Task<InsuranceReasoningResponse> ExecuteReasoningAsync(InsuranceReasoningRequest request,CancellationToken cancellationToken=default);
    Task<InsuranceReasoningResponse?> GetReasoningSessionAsync(Guid tenantId,Guid userId,Guid reasoningSessionId,CancellationToken cancellationToken=default);
}
