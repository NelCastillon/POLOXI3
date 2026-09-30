using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Channels;
using Xunit;
using HierarchyNodeDto = Legal.Application.Features.Intelligence.Decision.HierarchyNodeDto;

namespace Legal.Application.Tests.Intelligence;

// Slice-6 invariants for the ExternalResearch channel: only externally-retrieved authority that is
// verified AND decision-authorized feeds positive AuthoritySupport; everything else is dropped
// fail-soft. Mapping to authoritative hierarchy nodes is deterministic (lexical overlap), and a missing
// hierarchy, no research, or no match contributes nothing. POLOXI Wide2 still owns the outcome.
public sealed class ExternalResearchChannelTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Matter = Guid.NewGuid();
    private static readonly Guid Execution = Guid.NewGuid();

    [Fact]
    public async Task Verified_authorized_authority_becomes_verified_authority_support()
    {
        var node = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var decision = DecisionRepo(Evidence(
            "STATUTE", isVerified: true, isAuthorized: true,
            title: "Duty of reasonable care owed to the plaintiff.", provider: "Westlaw"));

        var channel = new ExternalResearchChannel(decision, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        var contribution = Assert.Single(result);
        Assert.Equal(node.HierarchyNodeId, contribution.HierarchyNodeId);
        Assert.Equal(DecisionChannelType.ExternalResearch, contribution.ChannelType);
        Assert.Equal(ContributionRelation.Establishes, contribution.Relation);
        Assert.Equal(ContributionVerificationState.Verified, contribution.VerificationState);
        Assert.Equal(DecisionChannelCodes.TargetSignal.AuthoritySupport, contribution.TargetSignalCode);
        Assert.Equal("STATUTE", contribution.ApplicabilityCode);
        Assert.Equal("Legal_DecisionEvidenceVerification.ExternalResearch", contribution.Provenance.SourceTypeCode);
        Assert.Contains("Westlaw", contribution.Provenance.VerificationReason);
    }

    [Fact]
    public async Task Unverified_authority_is_dropped()
    {
        var node = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var decision = DecisionRepo(Evidence(
            "STATUTE", isVerified: false, isAuthorized: true,
            title: "Duty of reasonable care owed to the plaintiff."));

        var channel = new ExternalResearchChannel(decision, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Unauthorized_authority_is_dropped()
    {
        var node = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var decision = DecisionRepo(Evidence(
            "STATUTE", isVerified: true, isAuthorized: false,
            title: "Duty of reasonable care owed to the plaintiff."));

        var channel = new ExternalResearchChannel(decision, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task No_hierarchy_contributes_nothing()
    {
        var hierarchy = HierarchyRepo();
        var decision = DecisionRepo(Evidence(
            "STATUTE", isVerified: true, isAuthorized: true,
            title: "Duty of reasonable care owed to the plaintiff."));

        var channel = new ExternalResearchChannel(decision, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task No_research_contributes_nothing()
    {
        var node = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var decision = DecisionRepo();

        var channel = new ExternalResearchChannel(decision, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Unmatched_authority_is_dropped()
    {
        var node = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var decision = DecisionRepo(Evidence(
            "CASE_LAW", isVerified: true, isAuthorized: true,
            title: "Maritime salvage jurisdiction over foreign vessels."));

        var channel = new ExternalResearchChannel(decision, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

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

    private static ExternalResearchEvidenceDto Evidence(
        string sourceTypeCode, bool isVerified, bool isAuthorized,
        string title, string? provider = null) => new(
        DecisionEvidenceVerificationId: Guid.NewGuid(),
        DecisionEvidenceId: Guid.NewGuid(),
        SourceTypeCode: sourceTypeCode,
        DispositionCode: isVerified ? "VERIFIED" : "UNVERIFIED",
        IsVerified: isVerified,
        IsDecisionAuthorized: isAuthorized,
        SourceProvider: provider,
        SourceRef: "https://example.test/authority",
        SourceTitle: title,
        Snippet: title,
        EvaluatedDateUtc: DateTime.UtcNow);

    private static FakeResearchHierarchyRepository HierarchyRepo(params HierarchyNodeDto[] nodes)
        => new(nodes.Length == 0 ? null : nodes);

    private static FakeExternalResearchDecisionRepository DecisionRepo(params ExternalResearchEvidenceDto[] evidence)
        => new(evidence);

    private sealed class FakeResearchHierarchyRepository(HierarchyNodeDto[]? nodes) : ILegalHierarchyExecutionRepository
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

    private sealed class FakeExternalResearchDecisionRepository(ExternalResearchEvidenceDto[] evidence) : StubDecisionRepository
    {
        public override Task<IReadOnlyCollection<ExternalResearchEvidenceDto>> GetMatterExternalResearchEvidenceAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ExternalResearchEvidenceDto>>(evidence);
    }
}