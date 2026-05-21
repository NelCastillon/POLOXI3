using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application.Abstractions.Services;

public interface ICommissionAccrualEntryService
{
    Task<CommissionAccrualEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CommissionAccrualEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCommissionAccrualEntryRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCommissionAccrualEntryRequest request, CancellationToken cancellationToken = default);
    Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
