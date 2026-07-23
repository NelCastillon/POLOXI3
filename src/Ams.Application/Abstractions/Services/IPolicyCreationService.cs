using Ams.Application.Features.Submissions;

namespace Ams.Application.Abstractions.Services;

public interface IPolicyCreationService
{
    Task<Guid> CreatePolicyFromConfirmedBindAsync(PolicyCreationFromConfirmedBindRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManualPolicyOptionDto>> GetManualPolicyOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ManualPolicyDraftDto> SaveManualPolicyDraftAsync(Guid? draftId, UpsertManualPolicyDraftRequest request, CancellationToken cancellationToken = default);
    Task<ManualPolicyDraftDto?> GetManualPolicyDraftAsync(Guid tenantId, Guid accountId, Guid draftId, CancellationToken cancellationToken = default);
    Task<ManualPolicyValidationResultDto> ValidateManualPolicyAsync(CreateManualPolicyRequest request, CancellationToken cancellationToken = default);
    Task<ManualPolicyCreateResultDto> CreateManualPolicyAsync(CreateManualPolicyRequest request, CancellationToken cancellationToken = default);
}
