using Ams.Application.Common.Dtos;
using Ams.Application.Features.Submissions;

namespace Ams.Application.Abstractions.Services;

public interface IPolicyCreationService
{
    Task<Guid> CreatePolicyFromConfirmedBindAsync(PolicyCreationFromConfirmedBindRequest request, CancellationToken cancellationToken = default);
    Task<BinderReviewDto?> GetBinderReviewAsync(Guid policyBindTransactionId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<BinderReviewDto> SaveBinderReviewAsync(Guid policyBindTransactionId, UpsertBinderReviewRequest request, CancellationToken cancellationToken = default);
    Task DecideBinderReviewAsync(Guid policyBindTransactionId, DecideBinderReviewRequest request, CancellationToken cancellationToken = default);
    Task<PolicyGenerationRequestDto> QueuePolicyGenerationAsync(Guid policyBindTransactionId, QueuePolicyGenerationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManualPolicyOptionDto>> GetManualPolicyOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ManualPolicyDraftDto> SaveManualPolicyDraftAsync(Guid? draftId, UpsertManualPolicyDraftRequest request, CancellationToken cancellationToken = default);
    Task<ManualPolicyDraftDto?> GetManualPolicyDraftAsync(Guid tenantId, Guid accountId, Guid draftId, CancellationToken cancellationToken = default);
    Task<ManualPolicyValidationResultDto> ValidateManualPolicyAsync(CreateManualPolicyRequest request, CancellationToken cancellationToken = default);
    Task<ManualPolicyCreateResultDto> CreateManualPolicyAsync(CreateManualPolicyRequest request, CancellationToken cancellationToken = default);
}
