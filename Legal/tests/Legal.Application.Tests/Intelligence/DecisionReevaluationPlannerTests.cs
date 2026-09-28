using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Continuous Decision Integrity Phase 2 acceptance: the recorded Phase-1 impacts must drive the ONE
// authoritative scorer. This closes the gap the document-to-decision trace test previously bridged
// with hand-built signals: here the signals are DERIVED from Legal_DecisionImpact rows, and POLOXI
// Core (DecisionRecompetition) still owns the verdict. Deterministic and idempotent.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionReevaluationPlannerTests
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
            Diversity: 0.30m, RedundancyPenalty: 0.05m, CompositeScore: composite, DecisionSupportCeiling: 0.95m,
            RankOrder: winner ? 1 : 2, IsWinner: winner, IsEliminated: false);

    private static DecisionBranchPersistence Branch(string code)
        => new(
            Guid.NewGuid(), null, 1, code, code, null, "ACTIVE",
            InformationValue: 0.40m, DecisionRelevance: 0.50m, FlipPotential: 0.35m, EvidenceAvailability: 0.40m,
            AdvScore: 0.30m, Cost: 0.10m, IsOnFrontier: true, StopReason: null, SortOrder: 0);

    private static DecisionImpactDto CandidateImpact(string candidateCode, string severity)
        => new(
            Guid.NewGuid(), MatterChangeEventId: Guid.NewGuid(),
            DecisionImpactKind.Candidate, candidateCode, candidateCode,
            PreviousStateCode: "Previously evaluated", CurrentStateCode: "Reevaluation required",
            severity, Rationale: "depends on affected proposition");

    [Fact]
    public void MaterialContradictionImpact_WeakensAffectedCandidate_ViaRecompetition()
    {
        // C1 leads narrowly; a new source materially contradicts a proposition C1 relied on.
        var c1 = Candidate("C1", "Acme prevails", composite: 0.62m, verification: 0.80m, winner: true);
        var c2 = Candidate("C2", "Globex prevails", composite: 0.58m, verification: 0.60m, winner: false);
        var candidates = new[] { c1, c2 };
        var branches = new[] { Branch("C1.B1"), Branch("C2.B1") };

        var impacts = new[] { CandidateImpact("C1", DecisionImpactSeverity.Material) };
        var reopenAllowed = new HashSet<Guid>(branches.Select(b => b.DecisionBranchId));

        var plan = DecisionReevaluationPlanner.Run(
            MatterChangeClassification.MaterialContradiction, impacts, candidates, branches, Settings(), reopenAllowed);

        // The impact produced exactly one signed signal targeting the affected candidate, negative.
        Assert.True(plan.ReevaluationOccurred);
        var signal = Assert.Single(plan.Signals);
        Assert.Equal(c1.DecisionCandidateId, signal.CandidateId);
        Assert.True(signal.SupportDelta < 0);

        // POLOXI Core flipped the winner away from the weakened candidate.
        Assert.True(plan.Result.WinnerChanged);
        Assert.Equal(c1.DecisionCandidateId, plan.Result.PreviousWinnerId);
        Assert.Equal(c2.DecisionCandidateId, plan.Result.CurrentWinnerId);

        // Idempotent: re-running with the recompeted state + same impacts does not flip again.
        var second = DecisionReevaluationPlanner.Run(
            MatterChangeClassification.MaterialContradiction, impacts,
            plan.Result.Candidates, plan.Result.Branches, Settings(), reopenAllowed);
        Assert.Equal(plan.Result.CurrentWinnerId, second.Result.CurrentWinnerId);
    }

    [Fact]
    public void SupportingImpact_StrengthensAffectedCandidate_WithPositiveDelta()
    {
        var trailing = Candidate("C1", "Liability established", composite: 0.50m, verification: 0.55m, winner: false);
        var leading = Candidate("C2", "Liability not established", composite: 0.54m, verification: 0.62m, winner: true);
        var candidates = new[] { trailing, leading };
        var branches = new[] { Branch("C1.B1"), Branch("C2.B1") };

        var impacts = new[] { CandidateImpact("C1", DecisionImpactSeverity.Material) };
        var reopenAllowed = new HashSet<Guid>(branches.Select(b => b.DecisionBranchId));

        var plan = DecisionReevaluationPlanner.Run(
            MatterChangeClassification.PotentialImpact, impacts, candidates, branches, Settings(), reopenAllowed);

        var signal = Assert.Single(plan.Signals);
        Assert.Equal(trailing.DecisionCandidateId, signal.CandidateId);
        Assert.True(signal.SupportDelta > 0);

        var trailingAfter = plan.Result.Candidates.Single(c => c.DecisionCandidateId == trailing.DecisionCandidateId);
        Assert.True(trailingAfter.CompositeScore > trailing.CompositeScore);
    }

    [Fact]
    public void NoCandidateImpacts_ProducesNoSignals_AndLeavesRankingUnchanged()
    {
        var c1 = Candidate("C1", "Acme prevails", composite: 0.62m, verification: 0.80m, winner: true);
        var c2 = Candidate("C2", "Globex prevails", composite: 0.58m, verification: 0.60m, winner: false);
        var candidates = new[] { c1, c2 };
        var branches = new[] { Branch("C1.B1"), Branch("C2.B1") };

        // Only a proposition-kind impact (no candidate dependents) → nothing to recompete.
        var impacts = new[]
        {
            new DecisionImpactDto(Guid.NewGuid(), Guid.NewGuid(), DecisionImpactKind.Proposition,
                "P1", "P1", "Supported", "Review pending", DecisionImpactSeverity.Potential, "matched")
        };

        var plan = DecisionReevaluationPlanner.Run(
            MatterChangeClassification.PotentialImpact, impacts, candidates, branches, Settings(), new HashSet<Guid>());

        Assert.False(plan.ReevaluationOccurred);
        Assert.False(plan.Result.WinnerChanged);
        Assert.Equal(c1.DecisionCandidateId, plan.Result.CurrentWinnerId);
    }
}
