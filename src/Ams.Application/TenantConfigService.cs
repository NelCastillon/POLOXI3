using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.TenantConfig;

namespace Ams.Application;

public sealed class TenantConfigService : ITenantConfigService
{
    private readonly ITenantConfigRepository _repo;
    public TenantConfigService(ITenantConfigRepository repo) => _repo = repo;

    public Task<TenantConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<TenantConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, kind, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateTenantConfigItemRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateTenantConfigItemRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}
