using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Agency;

namespace Ams.Application;

public sealed class AgencyBusinessHoursService : IAgencyBusinessHoursService
{
    private readonly IAgencyBusinessHoursRepository _repository;

    public AgencyBusinessHoursService(IAgencyBusinessHoursRepository repository)
        => _repository = repository;

    public Task<AgencyBusinessHoursDto> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetByTenantIdAsync(tenantId, cancellationToken);

    public Task UpdateAsync(Guid tenantId, UpdateAgencyBusinessHoursRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(tenantId, request, cancellationToken);
}
