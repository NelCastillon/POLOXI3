using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// POLOXI Legal B3 (Hallucination Solver) acceptance: verified decision-support signals must project
// into domain-neutral branch deltas and feed the authoritative Candidate x Branch recompetition
// engine — exactly the seam wired in LegalDecisionService when the DB-backed toggle is ON. The core
// invariant is enforced here: unverified/required material signals grant ZERO positive authority
// (B2 parity), while a contradicted essential signal moves the ranking on verified evidence only.
public sealed class DecisionB3SolverInvariantTests
{
    private static DecisionCoreSettings Settings() => new(
        0.20, 0.25, 0.25, 0.15, 0.10, 0.05,
        0.35, 0.25, 0.15, 0.10, 0.30, 0.40,
        4, 24, 8);

    private static DecisionCandidatePersistence Candidate(string code, string name, decimal composite, decimal verification, bool winner)
        => new(
            Guid.NewGuid(), code, name, name,
            LegalSupport: 0.60m, FactSupport: 0.60m, EvidenceSupport: 0.55m, AuthoritySupport: 0.55m,
            Verification: verification, Uncertainty: 0.30m, Discrimination: 0.40m, RankingImpact: 0.40m,
            Diversity: 0.30m, RedundancyPenalty: 0.05m, CompositeScore: composite, DecisionSupportCeiling: 0.90m,
            RankOrder: winner ? 1 : 2, IsWinner: winner, IsEliminated: false);

    private static DecisionBranchPersistence Branch(string code, string state, bool onFrontier)
        => new(
            Guid.NewGuid(), null, 1, code, code, null, state,
            InformationValue: 0.40m, DecisionRelevance: 0.50m, FlipPotential: 0.35m, EvidenceAvailability: 0.40m,
            AdvScore: 0.30m, Cost: 0.10m, IsOnFrontier: onFrontier, StopReason: null, SortOrder: 0);

    private static DecisionSupportSignal Signal(
        Guid branchId, DecisionSupportVerificationState state, decimal impact, decimal strength = 0m, bool requiresVerification = true)
        => new()
        {
            DecisionSessionId = Guid.NewGuid(),
            Statement = "The controlling authority applies to this matter.",
            NormalizedStatement = "controlling authority applies",
            Origin = DecisionSupportOrigin.LlmGenerated,
            VerificationState = state,
            RequiresVerification = requiresVerification,
            VerificationStrength = strength,
            DecisionImpact = impact,
            SourceBranchId = branchId,
        };

    [Fact]
    public void ContradictedEssentialSignal_Projects_AndFlipsWinner()
    {
        // Arrange: C1 leads C2 narrowly; C1 owns branch B1 whose essential material claim is contradicted.
        var winner = Candidate("C1", "Acme prevails", composite: 0.62m, verification: 0.80m, winner: true);
        var challenger = Candidate("C2", "Globex prevails", composite: 0.58m, verification: 0.60m, winner: false);
        var candidates = new[] { winner, challenger };

        var c1Branch = Branch("C1.B1", "ACTIVE", onFrontier: true);
        var c2Branch = Branch("C2.B1", "ACTIVE", onFrontier: true);
        var branches = new[] { c1Branch, c2Branch };

        // A contradicted, high-impact material signal on C1's essential branch.
        var signals = new[]
        {
            Signal(c1Branch.DecisionBranchId, DecisionSupportVerificationState.Contradicted, impact: 0.90m),
        };

        // Act: project (the seam's verifiedSignalService.Project) then recompete (DecisionRecompetition.Run).
        var projected = new VerifiedDecisionSignalService().Project(signals);
        Assert.NotEmpty(projected);
        Assert.All(projected, d => Assert.True(d.SupportDelta < 0));

        var reopenAllowed = new HashSet<Guid>(branches.Where(b => b.IsOnFrontier).Select(b => b.DecisionBranchId));
        var result = DecisionRecompetition.Run(candidates, branches, projected, Settings(), reopenAllowed);

        // Assert: the contradicted essential claim overturned C1.
        Assert.True(result.WinnerChanged);
        Assert.Equal(winner.DecisionCandidateId, result.PreviousWinnerId);
        Assert.Equal(challenger.DecisionCandidateId, result.CurrentWinnerId);
    }

    [Fact]
    public void UnverifiedRequiredSignal_GrantsNoAuthority_PreservingBaseline()
    {
        // A material (RequiresVerification) but Unverified signal must contribute ZERO positive support:
        // the model cannot manufacture authority. Ranking stays identical to the B2 baseline.
        var winner = Candidate("C1", "Acme prevails", composite: 0.62m, verification: 0.80m, winner: true);
        var challenger = Candidate("C2", "Globex prevails", composite: 0.58m, verification: 0.60m, winner: false);
        var candidates = new[] { winner, challenger };
        var c1Branch = Branch("C1.B1", "ACTIVE", onFrontier: true);
        var branches = new[] { c1Branch, Branch("C2.B1", "ACTIVE", true) };

        var signals = new[]
        {
            Signal(c1Branch.DecisionBranchId, DecisionSupportVerificationState.Unverified, impact: 0.90m, strength: 0.90m),
        };

        // The projection zeroes the unverified-required signal, so recompetition sees no deltas.
        var projected = new VerifiedDecisionSignalService().Project(signals);
        Assert.Empty(projected);

        var result = DecisionRecompetition.Run(candidates, branches, projected, Settings(), new HashSet<Guid>());

        Assert.False(result.WinnerChanged);
        Assert.Equal(winner.DecisionCandidateId, result.CurrentWinnerId);
        Assert.Equal(0, result.ReopenedBranchCount);
    }
}
