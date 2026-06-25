using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyCoverages;

namespace Ams.Application.Abstractions.Persistence;

public interface IPolicyCoverageRepository
{
    Task<IReadOnlyList<PolicyCoverageDetailDto>> GetByPolicyAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyCoverageDetailTemplateDto>> GetTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PolicyCoverageDetailDto?> GetByCodeAsync(Guid tenantId, Guid policyId, string coverageCode, CancellationToken cancellationToken = default);
    Task<PolicyCoverageDetailDto?> GetByIdAsync(Guid tenantId, Guid coverageDetailId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreatePolicyCoverageDetailRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid coverageDetailId, UpdatePolicyCoverageDetailRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid coverageDetailId, DeletePolicyCoverageDetailRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateFieldAsync(CreatePolicyCoverageFieldRequest request, CancellationToken cancellationToken = default);
    Task UpdateFieldAsync(Guid fieldId, UpdatePolicyCoverageFieldRequest request, CancellationToken cancellationToken = default);
    Task DeleteFieldAsync(Guid tenantId, Guid fieldId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
}
