using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.FeatureCatalog;

namespace Ams.Application;

public sealed class FeatureCatalogService : IFeatureCatalogService
{
    private readonly IFeatureCatalogRepository _repository;

    public FeatureCatalogService(IFeatureCatalogRepository repository) => _repository = repository;

    public Task<PagedResult<FeatureCatalogDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<FeatureCatalogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<Guid> CreateAsync(CreateFeatureRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateFeatureRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
        => _repository.SetEnabledAsync(id, enabled, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, cancellationToken);
}
