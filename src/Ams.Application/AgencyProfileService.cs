using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Agency;

namespace Ams.Application;

public sealed class AgencyProfileService : IAgencyProfileService
{
    private readonly IAgencyProfileRepository _repository;
    public AgencyProfileService(IAgencyProfileRepository repository) => _repository = repository;
    public Task<AgencyProfileDto?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default) => _repository.GetByTenantIdAsync(tenantId, cancellationToken);
    public Task UpdateAsync(Guid tenantId, UpdateAgencyProfileRequest request, CancellationToken cancellationToken = default) => _repository.UpdateAsync(tenantId, request, cancellationToken);
}
