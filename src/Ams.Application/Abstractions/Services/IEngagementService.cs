using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Engagements;

namespace Ams.Application.Abstractions.Services;

public interface IEngagementService
{
    Task<EngagementDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<EngagementDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PagedResult<EngagementTaskDto>> SearchTasksAsync(Guid tenantId, Guid? engagementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateEngagementRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateEngagementRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
