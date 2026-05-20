using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Opportunities;

namespace Ams.Application;

public sealed class OpportunityService : IOpportunityService
{
    private readonly IOpportunityRepository _repository;

    public OpportunityService(IOpportunityRepository repository)
    {
        _repository = repository;
    }

    public Task<Guid> CreateAsync(CreateOpportunityRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task<OpportunityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<OpportunityDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetDetailAsync(id, cancellationToken);

    public Task<PagedResult<OpportunityDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateOpportunityRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task UpdateStageAsync(Guid id, UpdateOpportunityStageRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateStageAsync(id, request, cancellationToken);

    public Task<Guid> UpsertActivityAsync(UpsertOpportunityActivityRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertActivityAsync(request, cancellationToken);

    public Task DeleteActivityAsync(Guid activityId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteActivityAsync(activityId, modifiedByUserId, cancellationToken);

    public Task<Guid> UpsertSubmissionAsync(UpsertOpportunitySubmissionRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertSubmissionAsync(request, cancellationToken);

    public Task DeleteSubmissionAsync(Guid submissionId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteSubmissionAsync(submissionId, modifiedByUserId, cancellationToken);

    public Task<Guid> UpsertCompetitorAsync(UpsertOpportunityCompetitorRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertCompetitorAsync(request, cancellationToken);

    public Task DeleteCompetitorAsync(Guid competitorId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteCompetitorAsync(competitorId, modifiedByUserId, cancellationToken);
}
