using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.LeadActivities;

namespace Ams.Application.Abstractions.Persistence;

public interface ILeadActivityRepository
{
    Task<Guid> CreateAsync(CreateLeadActivityRequest request, CancellationToken cancellationToken = default);
    Task<LeadActivityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<LeadActivityDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
