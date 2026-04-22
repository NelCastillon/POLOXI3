using Ams.Application.Common.Dtos;
using Ams.Application.Features.Agency;

namespace Ams.Application.Abstractions.Persistence;

public interface IAgencyProfileRepository
{
    Task<AgencyProfileDto?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid tenantId, UpdateAgencyProfileRequest request, CancellationToken cancellationToken = default);
}
