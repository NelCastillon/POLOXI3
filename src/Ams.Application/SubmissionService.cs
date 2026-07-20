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

    public SubmissionService(
        ISubmissionRepository repository,
        IAccountRepository accountRepository,
        IOpportunityRepository opportunityRepository)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _opportunityRepository = opportunityRepository;
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

    public Task<IReadOnlyList<SubmissionDocumentChecklistDto>> GetDocumentChecklistAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetDocumentChecklistAsync(submissionId, tenantId, cancellationToken);

    public Task<SubmissionReadinessDto> GetReadinessAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetReadinessAsync(submissionId, tenantId, cancellationToken);

    public Task<IReadOnlyList<SubmissionTaskTemplateDto>> GetTaskTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetTaskTemplatesAsync(tenantId, cancellationToken);

    public Task<SubmissionMetricsDto> GetMetricsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetMetricsAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<PolicyCreationSourceDto>> GetPolicyCreationSourcesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyCreationSourcesAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<PolicyBindStatusDto>> GetPolicyBindStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyBindStatusesAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<PolicyBindTransactionDto>> GetPolicyBindTransactionsAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyBindTransactionsAsync(submissionId, cancellationToken);

    public Task<SubmissionActionResult> SubmitToMarketAsync(Guid id, SubmitSubmissionToMarketRequest request, CancellationToken cancellationToken = default)
        => _repository.SubmitToMarketAsync(id, request, cancellationToken);

    public Task<SubmissionActionResult> RequestQuoteAsync(Guid id, RequestSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
        => _repository.RequestQuoteAsync(id, request, cancellationToken);

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

    public Task<IReadOnlyList<QuoteComparisonDto>> GetQuoteComparisonAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetQuoteComparisonAsync(submissionId, cancellationToken);

    public Task<QuoteComparisonDto?> GetQuoteByIdAsync(Guid quoteId, CancellationToken cancellationToken = default)
        => _repository.GetQuoteByIdAsync(quoteId, cancellationToken);

    public Task<SubmissionActionResult> RecordQuoteResponseAsync(Guid submissionId, RecordSubmissionQuoteResponseRequest request, CancellationToken cancellationToken = default)
        => _repository.RecordQuoteResponseAsync(submissionId, request, cancellationToken);

    public Task UpdateQuoteAsync(Guid quoteId, UpdateSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateQuoteAsync(quoteId, request, cancellationToken);

    public Task SelectQuoteAsync(Guid submissionId, SelectSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
        => _repository.SelectQuoteAsync(submissionId, request, cancellationToken);

    public Task<ProposalDto?> GetProposalByIdAsync(Guid proposalId, CancellationToken cancellationToken = default)
        => _repository.GetProposalByIdAsync(proposalId, cancellationToken);

    public Task<IReadOnlyList<ProposalWorkflowDto>> GetProposalsAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetProposalsAsync(submissionId, cancellationToken);

    public Task<Guid> GenerateProposalAsync(GenerateProposalRequest request, CancellationToken cancellationToken = default)
        => _repository.GenerateProposalAsync(request, cancellationToken);

    public Task DeliverProposalAsync(Guid proposalId, ProposalDeliveryRequest request, CancellationToken cancellationToken = default)
        => _repository.DeliverProposalAsync(proposalId, request, cancellationToken);

    public Task RecordProposalDecisionAsync(Guid proposalId, ProposalDecisionRequest request, CancellationToken cancellationToken = default)
        => _repository.RecordProposalDecisionAsync(proposalId, request, cancellationToken);

    public Task<IReadOnlyList<AppetiteMatchDto>> SearchAppetiteAsync(AppetiteSearchRequest request, CancellationToken cancellationToken = default)
        => _repository.SearchAppetiteAsync(request, cancellationToken);

    public Task<PolicyBindDto?> GetPolicyBySubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyBySubmissionAsync(submissionId, cancellationToken);

    public Task<PagedResult<PolicyRegisterDto>> SearchPoliciesAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchPoliciesAsync(tenantId, searchTerm, status, lineOfBusiness, pageNumber, pageSize, cancellationToken);

    public Task<PolicyRegisterDto?> GetPolicyByIdAsync(Guid policyId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyByIdAsync(policyId, cancellationToken);

    public Task<Guid> CreatePolicyRegisterAsync(UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default)
        => _repository.CreatePolicyRegisterAsync(request, cancellationToken);

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

            return await _repository.BindPolicyAsync(request, cancellationToken);
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

        var result = await CreatePolicyAsync(request.SubmissionId.Value, new CreatePolicyFromSubmissionRequest(
            TenantId: request.TenantId,
            QuoteId: request.QuoteId.HasValue && request.QuoteId.Value != Guid.Empty ? request.QuoteId : null,
            CarrierId: request.CarrierId,
            AnnualPremium: request.AnnualPremium,
            EffectiveDate: request.EffectiveDate,
            ExpirationDate: request.ExpirationDate,
            PolicyNumber: request.PolicyNumber,
            PolicySourceCode: request.PolicySourceCode,
            PolicySourceReason: request.PolicySourceReason,
            PolicySourceNotes: request.PolicySourceNotes), cancellationToken);

        return result.Id;
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
