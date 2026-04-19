using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Plans;

namespace Ams.Application.Abstractions.Services;

public interface IPlanService
{
    Task<PlanDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<PlanDto>> SearchAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreatePlanRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdatePlanRequest request, CancellationToken cancellationToken = default);
    Task CloneAsync(Guid id, string newPlanCode, string newPlanName, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
