using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Verified Decision Signals — projection service tests (frozen slice-1 design).
//
// Locks the core invariant: a material signal that requires verification and is not Supported
// contributes ZERO positive support; Contradicted contributes a negative delta; lineage-less signals
// produce nothing; and the projection is deterministic.
// ─────────────────────────────────────────────────────────────────────────────────────────────
public sealed class VerifiedDecisionSignalServiceTests
{
    private static readonly VerifiedDecisionSignalService Service = new();

    private static DecisionSupportSignal Signal(
        DecisionSupportVerificationState state = DecisionSupportVerificationState.Supported,
        bool requiresVerification = true,
        decimal impact = 0.8m,
        decimal strength = 1.0m,
        Guid? branchId = null,
        Guid? candidateId = null) => new()
        {
            DecisionSessionId = Guid.NewGuid(),
            Statement = "statement",
            NormalizedStatement = "statement",
            Origin = DecisionSupportOrigin.LlmGenerated,
            VerificationState = state,
            RequiresVerification = requiresVerification,
            VerificationStrength = strength,
            DecisionImpact = impact,
            SourceBranchId = branchId,
            SourceCandidateId = candidateId,
        };

    // 1. Supported + verified material signal with branch lineage → positive delta.
    [Fact]
    public void Supported_material_signal_emits_positive_branch_delta()
    {
        var branchId = Guid.NewGuid();
        var result = Service.Project([Signal(branchId: branchId)]);

        var signal = Assert.Single(result);
        Assert.Equal(branchId, signal.BranchId);
        Assert.Null(signal.CandidateId);
        Assert.True(signal.SupportDelta > 0);
        Assert.Equal(DecisionBranchSignalKinds.SupportChanged, signal.ReasonCode);
    }

    // 2. Unverified material signal (verification required) → NO signal (core invariant).
    [Fact]
    public void Unverified_material_signal_emits_no_signal()
    {
        var result = Service.Project(
        [
            Signal(state: DecisionSupportVerificationState.Unverified, branchId: Guid.NewGuid())
        ]);

        Assert.Empty(result);
    }

    // 3. Disputed material signal (verification required) → NO positive signal.
    [Fact]
    public void Disputed_material_signal_emits_no_signal()
    {
        var result = Service.Project(
        [
            Signal(state: DecisionSupportVerificationState.Disputed, branchId: Guid.NewGuid())
        ]);

        Assert.Empty(result);
    }

    // 4. Contradicted signal → NEGATIVE delta (contradiction is meaningful evidence).
    [Fact]
    public void Contradicted_signal_emits_negative_delta()
    {
        var candidateId = Guid.NewGuid();
        var result = Service.Project(
        [
            Signal(state: DecisionSupportVerificationState.Contradicted, candidateId: candidateId)
        ]);

        var signal = Assert.Single(result);
        Assert.Equal(candidateId, signal.CandidateId);
        Assert.True(signal.SupportDelta < 0);
        Assert.Equal(DecisionBranchSignalKinds.ConstraintChanged, signal.ReasonCode);
    }

    // 5. Signal with no branch/candidate lineage → NO signal.
    [Fact]
    public void Signal_without_lineage_emits_no_signal()
    {
        var result = Service.Project([Signal(branchId: null, candidateId: null)]);

        Assert.Empty(result);
    }

    // 6. Unverified but NOT material (verification not required) → positive delta at full strength.
    [Fact]
    public void Unverified_immaterial_signal_still_contributes()
    {
        var branchId = Guid.NewGuid();
        var result = Service.Project(
        [
            Signal(
                state: DecisionSupportVerificationState.Unverified,
                requiresVerification: false,
                strength: 0m,
                branchId: branchId)
        ]);

        var signal = Assert.Single(result);
        Assert.True(signal.SupportDelta > 0);
    }

    // 7. Zero-impact signal → NO signal even when supported.
    [Fact]
    public void Zero_impact_signal_emits_no_signal()
    {
        var result = Service.Project([Signal(impact: 0m, branchId: Guid.NewGuid())]);

        Assert.Empty(result);
    }

    // Signal with both branch and candidate lineage emits one delta each.
    [Fact]
    public void Signal_with_both_lineages_emits_branch_and_candidate_deltas()
    {
        var result = Service.Project(
        [
            Signal(branchId: Guid.NewGuid(), candidateId: Guid.NewGuid())
        ]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.BranchId is not null && s.CandidateId is null);
        Assert.Contains(result, s => s.CandidateId is not null && s.BranchId is null);
    }

    // Determinism: identical inputs yield identical, order-stable output.
    [Fact]
    public void Projection_is_deterministic()
    {
        var input = new[]
        {
            Signal(branchId: Guid.NewGuid()),
            Signal(state: DecisionSupportVerificationState.Contradicted, candidateId: Guid.NewGuid()),
        };

        var first = Service.Project(input);
        var second = Service.Project(input);

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].BranchId, second[i].BranchId);
            Assert.Equal(first[i].CandidateId, second[i].CandidateId);
            Assert.Equal(first[i].SupportDelta, second[i].SupportDelta);
            Assert.Equal(first[i].ReasonCode, second[i].ReasonCode);
        }
    }
}
