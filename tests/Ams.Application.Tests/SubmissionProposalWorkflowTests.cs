using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Accounts;
using Ams.Application.Features.Opportunities;
using Ams.Application.Features.Submissions;
using Xunit;

namespace Ams.Application.Tests;

public sealed class SubmissionProposalWorkflowTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SubmissionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ProposalId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid QuoteId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid UserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid AccountId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid OpportunityId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid DispatchId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    [Fact]
    public async Task GenerateProposalAsync_Forwards_QuoteIds_And_CustomIntroduction()
    {
        var repository = new FakeSubmissionRepository { GeneratedProposalId = ProposalId };
        var service = CreateService(repository);
        var request = new GenerateProposalRequest(SubmissionId, TenantId, "Enterprise proposal", [QuoteId], "Executive introduction");

        var result = await service.GenerateProposalAsync(request);

        Assert.Equal(ProposalId, result);
        Assert.Same(request, repository.LastGenerateProposalRequest);
        Assert.Equal(QuoteId, repository.LastGenerateProposalRequest!.QuoteIds.Single());
        Assert.Equal("Executive introduction", repository.LastGenerateProposalRequest.CustomIntroduction);
    }

    [Fact]
    public async Task DeliverProposalAsync_Forwards_Delivery_Metadata()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);
        var request = new ProposalDeliveryRequest(TenantId, "Email", "client@example.com", UserId);

        var result = await service.DeliverProposalAsync(ProposalId, request);

        Assert.Equal(DispatchId, result.ProposalDeliveryDispatchId);
        Assert.Equal("Queued", result.StatusCode);
        Assert.Equal(ProposalId, repository.LastDeliveredProposalId);
        Assert.Same(request, repository.LastProposalDeliveryRequest);
        Assert.Equal("Email", repository.LastProposalDeliveryRequest!.DeliveryMethod);
        Assert.Equal("client@example.com", repository.LastProposalDeliveryRequest.Recipient);
        Assert.Equal(UserId, repository.LastProposalDeliveryRequest.SentByUserId);
    }

    [Fact]
    public async Task ProposalReads_ForwardTenantScope()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);

        await service.GetProposalByIdAsync(ProposalId, TenantId);
        await service.GetProposalsAsync(SubmissionId, TenantId);

        Assert.Equal(TenantId, repository.LastProposalReadTenantId);
        Assert.Equal(TenantId, repository.LastProposalListTenantId);
    }

    [Fact]
    public async Task GetProposalWorkflowLaunchAsync_ForwardsOpportunityAndTenant()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);

        var result = await service.GetProposalWorkflowLaunchAsync(OpportunityId, TenantId);

        Assert.Equal(OpportunityId, repository.LastLaunchOpportunityId);
        Assert.Equal(TenantId, repository.LastLaunchTenantId);
        Assert.Equal(SubmissionId, result.SubmissionId);
        Assert.True(result.HasProposalReadyQuotes);
        Assert.Equal("OpenProposalWorkflow", result.NextActionCode);
    }

    [Fact]
    public async Task RetryProposalDeliveryAsync_ForwardsTenantAndReturnsQueueState()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);
        var request = new RetryProposalDeliveryRequest(TenantId, UserId);

        var result = await service.RetryProposalDeliveryAsync(DispatchId, request);

        Assert.Equal(DispatchId, repository.LastRetryDispatchId);
        Assert.Same(request, repository.LastRetryRequest);
        Assert.Equal("Queued", result.StatusCode);
    }

    [Fact]
    public async Task GetProposalBindContinuationAsync_ReturnsPersistedAuthorizationEligibility()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);

        var result = await service.GetProposalBindContinuationAsync(ProposalId, TenantId);

        Assert.True(result.CanRequestBind);
        Assert.Equal(QuoteId, result.SelectedQuoteId);
        Assert.Equal(TenantId, repository.LastBindContinuationTenantId);
    }

    [Fact]
    public async Task RecordProposalDecisionAsync_Forwards_Decision_Metadata()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);
        var request = new ProposalDecisionRequest(TenantId, "Accepted", "Client approved selected option.", UserId);

        await service.RecordProposalDecisionAsync(ProposalId, request);

        Assert.Equal(ProposalId, repository.LastDecisionProposalId);
        Assert.Same(request, repository.LastProposalDecisionRequest);
        Assert.Equal("Accepted", repository.LastProposalDecisionRequest!.Decision);
        Assert.Equal("Client approved selected option.", repository.LastProposalDecisionRequest.DecisionNotes);
        Assert.Equal(UserId, repository.LastProposalDecisionRequest.DecidedByUserId);
    }

    [Fact]
    public async Task BindPolicyAsync_Forwards_Optional_Proposal_And_CustomerAuthorization_Metadata()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);
        var request = new BindPolicyRequest(
            SubmissionId,
            QuoteId,
            TenantId,
            AccountId,
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            12500m,
            DateTime.Today,
            DateTime.Today.AddYears(1),
            ProposalId: ProposalId,
            CustomerAuthorizationMethodCode: "EmailApproval",
            CustomerAuthorizationReference: "client approval email",
            CustomerAuthorizationNotes: "Customer approved exact terms by email.",
            CustomerAuthorizedByName: "Pat Customer",
            CustomerAuthorizedDateUtc: DateTime.UtcNow,
            RequestedByUserId: UserId);

        await service.BindPolicyAsync(request);

        Assert.Same(request, repository.LastBindPolicyRequest);
        Assert.Equal(ProposalId, repository.LastBindPolicyRequest!.ProposalId);
        Assert.Equal("Pending", repository.LastBindPolicyRequest.BindStatusCode);
        Assert.Equal("EmailApproval", repository.LastBindPolicyRequest.CustomerAuthorizationMethodCode);
        Assert.Equal("client approval email", repository.LastBindPolicyRequest.CustomerAuthorizationReference);
    }

    private static SubmissionService CreateService(FakeSubmissionRepository repository)
        => new(repository, new FakeAccountRepository(), new FakeOpportunityRepository(), new FakePolicyCreationService());

    private sealed class FakeSubmissionRepository : ISubmissionRepository
    {
        public Guid GeneratedProposalId { get; set; } = Guid.NewGuid();
        public GenerateProposalRequest? LastGenerateProposalRequest { get; private set; }
        public Guid? LastDeliveredProposalId { get; private set; }
        public ProposalDeliveryRequest? LastProposalDeliveryRequest { get; private set; }
        public Guid? LastDecisionProposalId { get; private set; }
        public ProposalDecisionRequest? LastProposalDecisionRequest { get; private set; }
        public BindPolicyRequest? LastBindPolicyRequest { get; private set; }
        public Guid? LastProposalReadTenantId { get; private set; }
        public Guid? LastProposalListTenantId { get; private set; }
        public Guid? LastLaunchOpportunityId { get; private set; }
        public Guid? LastLaunchTenantId { get; private set; }
        public Guid? LastRetryDispatchId { get; private set; }
        public RetryProposalDeliveryRequest? LastRetryRequest { get; private set; }
        public Guid? LastBindContinuationTenantId { get; private set; }

        public Task<Guid> GenerateProposalAsync(GenerateProposalRequest request, CancellationToken cancellationToken = default)
        {
            LastGenerateProposalRequest = request;
            return Task.FromResult(GeneratedProposalId);
        }

        public Task<ProposalDeliveryDispatchDto> DeliverProposalAsync(Guid proposalId, ProposalDeliveryRequest request, CancellationToken cancellationToken = default)
        {
            LastDeliveredProposalId = proposalId;
            LastProposalDeliveryRequest = request;
            return Task.FromResult(new ProposalDeliveryDispatchDto { ProposalDeliveryDispatchId = DispatchId, ProposalId = proposalId, TenantId = request.TenantId, StatusCode = "Queued" });
        }

        public Task RecordProposalDecisionAsync(Guid proposalId, ProposalDecisionRequest request, CancellationToken cancellationToken = default)
        {
            LastDecisionProposalId = proposalId;
            LastProposalDecisionRequest = request;
            return Task.CompletedTask;
        }

        public Task<PagedResult<SubmissionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<SubmissionDto>());
        public Task<SubmissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<SubmissionDto?>(new SubmissionDto { SubmissionId = id, TenantId = TenantId, AccountId = AccountId, OpportunityId = Guid.NewGuid() });
        public Task<Guid> CreateAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task UpdateAsync(Guid id, UpdateSubmissionRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AssignAsync(Guid id, AssignSubmissionRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SubmissionActivityDto>> GetActivitiesAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubmissionActivityDto>>([]);
        public Task<Guid> AddNoteAsync(Guid submissionId, AddSubmissionNoteRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DocumentDto>>([]);
        public Task<IReadOnlyList<SubmissionTaskDto>> GetTasksAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubmissionTaskDto>>([]);
        public Task<Guid> CreateFollowUpTaskAsync(Guid submissionId, CreateSubmissionFollowUpTaskRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<IReadOnlyList<SubmissionLineDto>> GetLinesAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubmissionLineDto>>([]);
        public Task<IReadOnlyList<SubmissionIntakeQuestionDto>> GetIntakeAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubmissionIntakeQuestionDto>>([]);
        public Task UpdateIntakeQuestionAsync(Guid submissionId, Guid intakeQuestionId, UpdateSubmissionIntakeQuestionRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SubmissionReadinessEvidenceDocumentDto>> GetReadinessEvidenceDocumentsAsync(Guid submissionId, Guid intakeQuestionId, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubmissionReadinessEvidenceDocumentDto>>([]);
        public Task ReplaceReadinessEvidenceDocumentsAsync(Guid submissionId, Guid intakeQuestionId, ReplaceSubmissionReadinessEvidenceRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SubmissionDocumentChecklistDto>> GetDocumentChecklistAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubmissionDocumentChecklistDto>>([]);
        public Task<SubmissionReadinessDto> GetReadinessAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionReadinessDto { SubmissionId = submissionId, IsReadyForMarketing = true });
        public Task<SubmissionReadinessDto> GetMarketReadinessAsync(Guid submissionId, Guid submissionMarketId, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionReadinessDto { SubmissionId = submissionId, SubmissionMarketId = submissionMarketId, IsReadyForMarketing = true });
        public Task<SubmissionPackagePreviewDto> GetSubmissionPackagePreviewAsync(Guid submissionId, Guid? submissionMarketId, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionPackagePreviewDto { SubmissionId = submissionId, SubmissionMarketId = submissionMarketId });
        public Task<IReadOnlyList<SubmissionReadinessRequirementDto>> GetReadinessRequirementsAsync(Guid tenantId, string? searchTerm, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubmissionReadinessRequirementDto>>([]);
        public Task<Guid> UpsertReadinessRequirementAsync(Guid? readinessRequirementId, UpsertSubmissionReadinessRequirementRequest request, CancellationToken cancellationToken = default) => Task.FromResult(readinessRequirementId ?? Guid.NewGuid());
        public Task DeleteReadinessRequirementAsync(Guid readinessRequirementId, Guid tenantId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SubmissionTaskTemplateDto>> GetTaskTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubmissionTaskTemplateDto>>([]);
        public Task<SubmissionMetricsDto> GetMetricsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionMetricsDto());
        public Task<SubmissionActionResult> SubmitToMarketAsync(Guid id, SubmitSubmissionToMarketRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionActionResult(Guid.NewGuid(), "ok"));
        public Task<SubmissionActionResult> RequestQuoteAsync(Guid id, RequestSubmissionQuoteRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionActionResult(Guid.NewGuid(), "ok"));
        public Task<SubmissionActionResult> CopyAsync(Guid id, CopySubmissionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionActionResult(Guid.NewGuid(), "ok"));
        public Task<SubmissionActionResult> DeclineAsync(Guid id, DeclineSubmissionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionActionResult(id, "ok"));
        public Task<SubmissionActionResult> CreatePolicyAsync(Guid id, CreatePolicyFromSubmissionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionActionResult(Guid.NewGuid(), "ok"));
        public Task<IReadOnlyList<PolicyCreationSourceDto>> GetPolicyCreationSourcesAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PolicyCreationSourceDto>>([]);
        public Task<IReadOnlyList<PolicyBindStatusDto>> GetPolicyBindStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PolicyBindStatusDto>>([]);
        public Task<IReadOnlyList<PolicyBindTransactionDto>> GetPolicyBindTransactionsAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PolicyBindTransactionDto>>([]);
        public Task<PolicyBindTransactionDto?> GetPolicyBindTransactionAsync(Guid policyBindTransactionId, CancellationToken cancellationToken = default) => Task.FromResult<PolicyBindTransactionDto?>(null);
        public Task<IReadOnlyList<SubmissionMarketDto>> GetMarketsAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubmissionMarketDto>>([]);
        public Task<IReadOnlyList<SubmissionMarketDto>> GetMarketSuggestionsAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubmissionMarketDto>>([]);
        public Task<Guid> AddMarketAsync(AddSubmissionMarketRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task UpdateMarketStatusAsync(Guid submissionMarketId, UpdateSubmissionMarketStatusRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateMarketPackageAsync(UpdateSubmissionMarketPackageRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveMarketAsync(Guid submissionMarketId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> SynchronizeOverdueMarketRequestsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<QuoteComparisonDto>> GetQuoteComparisonAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<QuoteComparisonDto>>([]);
        public Task<QuoteComparisonDto?> GetQuoteByIdAsync(Guid quoteId, CancellationToken cancellationToken = default) => Task.FromResult<QuoteComparisonDto?>(null);
        public Task<SubmissionActionResult> RecordQuoteResponseAsync(Guid submissionId, RecordSubmissionQuoteResponseRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionActionResult(Guid.NewGuid(), "ok"));
        public Task<Guid> RecordCarrierInboundResponseAsync(Guid submissionId, RecordCarrierInboundResponseRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task UpdateQuoteAsync(Guid quoteId, UpdateSubmissionQuoteRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SelectQuoteAsync(Guid submissionId, SelectSubmissionQuoteRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ProposalDto?> GetProposalByIdAsync(Guid proposalId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            LastProposalReadTenantId = tenantId;
            return Task.FromResult<ProposalDto?>(new ProposalDto { ProposalId = proposalId, TenantId = tenantId, SubmissionId = SubmissionId });
        }
        public Task<IReadOnlyList<ProposalWorkflowDto>> GetProposalsAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            LastProposalListTenantId = tenantId;
            return Task.FromResult<IReadOnlyList<ProposalWorkflowDto>>([]);
        }
        public Task<ProposalWorkflowLaunchDto> GetProposalWorkflowLaunchAsync(Guid opportunityId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            LastLaunchOpportunityId = opportunityId;
            LastLaunchTenantId = tenantId;
            return Task.FromResult(new ProposalWorkflowLaunchDto { OpportunityId = opportunityId, TenantId = tenantId, SubmissionId = SubmissionId, HasSubmission = true, HasProposalReadyQuotes = true, ProposalReadyQuoteCount = 1, NextActionCode = "OpenProposalWorkflow" });
        }
        public Task<IReadOnlyList<ProposalWorkflowOptionDto>> GetProposalWorkflowOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProposalWorkflowOptionDto>>([]);
        public Task<IReadOnlyList<ProposalDeliveryDispatchDto>> GetProposalDeliveriesAsync(Guid proposalId, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProposalDeliveryDispatchDto>>([]);
        public Task<ProposalDeliveryDispatchDto> RetryProposalDeliveryAsync(Guid dispatchId, RetryProposalDeliveryRequest request, CancellationToken cancellationToken = default)
        {
            LastRetryDispatchId = dispatchId;
            LastRetryRequest = request;
            return Task.FromResult(new ProposalDeliveryDispatchDto { ProposalDeliveryDispatchId = dispatchId, TenantId = request.TenantId, ProposalId = ProposalId, StatusCode = "Queued" });
        }
        public Task<IReadOnlyList<ProposalDeliveryProviderDto>> GetProposalDeliveryProvidersAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProposalDeliveryProviderDto>>([]);
        public Task UpdateProposalDeliveryProviderAsync(Guid providerId, UpdateProposalDeliveryProviderRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PresentProposalAsync(Guid proposalId, ProposalPresentationRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ProposalBindContinuationDto> GetProposalBindContinuationAsync(Guid proposalId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            LastBindContinuationTenantId = tenantId;
            return Task.FromResult(new ProposalBindContinuationDto { ProposalId = proposalId, SubmissionId = SubmissionId, TenantId = tenantId, CanRequestBind = true, SelectedQuoteId = QuoteId, CustomerAuthorizationId = Guid.NewGuid() });
        }
        public Task<IReadOnlyList<AppetiteMatchDto>> SearchAppetiteAsync(AppetiteSearchRequest request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AppetiteMatchDto>>([]);
        public Task<PagedResult<PolicyRegisterDto>> SearchPoliciesAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<PolicyRegisterDto>());
        public Task<PolicyRegisterDto?> GetPolicyByIdAsync(Guid policyId, CancellationToken cancellationToken = default) => Task.FromResult<PolicyRegisterDto?>(null);
        public Task<Guid> CreatePolicyRegisterAsync(UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task UpdatePolicyRegisterAsync(Guid policyId, UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<SubmissionActionResult> ExecutePolicyRegisterActionAsync(Guid policyId, PolicyRegisterActionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionActionResult(policyId, "ok"));
        public Task<PolicyBindDto?> GetPolicyBySubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult<PolicyBindDto?>(null);
        public Task<Guid> BindPolicyAsync(BindPolicyRequest request, CancellationToken cancellationToken = default)
        {
            LastBindPolicyRequest = request;
            return Task.FromResult(Guid.NewGuid());
        }
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        public Task<Guid> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<AccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<AccountDto?>(new AccountDto { AccountId = id, TenantId = TenantId });
        public Task<PagedResult<AccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<AccountDto>());
        public Task UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ContactDto>> GetContactsByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContactDto>>([]);
        public Task<Account360Dto?> GetAccount360Async(Guid tenantId, Guid accountId, CancellationToken cancellationToken = default) => Task.FromResult<Account360Dto?>(null);
        public Task<Guid> UpsertNamedInsuredAsync(UpsertAccountNamedInsuredRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<Guid> UpsertLocationAsync(UpsertAccountLocationRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<Guid> UpsertVehicleAsync(UpsertAccountVehicleRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<Guid> UpsertDriverAsync(UpsertAccountDriverRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<Guid> UpsertPropertyAsync(UpsertAccountPropertyRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<Guid> UpsertScheduleItemAsync(UpsertAccountScheduleItemRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task DeleteAccount360ItemAsync(Guid tenantId, Guid accountId, string entityType, Guid entityId, Guid? userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<AccountDto>> FindMatchCandidatesAsync(AccountMatchCriteria criteria, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AccountDto>>([]);
    }

    private sealed class FakeOpportunityRepository : IOpportunityRepository
    {
        public Task<PagedResult<OpportunityDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<OpportunityDto>());
        public Task<OpportunityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<OpportunityDto?>(new OpportunityDto { OpportunityId = id, TenantId = TenantId, AccountId = Guid.NewGuid() });
        public Task<OpportunityDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<OpportunityDetailDto?>(new OpportunityDetailDto { Opportunity = new OpportunityDto { OpportunityId = id, TenantId = TenantId, AccountId = Guid.NewGuid() } });
        public Task<OpportunityConversionLaunchDto?> GetConversionLaunchAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<OpportunityConversionLaunchDto?>(null);
        public Task<PagedResult<OpportunityCompetitorLookupDto>> SearchCompetitorLookupsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<OpportunityCompetitorLookupDto>());
        public Task<Guid> CreateAsync(CreateOpportunityRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task UpdateAsync(Guid id, UpdateOpportunityRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<OpportunityStageUpdateResult> UpdateStageAsync(Guid id, UpdateOpportunityStageRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new OpportunityStageUpdateResult { OpportunityId = id, Stage = request.Stage, Message = "ok" });
        public Task<Guid> UpsertLineAsync(UpsertOpportunityLineRequest request, CancellationToken cancellationToken = default) => Task.FromResult(request.OpportunityLineId ?? Guid.NewGuid());
        public Task SetPrimaryLineAsync(Guid opportunityId, Guid opportunityLineId, Guid? userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteLineAsync(Guid opportunityLineId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Guid> UpsertActivityAsync(UpsertOpportunityActivityRequest request, CancellationToken cancellationToken = default) => Task.FromResult(request.ActivityId ?? Guid.NewGuid());
        public Task DeleteActivityAsync(Guid activityId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Guid> UpsertSubmissionAsync(UpsertOpportunitySubmissionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(request.SubmissionId ?? Guid.NewGuid());
        public Task DeleteSubmissionAsync(Guid submissionId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Guid> UpsertCompetitorAsync(UpsertOpportunityCompetitorRequest request, CancellationToken cancellationToken = default) => Task.FromResult(request.CompetitorId ?? Guid.NewGuid());
        public Task DeleteCompetitorAsync(Guid competitorId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePolicyCreationService : Ams.Application.Abstractions.Services.IPolicyCreationService
    {
        public Task<Guid> CreatePolicyFromConfirmedBindAsync(PolicyCreationFromConfirmedBindRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());
        public Task<IReadOnlyList<ManualPolicyOptionDto>> GetManualPolicyOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ManualPolicyOptionDto>>([]);
        public Task<ManualPolicyDraftDto> SaveManualPolicyDraftAsync(Guid? draftId, UpsertManualPolicyDraftRequest request, CancellationToken cancellationToken = default) => Task.FromException<ManualPolicyDraftDto>(new NotSupportedException());
        public Task<ManualPolicyDraftDto?> GetManualPolicyDraftAsync(Guid tenantId, Guid accountId, Guid draftId, CancellationToken cancellationToken = default) => Task.FromResult<ManualPolicyDraftDto?>(null);
        public Task<ManualPolicyValidationResultDto> ValidateManualPolicyAsync(CreateManualPolicyRequest request, CancellationToken cancellationToken = default) => Task.FromException<ManualPolicyValidationResultDto>(new NotSupportedException());
        public Task<ManualPolicyCreateResultDto> CreateManualPolicyAsync(CreateManualPolicyRequest request, CancellationToken cancellationToken = default) => Task.FromException<ManualPolicyCreateResultDto>(new NotSupportedException());
    }
}
