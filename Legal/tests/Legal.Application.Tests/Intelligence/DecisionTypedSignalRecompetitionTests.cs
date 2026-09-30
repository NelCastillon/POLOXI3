using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Typed POLOXI support signals (backward-compatible dimension targeting). Proves the stronger invariant:
// a typed contribution moves ONLY its designated dimension while the unchanged composite/ceiling math
// determines the outcome, and a null-target (legacy) signal keeps the historical V+A+E coupling exactly.
public sealed class DecisionTypedSignalRecompetitionTests
{
    private static DecisionCoreSettings Settings() => new(
        0.20, 0.25, 0.25, 0.15, 0.10, 0.05,
        0.35, 0.25, 0.15, 0.10, 0.30, 0.40,
        4, 24, 8);

    private static DecisionCandidatePersistence Candidate(string code, string name, decimal composite, bool winner)
        => new(
            Guid.NewGuid(), code, name, name,
            LegalSupport: 0.60m, FactSupport: 0.60m, EvidenceSupport: 0.50m, AuthoritySupport: 0.50m,
            Verification: 0.60m, Uncertainty: 0.40m, Discrimination: 0.40m, RankingImpact: 0.40m,
            Diversity: 0.30m, RedundancyPenalty: 0.05m, CompositeScore: composite, DecisionSupportCeiling: 0.90m,
            RankOrder: winner ? 1 : 2, IsWinner: winner, IsEliminated: false);

    private static DecisionBranchPersistence Branch(string code, string state, bool onFrontier)
        => new(
            Guid.NewGuid(), null, 1, code, code, null, state,
            InformationValue: 0.40m, DecisionRelevance: 0.50m, FlipPotential: 0.35m, EvidenceAvailability: 0.40m,
            AdvScore: 0.30m, Cost: 0.10m, IsOnFrontier: onFrontier, StopReason: null, SortOrder: 0);

    // A null-target signal must reproduce the exact legacy behavior: V += δ, A += δ, E += 0.5δ.
    [Fact]
    public void NullTarget_MovesVerificationAuthorityAndHalfEvidence_LegacyCoupling()
    {
        var c = Candidate("C1", "Acme prevails", composite: 0.60m, winner: true);
        var branches = new[] { Branch("C1.B1", "ACTIVE", false) };
        var signals = new[]
        {
            new DecisionBranchSignal(
                DecisionBranchSignalKinds.SupportChanged, BranchId: null, CandidateId: c.DecisionCandidateId,
                SupportDelta: 0.20, ReopenRequested: false, ReasonCode: "LEGACY", TargetSignal: null)
        };

        var result = DecisionRecompetition.Run(new[] { c }, branches, signals, Settings(), new HashSet<Guid>());
        var scored = result.Candidates.Single();

        Assert.Equal(0.80m, scored.Verification, 6);       // 0.60 + 0.20
        Assert.Equal(0.70m, scored.AuthoritySupport, 6);   // 0.50 + 0.20
        Assert.Equal(0.60m, scored.EvidenceSupport, 6);    // 0.50 + 0.10 (half weight)
        Assert.Equal(0.60m, scored.FactSupport, 6);        // untouched
        Assert.Equal(0.60m, scored.LegalSupport, 6);       // untouched
    }

    // A typed Evidence signal must move ONLY EvidenceSupport at full weight; every other dimension
    // (including Authority and Verification, which the legacy coupling would have moved) stays put.
    [Fact]
    public void TypedEvidence_MovesOnlyEvidence_AtFullWeight()
    {
        var c = Candidate("C1", "Acme prevails", composite: 0.60m, winner: true);
        var branches = new[] { Branch("C1.B1", "ACTIVE", false) };
        var signals = new[]
        {
            new DecisionBranchSignal(
                DecisionBranchSignalKinds.SupportChanged, BranchId: null, CandidateId: c.DecisionCandidateId,
                SupportDelta: 0.20, ReopenRequested: false, ReasonCode: "EVIDENCE",
                TargetSignal: DecisionSignalTarget.Evidence)
        };

        var result = DecisionRecompetition.Run(new[] { c }, branches, signals, Settings(), new HashSet<Guid>());
        var scored = result.Candidates.Single();

        Assert.Equal(0.70m, scored.EvidenceSupport, 6);    // 0.50 + 0.20 (full weight, not half)
        Assert.Equal(0.50m, scored.AuthoritySupport, 6);   // unchanged — NOT the legacy coupling
        Assert.Equal(0.60m, scored.Verification, 6);       // unchanged
        Assert.Equal(0.60m, scored.FactSupport, 6);        // unchanged
        Assert.Equal(0.60m, scored.LegalSupport, 6);       // unchanged
    }

