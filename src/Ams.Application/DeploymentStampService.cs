using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DeploymentStamps;

namespace Ams.Application;

public sealed class DeploymentStampService : IDeploymentStampService
{
    private readonly IDeploymentStampRepository _repository;

    public DeploymentStampService(IDeploymentStampRepository repository) => _repository = repository;

    public Task<PagedResult<DeploymentStampDto>> SearchAsync(string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, statusCode, pageNumber, pageSize, cancellationToken);

    public Task<DeploymentStampDto?> GetByIdAsync(Guid stampId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(stampId, cancellationToken);

    public Task<Guid> CreateAsync(CreateDeploymentStampRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid stampId, UpdateDeploymentStampRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(stampId, request, cancellationToken);

    public Task SetStatusAsync(Guid stampId, string statusCode, CancellationToken cancellationToken = default)
        => _repository.SetStatusAsync(stampId, statusCode, cancellationToken);

    public Task DeleteAsync(Guid stampId, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(stampId, cancellationToken);
}
