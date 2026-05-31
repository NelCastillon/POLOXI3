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

    public Task RemoveMarketAsync(Guid submissionMarketId, CancellationToken cancellationToken = default)
        => _repository.RemoveMarketAsync(submissionMarketId, cancellationToken);

    public Task<IReadOnlyList<QuoteComparisonDto>> GetQuoteComparisonAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetQuoteComparisonAsync(submissionId, cancellationToken);

    public Task<QuoteComparisonDto?> GetQuoteByIdAsync(Guid quoteId, CancellationToken cancellationToken = default)
        => _repository.GetQuoteByIdAsync(quoteId, cancellationToken);

    public Task<ProposalDto?> GetProposalByIdAsync(Guid proposalId, CancellationToken cancellationToken = default)
        => _repository.GetProposalByIdAsync(proposalId, cancellationToken);

    public Task<Guid> GenerateProposalAsync(GenerateProposalRequest request, CancellationToken cancellationToken = default)
        => _repository.GenerateProposalAsync(request, cancellationToken);

    public Task<IReadOnlyList<AppetiteMatchDto>> SearchAppetiteAsync(AppetiteSearchRequest request, CancellationToken cancellationToken = default)
        => _repository.SearchAppetiteAsync(request, cancellationToken);

    public Task<PolicyBindDto?> GetPolicyBySubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _repository.GetPolicyBySubmissionAsync(submissionId, cancellationToken);

    public async Task<Guid> BindPolicyAsync(BindPolicyRequest request, CancellationToken cancellationToken = default)
    {
        // Enterprise rule: binding a policy must trace back to a real submission within the
        // same tenant and the same account so the Policy is never orphaned or cross-tenant.
        if (request.SubmissionId == Guid.Empty)
        {
            throw new InvalidOperationException("Binding a policy requires a parent Submission. SubmissionId was not supplied.");
        }

        var submission = await _repository.GetByIdAsync(request.SubmissionId, cancellationToken)
            ?? throw new InvalidOperationException($"Parent submission '{request.SubmissionId}' was not found.");

        if (request.TenantId != Guid.Empty && submission.TenantId != request.TenantId)
        {
            throw new InvalidOperationException("Parent submission belongs to a different tenant; the policy cannot be bound.");
        }

        if (request.AccountId != Guid.Empty && submission.AccountId != request.AccountId)
        {
            throw new InvalidOperationException("Parent submission is not linked to the supplied account; the bind chain is inconsistent.");
        }

        return await _repository.BindPolicyAsync(request, cancellationToken);
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
