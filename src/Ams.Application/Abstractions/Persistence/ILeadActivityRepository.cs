using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.LeadActivities;

namespace Ams.Application.Abstractions.Persistence;

public interface ILeadActivityRepository
{
    Task<Guid> CreateAsync(CreateLeadActivityRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateLeadActivityRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
    Task<LeadActivityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeadActivityDto>> GetByLeadIdAsync(Guid leadId, CancellationToken cancellationToken = default);
    Task<PagedResult<LeadActivityDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
