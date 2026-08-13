using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyChecks;

namespace Ams.Application.Abstractions.Services;

public interface IPolicyCheckService
{
    Task<PolicyCheckCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PolicyCheckDetailDto?> GetDetailAsync(Guid policyCheckId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreatePolicyCheckRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid policyCheckId, UpdatePolicyCheckRequest request, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid policyCheckId, UpdatePolicyCheckStatusRequest request, CancellationToken cancellationToken = default);
    Task UpdateItemAsync(Guid policyCheckItemId, UpdatePolicyCheckItemRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AddDiscrepancyAsync(AddPolicyCheckDiscrepancyRequest request, CancellationToken cancellationToken = default);
    Task ResolveDiscrepancyAsync(Guid policyCheckDiscrepancyId, ResolvePolicyCheckDiscrepancyRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AddActivityAsync(AddPolicyCheckActivityRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid policyCheckId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
