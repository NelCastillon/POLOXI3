using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Opportunities;

namespace Ams.Application.Abstractions.Services;

public interface IOpportunityService
{
    Task<Guid> CreateAsync(CreateOpportunityRequest request, CancellationToken cancellationToken = default);
    Task<OpportunityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<OpportunityDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
