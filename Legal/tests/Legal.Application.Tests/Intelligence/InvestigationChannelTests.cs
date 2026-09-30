using Legal.Application.Abstractions.Persistence;
using Legal.Application.Common.Models;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Channels;
using Xunit;
using HierarchyNodeDto = Legal.Application.Features.Intelligence.Decision.HierarchyNodeDto;

namespace Legal.Application.Tests.Intelligence;

// Slice-4 invariants for the Investigation channel: only matter-scoped intelligence findings inform the
// decision, confirmed/resolved findings become VERIFIED ESTABLISHES support on FactSupport, open findings
// raise Uncertainty (never support), dismissed/false-positive findings are dropped, mapping is
// deterministic and fail-soft, and the finding's numeric Score/Confidence never crosses the boundary.
public sealed class InvestigationChannelTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Matter = Guid.NewGuid();
    private static readonly Guid Execution = Guid.NewGuid();

    [Fact]
    public async Task Confirmed_resolved_finding_becomes_verified_fact_support()
    {
        var breachNode = Node("Defendant was speeding at the time of the collision impact.", "PROPOSITION");
        var dutyNode = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(breachNode, dutyNode);
        var intelligence = IntelligenceRepo(Finding(
            "Speeding confirmed by telematics",
            "Vehicle telematics show the defendant speeding at the collision impact.",
            statusCode: "RESOLVED", resolutionCode: "CONFIRMED"));

        var channel = new InvestigationChannel(intelligence, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        var contribution = Assert.Single(result);
        Assert.Equal(breachNode.HierarchyNodeId, contribution.HierarchyNodeId);
        Assert.Equal(DecisionChannelType.Investigation, contribution.ChannelType);
        Assert.Equal(ContributionRelation.Establishes, contribution.Relation);
        Assert.Equal(ContributionVerificationState.Verified, contribution.VerificationState);
        Assert.Equal(DecisionChannelCodes.TargetSignal.FactSupport, contribution.TargetSignalCode);
        Assert.True(contribution.ContributesPositiveSupport);
        Assert.Equal("IntelligenceFinding.Confirmed", contribution.Provenance.SourceTypeCode);
    }

    [Fact]
    public async Task Open_finding_raises_uncertainty_without_support()
    {
        var node = Node("Defendant was speeding at the time of the collision impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var intelligence = IntelligenceRepo(Finding(
            "Possible speeding under investigation",
            "Vehicle telematics may show the defendant speeding at the collision impact.",
            statusCode: "OPEN", resolutionCode: null));

        var channel = new InvestigationChannel(intelligence, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        var contribution = Assert.Single(result);
        Assert.Equal(ContributionRelation.Challenges, contribution.Relation);
        Assert.Equal(ContributionVerificationState.Disputed, contribution.VerificationState);
        Assert.Equal(DecisionChannelCodes.TargetSignal.Uncertainty, contribution.TargetSignalCode);
        Assert.False(contribution.ContributesPositiveSupport);
        Assert.Equal("IntelligenceFinding.Open", contribution.Provenance.SourceTypeCode);
    }

    [Fact]
    public async Task Dismissed_finding_is_dropped()
    {
        var node = Node("Defendant was speeding at the time of the collision impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var intelligence = IntelligenceRepo(Finding(
            "Speeding allegation",
            "Vehicle telematics show the defendant speeding at the collision impact.",
            statusCode: "RESOLVED", resolutionCode: "FALSE_POSITIVE"));

        var channel = new InvestigationChannel(intelligence, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Fails_soft_when_no_authoritative_hierarchy()
    {
        var intelligence = IntelligenceRepo(Finding("Speeding confirmed", "Defendant speeding at impact.", "RESOLVED", "CONFIRMED"));
        var hierarchy = HierarchyRepo();

        var channel = new InvestigationChannel(intelligence, hierarchy);
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
    public async Task Fails_soft_when_no_findings()
    {
        var node = Node("Defendant was speeding at impact.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var intelligence = IntelligenceRepo();

        var channel = new InvestigationChannel(intelligence, hierarchy);
        var result = await channel.ResolveContributionsAsync(Context());

        Assert.Empty(result);
    }

    [Fact]
    public async Task Drops_finding_that_matches_no_node()
    {
        var node = Node("Defendant owed the plaintiff a duty of reasonable care.", "PROPOSITION");
        var hierarchy = HierarchyRepo(node);
        var intelligence = IntelligenceRepo(Finding(
            "Unrelated calibration certificate",
            "Surveillance camera timestamp calibration certificate expired.",
            statusCode: "RESOLVED", resolutionCode: "CONFIRMED"));

        var channel = new InvestigationChannel(intelligence, hierarchy);
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

    private static IntelligenceFindingDto Finding(string title, string summary, string statusCode, string? resolutionCode) => new(
        IntelligenceFindingId: Guid.NewGuid(),
        TenantId: Tenant,
        CapabilityCode: "INVESTIGATION",
        CapabilityName: "Scene Investigation",
        EntityTypeCode: "Matter",
        EntityId: Matter,
        FindingTypeCode: "RISK_SIGNAL",
        SeverityCode: "HIGH",
        StatusCode: statusCode,
        Title: title,
        Summary: summary,
        Explanation: summary,
        Score: 0.9m,
        Confidence: 0.8m,
        RuleVersion: null,
        DetectedDateUtc: DateTime.UtcNow,
        DueDateUtc: null,
        ResolvedDateUtc: statusCode == "RESOLVED" ? DateTime.UtcNow : null,
        ResolutionCode: resolutionCode,
        RowVersion: []);

    private static FakeInvestigationHierarchyRepository HierarchyRepo(params HierarchyNodeDto[] nodes)
        => new(nodes.Length == 0 ? null : nodes);

    private static FakeIntelligenceRepository IntelligenceRepo(params IntelligenceFindingDto[] findings)
        => new(findings);

    private sealed class FakeInvestigationHierarchyRepository(HierarchyNodeDto[]? nodes) : ILegalHierarchyExecutionRepository
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

    private sealed class FakeIntelligenceRepository(IntelligenceFindingDto[] findings) : StubIntelligenceRepository
    {
        public override Task<PagedResult<IntelligenceFindingDto>> SearchFindingsAsync(SearchIntelligenceFindingsQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<IntelligenceFindingDto>
            {
                Items = findings,
                TotalCount = findings.Length,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
            });
    }
}
