using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.HealthChecks;

namespace Ams.Application.Abstractions.Services;

public interface IHealthCheckService
{
    Task<PagedResult<HealthCheckDto>> SearchAsync(string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<HealthCheckDto?> GetByIdAsync(Guid healthCheckId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateHealthCheckRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid healthCheckId, UpdateHealthCheckRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid healthCheckId, CancellationToken cancellationToken = default);
}
