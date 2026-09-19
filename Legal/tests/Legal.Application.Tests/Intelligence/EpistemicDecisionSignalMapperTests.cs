using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA→Decision signal mapper tests (§12, §13).
//
// Locks the Stage-12 integration boundary: governed claim authority is projected into domain-neutral
// DecisionBranchSignal deltas WITHOUT the mapper ever manufacturing authority. Positive support flows
// only for authorized claims; contradiction flows as a negative delta; unverified/unsupported claims
// and lineage-less claims produce no signal at all.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class EpistemicDecisionSignalMapperTests
{
    private static readonly EpistemicDecisionSignalMapper Mapper = new();

    private static ClaimProposition Claim(
        Guid? branchId = null,
        Guid? candidateId = null) => new()
    {
        ClaimId = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Text = "claim",
        NormalizedText = "claim",
        ClaimType = ClaimType.Factual,
        Origin = ClaimOrigin.LlmGenerated,
        VerificationState = ClaimVerificationState.Supported,
        Materiality = 0.9m,
        SourceBranchId = branchId,
        SourceCandidateId = candidateId,
    };

    private static ClaimAuthorityDecision Authority(
        Guid claimId,
        decimal positive = 0m,
        decimal negative = 0m,
        ClaimDecisionAuthority authority = ClaimDecisionAuthority.Full,
        ClaimVerificationState state = ClaimVerificationState.Supported) => new()
    {
        ClaimId = claimId,
        VerificationState = state,
        DecisionAuthority = authority,
        AllowedPositiveContribution = positive,
        AllowedNegativeContribution = negative,
        MayInfluenceCompetition = positive > 0m || negative > 0m,
    };

    // Authorized claim with branch lineage → single positive support signal on that branch.
    [Fact]
    public void AuthorizedClaim_WithBranchLineage_EmitsPositiveBranchSignal()
    {
        var branchId = Guid.NewGuid();
        var claim = Claim(branchId: branchId);
        var governed = new GovernedClaim(claim, Authority(claim.ClaimId, positive: 0.6m));

        var signals = Mapper.Map([governed]);

        var signal = Assert.Single(signals);
        Assert.Equal(branchId, signal.BranchId);
        Assert.Null(signal.CandidateId);
        Assert.True(signal.SupportDelta > 0);
        Assert.Equal(DecisionBranchSignalKinds.SupportChanged, signal.SignalKind);
        Assert.False(signal.ReopenRequested);
    }

    // CORE INVARIANT: an unauthorized (zero positive, zero negative) claim emits NO signal — the
    // mapper never manufactures authority from an unverified claim.
    [Fact]
    public void UnauthorizedClaim_EmitsNoSignal()
    {
        var claim = Claim(branchId: Guid.NewGuid());
        var governed = new GovernedClaim(
            claim,
            Authority(claim.ClaimId, positive: 0m, negative: 0m,
                authority: ClaimDecisionAuthority.None,
                state: ClaimVerificationState.Unverified));

        var signals = Mapper.Map([governed]);

        Assert.Empty(signals);
    }

    // Contradicted claim → NEGATIVE support delta (contradiction is meaningful, not erased).
    [Fact]
    public void ContradictedClaim_EmitsNegativeSignal()
    {
        var branchId = Guid.NewGuid();
        var claim = Claim(branchId: branchId);
        var governed = new GovernedClaim(
            claim,
            Authority(claim.ClaimId, positive: 0m, negative: 0.5m,
                authority: ClaimDecisionAuthority.Limited,
                state: ClaimVerificationState.Contradicted));

        var signals = Mapper.Map([governed]);

        var signal = Assert.Single(signals);
        Assert.Equal(branchId, signal.BranchId);
        Assert.True(signal.SupportDelta < 0);
        Assert.Equal(DecisionBranchSignalKinds.ConstraintChanged, signal.ReasonCode);
    }

    // A claim with no branch/candidate lineage cannot move the ranking → no signal.
    [Fact]
    public void AuthorizedClaim_WithNoLineage_EmitsNoSignal()
    {
        var claim = Claim();
        var governed = new GovernedClaim(claim, Authority(claim.ClaimId, positive: 0.8m));

        var signals = Mapper.Map([governed]);

        Assert.Empty(signals);
    }

    // Direct candidate lineage (no branch) → candidate-keyed signal so recompetition folds it in.
    [Fact]
    public void AuthorizedClaim_WithCandidateLineageOnly_EmitsCandidateSignal()
    {
        var candidateId = Guid.NewGuid();
        var claim = Claim(candidateId: candidateId);
        var governed = new GovernedClaim(claim, Authority(claim.ClaimId, positive: 0.7m));

        var signals = Mapper.Map([governed]);

        var signal = Assert.Single(signals);
        Assert.Null(signal.BranchId);
        Assert.Equal(candidateId, signal.CandidateId);
        Assert.True(signal.SupportDelta > 0);
    }

    // Branch lineage takes precedence: a claim with BOTH branch and candidate lineage emits only the
    // branch signal (recompetition folds the branch into its owning candidate — no double-count).
    [Fact]
    public void ClaimWithBranchAndCandidate_EmitsOnlyBranchSignal()
    {
        var branchId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var claim = Claim(branchId: branchId, candidateId: candidateId);
        var governed = new GovernedClaim(claim, Authority(claim.ClaimId, positive: 0.6m));

        var signals = Mapper.Map([governed]);

        var signal = Assert.Single(signals);
        Assert.Equal(branchId, signal.BranchId);
        Assert.Null(signal.CandidateId);
    }

    // Determinism: mapping the same governed claims twice yields the same signal set.
    [Fact]
    public void Map_IsDeterministic()
    {
        var branchId = Guid.NewGuid();
        var claim = Claim(branchId: branchId);
        var governed = new GovernedClaim(claim, Authority(claim.ClaimId, positive: 0.55m));

        var first = Mapper.Map([governed]);
        var second = Mapper.Map([governed]);

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first[0].BranchId, second[0].BranchId);
        Assert.Equal(first[0].SupportDelta, second[0].SupportDelta);
        Assert.Equal(first[0].ReasonCode, second[0].ReasonCode);
    }
}
