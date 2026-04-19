using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.HealthChecks;

namespace Ams.Application;

public sealed class HealthCheckService : IHealthCheckService
{
    private readonly IHealthCheckRepository _repository;

    public HealthCheckService(IHealthCheckRepository repository) => _repository = repository;

    public Task<PagedResult<HealthCheckDto>> SearchAsync(string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, statusCode, pageNumber, pageSize, cancellationToken);

    public Task<HealthCheckDto?> GetByIdAsync(Guid healthCheckId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(healthCheckId, cancellationToken);

    public Task<Guid> CreateAsync(CreateHealthCheckRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid healthCheckId, UpdateHealthCheckRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(healthCheckId, request, cancellationToken);

    public Task DeleteAsync(Guid healthCheckId, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(healthCheckId, cancellationToken);
}
