using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Engagements;

namespace Ams.Application.Abstractions.Services;

public interface IEngagementMilestoneService
{
    Task<EngagementMilestoneDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<EngagementMilestoneDto>> SearchAsync(Guid tenantId, Guid? engagementId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateEngagementMilestoneRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateEngagementMilestoneRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
