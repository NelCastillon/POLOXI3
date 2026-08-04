using Ams.Application.Common.Models;
using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Persistence;

public interface IIntelligenceRepository
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
    Task DecideRecommendationAsync(DecideRecommendationRequest request,CancellationToken cancellationToken=default);
    Task<IntelligenceSearchResponse> SearchAsync(IntelligenceSearchRequest request,IReadOnlyCollection<SemanticConceptMatchDto> concepts,IReadOnlyCollection<string> expandedTerms,CancellationToken cancellationToken=default);
    Task<PagedResult<AiReviewQueueItemDto>> SearchReviewQueueAsync(SearchAiReviewQueueQuery query,CancellationToken cancellationToken=default);
    Task DecideReviewAsync(DecideAiReviewRequest request,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<AiEvaluationDefinitionDto>> GetEvaluationDefinitionsAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task<IReadOnlyCollection<AiEvaluationRunDto>> GetEvaluationRunsAsync(Guid tenantId,int pageSize,CancellationToken cancellationToken=default);
    Task<Guid> QueueEvaluationAsync(QueueAiEvaluationRequest request,CancellationToken cancellationToken=default);
    Task<IntelligenceDashboardDto> GetDashboardAsync(Guid tenantId,CancellationToken cancellationToken=default);
}

public interface IRecommendationGenerationRepository
{
    Task GenerateAsync(GenerateRecommendationsRequest request,CancellationToken cancellationToken=default);
}
