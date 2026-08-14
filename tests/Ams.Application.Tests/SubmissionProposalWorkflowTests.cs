using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Accounts;
using Ams.Application.Features.Iam;
using Ams.Application.Features.Opportunities;
using Ams.Application.Features.Security;
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
    public async Task GetQuoteByIdAsync_ForwardsTenantScope()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);

        await service.GetQuoteByIdAsync(QuoteId, TenantId);

        Assert.Equal(QuoteId, repository.LastQuoteReadId);
        Assert.Equal(TenantId, repository.LastQuoteReadTenantId);
    }

    [Fact]
    public async Task GetQuoteComparisonAsync_ForwardsSubmissionAndTenantScope()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);

        await service.GetQuoteComparisonAsync(SubmissionId, TenantId);

        Assert.Equal(SubmissionId, repository.LastQuoteComparisonSubmissionId);
        Assert.Equal(TenantId, repository.LastQuoteComparisonTenantId);
    }

    [Fact]
    public async Task GetQuoteRegisterAsync_ForwardsTenantScopeAndReturnsRepositoryRows()
    {
        var expected = new SubmissionQuoteRegisterDto { QuoteId = QuoteId, SubmissionId = SubmissionId };
        var repository = new FakeSubmissionRepository { QuoteRegisterItems = [expected] };
        var service = CreateService(repository);

        var result = await service.GetQuoteRegisterAsync(TenantId);

        Assert.Equal(TenantId, repository.LastQuoteRegisterTenantId);
        Assert.Same(expected, Assert.Single(result));
    }

    [Fact]
    public async Task GetBindQueueAsync_ForwardsTenantScopeAndReturnsRepositoryRows()
    {
        var expected = new BindQueueItemDto { TenantId = TenantId, SubmissionId = SubmissionId, QuoteId = QuoteId };
        var repository = new FakeSubmissionRepository { BindQueueItems = [expected] };
        var service = CreateService(repository);

        var result = await service.GetBindQueueAsync(TenantId);

        Assert.Equal(TenantId, repository.LastBindQueueTenantId);
        Assert.Same(expected, Assert.Single(result));
    }

    [Fact]
    public async Task RecordBindCarrierResponseAsync_DoesNotCreatePolicy()
    {
        var bindRequestId = Guid.NewGuid();
        var repository = new FakeSubmissionRepository
        {
            BindRequestDetail = new BindRequestDetailDto
            {
                Request = new PolicyBindTransactionDto
                {
                    PolicyBindTransactionId = bindRequestId,
                    TenantId = TenantId,
                    SubmissionId = SubmissionId,
                    BindStatusCode = "Approved"
                },
                AllowedTransitions = [new BindStatusTransitionDto { FromStatusCode = "Approved", ToStatusCode = "BinderReceived", RequiresCarrierResponse = true }]
            }
        };
        var policyCreation = new FakePolicyCreationService();
        var service = CreateService(repository, policyCreation);
        var request = new RecordBindCarrierResponseRequest(TenantId, "BinderReceived", "Binder", "Email", null, null, "Carrier binder received.", "CAR-100", "BIN-100", 12500m, null, UserId, "Email", true);

        var policyId = await service.RecordBindCarrierResponseAsync(bindRequestId, request);

        Assert.Null(policyId);
        Assert.Equal(0, policyCreation.CreatePolicyCallCount);
        Assert.Same(request, repository.LastCarrierResponseRequest);
        Assert.Null(repository.LastBindStatusRequest);
    }

    [Fact]
    public async Task RequestQuoteAsync_RejectsHistoricalSubmission()
    {
        var repository = new FakeSubmissionRepository
        {
            BindTransactions = [new PolicyBindTransactionDto { TenantId = TenantId, SubmissionId = SubmissionId, PolicyId = Guid.NewGuid() }]
        };
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequestQuoteAsync(
            SubmissionId,
            new RequestSubmissionQuoteRequest(TenantId, null, null, null, null, null)));

        Assert.Contains("historical", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repository.RequestQuoteCallCount);
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
    public async Task SubmitAndDecideProposalReviewAsync_Forwards_Governance_Metadata()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);
        var reviewerId = Guid.NewGuid();
        var submitRequest = new SubmitProposalReviewRequest(TenantId, reviewerId, "Review requested.", DateTime.UtcNow.AddDays(1), UserId);
        var decisionRequest = new DecideProposalReviewRequest(TenantId, "Approved", "Approved for delivery.", UserId);

        await service.SubmitProposalReviewAsync(ProposalId, submitRequest);
        await service.DecideProposalReviewAsync(ProposalId, decisionRequest);

        Assert.Equal(ProposalId, repository.LastReviewProposalId);
        Assert.Same(submitRequest, repository.LastSubmitReviewRequest);
        Assert.Same(decisionRequest, repository.LastReviewDecisionRequest);
        Assert.Equal("Approved", repository.LastReviewDecisionRequest!.DecisionCode);
    }

    [Fact]
    public async Task ProcessProposalProviderCallbackAsync_Forwards_IdempotencyCorrelationMetadata()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);
        var request = new ProposalProviderCallbackRequest(TenantId, "DocuSign", "event-123", "envelope-456", "recipient-delivered", "Delivered", "{}", "sha256=signature");

        var result = await service.ProcessProposalProviderCallbackAsync(request);

        Assert.Equal(repository.CallbackId, result);
        Assert.Same(request, repository.LastCallbackRequest);
        Assert.Equal("event-123", repository.LastCallbackRequest!.ProviderEventId);
        Assert.Equal("envelope-456", repository.LastCallbackRequest.ExternalEnvelopeId);
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
        Assert.Equal("Draft", repository.LastBindPolicyRequest.BindStatusCode);
        Assert.Equal("EmailApproval", repository.LastBindPolicyRequest.CustomerAuthorizationMethodCode);
        Assert.Equal("client approval email", repository.LastBindPolicyRequest.CustomerAuthorizationReference);
    }

    [Fact]
    public async Task BindPolicyAsync_Rejects_NonDraft_Initial_Status()
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
            BindStatusCode: "Submitted",
            CustomerAuthorizationMethodCode: "EmailApproval",
            RequestedByUserId: UserId);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BindPolicyAsync(request));

        Assert.Contains("start in Draft", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(repository.LastBindPolicyRequest);
    }

    [Fact]
    public async Task RecordQuoteResponseAsync_Forwards_Persisted_Line_Terms()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);
        var submissionLineId = Guid.NewGuid();
        var marketId = Guid.NewGuid();
        var line = new SubmissionQuoteLineTermRequest(
            submissionLineId,
            "General Liability",
            "Quoted",
            12500m,
            2500m,
            1000000m,
            12.5m,
            "Occurrence form",
            "Signed application",
            "Known losses",
            "Annual",
            3125m,
            450m,
            250m,
            true,
            true,
            "Primary coverage",
            1);
        var request = new RecordSubmissionQuoteResponseRequest(
            TenantId,
            marketId,
            QuoteId,
            "Quoted",
            12500m,
            2500m,
            1000000m,
            12.5m,
            "Signed application",
            "Known losses",
            "A",
            "Annual",
            3125m,
            450m,
            250m,
            true,
            null,
            "Package response",
            DateTime.UtcNow.AddDays(30),
            "Manual",
            "CAR-1001",
            UserId,
            DateTime.UtcNow.Date,
            "Occurrence form",
            true,
            [line]);

        await service.RecordQuoteResponseAsync(SubmissionId, request);

        Assert.Equal(SubmissionId, repository.LastQuoteResponseSubmissionId);
        Assert.Same(request, repository.LastQuoteResponseRequest);
        var persistedLine = Assert.Single(repository.LastQuoteResponseRequest!.Lines!);
        Assert.Equal(submissionLineId, persistedLine.SubmissionLineId);
        Assert.Equal(12500m, persistedLine.QuotedPremium);
        Assert.Equal(12.5m, persistedLine.CommissionPercent);
        Assert.True(persistedLine.IsBindable);
    }

    [Fact]
    public async Task UpdateQuoteAsync_Forwards_Multiple_Line_Terms()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);
        var lines = new[]
        {
            CreateQuoteLine("Property", 18000m, 0),
            CreateQuoteLine("General Liability", 7000m, 1)
        };
        var request = new UpdateSubmissionQuoteRequest(
            TenantId, "Quoted", 25000m, 5000m, 2000000m, 15m,
            null, null, "A", "Annual", null, 900m, 300m, true,
            null, "Two-line package", DateTime.UtcNow.AddDays(45), UserId,
            "Manual", "CAR-2002", UserId, Guid.NewGuid(), DateTime.UtcNow.Date,
            "Package coverage", true, lines);

        await service.UpdateQuoteAsync(QuoteId, request);

        Assert.Equal(QuoteId, repository.LastUpdatedQuoteId);
        Assert.Same(request, repository.LastUpdateQuoteRequest);
        Assert.Equal(2, repository.LastUpdateQuoteRequest!.Lines!.Count);
        Assert.Equal(25000m, repository.LastUpdateQuoteRequest.Lines.Sum(line => line.QuotedPremium));
    }

    private static SubmissionQuoteLineTermRequest CreateQuoteLine(string lineOfBusiness, decimal premium, int sortOrder)
        => new(Guid.NewGuid(), lineOfBusiness, "Quoted", premium, 2500m, 1000000m, 15m,
            null, null, null, "Annual", null, null, null, true, true, null, sortOrder);

    [Fact]
    public async Task CreateAsync_Uses_Canonical_Opportunity_Lob_And_Configured_Options()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);
        var request = CreateSubmissionRequest();

        var result = await service.CreateAsync(request);

        Assert.NotEqual(Guid.Empty, result);
        Assert.Same(request, repository.LastCreateRequest);
    }

    [Fact]
    public async Task CreateAsync_Rejects_Unconfigured_Risk_State()
    {
        var repository = new FakeSubmissionRepository();
        var service = CreateService(repository);
        var request = CreateSubmissionRequest() with { RiskState = "ZZ" };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));

        Assert.Contains("risk state", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(repository.LastCreateRequest);
    }

    private static CreateSubmissionRequest CreateSubmissionRequest()
        => new(TenantId, AccountId, OpportunityId, "Standard", DateTime.UtcNow.Date.AddDays(30), DateTime.UtcNow.Date.AddYears(1),
            25000m, UserId, "TX", "Test Insured", UserId, "Risk description", "Internal notes", false, UserId);

    private static SubmissionService CreateService(FakeSubmissionRepository repository, FakePolicyCreationService? policyCreationService = null)
        => new(repository, new FakeAccountRepository(), new FakeOpportunityRepository(), policyCreationService ?? new FakePolicyCreationService(), new FakeUserRepository(), new FakeReferenceOptionRepository());

    private sealed class FakeSubmissionRepository : ISubmissionRepository
    {
        public Guid GeneratedProposalId { get; set; } = Guid.NewGuid();
        public GenerateProposalRequest? LastGenerateProposalRequest { get; private set; }
        public Guid? LastDeliveredProposalId { get; private set; }
        public ProposalDeliveryRequest? LastProposalDeliveryRequest { get; private set; }
        public Guid CallbackId { get; } = Guid.NewGuid();
        public Guid? LastReviewProposalId { get; private set; }
        public SubmitProposalReviewRequest? LastSubmitReviewRequest { get; private set; }
        public DecideProposalReviewRequest? LastReviewDecisionRequest { get; private set; }
        public ProposalProviderCallbackRequest? LastCallbackRequest { get; private set; }
        public BindPolicyRequest? LastBindPolicyRequest { get; private set; }
        public Guid? LastProposalReadTenantId { get; private set; }
        public Guid? LastProposalListTenantId { get; private set; }
        public Guid? LastLaunchOpportunityId { get; private set; }
        public Guid? LastLaunchTenantId { get; private set; }
        public Guid? LastRetryDispatchId { get; private set; }
        public RetryProposalDeliveryRequest? LastRetryRequest { get; private set; }
        public Guid? LastBindContinuationTenantId { get; private set; }
        public Guid? LastQuoteResponseSubmissionId { get; private set; }
        public RecordSubmissionQuoteResponseRequest? LastQuoteResponseRequest { get; private set; }
        public Guid? LastUpdatedQuoteId { get; private set; }
        public UpdateSubmissionQuoteRequest? LastUpdateQuoteRequest { get; private set; }
        public Guid? LastQuoteReadId { get; private set; }
        public Guid? LastQuoteReadTenantId { get; private set; }
        public Guid? LastQuoteComparisonSubmissionId { get; private set; }
        public Guid? LastQuoteComparisonTenantId { get; private set; }
        public Guid? LastQuoteRegisterTenantId { get; private set; }
        public IReadOnlyList<SubmissionQuoteRegisterDto> QuoteRegisterItems { get; set; } = [];
        public Guid? LastBindQueueTenantId { get; private set; }
        public IReadOnlyList<BindQueueItemDto> BindQueueItems { get; set; } = [];
        public BindRequestDetailDto? BindRequestDetail { get; set; }
        public RecordBindCarrierResponseRequest? LastCarrierResponseRequest { get; private set; }
        public UpdateBindRequestStatusRequest? LastBindStatusRequest { get; private set; }
        public IReadOnlyList<PolicyBindTransactionDto> BindTransactions { get; set; } = [];
        public int RequestQuoteCallCount { get; private set; }
        public CreateSubmissionRequest? LastCreateRequest { get; private set; }

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

        public Task SubmitProposalReviewAsync(Guid proposalId, SubmitProposalReviewRequest request, CancellationToken cancellationToken = default)
        {
            LastReviewProposalId = proposalId;
            LastSubmitReviewRequest = request;
            return Task.CompletedTask;
        }

        public Task DecideProposalReviewAsync(Guid proposalId, DecideProposalReviewRequest request, CancellationToken cancellationToken = default)
        {
            LastReviewProposalId = proposalId;
            LastReviewDecisionRequest = request;
            return Task.CompletedTask;
        }

        public Task<Guid> UpsertProposalRecipientAsync(Guid proposalId, UpsertProposalRecipientRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task DeleteProposalRecipientAsync(Guid proposalId, Guid recipientId, Guid tenantId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ProposalSlaPolicyDto>> GetProposalSlaPoliciesAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProposalSlaPolicyDto>>([]);
        public Task<Guid> UpsertProposalSlaPolicyAsync(UpsertProposalSlaPolicyRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task<Guid> ProcessProposalProviderCallbackAsync(ProposalProviderCallbackRequest request, CancellationToken cancellationToken = default)
        {
            LastCallbackRequest = request;
            return Task.FromResult(CallbackId);
        }

        public Task<PagedResult<SubmissionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<SubmissionDto>());
        public Task<SubmissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<SubmissionDto?>(new SubmissionDto { SubmissionId = id, TenantId = TenantId, AccountId = AccountId, OpportunityId = Guid.NewGuid() });
        public Task<Guid> CreateAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default)
        {
            LastCreateRequest = request;
            return Task.FromResult(Guid.NewGuid());
        }
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
        public Task<SubmissionActionResult> RequestQuoteAsync(Guid id, RequestSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
        {
            RequestQuoteCallCount++;
            return Task.FromResult(new SubmissionActionResult(Guid.NewGuid(), "ok"));
        }
        public Task<SubmissionActionResult> CopyAsync(Guid id, CopySubmissionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionActionResult(Guid.NewGuid(), "ok"));
        public Task<SubmissionActionResult> DeclineAsync(Guid id, DeclineSubmissionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionActionResult(id, "ok"));
        public Task<SubmissionActionResult> CreatePolicyAsync(Guid id, CreatePolicyFromSubmissionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new SubmissionActionResult(Guid.NewGuid(), "ok"));
        public Task<IReadOnlyList<PolicyCreationSourceDto>> GetPolicyCreationSourcesAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PolicyCreationSourceDto>>([]);
        public Task<IReadOnlyList<PolicyBindStatusDto>> GetPolicyBindStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PolicyBindStatusDto>>([]);
        public Task<IReadOnlyList<BindQueueItemDto>> GetBindQueueAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            LastBindQueueTenantId = tenantId;
            return Task.FromResult(BindQueueItems);
        }
        public Task<IReadOnlyList<PolicyBindTransactionDto>> GetPolicyBindTransactionsAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult(BindTransactions);
        public Task<PolicyBindTransactionDto?> GetPolicyBindTransactionAsync(Guid policyBindTransactionId, CancellationToken cancellationToken = default) => Task.FromResult<PolicyBindTransactionDto?>(null);
        public Task<IReadOnlyList<SubmissionMarketDto>> GetMarketsAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubmissionMarketDto>>([]);
        public Task<IReadOnlyList<SubmissionMarketDto>> GetMarketSuggestionsAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SubmissionMarketDto>>([]);
        public Task<Guid> AddMarketAsync(AddSubmissionMarketRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task UpdateMarketStatusAsync(Guid submissionMarketId, UpdateSubmissionMarketStatusRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateMarketPackageAsync(UpdateSubmissionMarketPackageRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveMarketAsync(Guid submissionMarketId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> SynchronizeOverdueMarketRequestsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<SubmissionQuoteRegisterDto>> GetQuoteRegisterAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            LastQuoteRegisterTenantId = tenantId;
            return Task.FromResult(QuoteRegisterItems);
        }

        public Task<IReadOnlyList<QuoteComparisonDto>> GetQuoteComparisonAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            LastQuoteComparisonSubmissionId = submissionId;
            LastQuoteComparisonTenantId = tenantId;
            return Task.FromResult<IReadOnlyList<QuoteComparisonDto>>([]);
        }
        public Task<QuoteComparisonDto?> GetQuoteByIdAsync(Guid quoteId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            LastQuoteReadId = quoteId;
            LastQuoteReadTenantId = tenantId;
            return Task.FromResult<QuoteComparisonDto?>(null);
        }
        public Task<SubmissionActionResult> RecordQuoteResponseAsync(Guid submissionId, RecordSubmissionQuoteResponseRequest request, CancellationToken cancellationToken = default)
        {
            LastQuoteResponseSubmissionId = submissionId;
            LastQuoteResponseRequest = request;
            return Task.FromResult(new SubmissionActionResult(Guid.NewGuid(), "ok"));
        }
        public Task<Guid> RecordCarrierInboundResponseAsync(Guid submissionId, RecordCarrierInboundResponseRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task UpdateQuoteAsync(Guid quoteId, UpdateSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
        {
            LastUpdatedQuoteId = quoteId;
            LastUpdateQuoteRequest = request;
            return Task.CompletedTask;
        }
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
        public Task<IReadOnlyList<ProposalDeliveryMonitorDto>> GetProposalDeliveryMonitorAsync(Guid tenantId, string? status, string? searchTerm, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProposalDeliveryMonitorDto>>([]);
        public Task<ProposalDeliveryDispatchDto> UpdateProposalDeliveryRecipientAsync(Guid dispatchId, UpdateProposalDeliveryRecipientRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ProposalDeliveryDispatchDto { ProposalDeliveryDispatchId = dispatchId, TenantId = request.TenantId, ProposalId = ProposalId, StatusCode = "Queued" });
        public Task<ProposalDeliveryDispatchDto> ResendProposalDeliveryAsync(Guid dispatchId, ResendProposalDeliveryRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new ProposalDeliveryDispatchDto { ProposalDeliveryDispatchId = dispatchId, TenantId = request.TenantId, ProposalId = ProposalId, StatusCode = "Queued" });
        public Task DeleteProposalDeliveryAsync(Guid dispatchId, DeleteProposalDeliveryRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
        public Task<ClientAcceptanceReadinessDto> GetClientAcceptanceReadinessAsync(Guid proposalId, Guid? quoteId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(new ClientAcceptanceReadinessDto { ProposalId = proposalId, TenantId = tenantId, SelectedQuoteId = quoteId });
        public Task<IReadOnlyList<ClientAcceptanceDto>> GetClientAcceptancesAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ClientAcceptanceDto>>([]);
        public Task<ClientAcceptanceDto?> GetClientAcceptanceByIdAsync(Guid clientAcceptanceId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<ClientAcceptanceDto?>(null);
        public Task<Guid> RecordClientAcceptanceAsync(RecordClientAcceptanceRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());
        public Task WithdrawClientAcceptanceAsync(Guid clientAcceptanceId, WithdrawClientAcceptanceRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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
        public Task<BindRequestDetailDto?> GetBindRequestDetailAsync(Guid policyBindTransactionId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(BindRequestDetail);
        public Task<BindCommissionEstimateDto> GetBindCommissionEstimateAsync(Guid submissionId, Guid quoteId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(new BindCommissionEstimateDto());
        public Task<IReadOnlyList<BindValidationResultDto>> ValidateBindRequestAsync(Guid policyBindTransactionId, ValidateBindRequestRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BindValidationResultDto>>([]);
        public Task UpdateBindRequestStatusAsync(Guid policyBindTransactionId, UpdateBindRequestStatusRequest request, CancellationToken cancellationToken = default)
        {
            LastBindStatusRequest = request;
            return Task.CompletedTask;
        }
        public Task<Guid> RequestBindApprovalAsync(Guid policyBindTransactionId, RequestBindApprovalRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());
        public Task DecideBindApprovalAsync(Guid policyBindTransactionId, Guid bindApprovalId, DecideBindApprovalRequest request, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task RecordBindCarrierResponseAsync(Guid policyBindTransactionId, RecordBindCarrierResponseRequest request, CancellationToken cancellationToken = default)
        {
            LastCarrierResponseRequest = request;
            return Task.CompletedTask;
        }
        public Task<BindPackageDto> PrepareBindPackageAsync(Guid policyBindTransactionId, PrepareBindPackageRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new BindPackageDto { BindPackageId = Guid.NewGuid(), PolicyBindTransactionId = policyBindTransactionId, PackageNumber = "BPK-TEST", StatusCode = "Prepared" });
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
        public Task ReplaceServiceAssignmentsAsync(ReplaceAccountServiceAssignmentsRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
        public Task<OpportunityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<OpportunityDto?>(new OpportunityDto { OpportunityId = id, TenantId = TenantId, AccountId = AccountId });
        public Task<OpportunityDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<OpportunityDetailDto?>(new OpportunityDetailDto
        {
            Opportunity = new OpportunityDto { OpportunityId = id, TenantId = TenantId, AccountId = AccountId },
            Lines = [new OpportunityLineDto { OpportunityLineId = Guid.NewGuid(), LobId = Guid.NewGuid(), LineOfBusiness = "General Liability", IsPrimary = true }]
        });
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

    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<UserDto?>(new UserDto
        {
            UserId = id,
            TenantId = TenantId,
            StatusCode = "Active",
            AssignedRoleCodes = "Producer,CSR"
        });
        public Task<PagedResult<UserDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => Task.FromResult(new PagedResult<UserDto>());
        public Task<IReadOnlyList<JobTitleDto>> GetJobTitlesAsync(Guid tenantId, Guid? departmentId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<JobTitleDto>>([]);
        public Task<Guid> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task UpdateAsync(UpdateUserRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetActiveAsync(Guid userId, bool isActive, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LockAsync(Guid userId, DateTime? lockoutEnd, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnlockAsync(Guid userId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetMfaAsync(Guid userId, bool enabled, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AssignBranchAsync(Guid userId, Guid? branchId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ChangeStatusAsync(ChangeUserStatusRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<UserPermissionDto>> GetDirectPermissionsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<UserPermissionDto>>([]);
        public Task<IEnumerable<UserPermissionDto>> GetDirectUsersByPermissionAsync(Guid permissionId, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<UserPermissionDto>>([]);
        public Task<Guid> GrantPermissionAsync(GrantUserPermissionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Guid.NewGuid());
        public Task RevokePermissionAsync(Guid userPermissionId, Guid? revokedByUserId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeReferenceOptionRepository : ISubmissionReferenceOptionRepository
    {
        public Task<List<SubmissionReferenceOptionDto>> GetAllAsync(Guid tenantId, string? optionGroup = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<SubmissionReferenceOptionDto>
            {
                new() { TenantId = tenantId, OptionGroup = "SubmissionPriority", OptionCode = "Standard", OptionName = "Standard", IsDefault = true, IsActive = true },
                new() { TenantId = tenantId, OptionGroup = "RiskState", OptionCode = "TX", OptionName = "Texas", IsDefault = true, IsActive = true }
            }.Where(option => optionGroup is null || option.OptionGroup == optionGroup).ToList());
    }

    private sealed class FakePolicyCreationService : Ams.Application.Abstractions.Services.IPolicyCreationService
    {
        public int CreatePolicyCallCount { get; private set; }

        public Task<Guid> CreatePolicyFromConfirmedBindAsync(PolicyCreationFromConfirmedBindRequest request, CancellationToken cancellationToken = default)
        {
            CreatePolicyCallCount++;
            return Task.FromResult(Guid.NewGuid());
        }
        public Task<BinderReviewDto?> GetBinderReviewAsync(Guid policyBindTransactionId, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<BinderReviewDto?>(null);
        public Task<BinderReviewDto> SaveBinderReviewAsync(Guid policyBindTransactionId, UpsertBinderReviewRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new BinderReviewDto { PolicyBindTransactionId = policyBindTransactionId, TenantId = request.TenantId });
        public Task DecideBinderReviewAsync(Guid policyBindTransactionId, DecideBinderReviewRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PolicyGenerationRequestDto> QueuePolicyGenerationAsync(Guid policyBindTransactionId, QueuePolicyGenerationRequest request, CancellationToken cancellationToken = default) => Task.FromResult(new PolicyGenerationRequestDto { PolicyBindTransactionId = policyBindTransactionId, TenantId = request.TenantId, StatusCode = "Queued" });
        public Task<IReadOnlyList<ManualPolicyOptionDto>> GetManualPolicyOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ManualPolicyOptionDto>>([]);
        public Task<ManualPolicyDraftDto> SaveManualPolicyDraftAsync(Guid? draftId, UpsertManualPolicyDraftRequest request, CancellationToken cancellationToken = default) => Task.FromException<ManualPolicyDraftDto>(new NotSupportedException());
        public Task<ManualPolicyDraftDto?> GetManualPolicyDraftAsync(Guid tenantId, Guid accountId, Guid draftId, CancellationToken cancellationToken = default) => Task.FromResult<ManualPolicyDraftDto?>(null);
        public Task<ManualPolicyValidationResultDto> ValidateManualPolicyAsync(CreateManualPolicyRequest request, CancellationToken cancellationToken = default) => Task.FromException<ManualPolicyValidationResultDto>(new NotSupportedException());
        public Task<ManualPolicyCreateResultDto> CreateManualPolicyAsync(CreateManualPolicyRequest request, CancellationToken cancellationToken = default) => Task.FromException<ManualPolicyCreateResultDto>(new NotSupportedException());
    }
}
