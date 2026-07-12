using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.BillingAccounts;

namespace Ams.Application.Abstractions.Services;

public interface IBillingAccountService
{
    Task EnsureSchemaAndSeedAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<BillingAccountDto?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<PagedResult<BillingAccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillingModeDashboardRowDto>> GetBillingModeDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateBillingAccountRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid accountId, UpdateBillingAccountRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid accountId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
}
