using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PricingRules;

namespace Ams.Application.Abstractions.Persistence;

public interface IPricingRuleRepository
{
    Task<Guid> CreateAsync(CreatePricingRuleRequest request, CancellationToken cancellationToken = default);
    Task<PricingRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<PricingRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