    // A typed Authority signal moves only Authority; Verification/Evidence stay put.
    [Fact]
    public void TypedAuthority_MovesOnlyAuthority()
    {
        var c = Candidate("C1", "Acme prevails", composite: 0.60m, winner: true);
        var branches = new[] { Branch("C1.B1", "ACTIVE", false) };
        var signals = new[]
        {
            new DecisionBranchSignal(
                DecisionBranchSignalKinds.SupportChanged, BranchId: null, CandidateId: c.DecisionCandidateId,
                SupportDelta: 0.30, ReopenRequested: false, ReasonCode: "AUTHORITY",
                TargetSignal: DecisionSignalTarget.Authority)
        };

        var scored = DecisionRecompetition.Run(new[] { c }, branches, signals, Settings(), new HashSet<Guid>())
            .Candidates.Single();

        Assert.Equal(0.80m, scored.AuthoritySupport, 6);   // 0.50 + 0.30
        Assert.Equal(0.60m, scored.Verification, 6);       // unchanged
        Assert.Equal(0.50m, scored.EvidenceSupport, 6);    // unchanged
    }

    // Multiple typed signals to the same dimension aggregate (sum) before a single clamp, and the
    // result must be independent of signal ordering (deterministic/idempotent).
    [Fact]
    public void MultipleTypedEvidenceSignals_AggregateBeforeClamp_OrderIndependent()
    {
        var c = Candidate("C1", "Acme prevails", composite: 0.60m, winner: true);
        var branches = new[] { Branch("C1.B1", "ACTIVE", false) };

        DecisionBranchSignal Ev(double d) => new(
            DecisionBranchSignalKinds.SupportChanged, BranchId: null, CandidateId: c.DecisionCandidateId,
            SupportDelta: d, ReopenRequested: false, ReasonCode: "EVIDENCE",
            TargetSignal: DecisionSignalTarget.Evidence);

        var forward = new[] { Ev(0.30), Ev(0.20), Ev(-0.25) };
        var reversed = new[] { Ev(-0.25), Ev(0.20), Ev(0.30) };

        var a = DecisionRecompetition.Run(new[] { c }, branches, forward, Settings(), new HashSet<Guid>())
            .Candidates.Single();
        var b = DecisionRecompetition.Run(new[] { c }, branches, reversed, Settings(), new HashSet<Guid>())
            .Candidates.Single();

        // 0.50 + (0.30 + 0.20 - 0.25) = 0.75, regardless of order.
        Assert.Equal(0.75m, a.EvidenceSupport, 6);
        Assert.Equal(a.EvidenceSupport, b.EvidenceSupport);
    }

    // Legacy (null) and typed signals on the same candidate compose additively without interfering:
    // the untargeted delta drives the V+A+E coupling while the typed Evidence delta adds on top of E.
    [Fact]
    public void LegacyAndTypedSignals_ComposeIndependently()
    {
        var c = Candidate("C1", "Acme prevails", composite: 0.60m, winner: true);
        var branches = new[] { Branch("C1.B1", "ACTIVE", false) };
        var signals = new[]
        {
            new DecisionBranchSignal(
                DecisionBranchSignalKinds.SupportChanged, BranchId: null, CandidateId: c.DecisionCandidateId,
                SupportDelta: 0.10, ReopenRequested: false, ReasonCode: "LEGACY", TargetSignal: null),
            new DecisionBranchSignal(
                DecisionBranchSignalKinds.SupportChanged, BranchId: null, CandidateId: c.DecisionCandidateId,
                SupportDelta: 0.20, ReopenRequested: false, ReasonCode: "EVIDENCE",
                TargetSignal: DecisionSignalTarget.Evidence)
        };

        var scored = DecisionRecompetition.Run(new[] { c }, branches, signals, Settings(), new HashSet<Guid>())
            .Candidates.Single();

        Assert.Equal(0.70m, scored.Verification, 6);       // 0.60 + 0.10 (legacy only)
        Assert.Equal(0.60m, scored.AuthoritySupport, 6);   // 0.50 + 0.10 (legacy only)
        Assert.Equal(0.75m, scored.EvidenceSupport, 6);    // 0.50 + 0.05 (legacy half) + 0.20 (typed)
    }
}
