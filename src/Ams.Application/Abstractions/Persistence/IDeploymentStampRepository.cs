using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DeploymentStamps;

namespace Ams.Application.Abstractions.Persistence;

public interface IDeploymentStampRepository
{
    Task<PagedResult<DeploymentStampDto>> SearchAsync(string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<DeploymentStampDto?> GetByIdAsync(Guid stampId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateDeploymentStampRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid stampId, UpdateDeploymentStampRequest request, CancellationToken cancellationToken = default);
    Task SetStatusAsync(Guid stampId, string statusCode, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid stampId, CancellationToken cancellationToken = default);
}
