using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application.Abstractions.Services;

public interface ICommissionPlannerScenarioService
{
    Task<CommissionPlannerScenarioDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CommissionPlannerScenarioDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, string? scenarioTypeCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCommissionPlannerScenarioRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCommissionPlannerScenarioRequest request, CancellationToken cancellationToken = default);
    Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
