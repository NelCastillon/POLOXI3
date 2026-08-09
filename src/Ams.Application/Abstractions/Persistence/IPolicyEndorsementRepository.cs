using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyEndorsements;

namespace Ams.Application.Abstractions.Persistence;

public interface IPolicyEndorsementRepository
{
    Task<PolicyEndorsementCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyEndorsementOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PolicyEndorsementCatalogDto> GetCatalogAsync(Guid tenantId, string? lineOfBusinessCode = null, CancellationToken cancellationToken = default);
    Task<PolicyEndorsementTypeCatalogDto?> GetTypeCatalogAsync(Guid tenantId, string typeCode, CancellationToken cancellationToken = default);
    Task UpdateTypeProfileAsync(Guid endorsementTypeId, UpdatePolicyEndorsementTypeProfileRequest request, CancellationToken cancellationToken = default);
    Task ReplaceTypeConfigurationAsync(Guid endorsementTypeId, ReplacePolicyEndorsementTypeConfigurationRequest request, CancellationToken cancellationToken = default);
    Task<PolicyEndorsementDetailDto?> GetDetailAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken = default);
    Task<PolicyEndorsementWorkflowDetailDto?> GetWorkflowDetailAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken = default);
    Task<PolicyEndorsementRoutePreviewDto?> GetRoutePreviewAsync(Guid tenantId, Guid endorsementId, string routePurposeCode, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyEndorsementApprovalInboxItemDto>> GetApprovalInboxAsync(Guid tenantId, Guid assignedToUserId, CancellationToken cancellationToken = default);
    Task<PolicyEndorsementPolicyWorkspaceDto?> GetPolicyWorkspaceAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreatePolicyEndorsementRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateTransactionAsync(CreatePolicyEndorsementTransactionRequest request, CancellationToken cancellationToken = default);
    Task SaveDraftAsync(Guid endorsementId, SavePolicyEndorsementDraftRequest request, CancellationToken cancellationToken = default);
    Task TransitionAsync(Guid endorsementId, TransitionPolicyEndorsementRequest request, CancellationToken cancellationToken = default);
    Task DecideApprovalAsync(Guid endorsementId, Guid approvalId, DecidePolicyEndorsementApprovalRequest request, CancellationToken cancellationToken = default);
    Task AssignApprovalAsync(Guid endorsementId, Guid approvalId, AssignPolicyEndorsementApprovalRequest request, CancellationToken cancellationToken = default);
    Task<Guid> RequestInformationAsync(Guid endorsementId, RequestPolicyEndorsementInformationRequest request, CancellationToken cancellationToken = default);
    Task RespondToInformationRequestAsync(Guid endorsementId, Guid informationRequestId, RespondPolicyEndorsementInformationRequest request, CancellationToken cancellationToken = default);
    Task ResubmitInformationRequestAsync(Guid endorsementId, Guid informationRequestId, ResubmitPolicyEndorsementInformationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyEndorsementCarrierDispatchWorkItem>> ClaimCarrierDispatchesAsync(string workerId, int take, TimeSpan lease, CancellationToken cancellationToken = default);
    Task CompleteCarrierDispatchAsync(Guid dispatchId, string workerId, CompletePolicyEndorsementCarrierDispatch result, CancellationToken cancellationToken = default);
    Task FailCarrierDispatchAsync(Guid dispatchId, string workerId, FailPolicyEndorsementCarrierDispatch result, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyEndorsementAccountingWorkItem>> ClaimAccountingWorkAsync(string workerId, int take, TimeSpan lease, CancellationToken cancellationToken = default);
    Task CompleteAccountingWorkAsync(Guid workId, string workerId, CompletePolicyEndorsementAccountingWork result, CancellationToken cancellationToken = default);
    Task FailAccountingWorkAsync(Guid workId, string workerId, FailPolicyEndorsementWork result, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyEndorsementDocumentWorkItem>> ClaimDocumentWorkAsync(string workerId, int take, TimeSpan lease, CancellationToken cancellationToken = default);
    Task CompleteDocumentWorkAsync(Guid workId, string workerId, CompletePolicyEndorsementDocumentWork result, CancellationToken cancellationToken = default);
    Task FailDocumentWorkAsync(Guid workId, string workerId, FailPolicyEndorsementWork result, CancellationToken cancellationToken = default);
    Task LinkReversalAsync(Guid tenantId, Guid originalEndorsementId, Guid reversalEndorsementId, Guid? actorUserId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid endorsementId, UpdatePolicyEndorsementRequest request, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid endorsementId, UpdatePolicyEndorsementStatusRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AddActivityAsync(AddPolicyEndorsementActivityRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertDeltaAsync(UpsertPolicyEndorsementDeltaRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid endorsementId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
