using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DeploymentBindings;

namespace Ams.Application.Abstractions.Persistence;

public interface IDeploymentBindingRepository
{
    Task<PagedResult<DeploymentBindingDto>> SearchAsync(string? searchTerm, string? statusCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<DeploymentBindingDto?>             GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid>                              CreateAsync(CreateDeploymentBindingRequest request, CancellationToken cancellationToken = default);
    Task                                    UpdateAsync(Guid id, UpdateDeploymentBindingRequest request, CancellationToken cancellationToken = default);
    Task                                    SetStatusAsync(Guid id, string statusCode, CancellationToken cancellationToken = default);
    Task                                    DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
