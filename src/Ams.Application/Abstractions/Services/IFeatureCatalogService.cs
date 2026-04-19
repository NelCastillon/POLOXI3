using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.FeatureCatalog;

namespace Ams.Application.Abstractions.Services;

public interface IFeatureCatalogService
{
    Task<PagedResult<FeatureCatalogDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<FeatureCatalogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateFeatureRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateFeatureRequest request, CancellationToken cancellationToken = default);
    Task SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
