using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Regions;

namespace Ams.Application;

public sealed class RegionService : IRegionService
{
    private readonly IRegionRepository _repo;

    public RegionService(IRegionRepository repo) => _repo = repo;

    public Task<PagedResult<RegionDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repo.SearchAsync(searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<RegionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repo.GetByIdAsync(id, cancellationToken);

    public Task<Guid> CreateAsync(CreateRegionRequest request, CancellationToken cancellationToken = default)
        => _repo.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateRegionRequest request, CancellationToken cancellationToken = default)
        => _repo.UpdateAsync(id, request, cancellationToken);

    public Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
        => _repo.SetActiveAsync(id, isActive, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => _repo.DeleteAsync(id, cancellationToken);
}
