using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Common.Models;
using Ams.Application.Features.Opportunities;

namespace Ams.Application;

public sealed class OpportunityService : IOpportunityService
{
    private readonly IOpportunityRepository _repository;
    private readonly IAccountRepository _accountRepository;

    public OpportunityService(IOpportunityRepository repository, IAccountRepository accountRepository)
    {
        _repository = repository;
        _accountRepository = accountRepository;
    }

    public async Task<Guid> CreateAsync(CreateOpportunityRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: an Opportunity must never be orphaned and must stay within tenant scope.
        // Validate the parent Account exists and belongs to the same tenant before creating.
        await TenantGuard.EnsureParentAsync(request.AccountId, request.TenantId, _accountRepository.GetByIdAsync, a => a.TenantId, "Account", "opportunity", cancellationToken);

        return await _repository.CreateAsync(request, cancellationToken);
    }

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
