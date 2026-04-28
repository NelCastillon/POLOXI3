using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Leads;

namespace Ams.Application;

public sealed class LeadService : ILeadService
{
    private readonly ILeadRepository _repository;

    public LeadService(ILeadRepository repository)
    {
        _repository = repository;
    }

    public Task<Guid> CreateAsync(CreateLeadRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task<LeadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<LeadDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<IReadOnlyList<LeadScoringRuleDto>> GetScoringRulesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetScoringRulesAsync(tenantId, cancellationToken);
}
