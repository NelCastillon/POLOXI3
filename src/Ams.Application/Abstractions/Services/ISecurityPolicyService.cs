using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application.Abstractions.Services;

public interface ISecurityPolicyService
{
    Task<SecurityPolicyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SecurityPolicyDto?> GetByCodeAsync(Guid tenantId, string policyCode, CancellationToken cancellationToken = default);
    Task<PagedResult<SecurityPolicyDto>> SearchAsync(Guid tenantId, string? searchTerm, string? resourceCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IEnumerable<SecurityPolicyDto>> GetActiveByResourceAsync(Guid tenantId, string resourceCode, string actionCode, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateSecurityPolicyRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid policyId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
