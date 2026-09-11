using Legal.Application.Features.Intelligence;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ── Phase 0 golden-master baseline ──────────────────────────────────────────────────────────
// WideNarrowingPolicy is the deterministic, zero-LLM heart of POLOXI adaptive narrowing. These
// tests pin its CURRENT behavior so any Phase 1+ extraction/refactor that changes an outcome is
// caught immediately. They are characterization tests: assertions encode observed behavior, not a
// new spec. Do NOT "fix" a failing assertion by editing the test — a failure means behavior moved.
public sealed class WideNarrowingPolicyGoldenMasterTests
{
    private static WideConfiguration Config() =>
        new(TargetConfidence: .80m, MinimumBranchConfidence: .30m, MaximumBranchesPerLevel: 5,
            AbsoluteDepthCeiling: 4, MaximumTotalLlmCalls: 20);

    private static WideBranchRecord Branch(string name, string state, decimal evidenceSupport) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), 1, name, name, name,
            null, null, "GROUNDED", 0, 0m, true, null, false, null, 0)
        {
            BranchStateCode = state,
            EvidenceSupport = evidenceSupport,
        };

    private static WideExternalKnowledgeSnippet Snippet(string title, string url) =>
        new(query: title, title: title, url: url, snippet: title, score: .9m, retrievedDateUtc: DateTime.UtcNow);

    // ── Branch narrowing ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Branch_WithAdequateCoverage_AndLowInformationValue_IsResolved()
    {
        var cfg = Config();
        var branch = Branch("Premium", WideBranchStates.Active, evidenceSupport: .80m); // >= .60 floor
        var iv = new Dictionary<Guid, decimal> { [branch.WideBranchId] = .10m }; // < .35 floor
        var priorSupport = new Dictionary<Guid, decimal>();

        var result = WideNarrowingPolicy.EvaluateBranches([branch], iv, priorSupport, cfg);

        Assert.Contains(branch.WideBranchId, result.ResolvedBranchIds);
        Assert.Empty(result.ReopenedBranchIds);
    }

    [Fact]
    public void Branch_WithMissingInformationValueEstimate_IsNotResolved()
    {
        // Invariant: absence of an IV estimate is uncertainty, not resolution.
        var cfg = Config();
        var branch = Branch("Premium", WideBranchStates.Active, evidenceSupport: .90m);
        var result = WideNarrowingPolicy.EvaluateBranches(
            [branch], new Dictionary<Guid, decimal>(), new Dictionary<Guid, decimal>(), cfg);

        Assert.Empty(result.ResolvedBranchIds);
    }

    [Fact]
    public void Branch_BelowCoverageFloor_IsNotResolved_EvenWithLowInformationValue()
    {
        var cfg = Config();
        var branch = Branch("Premium", WideBranchStates.Active, evidenceSupport: .40m); // < .60 floor
        var iv = new Dictionary<Guid, decimal> { [branch.WideBranchId] = .05m };
        var result = WideNarrowingPolicy.EvaluateBranches([branch], iv, new Dictionary<Guid, decimal>(), cfg);

        Assert.Empty(result.ResolvedBranchIds);
    }

    [Fact]
    public void ResolvedBranch_ReopensWhenEvidenceSupportMovesBeyondDelta()
    {
        // Invariant 4: reversible uncertainty — a resolved branch reopens on material evidence change.
        var cfg = Config();
        var branch = Branch("Premium", WideBranchStates.Resolved, evidenceSupport: .80m);
        var priorSupport = new Dictionary<Guid, decimal> { [branch.WideBranchId] = .50m }; // delta .30 >= .15
        var result = WideNarrowingPolicy.EvaluateBranches(
            [branch], new Dictionary<Guid, decimal>(), priorSupport, cfg);

        Assert.Contains(branch.WideBranchId, result.ReopenedBranchIds);
    }

    [Fact]
    public void ResolvedBranch_StaysResolvedWhenEvidenceStable()
    {
        var cfg = Config();
        var branch = Branch("Premium", WideBranchStates.Resolved, evidenceSupport: .82m);
        var priorSupport = new Dictionary<Guid, decimal> { [branch.WideBranchId] = .80m }; // delta .02 < .15
        var result = WideNarrowingPolicy.EvaluateBranches(
            [branch], new Dictionary<Guid, decimal>(), priorSupport, cfg);

        Assert.Empty(result.ReopenedBranchIds);
        Assert.Empty(result.ResolvedBranchIds);
    }

    [Fact]
    public void PrunedBranch_IsIgnored()
    {
        var cfg = Config();
        var branch = Branch("Bad", WideBranchStates.Pruned, evidenceSupport: .90m);
        var iv = new Dictionary<Guid, decimal> { [branch.WideBranchId] = .05m };
        var result = WideNarrowingPolicy.EvaluateBranches([branch], iv, new Dictionary<Guid, decimal>(), cfg);

        Assert.Empty(result.ResolvedBranchIds);
        Assert.Empty(result.ReopenedBranchIds);
    }

    // ── Candidate narrowing ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Candidate_WithThinCoverage_GoesToWatch_NotEliminated()
    {
        // Invariant 1: missing evidence ⇒ WATCH ⇒ investigate, never eliminate on missing data.
        var cfg = Config();
        var signals = new Dictionary<string, decimal> { ["Alpha"] = .10m };
        var result = WideNarrowingPolicy.EvaluateCandidates(
            signals, [], new Dictionary<string, string>(), cfg);

        Assert.Equal(WideCandidateStates.Watch, result.CandidateStates["Alpha"]);
    }

    [Fact]
    public void Candidate_FarBehindLeader_WithAdequateCoverage_IsDeferred()
    {
        var cfg = Config();
        var signals = new Dictionary<string, decimal> { ["Leader"] = 1.0m, ["Trailer"] = .10m };
        // Give both adequate host coverage (>= NarrowingCandidateCoverageFloor .50 of maxHosts=2 ⇒ 1 host).
        var knowledge = new[]
        {
            Snippet("Leader wins", "https://a.com/1"),
            Snippet("Trailer story", "https://b.com/1"),
        };
        var result = WideNarrowingPolicy.EvaluateCandidates(
            signals, knowledge, new Dictionary<string, string>(), cfg);

        Assert.Equal(WideCandidateStates.Deferred, result.CandidateStates["Trailer"]);
        Assert.Equal(WideCandidateStates.Active, result.CandidateStates["Leader"]);
    }

    // ── Expansion gate ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Expansion_AdmitsNameWithEnoughDistinctHosts()
    {
        var cfg = Config(); // NarrowingDiscoveryMinimumSupport = 2
        var knowledge = new[]
        {
            Snippet("Raleigh grows", "https://one.com/x"),
            Snippet("Raleigh booms", "https://two.com/y"),
        };
        var result = WideNarrowingPolicy.EvaluateExpansion(["Raleigh"], knowledge, cfg);

        Assert.Contains("Raleigh", result.AdmittedNames);
        Assert.Empty(result.RejectedNames);
    }

    [Fact]
    public void Expansion_RejectsNameBelowHostThreshold()
    {
        var cfg = Config();
        var knowledge = new[] { Snippet("Raleigh only once", "https://one.com/x") };
        var result = WideNarrowingPolicy.EvaluateExpansion(["Raleigh"], knowledge, cfg);

        Assert.Contains("Raleigh", result.RejectedNames);
        Assert.Empty(result.AdmittedNames);
    }

    // ── Trend precedence ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Trend_ReopenBeatsExpansion()
    {
        var trend = WideNarrowingPolicy.ComputeTrend(
            activeBranchesBefore: 3, activeBranchesAfter: 3,
            candidatesBefore: 2, candidatesAfter: 4,
            reopenedCount: 1, admittedCount: 2,
            normalizedEntropyAfter: null, convergenceTrigger: .3m, actualInformationGain: null);

        Assert.Equal(WideNarrowingTrends.Reopened, trend);
    }

    [Fact]
    public void Trend_ConvergedWhenEntropyBelowTrigger()
    {
        var trend = WideNarrowingPolicy.ComputeTrend(
            activeBranchesBefore: 3, activeBranchesAfter: 3,
            candidatesBefore: 4, candidatesAfter: 4,
            reopenedCount: 0, admittedCount: 0,
            normalizedEntropyAfter: .10m, convergenceTrigger: .30m, actualInformationGain: 0m);

        Assert.Equal(WideNarrowingTrends.Converged, trend);
    }

    [Fact]
    public void Trend_StableWhenNothingChanges()
    {
        var trend = WideNarrowingPolicy.ComputeTrend(
            activeBranchesBefore: 3, activeBranchesAfter: 3,
            candidatesBefore: 4, candidatesAfter: 4,
            reopenedCount: 0, admittedCount: 0,
            normalizedEntropyAfter: .90m, convergenceTrigger: .30m, actualInformationGain: 0m);

        Assert.Equal(WideNarrowingTrends.Stable, trend);
    }
}
