using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Channels;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// LegalChannelSignalAdapter tests — the DOMAIN side of the POLOXI admission boundary.
//
// Locks the projection contract: verified channel contributions become TYPED, domain-neutral
// DecisionBranchSignal deltas that land only on the informed POLOXI dimension. Positive δ flows only
// for verified support; contradiction flows negative; challenges request reopen with no positive δ;
// context/insufficient/unverified and lineage-less contributions produce no signal.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LegalChannelSignalAdapterTests
{
    // Fixed lineage resolver so tests never depend on unimplemented infrastructure mapping.
    private sealed class FixedLineageResolver(ChannelContributionLineage lineage) : IChannelContributionLineageResolver
    {
        public ChannelContributionLineage Resolve(DecisionContribution contribution) => lineage;
    }

    private static LegalChannelSignalAdapter AdapterWith(ChannelContributionLineage lineage)
        => new(new FixedLineageResolver(lineage));

    private static DecisionContribution Contribution(
        DecisionChannelType channel = DecisionChannelType.DocumentEvidence,
        ContributionRelation relation = ContributionRelation.Supports,
        ContributionVerificationState verification = ContributionVerificationState.Verified,
        string targetSignalCode = DecisionChannelCodes.TargetSignal.EvidenceSupport,
        double? magnitude = null) => new()
        {
            TenantId = Guid.NewGuid(),
            DecisionMatterId = Guid.NewGuid(),
            HierarchyExecutionId = Guid.NewGuid(),
            HierarchyNodeId = Guid.NewGuid(),
            ChannelType = channel,
            Relation = relation,
            VerificationState = verification,
            TargetSignalCode = targetSignalCode,
            Magnitude = magnitude,
            Provenance = new ContributionProvenance { SourceTypeCode = "Test" },
        };

    [Fact]
    public void VerifiedEvidenceSupport_EmitsPositiveTypedEvidenceSignal_OnBranchLineage()
    {
        var branchId = Guid.NewGuid();
        var adapter = AdapterWith(new ChannelContributionLineage([branchId], []));

        var signals = adapter.Project([Contribution()]);

        var signal = Assert.Single(signals);
        Assert.Equal(branchId, signal.BranchId);
        Assert.Null(signal.CandidateId);
        Assert.True(signal.SupportDelta > 0);
        Assert.False(signal.ReopenRequested);
        Assert.Equal(DecisionSignalTarget.Evidence, signal.TargetSignal);
    }

    [Fact]
    public void VerifiedAuthoritySupport_EmitsTypedAuthoritySignal()
    {
        var adapter = AdapterWith(new ChannelContributionLineage([Guid.NewGuid()], []));

        var signals = adapter.Project([Contribution(
            channel: DecisionChannelType.LegalAuthority,
            targetSignalCode: DecisionChannelCodes.TargetSignal.AuthoritySupport)]);

        Assert.Equal(DecisionSignalTarget.Authority, Assert.Single(signals).TargetSignal);
    }

    [Fact]
    public void Contradiction_EmitsNegativeTypedDelta()
    {
        var adapter = AdapterWith(new ChannelContributionLineage([Guid.NewGuid()], []));

        var signals = adapter.Project([Contribution(relation: ContributionRelation.Contradicts)]);

        var signal = Assert.Single(signals);
        Assert.True(signal.SupportDelta < 0);
        Assert.Equal(DecisionBranchSignalKinds.ConstraintChanged, signal.ReasonCode);
    }

    [Fact]
    public void Challenge_RequestsReopen_WithNoPositiveDelta()
    {
        var adapter = AdapterWith(new ChannelContributionLineage([Guid.NewGuid()], []));

        var signals = adapter.Project([Contribution(relation: ContributionRelation.Challenges)]);

        var signal = Assert.Single(signals);
        Assert.True(signal.ReopenRequested);
        Assert.Equal(0.0, signal.SupportDelta);
        Assert.Equal(DecisionBranchSignalKinds.ReopenRequested, signal.ReasonCode);
    }

    [Fact]
    public void UnverifiedSupport_EmitsNoSignal()
    {
        var adapter = AdapterWith(new ChannelContributionLineage([Guid.NewGuid()], []));

        var signals = adapter.Project([Contribution(verification: ContributionVerificationState.Unverified)]);

        Assert.Empty(signals);
    }

    [Theory]
    [InlineData(ContributionRelation.ContextOnly)]
    [InlineData(ContributionRelation.Insufficient)]
    public void ContextOrInsufficient_EmitsNoSignal(ContributionRelation relation)
    {
        var adapter = AdapterWith(new ChannelContributionLineage([Guid.NewGuid()], []));

        var signals = adapter.Project([Contribution(relation: relation)]);

        Assert.Empty(signals);
    }

    [Fact]
    public void NoLineage_EmitsNoSignal()
    {
        var adapter = AdapterWith(ChannelContributionLineage.None);

        var signals = adapter.Project([Contribution()]);

        Assert.Empty(signals);
    }

    [Fact]
    public void CandidateLineageOnly_EmitsCandidateKeyedSignal()
    {
        var candidateId = Guid.NewGuid();
        var adapter = AdapterWith(new ChannelContributionLineage([], [candidateId]));

        var signal = Assert.Single(adapter.Project([Contribution()]));
        Assert.Null(signal.BranchId);
        Assert.Equal(candidateId, signal.CandidateId);
    }

    [Fact]
    public void BranchAndCandidateLineage_EmitsOnlyBranchSignal_NoDoubleCount()
    {
        var branchId = Guid.NewGuid();
        var adapter = AdapterWith(new ChannelContributionLineage([branchId], [Guid.NewGuid()]));

        var signal = Assert.Single(adapter.Project([Contribution()]));
        Assert.Equal(branchId, signal.BranchId);
        Assert.Null(signal.CandidateId);
    }

    [Fact]
    public void QualifiedSupport_EmitsSmallerPositiveDelta_ThanFullSupport()
    {
        var adapter = AdapterWith(new ChannelContributionLineage([Guid.NewGuid()], []));

        var full = Assert.Single(adapter.Project([Contribution(relation: ContributionRelation.Supports)]));
        var qualified = Assert.Single(adapter.Project([Contribution(relation: ContributionRelation.Qualifies)]));

        Assert.True(qualified.SupportDelta > 0);
        Assert.True(qualified.SupportDelta < full.SupportDelta);
    }

    [Fact]
    public void Projection_IsDeterministic()
    {
        var adapter = AdapterWith(new ChannelContributionLineage([Guid.NewGuid()], []));
        var contributions = new[] { Contribution(), Contribution(relation: ContributionRelation.Contradicts) };

        var first = adapter.Project(contributions);
        var second = adapter.Project(contributions);

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].SupportDelta, second[i].SupportDelta);
            Assert.Equal(first[i].TargetSignal, second[i].TargetSignal);
            Assert.Equal(first[i].ReasonCode, second[i].ReasonCode);
        }
    }

    [Fact]
    public void NullMagnitudeSupport_FallsBackToFixedDelta()
    {
        var adapter = AdapterWith(new ChannelContributionLineage([Guid.NewGuid()], []));

        var baseline = Assert.Single(adapter.Project([Contribution(magnitude: null)]));
        var maxPlacement = Assert.Single(adapter.Project([Contribution(magnitude: 1.0)]));

        // Null magnitude lands at the full-support ceiling, identical to the top of the placement band.
        Assert.Equal(baseline.SupportDelta, maxPlacement.SupportDelta);
    }

    [Fact]
    public void HigherPlacementMagnitude_EarnsStrongerSupportDelta()
    {
        var adapter = AdapterWith(new ChannelContributionLineage([Guid.NewGuid()], []));

        var low = Assert.Single(adapter.Project([Contribution(magnitude: 0.0)]));
        var mid = Assert.Single(adapter.Project([Contribution(magnitude: 0.5)]));
        var high = Assert.Single(adapter.Project([Contribution(magnitude: 1.0)]));

        Assert.True(low.SupportDelta > 0);
        Assert.True(low.SupportDelta < mid.SupportDelta);
        Assert.True(mid.SupportDelta < high.SupportDelta);
    }

    [Fact]
    public void PlacementMagnitude_IsClampedWithinSupportBand()
    {
        var adapter = AdapterWith(new ChannelContributionLineage([Guid.NewGuid()], []));

        var over = Assert.Single(adapter.Project([Contribution(magnitude: 5.0)]));
        var under = Assert.Single(adapter.Project([Contribution(magnitude: -5.0)]));
        var ceiling = Assert.Single(adapter.Project([Contribution(magnitude: 1.0)]));
        var floor = Assert.Single(adapter.Project([Contribution(magnitude: 0.0)]));

        Assert.Equal(ceiling.SupportDelta, over.SupportDelta);
        Assert.Equal(floor.SupportDelta, under.SupportDelta);
    }
}
