using Ams.Application.Common.Dtos;
using Ams.Application.Features.Agency;

namespace Ams.Application.Abstractions.Services;

public interface IAgencyProfileService
{
    Task<AgencyProfileDto?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid tenantId, UpdateAgencyProfileRequest request, CancellationToken cancellationToken = default);
}
