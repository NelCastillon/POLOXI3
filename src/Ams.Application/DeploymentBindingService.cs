using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DeploymentBindings;

namespace Ams.Application;

public sealed class DeploymentBindingService : IDeploymentBindingService
{
    private readonly IDeploymentBindingRepository _repository;

    public DeploymentBindingService(IDeploymentBindingRepository repository) => _repository = repository;

    public Task<PagedResult<DeploymentBindingDto>> SearchAsync(string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, statusCode, pageNumber, pageSize, cancellationToken);

    public Task<DeploymentBindingDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<Guid> CreateAsync(CreateDeploymentBindingRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateDeploymentBindingRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task SetStatusAsync(Guid id, string statusCode, CancellationToken cancellationToken = default)
        => _repository.SetStatusAsync(id, statusCode, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, cancellationToken);
}
