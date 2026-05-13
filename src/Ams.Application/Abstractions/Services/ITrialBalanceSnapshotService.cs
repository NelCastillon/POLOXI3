using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application.Abstractions.Services;

public interface ITrialBalanceSnapshotService
{
    Task<TrialBalanceSnapshotDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<TrialBalanceSnapshotDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateTrialBalanceSnapshotRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateTrialBalanceSnapshotRequest request, CancellationToken cancellationToken = default);
}
