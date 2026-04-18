using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Services;

public interface IDashboardService
{
    Task<DashboardKpiDto> GetKpiAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
