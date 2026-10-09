using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Channels;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Slice-2 invariants for the HumanIntelligence channel: attorney input is turned into QUALITATIVE
// source-truth only. A governance-approved assessment becomes a VERIFIED SUPPORTS contribution on
// FactSupport; an open challenge becomes a DISPUTED CHALLENGES contribution on Uncertainty (never
// positive support). Mapping is deterministic and fail-soft, and the boundary carries no magnitude —
// POLOXI Wide2 remains the sole decision engine.
public sealed class HumanIntelligenceChannelTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Matter = Guid.NewGuid();
    private static readonly Guid Execution = Guid.NewGuid();

    [Fact]
    public async Task Approved_assessment_becomes_verified_fact_support()
    {
        var breachNode = Node("Defendant was using a phone immediately before the collision impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(breachNode);
        var attorney = AttorneyRepo(true, HiNode(
            "Defendant was using a phone immediately before impact.",
            approved: Approved(),
            openChallenges: 0));

        var channel = new HumanIntelligenceChannel(attorney, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        var contribution = Assert.Single(result);
        Assert.Equal(breachNode.HierarchyNodeId, contribution.HierarchyNodeId);
        Assert.Equal(DecisionChannelType.HumanIntelligence, contribution.ChannelType);
        Assert.Equal(ContributionRelation.Supports, contribution.Relation);
        Assert.Equal(ContributionVerificationState.Verified, contribution.VerificationState);
        Assert.Equal(DecisionChannelCodes.TargetSignal.FactSupport, contribution.TargetSignalCode);
        Assert.True(contribution.ContributesPositiveSupport);
        Assert.Equal("AttorneyDecisionNode.ApprovedAssessment", contribution.Provenance.SourceTypeCode);
    }

    [Fact]
    public async Task Approved_assessment_carries_relative_placement_magnitude_from_sibling_band()
    {
        var breachNode = Node("Defendant was using a phone immediately before the collision impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(breachNode);
        // Attorney confirmed 0.60 inside the sibling band [0.40, 0.80] → relative position 0.5.
        var attorney = AttorneyRepo(true, HiNode(
            "Defendant was using a phone immediately before impact.",
            approved: Approved(confirmedValue: 0.60m, previousSiblingValue: 0.40m, nextSiblingValue: 0.80m),
            openChallenges: 0));

        var channel = new HumanIntelligenceChannel(attorney, hierarchy);
        var contribution = Assert.Single(await channel.ResolveContributionsAsync(Context()));

        Assert.NotNull(contribution.Magnitude);
        Assert.Equal(0.5, contribution.Magnitude!.Value, 3);
    }

    [Fact]
    public async Task Approved_assessment_without_sibling_band_has_null_magnitude()
    {
        var breachNode = Node("Defendant was using a phone immediately before the collision impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(breachNode);
        var attorney = AttorneyRepo(true, HiNode(
            "Defendant was using a phone immediately before impact.",
            approved: Approved(confirmedValue: 0.60m),
            openChallenges: 0));

        var channel = new HumanIntelligenceChannel(attorney, hierarchy);
        var contribution = Assert.Single(await channel.ResolveContributionsAsync(Context()));

        Assert.Null(contribution.Magnitude);
    }

    [Fact]
    public async Task Open_challenge_becomes_disputed_uncertainty_never_support()
    {
        var node = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var attorney = AttorneyRepo(true, HiNode(
            "Defendant owed plaintiff a duty of reasonable care.",
            approved: null,
            openChallenges: 2));

        var channel = new HumanIntelligenceChannel(attorney, hierarchy);
        var contribution = Assert.Single(await channel.ResolveContributionsAsync(Context()));

        Assert.Equal(node.HierarchyNodeId, contribution.HierarchyNodeId);
        Assert.Equal(ContributionRelation.Challenges, contribution.Relation);
        Assert.Equal(ContributionVerificationState.Disputed, contribution.VerificationState);
        Assert.Equal(DecisionChannelCodes.TargetSignal.Uncertainty, contribution.TargetSignalCode);
        Assert.False(contribution.ContributesPositiveSupport);
        Assert.Equal("AttorneyDecisionNode.OpenChallenge", contribution.Provenance.SourceTypeCode);
    }

    [Fact]
    public async Task Approved_node_with_open_challenge_emits_both_signals()
    {
        var node = Node("Defendant was using a phone immediately before the collision impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var attorney = AttorneyRepo(true, HiNode(
            "Defendant was using a phone immediately before impact.",
            approved: Approved(),
            openChallenges: 1));

        var channel = new HumanIntelligenceChannel(attorney, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Relation == ContributionRelation.Supports && c.VerificationState == ContributionVerificationState.Verified);
        Assert.Contains(result, c => c.Relation == ContributionRelation.Challenges && c.VerificationState == ContributionVerificationState.Disputed);
    }

    [Fact]
    public async Task Advisory_nodes_without_approval_or_challenge_are_dropped()
    {
        var node = Node("Defendant was using a phone immediately before impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var attorney = AttorneyRepo(true, HiNode(
            "Defendant was using a phone immediately before impact.",
            approved: null,
            openChallenges: 0));

        var channel = new HumanIntelligenceChannel(attorney, hierarchy);
        Assert.Empty(await channel.ResolveContributionsAsync(Context()));
    }

    [Fact]
    public async Task Fails_soft_when_attorney_input_disabled()
    {
        var node = Node("Defendant was using a phone immediately before impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var attorney = AttorneyRepo(false, HiNode(
            "Defendant was using a phone immediately before impact.",
            approved: Approved(),
            openChallenges: 3));

        var channel = new HumanIntelligenceChannel(attorney, hierarchy);
        Assert.Empty(await channel.ResolveContributionsAsync(Context()));
    }

    [Fact]
    public async Task Fails_soft_when_no_authoritative_hierarchy()
    {
        var hierarchy = HierarchyRepo();
        var attorney = AttorneyRepo(true, HiNode(
            "Defendant was using a phone immediately before impact.",
            approved: Approved(),
            openChallenges: 0));

        var channel = new HumanIntelligenceChannel(attorney, hierarchy);
        var context = new DecisionChannelResolveContext
        {
            TenantId = Tenant,
            UserId = User,
            DecisionMatterId = Matter,
            AuthoritativeHierarchyExecutionId = null,
        };

        Assert.Empty(await channel.ResolveContributionsAsync(context));
    }

    [Fact]
    public async Task Drops_attorney_node_that_matches_no_hierarchy_node()
    {
        var node = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var attorney = AttorneyRepo(true, HiNode(
            "Statute of limitations bar accrual tolling period.",
            approved: Approved(),
            openChallenges: 0));

        var channel = new HumanIntelligenceChannel(attorney, hierarchy);
        Assert.Empty(await channel.ResolveContributionsAsync(Context()));
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

    private static ApprovedMatterAssessmentDto Approved(
        decimal confirmedValue = 0.5m,
        decimal? previousSiblingValue = null,
        decimal? nextSiblingValue = null) => new(
        ApprovalId: Guid.NewGuid(),
        DecisionNodeId: Guid.NewGuid(),
        AssessmentId: Guid.NewGuid(),
        ConfirmedValue: confirmedValue,
        ApprovedByUserId: Guid.NewGuid(),
        ApprovedByDisplayName: "Jane Counsel",
        GovernancePolicyCode: "SELF_APPROVE",
        ApprovedDateUtc: DateTime.UtcNow,
        PreviousSiblingValue: previousSiblingValue,
        NextSiblingValue: nextSiblingValue);

    private static AttorneyDecisionNodeDto HiNode(string text, ApprovedMatterAssessmentDto? approved, int openChallenges) => new(
        DecisionNodeId: Guid.NewGuid(),
        ParentNodeId: null,
        CanonicalKey: "k",
        NodeKindCode: "Proposition",
        NodeLevel: 3,
        NodeText: text,
        OriginCode: "ATTORNEY",
        PlacementKey: null,
        StructuralStateCode: "ACTIVE",
        EvidenceStateCode: "NONE",
        AuthorityStateCode: "NONE",
        EvaluatedValue: null,
        Assessments: [],
        ApprovedAssessment: approved,
        OpenChallengeCount: openChallenges);

    private static FakeHierarchyRepo HierarchyRepo(params HierarchyNodeDto[] nodes)
        => new(nodes.Length == 0 ? null : nodes);

    private static FakeAttorneyRepo AttorneyRepo(bool enabled, params AttorneyDecisionNodeDto[] nodes)
        => new(enabled, nodes);

    private sealed class FakeHierarchyRepo(HierarchyNodeDto[]? nodes) : ILegalHierarchyExecutionRepository
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

    // Only GetMatterHumanIntelligenceAsync is used by the channel; every other member throws.
    private sealed class FakeAttorneyRepo(bool enabled, AttorneyDecisionNodeDto[] nodes) : IAttorneyDecisionInputRepository
    {
        public Task<MatterHumanIntelligenceDto> GetMatterHumanIntelligenceAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
            => Task.FromResult(new MatterHumanIntelligenceDto(
                matterId, enabled, nodes.Length, 1,
                nodes.Sum(n => n.Assessments.Count),
                nodes.Count(n => n.ApprovedAssessment is not null),
                nodes.Sum(n => n.OpenChallengeCount), nodes));

        public Task<IReadOnlyList<(Guid NodeId, string NodeText, decimal? Value)>> GetSiblingValuesAsync(Guid tenantId, Guid matterId, Guid candidateNodeId, Guid? parentNodeId, int nodeLevel, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<(Guid NodeId, int Depth, decimal Value, long NodeVersion)>> GetAncestorScoresAsync(Guid tenantId, Guid matterId, Guid parentNodeId, int maxDepth, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<AttorneyDuplicateCandidateDto>> FindDuplicateCandidatesAsync(Guid tenantId, Guid matterId, string nodeText, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<long> GetHierarchyVersionAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<CommitResult?> TryGetCommittedAsync(Guid tenantId, Guid idempotencyKey, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<CommitResult> CommitAttorneyInputAsync(Guid tenantId, Guid actorUserId, CommitAttorneyInputCommand command, string canonicalKey, string placementKey, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<AttorneyRelativeAssessmentDto> SubmitAssessmentAsync(Guid tenantId, Guid actorUserId, SubmitAttorneyAssessmentCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ApprovedMatterAssessmentDto> ApproveAssessmentAsync(Guid tenantId, Guid actorUserId, ApproveMatterAssessmentCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<Guid> RaiseChallengeAsync(Guid tenantId, Guid actorUserId, RaiseChallengeCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<CommitResult> RepositionAsync(Guid tenantId, Guid actorUserId, RepositionNodeCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<RetractResult> RetractAttorneyInputAsync(Guid tenantId, Guid actorUserId, RetractAttorneyInputCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ResolvedBranchNode> ResolveBranchNodeAsync(Guid tenantId, Guid actorUserId, ResolveBranchNodeCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
