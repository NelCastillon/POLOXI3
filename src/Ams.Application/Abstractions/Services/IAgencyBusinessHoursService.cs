using Ams.Application.Common.Dtos;
using Ams.Application.Features.Agency;

namespace Ams.Application.Abstractions.Services;

public interface IAgencyBusinessHoursService
{
    Task<AgencyBusinessHoursDto> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid tenantId, UpdateAgencyBusinessHoursRequest request, CancellationToken cancellationToken = default);
}
