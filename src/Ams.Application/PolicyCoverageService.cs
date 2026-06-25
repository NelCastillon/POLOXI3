using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyCoverages;

namespace Ams.Application;

public sealed class PolicyCoverageService : IPolicyCoverageService
{
    private readonly IPolicyCoverageRepository _repo;

    public PolicyCoverageService(IPolicyCoverageRepository repo) => _repo = repo;

    public Task<IReadOnlyList<PolicyCoverageDetailDto>> GetByPolicyAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
        => _repo.GetByPolicyAsync(tenantId, policyId, cancellationToken);

    public Task<IReadOnlyList<PolicyCoverageDetailTemplateDto>> GetTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repo.GetTemplatesAsync(tenantId, cancellationToken);

    public Task<PolicyCoverageDetailDto?> GetByCodeAsync(Guid tenantId, Guid policyId, string coverageCode, CancellationToken cancellationToken = default)
        => _repo.GetByCodeAsync(tenantId, policyId, coverageCode, cancellationToken);

    public Task<PolicyCoverageDetailDto?> GetByIdAsync(Guid tenantId, Guid coverageDetailId, CancellationToken cancellationToken = default)
        => _repo.GetByIdAsync(tenantId, coverageDetailId, cancellationToken);

    public Task<Guid> CreateAsync(CreatePolicyCoverageDetailRequest request, CancellationToken cancellationToken = default)
        => _repo.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid coverageDetailId, UpdatePolicyCoverageDetailRequest request, CancellationToken cancellationToken = default)
        => _repo.UpdateAsync(coverageDetailId, request, cancellationToken);

    public Task DeleteAsync(Guid coverageDetailId, DeletePolicyCoverageDetailRequest request, CancellationToken cancellationToken = default)
        => _repo.DeleteAsync(coverageDetailId, request, cancellationToken);

    public Task<Guid> CreateFieldAsync(CreatePolicyCoverageFieldRequest request, CancellationToken cancellationToken = default)
        => _repo.CreateFieldAsync(request, cancellationToken);

    public Task UpdateFieldAsync(Guid fieldId, UpdatePolicyCoverageFieldRequest request, CancellationToken cancellationToken = default)
        => _repo.UpdateFieldAsync(fieldId, request, cancellationToken);

    public Task DeleteFieldAsync(Guid tenantId, Guid fieldId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
        => _repo.DeleteFieldAsync(tenantId, fieldId, modifiedByUserId, cancellationToken);
}
