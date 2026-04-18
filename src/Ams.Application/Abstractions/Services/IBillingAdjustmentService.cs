using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IBillingAdjustmentService
{
    Task<BillingAdjustmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<BillingAdjustmentDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
