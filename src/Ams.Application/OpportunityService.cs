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

    public Task<PagedResult<OpportunityDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
}
