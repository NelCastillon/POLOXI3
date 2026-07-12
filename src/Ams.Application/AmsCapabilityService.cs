using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Enterprise;

namespace Ams.Application;

public sealed class AmsCapabilityService : IAmsCapabilityService
{
    private readonly IAmsCapabilityRepository _repository;

    public AmsCapabilityService(IAmsCapabilityRepository repository) => _repository = repository;

    public Task<AmsCapabilityDto?> GetByIdAsync(Guid capabilityId, CancellationToken ct = default)
        => _repository.GetByIdAsync(capabilityId, ct);

    public Task<AmsCapabilityPageDto> SearchAsync(SearchAmsCapabilitiesRequest request, CancellationToken ct = default)
        => _repository.SearchAsync(request, ct);

    public Task UpdateAsync(Guid capabilityId, UpdateAmsCapabilityRequest request, CancellationToken ct = default)
        => _repository.UpdateAsync(capabilityId, request, ct);
}
