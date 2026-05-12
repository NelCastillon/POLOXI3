using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.LeadActivities;

namespace Ams.Application;

public sealed class LeadActivityService : ILeadActivityService
{
    private readonly ILeadActivityRepository _repository;

    public LeadActivityService(ILeadActivityRepository repository)
    {
        _repository = repository;
    }

    public Task<Guid> CreateAsync(CreateLeadActivityRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(UpdateLeadActivityRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(request, cancellationToken);

    public Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, modifiedByUserId, cancellationToken);

    public Task<LeadActivityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<LeadActivityDto>> GetByLeadIdAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _repository.GetByLeadIdAsync(leadId, cancellationToken);

    public Task<PagedResult<LeadActivityDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
}
