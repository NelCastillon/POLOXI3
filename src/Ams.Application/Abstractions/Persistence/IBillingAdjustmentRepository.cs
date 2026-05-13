using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;

namespace Ams.Application.Abstractions.Persistence;

public interface IBillingAdjustmentRepository
{
    Task<BillingAdjustmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<BillingAdjustmentDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateBillingAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateBillingAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
}
