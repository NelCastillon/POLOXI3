using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PricingRules;

namespace Ams.Application;

public sealed class PricingRuleService : IPricingRuleService
{
    private readonly IPricingRuleRepository _repository;

    public PricingRuleService(IPricingRuleRepository repository)
    {
        _repository = repository;
    }

    public Task<Guid> CreateAsync(CreatePricingRuleRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task<PricingRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<PricingRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
}
