using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AiConfig;

namespace Ams.Application;

public sealed class AiConfigService : IAiConfigService
{
    private readonly IAiConfigRepository _repo;
    public AiConfigService(IAiConfigRepository repo) => _repo = repo;

    public Task<AiConfigItemDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(tenantId, id, ct);
    public Task<PagedResult<AiConfigItemDto>> SearchAsync(Guid tenantId, string kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, kind, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateAiConfigItemRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid tenantId, Guid id, UpdateAiConfigItemRequest request, CancellationToken ct = default) => _repo.UpdateAsync(tenantId, id, request, ct);
    public Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default) => _repo.DeleteAsync(tenantId, id, ct);
}
