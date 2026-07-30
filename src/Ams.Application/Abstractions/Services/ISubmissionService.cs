using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Submissions;

namespace Ams.Application.Abstractions.Services;

public interface ISubmissionService
{
    // Submission register
    Task<PagedResult<SubmissionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<SubmissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateSubmissionRequest request, CancellationToken cancellationToken = default);
    Task AssignAsync(Guid id, AssignSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmissionActivityDto>> GetActivitiesAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<Guid> AddNoteAsync(Guid submissionId, AddSubmissionNoteRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmissionTaskDto>> GetTasksAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> CreateFollowUpTaskAsync(Guid submissionId, CreateSubmissionFollowUpTaskRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmissionLineDto>> GetLinesAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmissionIntakeQuestionDto>> GetIntakeAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task UpdateIntakeQuestionAsync(Guid submissionId, Guid intakeQuestionId, UpdateSubmissionIntakeQuestionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmissionReadinessEvidenceDocumentDto>> GetReadinessEvidenceDocumentsAsync(Guid submissionId, Guid intakeQuestionId, Guid tenantId, CancellationToken cancellationToken = default);
    Task ReplaceReadinessEvidenceDocumentsAsync(Guid submissionId, Guid intakeQuestionId, ReplaceSubmissionReadinessEvidenceRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmissionDocumentChecklistDto>> GetDocumentChecklistAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SubmissionReadinessDto> GetReadinessAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SubmissionReadinessDto> GetMarketReadinessAsync(Guid submissionId, Guid submissionMarketId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SubmissionPackagePreviewDto> GetSubmissionPackagePreviewAsync(Guid submissionId, Guid? submissionMarketId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmissionReadinessRequirementDto>> GetReadinessRequirementsAsync(Guid tenantId, string? searchTerm, CancellationToken cancellationToken = default);
    Task<Guid> UpsertReadinessRequirementAsync(Guid? readinessRequirementId, UpsertSubmissionReadinessRequirementRequest request, CancellationToken cancellationToken = default);
    Task DeleteReadinessRequirementAsync(Guid readinessRequirementId, Guid tenantId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmissionTaskTemplateDto>> GetTaskTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<SubmissionMetricsDto> GetMetricsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyCreationSourceDto>> GetPolicyCreationSourcesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyBindStatusDto>> GetPolicyBindStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyBindTransactionDto>> GetPolicyBindTransactionsAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<SubmissionActionResult> SubmitToMarketAsync(Guid id, SubmitSubmissionToMarketRequest request, CancellationToken cancellationToken = default);
    Task<SubmissionActionResult> RequestQuoteAsync(Guid id, RequestSubmissionQuoteRequest request, CancellationToken cancellationToken = default);
    Task<SubmissionActionResult> CopyAsync(Guid id, CopySubmissionRequest request, CancellationToken cancellationToken = default);
    Task<SubmissionActionResult> DeclineAsync(Guid id, DeclineSubmissionRequest request, CancellationToken cancellationToken = default);
    Task<SubmissionActionResult> CreatePolicyAsync(Guid id, CreatePolicyFromSubmissionRequest request, CancellationToken cancellationToken = default);

    // Markets
    Task<IReadOnlyList<SubmissionMarketDto>> GetMarketsAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmissionMarketDto>> GetMarketSuggestionsAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<Guid> AddMarketAsync(AddSubmissionMarketRequest request, CancellationToken cancellationToken = default);
    Task UpdateMarketStatusAsync(Guid submissionMarketId, UpdateSubmissionMarketStatusRequest request, CancellationToken cancellationToken = default);
    Task UpdateMarketPackageAsync(UpdateSubmissionMarketPackageRequest request, CancellationToken cancellationToken = default);
    Task RemoveMarketAsync(Guid submissionMarketId, CancellationToken cancellationToken = default);
    Task<int> SynchronizeOverdueMarketRequestsAsync(CancellationToken cancellationToken = default);

    // Quotes
    Task<IReadOnlyList<QuoteComparisonDto>> GetQuoteComparisonAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<QuoteComparisonDto?> GetQuoteByIdAsync(Guid quoteId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SubmissionActionResult> RecordQuoteResponseAsync(Guid submissionId, RecordSubmissionQuoteResponseRequest request, CancellationToken cancellationToken = default);
    Task<Guid> RecordCarrierInboundResponseAsync(Guid submissionId, RecordCarrierInboundResponseRequest request, CancellationToken cancellationToken = default);
    Task UpdateQuoteAsync(Guid quoteId, UpdateSubmissionQuoteRequest request, CancellationToken cancellationToken = default);
    Task SelectQuoteAsync(Guid submissionId, SelectSubmissionQuoteRequest request, CancellationToken cancellationToken = default);

    // Proposals
    Task<ProposalDto?> GetProposalByIdAsync(Guid proposalId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProposalWorkflowDto>> GetProposalsAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<ProposalWorkflowLaunchDto> GetProposalWorkflowLaunchAsync(Guid opportunityId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProposalWorkflowOptionDto>> GetProposalWorkflowOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> GenerateProposalAsync(GenerateProposalRequest request, CancellationToken cancellationToken = default);
    Task SubmitProposalReviewAsync(Guid proposalId, SubmitProposalReviewRequest request, CancellationToken cancellationToken = default);
    Task DecideProposalReviewAsync(Guid proposalId, DecideProposalReviewRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertProposalRecipientAsync(Guid proposalId, UpsertProposalRecipientRequest request, CancellationToken cancellationToken = default);
    Task DeleteProposalRecipientAsync(Guid proposalId, Guid recipientId, Guid tenantId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProposalSlaPolicyDto>> GetProposalSlaPoliciesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> UpsertProposalSlaPolicyAsync(UpsertProposalSlaPolicyRequest request, CancellationToken cancellationToken = default);
    Task<Guid> ProcessProposalProviderCallbackAsync(ProposalProviderCallbackRequest request, CancellationToken cancellationToken = default);
    Task<ProposalDeliveryDispatchDto> DeliverProposalAsync(Guid proposalId, ProposalDeliveryRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProposalDeliveryMonitorDto>> GetProposalDeliveryMonitorAsync(Guid tenantId, string? status, string? searchTerm, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProposalDeliveryDispatchDto>> GetProposalDeliveriesAsync(Guid proposalId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<ProposalDeliveryDispatchDto> RetryProposalDeliveryAsync(Guid dispatchId, RetryProposalDeliveryRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProposalDeliveryProviderDto>> GetProposalDeliveryProvidersAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task UpdateProposalDeliveryProviderAsync(Guid providerId, UpdateProposalDeliveryProviderRequest request, CancellationToken cancellationToken = default);
    Task PresentProposalAsync(Guid proposalId, ProposalPresentationRequest request, CancellationToken cancellationToken = default);
    Task<ProposalBindContinuationDto> GetProposalBindContinuationAsync(Guid proposalId, Guid tenantId, CancellationToken cancellationToken = default);

    // Client acceptance
    Task<ClientAcceptanceReadinessDto> GetClientAcceptanceReadinessAsync(Guid proposalId, Guid? quoteId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientAcceptanceDto>> GetClientAcceptancesAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<ClientAcceptanceDto?> GetClientAcceptanceByIdAsync(Guid clientAcceptanceId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> RecordClientAcceptanceAsync(RecordClientAcceptanceRequest request, CancellationToken cancellationToken = default);
    Task WithdrawClientAcceptanceAsync(Guid clientAcceptanceId, WithdrawClientAcceptanceRequest request, CancellationToken cancellationToken = default);

    // Appetite
    Task<IReadOnlyList<AppetiteMatchDto>> SearchAppetiteAsync(AppetiteSearchRequest request, CancellationToken cancellationToken = default);

    // Bind
    Task<PagedResult<PolicyRegisterDto>> SearchPoliciesAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PolicyRegisterDto?> GetPolicyByIdAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<Guid> CreatePolicyRegisterAsync(UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default);
    Task UpdatePolicyRegisterAsync(Guid policyId, UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default);
    Task<SubmissionActionResult> ExecutePolicyRegisterActionAsync(Guid policyId, PolicyRegisterActionRequest request, CancellationToken cancellationToken = default);
    Task<PolicyBindDto?> GetPolicyBySubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<Guid> BindPolicyAsync(BindPolicyRequest request, CancellationToken cancellationToken = default);
}

public interface ISubmissionReferenceOptionService
{
    Task<List<SubmissionReferenceOptionDto>> GetAllAsync(Guid tenantId, string? optionGroup = null, CancellationToken cancellationToken = default);
}
