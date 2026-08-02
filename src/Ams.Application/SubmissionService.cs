using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Guards;
using Ams.Application.Common.Models;
using Ams.Application.Features.Submissions;

namespace Ams.Application;

public sealed class SubmissionService : ISubmissionService
{
    private readonly ISubmissionRepository _repository;
    private readonly IAccountRepository _accountRepository;
    private readonly IOpportunityRepository _opportunityRepository;
    private readonly IPolicyCreationService _policyCreationService;

    public SubmissionService(
        ISubmissionRepository repository,
        IAccountRepository accountRepository,
        IOpportunityRepository opportunityRepository,
        IPolicyCreationService policyCreationService)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _opportunityRepository = opportunityRepository;
        _policyCreationService = policyCreationService;
    }

    public Task<PagedResult<SubmissionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, status, lineOfBusiness, pageNumber, pageSize, cancellationToken);

    public Task<SubmissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public async Task<Guid> CreateAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: a Submission must never be orphaned. It requires an Account
        // and an Opportunity, both within the same tenant, and the Opportunity must
        // belong to the same Account.
        await TenantGuard.EnsureParentAsync(request.AccountId, request.TenantId, _accountRepository.GetByIdAsync, a => a.TenantId, "Account", "submission", cancellationToken);

        var opportunity = await TenantGuard.EnsureParentAsync(request.OpportunityId, request.TenantId, _opportunityRepository.GetByIdAsync, o => o.TenantId, "Opportunity", "submission", cancellationToken);

        if (opportunity.AccountId != request.AccountId)
        {
            throw new InvalidOperationException("Parent opportunity is not linked to the supplied account; the submission chain is inconsistent.");
        }

        return await _repository.CreateAsync(request, cancellationToken);
    }

    public Task UpdateAsync(Guid id, UpdateSubmissionRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task AssignAsync(Guid id, AssignSubmissionRequest request, CancellationToken cancellationToken = default)
        => _repository.AssignAsync(id, request, cancellationToken);

    public Task<IReadOnlyList<SubmissionActivityDto>> GetActivitiesAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetActivitiesAsync(submissionId, cancellationToken);

    public Task<Guid> AddNoteAsync(Guid submissionId, AddSubmissionNoteRequest request, CancellationToken cancellationToken = default)
        => _repository.AddNoteAsync(submissionId, request, cancellationToken);

    public Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetDocumentsAsync(submissionId, tenantId, cancellationToken);

    public Task<IReadOnlyList<SubmissionTaskDto>> GetTasksAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetTasksAsync(submissionId, tenantId, cancellationToken);

    public Task<Guid> CreateFollowUpTaskAsync(Guid submissionId, CreateSubmissionFollowUpTaskRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateFollowUpTaskAsync(submissionId, request, cancellationToken);

    public Task<IReadOnlyList<SubmissionLineDto>> GetLinesAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetLinesAsync(submissionId, cancellationToken);

    public Task<IReadOnlyList<SubmissionIntakeQuestionDto>> GetIntakeAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetIntakeAsync(submissionId, cancellationToken);

    public Task UpdateIntakeQuestionAsync(Guid submissionId, Guid intakeQuestionId, UpdateSubmissionIntakeQuestionRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateIntakeQuestionAsync(submissionId, intakeQuestionId, request, cancellationToken);

    public Task<IReadOnlyList<SubmissionReadinessEvidenceDocumentDto>> GetReadinessEvidenceDocumentsAsync(Guid submissionId, Guid intakeQuestionId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetReadinessEvidenceDocumentsAsync(submissionId, intakeQuestionId, tenantId, cancellationToken);

    public Task ReplaceReadinessEvidenceDocumentsAsync(Guid submissionId, Guid intakeQuestionId, ReplaceSubmissionReadinessEvidenceRequest request, CancellationToken cancellationToken = default)
        => _repository.ReplaceReadinessEvidenceDocumentsAsync(submissionId, intakeQuestionId, request, cancellationToken);

    public Task<IReadOnlyList<SubmissionDocumentChecklistDto>> GetDocumentChecklistAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetDocumentChecklistAsync(submissionId, tenantId, cancellationToken);

    public Task<SubmissionReadinessDto> GetReadinessAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetReadinessAsync(submissionId, tenantId, cancellationToken);

    public Task<BindCommissionEstimateDto> GetBindCommissionEstimateAsync(Guid submissionId, Guid quoteId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetBindCommissionEstimateAsync(submissionId, quoteId, tenantId, cancellationToken);

    public Task<SubmissionReadinessDto> GetMarketReadinessAsync(Guid submissionId, Guid submissionMarketId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetMarketReadinessAsync(submissionId, submissionMarketId, tenantId, cancellationToken);

    public Task<SubmissionPackagePreviewDto> GetSubmissionPackagePreviewAsync(Guid submissionId, Guid? submissionMarketId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetSubmissionPackagePreviewAsync(submissionId, submissionMarketId, tenantId, cancellationToken);

    public Task<IReadOnlyList<SubmissionReadinessRequirementDto>> GetReadinessRequirementsAsync(Guid tenantId, string? searchTerm, CancellationToken cancellationToken = default)
        => _repository.GetReadinessRequirementsAsync(tenantId, searchTerm, cancellationToken);

    public Task<Guid> UpsertReadinessRequirementAsync(Guid? readinessRequirementId, UpsertSubmissionReadinessRequirementRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertReadinessRequirementAsync(readinessRequirementId, request, cancellationToken);

    public Task DeleteReadinessRequirementAsync(Guid readinessRequirementId, Guid tenantId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteReadinessRequirementAsync(readinessRequirementId, tenantId, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<SubmissionTaskTemplateDto>> GetTaskTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetTaskTemplatesAsync(tenantId, cancellationToken);

    public Task<SubmissionMetricsDto> GetMetricsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetMetricsAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<PolicyCreationSourceDto>> GetPolicyCreationSourcesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyCreationSourcesAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<PolicyBindStatusDto>> GetPolicyBindStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyBindStatusesAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<BindQueueItemDto>> GetBindQueueAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetBindQueueAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<PolicyBindTransactionDto>> GetPolicyBindTransactionsAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyBindTransactionsAsync(submissionId, cancellationToken);

    public async Task<SubmissionActionResult> SubmitToMarketAsync(Guid id, SubmitSubmissionToMarketRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSubmissionIsOpenAsync(id, request.TenantId, cancellationToken);
        return await _repository.SubmitToMarketAsync(id, request, cancellationToken);
    }

    public async Task<SubmissionActionResult> RequestQuoteAsync(Guid id, RequestSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSubmissionIsOpenAsync(id, request.TenantId, cancellationToken);
        return await _repository.RequestQuoteAsync(id, request, cancellationToken);
    }

    public Task<SubmissionActionResult> CopyAsync(Guid id, CopySubmissionRequest request, CancellationToken cancellationToken = default)
        => _repository.CopyAsync(id, request, cancellationToken);

    public Task<SubmissionActionResult> DeclineAsync(Guid id, DeclineSubmissionRequest request, CancellationToken cancellationToken = default)
        => _repository.DeclineAsync(id, request, cancellationToken);

    public async Task<SubmissionActionResult> CreatePolicyAsync(Guid id, CreatePolicyFromSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: a Policy can only be issued from a real submission within the same tenant.
        var submission = await _repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Submission '{id}' was not found.");

        if (request.TenantId != Guid.Empty && submission.TenantId != request.TenantId)
        {
            throw new InvalidOperationException("Submission belongs to a different tenant; a policy cannot be created from it.");
        }

        await EnsureSubmissionIsOpenAsync(id, request.TenantId, cancellationToken);
        return await _repository.CreatePolicyAsync(id, request, cancellationToken);
    }

    public Task<IReadOnlyList<SubmissionMarketDto>> GetMarketsAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetMarketsAsync(submissionId, cancellationToken);

    public Task<IReadOnlyList<SubmissionMarketDto>> GetMarketSuggestionsAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetMarketSuggestionsAsync(submissionId, cancellationToken);

    public Task<Guid> AddMarketAsync(AddSubmissionMarketRequest request, CancellationToken cancellationToken = default)
        => _repository.AddMarketAsync(request, cancellationToken);

    public Task UpdateMarketStatusAsync(Guid submissionMarketId, UpdateSubmissionMarketStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateMarketStatusAsync(submissionMarketId, request, cancellationToken);

    public Task UpdateMarketPackageAsync(UpdateSubmissionMarketPackageRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateMarketPackageAsync(request, cancellationToken);

    public Task RemoveMarketAsync(Guid submissionMarketId, CancellationToken cancellationToken = default)
        => _repository.RemoveMarketAsync(submissionMarketId, cancellationToken);

    public Task<int> SynchronizeOverdueMarketRequestsAsync(CancellationToken cancellationToken = default)
        => _repository.SynchronizeOverdueMarketRequestsAsync(cancellationToken);

    public Task<IReadOnlyList<QuoteComparisonDto>> GetQuoteComparisonAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetQuoteComparisonAsync(submissionId, tenantId, cancellationToken);

    public Task<QuoteComparisonDto?> GetQuoteByIdAsync(Guid quoteId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetQuoteByIdAsync(quoteId, tenantId, cancellationToken);

    public async Task<SubmissionActionResult> RecordQuoteResponseAsync(Guid submissionId, RecordSubmissionQuoteResponseRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSubmissionIsOpenAsync(submissionId, request.TenantId, cancellationToken);
        return await _repository.RecordQuoteResponseAsync(submissionId, request, cancellationToken);
    }

    public Task<Guid> RecordCarrierInboundResponseAsync(Guid submissionId, RecordCarrierInboundResponseRequest request, CancellationToken cancellationToken = default)
        => _repository.RecordCarrierInboundResponseAsync(submissionId, request, cancellationToken);

    public Task UpdateQuoteAsync(Guid quoteId, UpdateSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateQuoteAsync(quoteId, request, cancellationToken);

    public async Task SelectQuoteAsync(Guid submissionId, SelectSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSubmissionIsOpenAsync(submissionId, request.TenantId, cancellationToken);
        await _repository.SelectQuoteAsync(submissionId, request, cancellationToken);
    }

    public Task<ProposalDto?> GetProposalByIdAsync(Guid proposalId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetProposalByIdAsync(proposalId, tenantId, cancellationToken);

    public Task<IReadOnlyList<ProposalWorkflowDto>> GetProposalsAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetProposalsAsync(submissionId, tenantId, cancellationToken);

    public Task<ProposalWorkflowLaunchDto> GetProposalWorkflowLaunchAsync(Guid opportunityId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetProposalWorkflowLaunchAsync(opportunityId, tenantId, cancellationToken);

    public Task<IReadOnlyList<ProposalWorkflowOptionDto>> GetProposalWorkflowOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetProposalWorkflowOptionsAsync(tenantId, cancellationToken);

    public Task<Guid> GenerateProposalAsync(GenerateProposalRequest request, CancellationToken cancellationToken = default)
        => _repository.GenerateProposalAsync(request, cancellationToken);

    public Task SubmitProposalReviewAsync(Guid proposalId, SubmitProposalReviewRequest request, CancellationToken cancellationToken = default)
        => _repository.SubmitProposalReviewAsync(proposalId, request, cancellationToken);

    public Task DecideProposalReviewAsync(Guid proposalId, DecideProposalReviewRequest request, CancellationToken cancellationToken = default)
        => _repository.DecideProposalReviewAsync(proposalId, request, cancellationToken);

    public Task<Guid> UpsertProposalRecipientAsync(Guid proposalId, UpsertProposalRecipientRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertProposalRecipientAsync(proposalId, request, cancellationToken);

    public Task DeleteProposalRecipientAsync(Guid proposalId, Guid recipientId, Guid tenantId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
        => _repository.DeleteProposalRecipientAsync(proposalId, recipientId, tenantId, modifiedByUserId, cancellationToken);

    public Task<IReadOnlyList<ProposalSlaPolicyDto>> GetProposalSlaPoliciesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetProposalSlaPoliciesAsync(tenantId, cancellationToken);

    public Task<Guid> UpsertProposalSlaPolicyAsync(UpsertProposalSlaPolicyRequest request, CancellationToken cancellationToken = default)
        => _repository.UpsertProposalSlaPolicyAsync(request, cancellationToken);

    public Task<Guid> ProcessProposalProviderCallbackAsync(ProposalProviderCallbackRequest request, CancellationToken cancellationToken = default)
        => _repository.ProcessProposalProviderCallbackAsync(request, cancellationToken);

    public Task<ProposalDeliveryDispatchDto> DeliverProposalAsync(Guid proposalId, ProposalDeliveryRequest request, CancellationToken cancellationToken = default)
        => _repository.DeliverProposalAsync(proposalId, request, cancellationToken);

    public Task<IReadOnlyList<ProposalDeliveryMonitorDto>> GetProposalDeliveryMonitorAsync(Guid tenantId, string? status, string? searchTerm, CancellationToken cancellationToken = default)
        => _repository.GetProposalDeliveryMonitorAsync(tenantId, status, searchTerm, cancellationToken);

    public Task<IReadOnlyList<ProposalDeliveryDispatchDto>> GetProposalDeliveriesAsync(Guid proposalId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetProposalDeliveriesAsync(proposalId, tenantId, cancellationToken);

    public Task<ProposalDeliveryDispatchDto> RetryProposalDeliveryAsync(Guid dispatchId, RetryProposalDeliveryRequest request, CancellationToken cancellationToken = default)
        => _repository.RetryProposalDeliveryAsync(dispatchId, request, cancellationToken);

    public Task<ProposalDeliveryDispatchDto> UpdateProposalDeliveryRecipientAsync(Guid dispatchId, UpdateProposalDeliveryRecipientRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateProposalDeliveryRecipientAsync(dispatchId, request, cancellationToken);

    public Task<ProposalDeliveryDispatchDto> ResendProposalDeliveryAsync(Guid dispatchId, ResendProposalDeliveryRequest request, CancellationToken cancellationToken = default)
        => _repository.ResendProposalDeliveryAsync(dispatchId, request, cancellationToken);

    public Task DeleteProposalDeliveryAsync(Guid dispatchId, DeleteProposalDeliveryRequest request, CancellationToken cancellationToken = default)
        => _repository.DeleteProposalDeliveryAsync(dispatchId, request, cancellationToken);

    public Task<IReadOnlyList<ProposalDeliveryProviderDto>> GetProposalDeliveryProvidersAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetProposalDeliveryProvidersAsync(tenantId, cancellationToken);

    public Task UpdateProposalDeliveryProviderAsync(Guid providerId, UpdateProposalDeliveryProviderRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateProposalDeliveryProviderAsync(providerId, request, cancellationToken);

    public Task PresentProposalAsync(Guid proposalId, ProposalPresentationRequest request, CancellationToken cancellationToken = default)
        => _repository.PresentProposalAsync(proposalId, request, cancellationToken);

    public Task<ProposalBindContinuationDto> GetProposalBindContinuationAsync(Guid proposalId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetProposalBindContinuationAsync(proposalId, tenantId, cancellationToken);

    public Task<ClientAcceptanceReadinessDto> GetClientAcceptanceReadinessAsync(Guid proposalId, Guid? quoteId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetClientAcceptanceReadinessAsync(proposalId, quoteId, tenantId, cancellationToken);

    public Task<IReadOnlyList<ClientAcceptanceDto>> GetClientAcceptancesAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetClientAcceptancesAsync(submissionId, tenantId, cancellationToken);

    public Task<ClientAcceptanceDto?> GetClientAcceptanceByIdAsync(Guid clientAcceptanceId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetClientAcceptanceByIdAsync(clientAcceptanceId, tenantId, cancellationToken);

    public Task<Guid> RecordClientAcceptanceAsync(RecordClientAcceptanceRequest request, CancellationToken cancellationToken = default)
        => _repository.RecordClientAcceptanceAsync(request, cancellationToken);

    public Task WithdrawClientAcceptanceAsync(Guid clientAcceptanceId, WithdrawClientAcceptanceRequest request, CancellationToken cancellationToken = default)
        => _repository.WithdrawClientAcceptanceAsync(clientAcceptanceId, request, cancellationToken);

    public Task<IReadOnlyList<AppetiteMatchDto>> SearchAppetiteAsync(AppetiteSearchRequest request, CancellationToken cancellationToken = default)
        => _repository.SearchAppetiteAsync(request, cancellationToken);

    public Task<PolicyBindDto?> GetPolicyBySubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyBySubmissionAsync(submissionId, cancellationToken);

    public Task<PagedResult<PolicyRegisterDto>> SearchPoliciesAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchPoliciesAsync(tenantId, searchTerm, status, lineOfBusiness, pageNumber, pageSize, cancellationToken);

    public Task<PolicyRegisterDto?> GetPolicyByIdAsync(Guid policyId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyByIdAsync(policyId, cancellationToken);

    public async Task<Guid> CreatePolicyRegisterAsync(UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default)
    {
        var bindTransactionId = await _repository.CreatePolicyRegisterAsync(request, cancellationToken);
        return await _policyCreationService.CreatePolicyFromConfirmedBindAsync(new PolicyCreationFromConfirmedBindRequest(request.TenantId, bindTransactionId, request.ModifiedByUserId), cancellationToken);
    }

    private async Task EnsureSubmissionIsOpenAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken)
    {
        var submission = await _repository.GetByIdAsync(submissionId, cancellationToken)
            ?? throw new InvalidOperationException("Submission was not found.");
        if (submission.TenantId != tenantId) throw new InvalidOperationException("Submission belongs to a different tenant.");
        if ((await _repository.GetPolicyBindTransactionsAsync(submissionId, cancellationToken)).Any(x => x.TenantId == tenantId && x.PolicyId.HasValue))
            throw new InvalidOperationException("This submission is historical because policy generation is complete. Continue in the policy workspace.");
    }

    public Task UpdatePolicyRegisterAsync(Guid policyId, UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdatePolicyRegisterAsync(policyId, request, cancellationToken);

    public Task<SubmissionActionResult> ExecutePolicyRegisterActionAsync(Guid policyId, PolicyRegisterActionRequest request, CancellationToken cancellationToken = default)
        => _repository.ExecutePolicyRegisterActionAsync(policyId, request, cancellationToken);

    public async Task<Guid> BindPolicyAsync(BindPolicyRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: binding a policy must trace back to a real submission within the
        // same tenant and the same account so the Policy is never orphaned or cross-tenant.
        if (!request.SubmissionId.HasValue || request.SubmissionId.Value == Guid.Empty)
        {
            if (request.AccountId == Guid.Empty)
            {
                throw new InvalidOperationException("Direct policy binding requires an Account when no parent Submission is supplied.");
            }

            var directBindTransactionId = await _repository.BindPolicyAsync(request, cancellationToken);
            if (await BindStatusCreatesPolicyAsync(request.TenantId, request.BindStatusCode, cancellationToken))
            {
                return await _policyCreationService.CreatePolicyFromConfirmedBindAsync(new PolicyCreationFromConfirmedBindRequest(request.TenantId, directBindTransactionId, request.RequestedByUserId), cancellationToken);
            }

            return directBindTransactionId;
        }

        var submission = await _repository.GetByIdAsync(request.SubmissionId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Parent submission '{request.SubmissionId}' was not found.");

        if (request.TenantId != Guid.Empty && submission.TenantId != request.TenantId)
        {
            throw new InvalidOperationException("Parent submission belongs to a different tenant; the policy cannot be bound.");
        }

        if (request.AccountId != Guid.Empty && submission.AccountId != request.AccountId)
        {
            throw new InvalidOperationException("Parent submission is not linked to the supplied account; the bind chain is inconsistent.");
        }

        var createsPolicy = await BindStatusCreatesPolicyAsync(request.TenantId, request.BindStatusCode, cancellationToken);
        var requestedStatus = request.BindStatusCode;
        var createRequest = createsPolicy || string.Equals(request.BindStatusCode, "Draft", StringComparison.OrdinalIgnoreCase)
            ? request
            : request with { BindStatusCode = "Draft" };
        var bindTransactionId = await _repository.BindPolicyAsync(createRequest, cancellationToken);
        if (createsPolicy)
        {
            return await _policyCreationService.CreatePolicyFromConfirmedBindAsync(new PolicyCreationFromConfirmedBindRequest(request.TenantId, bindTransactionId, request.RequestedByUserId), cancellationToken);
        }

        await _repository.ValidateBindRequestAsync(bindTransactionId, new ValidateBindRequestRequest(request.TenantId, request.RequestedByUserId), cancellationToken);
        if (!string.Equals(requestedStatus, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            await UpdateBindRequestStatusAsync(bindTransactionId, new UpdateBindRequestStatusRequest(request.TenantId, requestedStatus, "Initial bind request workflow status.", request.RequestedByUserId), cancellationToken);
        }

        return bindTransactionId;
    }

    public Task<BindRequestDetailDto?> GetBindRequestDetailAsync(Guid policyBindTransactionId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetBindRequestDetailAsync(policyBindTransactionId, tenantId, cancellationToken);

    public Task<IReadOnlyList<BindValidationResultDto>> ValidateBindRequestAsync(Guid policyBindTransactionId, ValidateBindRequestRequest request, CancellationToken cancellationToken = default)
        => _repository.ValidateBindRequestAsync(policyBindTransactionId, request, cancellationToken);

    public async Task UpdateBindRequestStatusAsync(Guid policyBindTransactionId, UpdateBindRequestStatusRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _repository.GetBindRequestDetailAsync(policyBindTransactionId, request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Bind request was not found for this tenant.");
        var current = detail.Request.BindStatusCode;
        var next = request.StatusCode;
        if (string.Equals(current, next, StringComparison.OrdinalIgnoreCase)) return;

        var currentStatus = (await _repository.GetPolicyBindStatusesAsync(request.TenantId, cancellationToken))
            .SingleOrDefault(x => string.Equals(x.StatusCode, current, StringComparison.OrdinalIgnoreCase));
        if (currentStatus?.IsTerminal == true)
            throw new InvalidOperationException($"A terminal bind request in '{currentStatus.StatusName}' cannot transition to another status.");
        if (string.Equals(next, "Bound", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bound status can only be recorded through an authoritative carrier response.");

        var transition = detail.AllowedTransitions.SingleOrDefault(x => string.Equals(x.ToStatusCode, next, StringComparison.OrdinalIgnoreCase));
        if (transition is null || transition.RequiresCarrierResponse)
            throw new InvalidOperationException($"Transition from '{current}' to '{next}' is not allowed through a manual status change.");

        if (transition.RequiresValidation)
        {
            var validations = await _repository.ValidateBindRequestAsync(policyBindTransactionId, new ValidateBindRequestRequest(request.TenantId, request.ChangedByUserId), cancellationToken);
            var blockers = validations.Where(x => x.IsBlocking && x.StatusCode is not ("Passed" or "Waived")).ToArray();
            if (blockers.Length > 0)
                throw new InvalidOperationException("Bind request is blocked: " + string.Join("; ", blockers.Select(x => x.Message ?? x.RequirementName)));
            if (detail.Request.ApprovalRequired && !detail.Approvals.Any(x => x.StatusCode == "Approved"))
                throw new InvalidOperationException("Required manager approval has not been completed.");
            if (detail.Request.PaymentRequired && !detail.Request.PaymentVerified)
                throw new InvalidOperationException("Required payment has not been verified.");
        }

        await _repository.UpdateBindRequestStatusAsync(policyBindTransactionId, request, cancellationToken);
    }

    public Task<Guid> RequestBindApprovalAsync(Guid policyBindTransactionId, RequestBindApprovalRequest request, CancellationToken cancellationToken = default)
        => _repository.RequestBindApprovalAsync(policyBindTransactionId, request, cancellationToken);

    public Task DecideBindApprovalAsync(Guid policyBindTransactionId, Guid bindApprovalId, DecideBindApprovalRequest request, CancellationToken cancellationToken = default)
        => _repository.DecideBindApprovalAsync(policyBindTransactionId, bindApprovalId, request, cancellationToken);

    public async Task<Guid?> RecordBindCarrierResponseAsync(Guid policyBindTransactionId, RecordBindCarrierResponseRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _repository.GetBindRequestDetailAsync(policyBindTransactionId, request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Bind request was not found for this tenant.");
        var createsPolicy = await BindStatusCreatesPolicyAsync(request.TenantId, request.StatusCode, cancellationToken);
        var transition = detail.AllowedTransitions.SingleOrDefault(x => string.Equals(x.ToStatusCode, request.StatusCode, StringComparison.OrdinalIgnoreCase));
        if (transition is null || !transition.RequiresCarrierResponse)
            throw new InvalidOperationException($"Carrier response cannot transition this bind request from '{detail.Request.BindStatusCode}' to '{request.StatusCode}'.");
        if (createsPolicy)
        {
            if (!request.ConfirmationCertified || string.IsNullOrWhiteSpace(request.ConfirmationSourceCode))
                throw new InvalidOperationException("Certified carrier confirmation and its source are required before coverage can be marked bound.");
            if (string.IsNullOrWhiteSpace(request.CarrierReferenceNumber) && string.IsNullOrWhiteSpace(request.BinderNumber) && !request.ConfirmationDocumentId.HasValue)
                throw new InvalidOperationException("Carrier confirmation requires a carrier reference, binder number, or confirmation document.");
        }

        await _repository.RecordBindCarrierResponseAsync(policyBindTransactionId, request, cancellationToken);
        await _repository.UpdateBindRequestStatusAsync(policyBindTransactionId, new UpdateBindRequestStatusRequest(request.TenantId, request.StatusCode, request.MessageBody, request.RecordedByUserId), cancellationToken);

        if (request.StatusCode == "NeedInformation")
        {
            await _repository.CreateFollowUpTaskAsync(detail.Request.SubmissionId, new CreateSubmissionFollowUpTaskRequest(request.TenantId, "Carrier requested bind information", request.MessageBody, "High", detail.Request.RequestedByUserId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), request.RecordedByUserId), cancellationToken);
        }

        return null;
    }

    public async Task<BindPackageDto> PrepareBindPackageAsync(Guid policyBindTransactionId, PrepareBindPackageRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _repository.GetBindRequestDetailAsync(policyBindTransactionId, request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Bind request was not found for this tenant.");
        var validations = await _repository.ValidateBindRequestAsync(policyBindTransactionId, new ValidateBindRequestRequest(request.TenantId, request.PreparedByUserId), cancellationToken);
        var missingDocuments = validations.Where(x => x.RequirementTypeCode == "Document" && x.IsBlocking && x.StatusCode is not ("Passed" or "Waived")).ToArray();
        if (missingDocuments.Length > 0)
            throw new InvalidOperationException("Binder package cannot be prepared until required documents are available: " + string.Join(", ", missingDocuments.Select(x => x.RequirementName)));
        return await _repository.PrepareBindPackageAsync(policyBindTransactionId, request, cancellationToken);
    }

    private async Task<bool> BindStatusCreatesPolicyAsync(Guid tenantId, string statusCode, CancellationToken cancellationToken)
    {
        var statuses = await _repository.GetPolicyBindStatusesAsync(tenantId, cancellationToken);
        return statuses.Any(status => status.IsActive && status.CreatesPolicy && string.Equals(status.StatusCode, statusCode, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class SubmissionReferenceOptionService : ISubmissionReferenceOptionService
{
    private readonly ISubmissionReferenceOptionRepository _repository;

    public SubmissionReferenceOptionService(ISubmissionReferenceOptionRepository repository)
    {
        _repository = repository;
    }

    public Task<List<SubmissionReferenceOptionDto>> GetAllAsync(Guid tenantId, string? optionGroup = null, CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(tenantId, optionGroup, cancellationToken);
}
