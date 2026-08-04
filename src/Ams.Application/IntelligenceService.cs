using System.ComponentModel.DataAnnotations;
using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Models;
using Ams.Application.Features.Intelligence;

namespace Ams.Application;

public sealed class IntelligenceService(IIntelligenceRepository repository,IRecommendationGenerationRepository recommendationRepository,ISemanticQueryExpander queryExpander):IIntelligenceService
{
    public Task<IReadOnlyCollection<AiProviderDto>> GetProvidersAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetProvidersAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task<IReadOnlyCollection<AiModelDeploymentDto>> GetModelsAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetModelsAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task<IReadOnlyCollection<AiFeaturePolicyDto>> GetFeaturePoliciesAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetFeaturePoliciesAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task SaveFeaturePolicyAsync(SaveAiFeaturePolicyRequest request,CancellationToken cancellationToken=default){Validate(request);return repository.SaveFeaturePolicyAsync(request,cancellationToken);}
    public Task<PagedResult<AiExecutionSummaryDto>> SearchExecutionsAsync(SearchAiExecutionsQuery query,CancellationToken cancellationToken=default){ValidatePage(query.TenantId,query.PageNumber,query.PageSize);return repository.SearchExecutionsAsync(query with{PageSize=Math.Clamp(query.PageSize,1,200)},cancellationToken);}
    public Task<AiExecutionDetailDto?> GetExecutionAsync(Guid tenantId,Guid executionId,CancellationToken cancellationToken=default)=>repository.GetExecutionAsync(Required(tenantId,nameof(tenantId)),Required(executionId,nameof(executionId)),cancellationToken);
    public Task SubmitExecutionFeedbackAsync(SubmitAiExecutionFeedbackRequest request,CancellationToken cancellationToken=default){Validate(request);return repository.SubmitExecutionFeedbackAsync(request,cancellationToken);}
    public Task<IReadOnlyCollection<RecommendationTypeDto>> GetRecommendationTypesAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetRecommendationTypesAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task<PagedResult<RecommendationDto>> SearchRecommendationsAsync(SearchRecommendationsQuery query,CancellationToken cancellationToken=default){ValidatePage(query.TenantId,query.PageNumber,query.PageSize);return repository.SearchRecommendationsAsync(query with{PageSize=Math.Clamp(query.PageSize,1,200)},cancellationToken);}
    public Task QueueRecommendationsAsync(GenerateRecommendationsRequest request,CancellationToken cancellationToken=default){Validate(request);return recommendationRepository.GenerateAsync(request,cancellationToken);}
    public Task DecideRecommendationAsync(DecideRecommendationRequest request,CancellationToken cancellationToken=default){Validate(request);return repository.DecideRecommendationAsync(request,cancellationToken);}
    public async Task<IntelligenceSearchResponse> SearchAsync(IntelligenceSearchRequest request,CancellationToken cancellationToken=default){Validate(request);var expansion=await queryExpander.ExpandAsync(request.TenantId,request.Query,20,cancellationToken);return await repository.SearchAsync(request with{MaximumResults=Math.Clamp(request.MaximumResults,1,100)},expansion.Concepts,expansion.Terms,cancellationToken);}
    public Task<PagedResult<AiReviewQueueItemDto>> SearchReviewQueueAsync(SearchAiReviewQueueQuery query,CancellationToken cancellationToken=default){ValidatePage(query.TenantId,query.PageNumber,query.PageSize);return repository.SearchReviewQueueAsync(query with{PageSize=Math.Clamp(query.PageSize,1,200)},cancellationToken);}
    public Task DecideReviewAsync(DecideAiReviewRequest request,CancellationToken cancellationToken=default){Validate(request);return repository.DecideReviewAsync(request,cancellationToken);}
    public Task<IReadOnlyCollection<AiEvaluationDefinitionDto>> GetEvaluationDefinitionsAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetEvaluationDefinitionsAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task<IReadOnlyCollection<AiEvaluationRunDto>> GetEvaluationRunsAsync(Guid tenantId,int pageSize,CancellationToken cancellationToken=default)=>repository.GetEvaluationRunsAsync(Required(tenantId,nameof(tenantId)),Math.Clamp(pageSize,1,500),cancellationToken);
    public Task<Guid> QueueEvaluationAsync(QueueAiEvaluationRequest request,CancellationToken cancellationToken=default){Validate(request);if(request.WindowEndUtc<=request.WindowStartUtc)throw new ValidationException("Evaluation window end must be after its start.");return repository.QueueEvaluationAsync(request,cancellationToken);}
    public Task<IntelligenceDashboardDto> GetDashboardAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetDashboardAsync(Required(tenantId,nameof(tenantId)),cancellationToken);

    private static Guid Required(Guid value,string name)=>value==Guid.Empty?throw new ValidationException($"{name} is required."):value;
    private static void ValidatePage(Guid tenantId,int pageNumber,int pageSize){Required(tenantId,nameof(tenantId));if(pageNumber<1||pageSize<1)throw new ValidationException("Page number and page size must be positive.");}
    private static void Validate(object request){var context=new ValidationContext(request);Validator.ValidateObject(request,context,true);foreach(var property in request.GetType().GetProperties().Where(x=>x.PropertyType==typeof(Guid))){if((Guid)(property.GetValue(request)??Guid.Empty)==Guid.Empty)throw new ValidationException($"{property.Name} is required.");}}
}
