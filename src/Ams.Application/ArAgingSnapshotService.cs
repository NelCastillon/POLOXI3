using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Billing;

namespace Ams.Application;

public sealed class ArAgingSnapshotService : IArAgingSnapshotService
{
    private readonly IArAgingSnapshotRepository _repository;
    public ArAgingSnapshotService(IArAgingSnapshotRepository repository) => _repository = repository;
    public Task<ArAgingSnapshotDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<ArAgingSnapshotDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateAsync(CreateArAgingSnapshotRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
    public Task UpdateAsync(Guid id, UpdateArAgingSnapshotRequest request, CancellationToken cancellationToken = default) => _repository.UpdateAsync(id, request, cancellationToken);
    public Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default) => _repository.DeleteAsync(id, modifiedByUserId, cancellationToken);
    public Task<int> SyncFromInvoicesAsync(Guid tenantId, DateOnly snapshotDate, Guid? createdByUserId = null, CancellationToken cancellationToken = default) => _repository.SyncFromInvoicesAsync(tenantId, snapshotDate, createdByUserId, cancellationToken);
}
