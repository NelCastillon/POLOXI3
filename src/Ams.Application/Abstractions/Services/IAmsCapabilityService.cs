using Ams.Application.Common.Dtos;
using Ams.Application.Features.Enterprise;

namespace Ams.Application.Abstractions.Services;

public interface IAmsCapabilityService
{
    Task<AmsCapabilityDto?> GetByIdAsync(Guid capabilityId, CancellationToken ct = default);
    Task<AmsCapabilityPageDto> SearchAsync(SearchAmsCapabilitiesRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid capabilityId, UpdateAmsCapabilityRequest request, CancellationToken ct = default);
}
