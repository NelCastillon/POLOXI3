using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Slice-1 invariants for the Decision Channel layer: contributions are QUALITATIVE source-truth only
// (no numeric score exists anywhere on the boundary), DocumentEvidence admits only VERIFIED anchors,
// mapping is deterministic and fail-soft, and the persistence row carries no magnitude — POLOXI Wide2
// remains the sole decision engine.
public sealed class DecisionChannelContributionTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Matter = Guid.NewGuid();
    private static readonly Guid Execution = Guid.NewGuid();

    [Fact]
    public async Task Resolves_verified_evidence_to_matching_node_as_qualitative_support()
    {
        var breachNode = Node("Defendant was using a phone immediately before the collision impact.", "PROPOSITION");
        var dutyNode = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(breachNode, dutyNode);
        var corpus = CorpusRepo(VerifiedAssertion("phone records show the defendant using a phone before impact"));

        var channel = new DocumentEvidenceChannel(corpus, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        var contribution = Assert.Single(result);
        Assert.Equal(breachNode.HierarchyNodeId, contribution.HierarchyNodeId);
        Assert.Equal(DecisionChannelType.DocumentEvidence, contribution.ChannelType);
        Assert.Equal(ContributionRelation.Supports, contribution.Relation);
        Assert.Equal(ContributionVerificationState.Verified, contribution.VerificationState);
        Assert.Equal(DecisionChannelCodes.TargetSignal.EvidenceSupport, contribution.TargetSignalCode);
        Assert.True(contribution.ContributesPositiveSupport);
        Assert.Equal("Legal_SourceAssertion", contribution.Provenance.SourceTypeCode);
    }

    [Fact]
    public async Task Ignores_unverified_anchors()
    {
        var node = Node("Defendant was using a phone immediately before impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var corpus = CorpusRepo(UnverifiedAssertion("phone records show the defendant using a phone before impact"));

        var channel = new DocumentEvidenceChannel(corpus, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Fails_soft_when_no_authoritative_hierarchy()
    {
        var corpus = CorpusRepo(VerifiedAssertion("phone records show the defendant using a phone before impact"));
        var hierarchy = HierarchyRepo();

        var channel = new DocumentEvidenceChannel(corpus, hierarchy);
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
    public async Task Drops_evidence_that_matches_no_node()
    {
        var node = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var corpus = CorpusRepo(VerifiedAssertion("surveillance camera timestamp calibration certificate"));

        var channel = new DocumentEvidenceChannel(corpus, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Skips_structural_grouping_and_dimension_nodes()
    {
        var grouping = Node("Liability analysis phone duty breach causation.", "GROUPING");
        var hierarchy = HierarchyRepo(grouping);
        var corpus = CorpusRepo(VerifiedAssertion("phone duty breach causation liability analysis"));

        var channel = new DocumentEvidenceChannel(corpus, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Orchestrator_persists_contributions_as_source_truth_without_scores()
    {
        var node = Node("Defendant was using a phone immediately before impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var corpus = CorpusRepo(VerifiedAssertion("phone records show the defendant using a phone before impact"));
        var channel = new DocumentEvidenceChannel(corpus, hierarchy);
        var repo = new FakeContributionRepository();

        var orchestrator = new DecisionChannelOrchestrator(
            [channel], repo, NullLogger<DecisionChannelOrchestrator>.Instance);

        var result = await orchestrator.IngestContributionsAsync(Tenant, User, Matter, Execution);

        Assert.Equal(1, result.ContributionsPersisted);
        var row = Assert.Single(repo.Saved);
        Assert.Equal("DocumentEvidence", row.ChannelTypeCode);
        Assert.Equal("SUPPORTS", row.RelationCode);
        Assert.Equal("Verified", row.VerificationStateCode);
        Assert.Equal(node.HierarchyNodeId, row.HierarchyNodeId);

        // No numeric magnitude anywhere on the persistence row — POLOXI owns the outcome.
        Assert.DoesNotContain(row.GetType().GetProperties(),
            p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?) ||
                 p.PropertyType == typeof(double) || p.PropertyType == typeof(double?));
    }

    [Fact]
    public async Task Orchestrator_is_fail_soft_when_a_channel_throws()
    {
        var repo = new FakeContributionRepository();
        var orchestrator = new DecisionChannelOrchestrator(
            [new ThrowingChannel()], repo, NullLogger<DecisionChannelOrchestrator>.Instance);

        var result = await orchestrator.IngestContributionsAsync(Tenant, User, Matter, Execution);

        Assert.Equal(0, result.ContributionsPersisted);
        Assert.Empty(repo.Saved);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────

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

    private static LegalSourceAssertionDto VerifiedAssertion(string quoted)
        => Assertion(quoted, LegalSourceAssertionStates.Verified);

    private static LegalSourceAssertionDto UnverifiedAssertion(string quoted)
        => Assertion(quoted, LegalSourceAssertionStates.Unverified);

    private static LegalSourceAssertionDto Assertion(string quoted, string state) => new(
        LegalSourceAssertionId: Guid.NewGuid(),
        MatterId: Matter,
        LegalDocumentVersionId: Guid.NewGuid(),
        LegalDocumentPassageId: Guid.NewGuid(),
        LegalEvidenceItemId: null,
        LegalPropositionSupportId: null,
        StartOffset: 0,
        EndOffset: quoted.Length,
        QuotedText: quoted,
        QuotedTextHash: "hash",
        SourceVersionHash: "vhash",
        AnchorMethodCode: LegalSourceAssertionMethods.ExactSpan,
        VerificationStateCode: state,
        VerifiedDateUtc: null,
        SupersededBySourceAssertionId: null,
        GenerationOriginCode: "DYNAMIC_LLM");

    private static FakeHierarchyRepository HierarchyRepo(params HierarchyNodeDto[] nodes)
        => new(nodes.Length == 0 ? null : nodes);

    private static FakeCorpusRepository CorpusRepo(params LegalSourceAssertionDto[] assertions)
        => new(assertions);

    private sealed class FakeHierarchyRepository(HierarchyNodeDto[]? nodes) : ILegalHierarchyExecutionRepository
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

    // Only GetMatterEvidenceGraphAsync is used by the channel; every other member throws.
    private sealed class FakeCorpusRepository(LegalSourceAssertionDto[] assertions) : StubCorpusRepository
    {
        public override Task<LegalMatterEvidenceGraphDto> GetMatterEvidenceGraphAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
            => Task.FromResult(new LegalMatterEvidenceGraphDto(matterId, 1, [], [], assertions));
    }

    private sealed class FakeContributionRepository : IChannelContributionRepository
    {
        public List<ChannelContributionPersistence> Saved { get; } = [];
        public Task SaveContributionsAsync(IReadOnlyCollection<ChannelContributionPersistence> contributions, CancellationToken cancellationToken = default)
        {
            Saved.AddRange(contributions);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyCollection<ChannelContributionDto>> GetContributionsForNodeAsync(Guid tenantId, Guid hierarchyNodeId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ChannelContributionDto>>([]);
        public Task<IReadOnlyCollection<ChannelContributionDto>> GetContributionsForExecutionAsync(Guid tenantId, Guid hierarchyExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ChannelContributionDto>>([]);
        public Task<IReadOnlyCollection<ChannelContributionDto>> GetContributionsForMatterAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ChannelContributionDto>>([]);
    }

    private sealed class ThrowingChannel : IDecisionChannel
    {
        public DecisionChannelType ChannelType => DecisionChannelType.DocumentEvidence;
        public Task<IReadOnlyList<DecisionContribution>> ResolveContributionsAsync(DecisionChannelResolveContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }
}
