using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Channels;
using Xunit;
using HierarchyNodeDto = Legal.Application.Features.Intelligence.Decision.HierarchyNodeDto;

namespace Legal.Application.Tests.Intelligence;

// Slice-5 invariants for the DecisionContract channel: only a GOVERNED (ACTIVE/APPROVED) contract
// carries an authoritative boundary; a KNOWN fact boundary becomes VERIFIED SUPPORTS on FactSupport,
// a DISPUTED fact boundary raises Uncertainty (never support), UNKNOWN is dropped; a STATUTE_RULE tag
// becomes VERIFIED ESTABLISHES on AuthoritySupport, a KEY_ISSUE tag becomes CONTEXT_ONLY; mapping is
// deterministic and fail-soft; a DRAFT/in-review contract contributes nothing.
public sealed class DecisionContractChannelTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Matter = Guid.NewGuid();
    private static readonly Guid Execution = Guid.NewGuid();

    [Fact]
    public async Task Known_fact_boundary_becomes_verified_fact_support()
    {
        var node = Node("Defendant was speeding at the time of the collision impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var contract = ContractRepo(Contract(
            DecisionContractStatuses.Active,
            boundaries: [Boundary("Defendant was speeding at the collision impact.", DecisionContractFactStates.Known)]));

        var channel = new DecisionContractChannel(contract, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        var contribution = Assert.Single(result);
        Assert.Equal(node.HierarchyNodeId, contribution.HierarchyNodeId);
        Assert.Equal(DecisionChannelType.DecisionContract, contribution.ChannelType);
        Assert.Equal(ContributionRelation.Supports, contribution.Relation);
        Assert.Equal(ContributionVerificationState.Verified, contribution.VerificationState);
        Assert.Equal(DecisionChannelCodes.TargetSignal.FactSupport, contribution.TargetSignalCode);
        Assert.True(contribution.ContributesPositiveSupport);
        Assert.Equal("DecisionContractFactBoundary.Known", contribution.Provenance.SourceTypeCode);
    }

    [Fact]
    public async Task Disputed_fact_boundary_raises_uncertainty_without_support()
    {
        var node = Node("Defendant was speeding at the time of the collision impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var contract = ContractRepo(Contract(
            DecisionContractStatuses.Approved,
            boundaries: [Boundary("Defendant was speeding at the collision impact.", DecisionContractFactStates.Disputed)]));

        var channel = new DecisionContractChannel(contract, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        var contribution = Assert.Single(result);
        Assert.Equal(ContributionRelation.Challenges, contribution.Relation);
        Assert.Equal(ContributionVerificationState.Disputed, contribution.VerificationState);
        Assert.Equal(DecisionChannelCodes.TargetSignal.Uncertainty, contribution.TargetSignalCode);
        Assert.False(contribution.ContributesPositiveSupport);
        Assert.Equal("DecisionContractFactBoundary.Disputed", contribution.Provenance.SourceTypeCode);
    }

    [Fact]
    public async Task Unknown_fact_boundary_is_dropped()
    {
        var node = Node("Defendant was speeding at the time of the collision impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var contract = ContractRepo(Contract(
            DecisionContractStatuses.Active,
            boundaries: [Boundary("Defendant was speeding at the collision impact.", DecisionContractFactStates.Unknown)]));

        var channel = new DecisionContractChannel(contract, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Statute_rule_tag_becomes_verified_authority_support()
    {
        var node = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var contract = ContractRepo(Contract(
            DecisionContractStatuses.Active,
            tags: [Tag(DecisionContractTagKinds.StatuteRule, "Duty of reasonable care owed to the plaintiff.")]));

        var channel = new DecisionContractChannel(contract, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        var contribution = Assert.Single(result);
        Assert.Equal(ContributionRelation.Establishes, contribution.Relation);
        Assert.Equal(ContributionVerificationState.Verified, contribution.VerificationState);
        Assert.Equal(DecisionChannelCodes.TargetSignal.AuthoritySupport, contribution.TargetSignalCode);
        Assert.Equal("DecisionContractTag.StatuteRule", contribution.Provenance.SourceTypeCode);
    }

    [Fact]
    public async Task Key_issue_tag_becomes_context_only_fact_support()
    {
        var node = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var contract = ContractRepo(Contract(
            DecisionContractStatuses.Active,
            tags: [Tag(DecisionContractTagKinds.KeyIssue, "Whether the defendant owed the plaintiff a duty of care.")]));

        var channel = new DecisionContractChannel(contract, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        var contribution = Assert.Single(result);
        Assert.Equal(ContributionRelation.ContextOnly, contribution.Relation);
        Assert.Equal(DecisionChannelCodes.TargetSignal.FactSupport, contribution.TargetSignalCode);
        Assert.Equal("DecisionContractTag.KeyIssue", contribution.Provenance.SourceTypeCode);
    }

    [Fact]
    public async Task Draft_contract_contributes_nothing()
    {
        var node = Node("Defendant was speeding at the time of the collision impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var contract = ContractRepo(Contract(
            DecisionContractStatuses.Draft,
            boundaries: [Boundary("Defendant was speeding at the collision impact.", DecisionContractFactStates.Known)]));

        var channel = new DecisionContractChannel(contract, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Fails_soft_when_no_authoritative_hierarchy()
    {
        var hierarchy = HierarchyRepo();
        var contract = ContractRepo(Contract(
            DecisionContractStatuses.Active,
            boundaries: [Boundary("Defendant was speeding at the collision impact.", DecisionContractFactStates.Known)]));

        var channel = new DecisionContractChannel(contract, hierarchy);
        var context = new DecisionChannelResolveContext
        {
            TenantId = Tenant,
            UserId = User,
            DecisionMatterId = Matter,
            AuthoritativeHierarchyExecutionId = null,
        };

        var result = await channel.ResolveContributionsAsync(context);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Fails_soft_when_no_contract()
    {
        var node = Node("Defendant was speeding at impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var contract = ContractRepo(null);

        var channel = new DecisionContractChannel(contract, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Drops_boundary_that_matches_no_node()
    {
        var node = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var contract = ContractRepo(Contract(
            DecisionContractStatuses.Active,
            boundaries: [Boundary("Surveillance camera timestamp calibration certificate expired.", DecisionContractFactStates.Known)]));

        var channel = new DecisionContractChannel(contract, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────

    private static DecisionChannelResolveContext Context() => new()
    {
        TenantId = Tenant,
        UserId = User,
        DecisionMatterId = Matter,
        AuthoritativeHierarchyExecutionId = Execution,
    };

    private static HierarchyNodeDto Node(string statement, string role) => new(
        HierarchyNodeId: Guid.NewGuid(),
        ParentHierarchyNodeId: null,
        Depth: 1,
        DisplayOrder: 0,
        NodeTypeCode: "ALTERNATIVE",
        NodeRoleCode: role,
        Title: null,
        Statement: statement,
        BranchStateCode: "ACTIVE",
        ContinueNarrowing: false,
        StopReasonCode: null,
        Confidence: null,
        CapabilityCode: null,
        OriginCode: "LLM_PROPOSAL");

    private static DecisionContractFactBoundaryDto Boundary(string snapshotText, string factStateCode) => new(
        DecisionContractFactBoundaryId: Guid.NewGuid(),
        FactId: null,
        FactStateCode: factStateCode,
        SnapshotText: snapshotText,
        DisplayOrder: 0);

    private static DecisionContractTagDto Tag(string tagKindCode, string tagText) => new(
        DecisionContractTagId: Guid.NewGuid(),
        TagKindCode: tagKindCode,
        TagText: tagText,
        DisplayOrder: 0);

    private static DecisionContractDto Contract(
        string statusCode,
        DecisionContractFactBoundaryDto[]? boundaries = null,
        DecisionContractTagDto[]? tags = null) => new(
        DecisionContractId: Guid.NewGuid(),
        MatterId: Matter,
        VersionNumber: 1,
        StatusCode: statusCode,
        DecisionQuestion: null,
        ClientObjective: null,
        SuccessDefinition: null,
        DecisionDate: null,
        Jurisdiction: null,
        CourtOrForum: null,
        GoverningLaw: null,
        ProceduralPosture: null,
        CaseType: null,
        ApplicableLegalFramework: null,
        MovingParty: null,
        InitialBurden: null,
        UltimateBurden: null,
        StandardOfProofOrReview: null,
        BurdenNotes: null,
        EvidenceBoundary: null,
        AuthorityBoundary: null,
        SourceRestrictions: null,
        AuthorityCutoffDate: null,
        DecisionHorizon: null,
        ExternalResearchPermitted: false,
        ReviewBeforeActivation: false,
        ReviewBeforeFinal: false,
        SemanticValidationMode: "STRICT",
        Notes: null,
        CreatedByUserId: User,
        CreatedByDisplayName: null,
        CreatedDateUtc: DateTime.UtcNow,
        ApprovedByUserId: null,
        ApprovedByDisplayName: null,
        ApprovedDateUtc: null,
        RowVersion: [],
        Candidates: [],
        FactBoundaries: boundaries ?? [],
        Tags: tags ?? []);

    private static FakeContractHierarchyRepository HierarchyRepo(params HierarchyNodeDto[] nodes)
        => new(nodes.Length == 0 ? null : nodes);

    private static FakeDecisionContractRepository ContractRepo(DecisionContractDto? contract)
        => new(contract);

    private sealed class FakeContractHierarchyRepository(HierarchyNodeDto[]? nodes) : ILegalHierarchyExecutionRepository
    {
        public Task<HierarchyExecutionDetailDto?> GetExecutionAsync(Guid tenantId, Guid hierarchyExecutionId, CancellationToken cancellationToken = default)
        {
            if (nodes is null)
                return Task.FromResult<HierarchyExecutionDetailDto?>(null);
            var summary = new HierarchyExecutionSummaryDto(
                hierarchyExecutionId, Matter, Guid.NewGuid(), 1, 1, "PRIMARY", "COMPLETED", "VALID",
                "AUTHORITATIVE", "m", "p", 1, "v", nodes.Length, 1, DateTime.UtcNow, DateTime.UtcNow, []);
            return Task.FromResult<HierarchyExecutionDetailDto?>(new HierarchyExecutionDetailDto(summary, nodes));
        }

        public Task<IReadOnlyList<HierarchyExecutionSummaryDto>> GetRunsAsync(Guid tenantId, Guid decisionMatterId, Guid decisionContractId, int decisionContractVersion, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<HierarchyAuthorityDto?> GetCurrentAuthorityAsync(Guid tenantId, Guid decisionMatterId, Guid decisionContractId, int decisionContractVersion, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<HierarchyExecutionSummaryDto> RecordExecutionAsync(Guid tenantId, Guid userId, HierarchyExecutionRecord record, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<PromoteHierarchyAuthorityResult> PromoteAuthorityAsync(Guid tenantId, Guid userId, PromoteHierarchyAuthorityCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeDecisionContractRepository(DecisionContractDto? contract) : ILegalDecisionContractRepository
    {
        public Task<DecisionContractDto?> GetCurrentContractAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
            => Task.FromResult(contract);

        public Task<DecisionContractDto?> GetContractByIdAsync(Guid tenantId, Guid decisionContractId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<DecisionContractDto> ProvisionAsync(Guid tenantId, Guid userId, Guid matterId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<DecisionContractVersionSummaryDto>> GetVersionHistoryAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<DecisionContractReviewDto>> GetReviewsAsync(Guid tenantId, Guid decisionContractId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<DecisionContractOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<string?> GetMatterTitleAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<PoloxiWeightDto>> GetPoloxiWeightsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task UpdateDecisionAsync(Guid tenantId, Guid userId, DecisionContractDecisionCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task UpdateLegalContextAsync(Guid tenantId, Guid userId, DecisionContractLegalContextCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task UpdateBurdenAsync(Guid tenantId, Guid userId, DecisionContractBurdenCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task UpdateBoundariesAsync(Guid tenantId, Guid userId, DecisionContractBoundariesCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task UpdateCandidatesAsync(Guid tenantId, Guid userId, DecisionContractCandidatesCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task UpdateSettingsAsync(Guid tenantId, Guid userId, DecisionContractSettingsCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task TransitionStatusAsync(Guid tenantId, Guid userId, Guid decisionContractId, byte[] rowVersion, string fromStatus, string toStatus, string reviewAction, string? comment, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<DecisionContractDto> CreateNewVersionAsync(Guid tenantId, Guid userId, Guid decisionContractId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task ActivateAsync(Guid tenantId, Guid userId, Guid decisionContractId, byte[] rowVersion, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
