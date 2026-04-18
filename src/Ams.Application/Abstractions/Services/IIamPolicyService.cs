using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application.Abstractions.Services;

public interface IIamPolicyService
{
    Task<FieldSecurityPolicyDto?> GetFieldPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<FieldSecurityPolicyDto>> SearchFieldPoliciesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateFieldPolicyAsync(CreateFieldSecurityPolicyRequest request, CancellationToken cancellationToken = default);
    Task DeleteFieldPolicyAsync(Guid policyId, Guid? deletedByUserId, CancellationToken cancellationToken = default);
    Task<RecordSecurityPolicyDto?> GetRecordPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<RecordSecurityPolicyDto>> SearchRecordPoliciesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateRecordPolicyAsync(CreateRecordSecurityPolicyRequest request, CancellationToken cancellationToken = default);
    Task DeleteRecordPolicyAsync(Guid policyId, Guid? deletedByUserId, CancellationToken cancellationToken = default);
}
