using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DataConfig;

namespace Ams.Application;

public sealed class DataConfigService : IDataConfigService
{
    private readonly IDataConfigRepository _repo;
    public DataConfigService(IDataConfigRepository repo) => _repo = repo;

    public Task<DataConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<DataConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, kind, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateDataConfigItemRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateDataConfigItemRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}
