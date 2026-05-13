using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;

namespace Ams.Application.Abstractions.Services;

public interface IArAgingSnapshotService
{
    Task<ArAgingSnapshotDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ArAgingSnapshotDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateArAgingSnapshotRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateArAgingSnapshotRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
    Task<int> SyncFromInvoicesAsync(Guid tenantId, DateOnly snapshotDate, Guid? createdByUserId = null, CancellationToken cancellationToken = default);
}
