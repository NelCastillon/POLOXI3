using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyLifecycle;

namespace Ams.Application.Abstractions.Persistence;

public interface IPolicyLifecycleRepository
{
    Task<IReadOnlyList<PolicyLifecycleOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyLifecycleWorkbenchRowDto>> GetWorkbenchAsync(Guid tenantId, string? mode = null, CancellationToken cancellationToken = default);
    Task<PolicyLifecycleDetailDto?> GetDetailAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default);
    Task<Guid> CreateTransactionAsync(CreatePolicyLifecycleTransactionRequest request, CancellationToken cancellationToken = default);
    Task TransitionTransactionAsync(Guid policyTransactionId, TransitionPolicyLifecycleTransactionRequest request, CancellationToken cancellationToken = default);
}
