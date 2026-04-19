using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Regions;

namespace Ams.Application.Abstractions.Services;

public interface IRegionService
{
    Task<PagedResult<RegionDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<RegionDto?>             GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid>                   CreateAsync(CreateRegionRequest request, CancellationToken cancellationToken = default);
    Task                         UpdateAsync(Guid id, UpdateRegionRequest request, CancellationToken cancellationToken = default);
    Task                         SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task                         DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
