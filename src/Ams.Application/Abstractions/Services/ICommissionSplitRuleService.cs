using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application.Abstractions.Services;

public interface ICommissionSplitRuleService
{
    Task<CommissionSplitRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CommissionSplitRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task EnsureSeedAsync(Guid tenantId, Guid? createdByUserId = null, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCommissionSplitRuleRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCommissionSplitRuleRequest request, CancellationToken cancellationToken = default);
}
