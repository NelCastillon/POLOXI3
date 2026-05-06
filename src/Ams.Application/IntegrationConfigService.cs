using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.IntegrationConfig;

namespace Ams.Application;

public sealed class IntegrationConfigService : IIntegrationConfigService
{
    private readonly IIntegrationConfigRepository _repo;
    public IntegrationConfigService(IIntegrationConfigRepository repo) => _repo = repo;

    public Task<IntegrationConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<IntegrationConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, kind, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateIntegrationConfigItemRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateIntegrationConfigItemRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}
