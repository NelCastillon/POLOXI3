using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Persistence;

public interface IDashboardRepository
{
    Task<DashboardKpiDto> GetKpiAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
