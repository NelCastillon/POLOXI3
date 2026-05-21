using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application.Abstractions.Persistence;

public interface ICommissionClawbackRepository
{
    Task<CommissionClawbackDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CommissionClawbackDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, string? reasonCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCommissionClawbackRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCommissionClawbackRequest request, CancellationToken cancellationToken = default);
    Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
