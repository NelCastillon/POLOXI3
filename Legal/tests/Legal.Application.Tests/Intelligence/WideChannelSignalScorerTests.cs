using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Channels;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// WideChannelSignalScorer tests — the ENFORCED fold of typed channel signals onto the Wide2 candidate
// competition. Locks the cross-universe bridge contract:
//   • decision-session GUID signals translate to Wide2 candidates via normalized DisplayName,
//   • a direct candidate signal moves that candidate's composite,
//   • a branch signal moves every candidate competing on that branch,
//   • signed contradiction δ lowers a composite and can flip the winner,
//   • reopen/zero-δ signals (Human Intelligence challenges) never silently move a score,
//   • unmapped signals and empty inputs are fail-soft no-ops.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class WideChannelSignalScorerTests
{
    private static WideCandidateDto Candidate(Guid id, int rank, string name, decimal composite, params string[] branchNames)
        => new(id, rank, name, null, composite,
            branchNames.Select(b => new WideCandidateBranchScoreDto(b, 0.5m)).ToArray());

    private static DecisionBranchSignal CandidateSignal(Guid decisionCandidateId, double delta)
        => new(DecisionBranchSignalKinds.SupportChanged, null, decisionCandidateId, delta, false, "TEST", DecisionSignalTarget.Evidence);

    private static DecisionBranchSignal BranchSignal(Guid decisionBranchId, double delta)
        => new(DecisionBranchSignalKinds.SupportChanged, decisionBranchId, null, delta, false, "TEST", DecisionSignalTarget.Evidence);

    [Fact]
    public void EmptySignals_ReturnsCandidatesUnchanged()
    {
        var candidates = new[] { Candidate(Guid.NewGuid(), 1, "A", 0.6m) };

        var result = WideChannelSignalScorer.Fold(candidates, [], new Dictionary<Guid, string>(), new Dictionary<Guid, string>());

        Assert.False(result.AnyApplied);
        Assert.Empty(result.Adjustments);
        Assert.Same(candidates, result.Candidates);
    }

    [Fact]
    public void DirectCandidateSupport_RaisesCompositeByDelta()
    {
        var id = Guid.NewGuid();
        var decisionCandidateId = Guid.NewGuid();
        var candidates = new[] { Candidate(id, 1, "Settlement", 0.50m) };
        var names = new Dictionary<Guid, string> { [decisionCandidateId] = "Settlement" };

        var result = WideChannelSignalScorer.Fold(
            candidates,
            [CandidateSignal(decisionCandidateId, 0.30)],
            names,
            new Dictionary<Guid, string>());

        Assert.True(result.AnyApplied);
        var adj = Assert.Single(result.Adjustments);
        Assert.Equal(0.30, adj.NetDelta, 3);
        Assert.Equal(0.80m, result.Candidates.Single(c => c.WideCandidateId == id).CompositeScore);
    }

    [Fact]
    public void BranchSignal_PushesEveryCandidateCompetingOnThatBranch()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var decisionBranchId = Guid.NewGuid();
        var candidates = new[]
        {
            Candidate(a, 1, "A", 0.50m, "Liability"),
            Candidate(b, 2, "B", 0.40m, "Liability"),
        };
        var branchNames = new Dictionary<Guid, string> { [decisionBranchId] = "Liability" };

        var result = WideChannelSignalScorer.Fold(
            candidates, [BranchSignal(decisionBranchId, 0.15)], new Dictionary<Guid, string>(), branchNames);

        Assert.Equal(2, result.Adjustments.Count);
        Assert.Equal(0.65m, result.Candidates.Single(c => c.WideCandidateId == a).CompositeScore);
        Assert.Equal(0.55m, result.Candidates.Single(c => c.WideCandidateId == b).CompositeScore);
    }

    [Fact]
    public void Contradiction_CanFlipTheWinnerAndRerank()
    {
        var leaderId = Guid.NewGuid();
        var runnerUpId = Guid.NewGuid();
        var leaderDecisionId = Guid.NewGuid();
        var candidates = new[]
        {
            Candidate(leaderId, 1, "Leader", 0.60m),
            Candidate(runnerUpId, 2, "RunnerUp", 0.55m),
        };
        var names = new Dictionary<Guid, string> { [leaderDecisionId] = "Leader" };

        var result = WideChannelSignalScorer.Fold(
            candidates, [CandidateSignal(leaderDecisionId, -0.30)], names, new Dictionary<Guid, string>());

        Assert.True(result.WinnerChanged);
        Assert.Equal(1, result.Candidates.Single(c => c.WideCandidateId == runnerUpId).RankNumber);
        Assert.Equal(2, result.Candidates.Single(c => c.WideCandidateId == leaderId).RankNumber);
        Assert.Equal(0.30m, result.Candidates.Single(c => c.WideCandidateId == leaderId).CompositeScore);
    }

    [Fact]
    public void ReopenOnlyZeroDeltaSignal_NeverMovesComposite()
    {
        var id = Guid.NewGuid();
        var decisionCandidateId = Guid.NewGuid();
        var candidates = new[] { Candidate(id, 1, "A", 0.60m) };
        var names = new Dictionary<Guid, string> { [decisionCandidateId] = "A" };
        var reopen = new DecisionBranchSignal(
            DecisionBranchSignalKinds.ReopenRequested, null, decisionCandidateId, 0.0, true, "CHALLENGE");

        var result = WideChannelSignalScorer.Fold(candidates, [reopen], names, new Dictionary<Guid, string>());

        Assert.False(result.AnyApplied);
        Assert.Equal(0.60m, result.Candidates.Single().CompositeScore);
    }

    [Fact]
    public void UnmappedSignal_IsFailSoftNoOp()
    {
        var candidates = new[] { Candidate(Guid.NewGuid(), 1, "A", 0.60m) };

        // Signal references a decision candidate GUID with no name-table entry → cannot bridge → ignored.
        var result = WideChannelSignalScorer.Fold(
            candidates, [CandidateSignal(Guid.NewGuid(), 0.30)], new Dictionary<Guid, string>(), new Dictionary<Guid, string>());

        Assert.False(result.AnyApplied);
        Assert.Equal(0.60m, result.Candidates.Single().CompositeScore);
    }

    [Fact]
    public void CompositeIsClampedToUnitInterval()
    {
        var highId = Guid.NewGuid();
        var lowId = Guid.NewGuid();
        var highDecisionId = Guid.NewGuid();
        var lowDecisionId = Guid.NewGuid();
        var candidates = new[]
        {
            Candidate(highId, 1, "High", 0.90m),
            Candidate(lowId, 2, "Low", 0.10m),
        };
        var names = new Dictionary<Guid, string> { [highDecisionId] = "High", [lowDecisionId] = "Low" };

        var result = WideChannelSignalScorer.Fold(
            candidates,
            [CandidateSignal(highDecisionId, 0.30), CandidateSignal(lowDecisionId, -0.30)],
            names,
            new Dictionary<Guid, string>());

        Assert.Equal(1.00m, result.Candidates.Single(c => c.WideCandidateId == highId).CompositeScore);
        Assert.Equal(0.00m, result.Candidates.Single(c => c.WideCandidateId == lowId).CompositeScore);
    }

    [Fact]
    public void MultipleSignalsOnSameCandidate_AccumulateNetDelta()
    {
        var id = Guid.NewGuid();
        var decisionCandidateId = Guid.NewGuid();
        var candidates = new[] { Candidate(id, 1, "A", 0.40m) };
        var names = new Dictionary<Guid, string> { [decisionCandidateId] = "A" };

        var result = WideChannelSignalScorer.Fold(
            candidates,
            [CandidateSignal(decisionCandidateId, 0.30), CandidateSignal(decisionCandidateId, -0.15)],
            names,
            new Dictionary<Guid, string>());

        var adj = Assert.Single(result.Adjustments);
        Assert.Equal(0.15, adj.NetDelta, 3);
        Assert.Equal(2, adj.SignalCount);
        Assert.Equal(0.55m, result.Candidates.Single().CompositeScore);
    }
}
