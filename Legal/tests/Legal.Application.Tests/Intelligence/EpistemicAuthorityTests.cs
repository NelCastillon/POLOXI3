using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-1/EA-2 invariant tests (§8, §11, §12, §39).
//
// Pins the central containment invariants:
//   • Unsupported/unverified claim ⇒ zero positive candidate contribution.
//   • Absence of support ≠ evidence of falsity (Unverified, never Contradicted).
//   • Contradiction still produces a negative signal; it is not silently erased.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class EpistemicAuthorityTests
{
    private static readonly EpistemicAuthoritySettings Settings = new();

    private static ClaimProposition Claim(
        ClaimVerificationState state,
        decimal verificationStrength = 0m,
        decimal materiality = 0.9m,
        bool essential = false,
        decimal decisionImpact = 0.5m,
        IReadOnlyList<ClaimSupportRef>? contradicting = null) => new()
        {
            ClaimId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Text = "claim",
            NormalizedText = "claim",
            ClaimType = ClaimType.Factual,
            Origin = ClaimOrigin.LlmGenerated,
            VerificationState = state,
            VerificationStrength = verificationStrength,
            Materiality = materiality,
            DecisionImpact = decisionImpact,
            IsEssential = essential,
            ContradictingEvidence = contradicting ?? [],
        };

    // ── Claim Authority Gate ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ClaimVerificationState.Proposed)]
    [InlineData(ClaimVerificationState.VerificationRequired)]
    [InlineData(ClaimVerificationState.VerificationInProgress)]
    [InlineData(ClaimVerificationState.Unverified)]
    public void UnverifiedClaim_CannotPositivelySupportCandidate(ClaimVerificationState state)
    {
        var gate = new ClaimAuthorityGate();

        var decision = gate.Evaluate(Claim(state, verificationStrength: 0.99m), Settings.ToAuthorityContext());

        Assert.Equal(ClaimDecisionAuthority.None, decision.DecisionAuthority);
        Assert.Equal(0m, decision.AllowedPositiveContribution);
        Assert.False(decision.MayInfluenceCompetition);
    }

    [Fact]
    public void FabricatedAuthority_UnverifiedSupport_YieldsZeroPositiveContribution()
    {
        // A confidently-asserted but unverified claim (e.g. a fabricated citation) must not help a candidate.
        var gate = new ClaimAuthorityGate();

        var decision = gate.Evaluate(
            Claim(ClaimVerificationState.Unverified, verificationStrength: 1m, essential: true),
            Settings.ToAuthorityContext());

        Assert.Equal(0m, decision.AllowedPositiveContribution);
        Assert.True(decision.RequiresFurtherVerification);
        Assert.True(decision.BlocksDecisionReadiness);
    }

    [Fact]
    public void SupportedMaterialStrongClaim_GetsFullAuthorityAndPositiveContribution()
    {
        var gate = new ClaimAuthorityGate();

        var decision = gate.Evaluate(
            Claim(ClaimVerificationState.Supported, verificationStrength: 0.9m, materiality: 0.9m),
            Settings.ToAuthorityContext());

        Assert.Equal(ClaimDecisionAuthority.Full, decision.DecisionAuthority);
        Assert.Equal(0.9m, decision.AllowedPositiveContribution);
        Assert.True(decision.MayInfluenceCompetition);
    }

    [Fact]
    public void ContradictedClaim_ProducesNegativeSignal_NotPositiveSupport()
    {
        var gate = new ClaimAuthorityGate();
        var contradicting = new[]
        {
            new ClaimSupportRef
            {
                SupportId = Guid.NewGuid(),
                ClaimId = Guid.NewGuid(),
                Relationship = ClaimSupportRelationship.Contradicts,
                Strength = 0.8m,
            },
        };

        var decision = gate.Evaluate(
            Claim(ClaimVerificationState.Contradicted, decisionImpact: 0.6m, contradicting: contradicting),
            Settings.ToAuthorityContext());

        Assert.Equal(0m, decision.AllowedPositiveContribution);
        Assert.True(decision.AllowedNegativeContribution > 0m);
        Assert.True(decision.MayInfluenceCompetition);
    }

    [Fact]
    public void DisputedEssentialClaim_IsFrontierEligible_AndBlocksReadiness()
    {
        var gate = new ClaimAuthorityGate();

        var decision = gate.Evaluate(
            Claim(ClaimVerificationState.Disputed, essential: true, decisionImpact: 0.8m),
            Settings.ToAuthorityContext());

        Assert.True(decision.MayInfluenceCompetition);
        Assert.True(decision.BlocksDecisionReadiness);
        Assert.Equal(0m, decision.AllowedPositiveContribution);
    }

    // ── State machine ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Classify_NoSupportAndNoContradiction_IsUnverified_NotContradicted()
    {
        var state = ClaimStateMachine.Classify(
            supportStrength: 0m,
            contradictionStrength: 0m,
            adequacyThreshold: Settings.VerificationAdequacyThreshold);

        Assert.Equal(ClaimVerificationState.Unverified, state);
        Assert.NotEqual(ClaimVerificationState.Contradicted, state);
    }

    [Fact]
    public void Classify_BothSides_YieldsDisputed()
    {
        var state = ClaimStateMachine.Classify(0.8m, 0.8m, Settings.VerificationAdequacyThreshold);
        Assert.Equal(ClaimVerificationState.Disputed, state);
    }

    [Fact]
    public void StateMachine_AllowsSupportedToContradictedRevision()
    {
        Assert.True(ClaimStateMachine.CanTransition(
            ClaimVerificationState.Supported, ClaimVerificationState.Contradicted));
    }

    [Fact]
    public void StateMachine_RejectsProposedDirectlyToSupported()
    {
        Assert.False(ClaimStateMachine.CanTransition(
            ClaimVerificationState.Proposed, ClaimVerificationState.Supported));
    }

    // ── Identity resolution ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void IdentityResolver_DetectsEquivalentRestatement()
    {
        var resolver = new ClaimIdentityResolver();
        var existing = new[]
        {
            Claim(ClaimVerificationState.Supported) with { NormalizedText = "agreement requires 30 day pre termination notice" },
        };
        var proposal = new ClaimProposal
        {
            ClaimKey = "p1",
            Text = "The agreement requires 30 day pre termination notice.",
        };

        var resolution = resolver.Resolve(proposal, existing);

        Assert.Equal(ClaimIdentityOutcome.Equivalent, resolution.Outcome);
        Assert.Equal(existing[0].ClaimId, resolution.MatchedClaimId);
    }

    [Fact]
    public void IdentityResolver_DetectsPolarityContradiction()
    {
        var resolver = new ClaimIdentityResolver();
        var existing = new[]
        {
            Claim(ClaimVerificationState.Supported) with { NormalizedText = "notice was required before termination of the agreement" },
        };
        var proposal = new ClaimProposal
        {
            ClaimKey = "p2",
            Text = "Notice was not required before termination of the agreement.",
        };

        var resolution = resolver.Resolve(proposal, existing);

        Assert.Equal(ClaimIdentityOutcome.Contradiction, resolution.Outcome);
    }

    [Fact]
    public void IdentityResolver_RegistersNewClaimWhenNoMatch()
    {
        var resolver = new ClaimIdentityResolver();
        var proposal = new ClaimProposal { ClaimKey = "p3", Text = "The insurer denied coverage in 2021." };

        var resolution = resolver.Resolve(proposal, []);

        Assert.Equal(ClaimIdentityOutcome.New, resolution.Outcome);
        Assert.Null(resolution.MatchedClaimId);
    }

    // ── Verification prioritization ────────────────────────────────────────────────────────────────

    [Fact]
    public void Prioritizer_RanksEssentialUnresolvedClaimHighest()
    {
        var prioritizer = new ClaimVerificationPrioritizer();
        var low = Claim(ClaimVerificationState.Unverified, materiality: 0.2m, decisionImpact: 0.1m);
        var essential = Claim(ClaimVerificationState.Unverified, materiality: 0.9m, essential: true, decisionImpact: 0.9m);

        var actions = prioritizer.Prioritize([low, essential], Settings);

        Assert.Equal(essential.ClaimId, actions[0].ClaimId);
    }

    [Fact]
    public void Prioritizer_SupportedClaimHasZeroVerificationValue()
    {
        var prioritizer = new ClaimVerificationPrioritizer();
        var supported = Claim(ClaimVerificationState.Supported, verificationStrength: 0.9m);

        Assert.Equal(0m, prioritizer.ComputeInformationValue(supported));
    }

    [Fact]
    public void Prioritizer_NotExhausted_WhenHighValueVerificationRemains()
    {
        var prioritizer = new ClaimVerificationPrioritizer();
        var essential = Claim(ClaimVerificationState.Unverified, materiality: 0.95m, essential: true, decisionImpact: 0.91m);

        Assert.False(prioritizer.IsVerificationExhausted([essential], Settings));
    }

    [Fact]
    public void Prioritizer_Exhausted_WhenOnlyResolvedClaimsRemain()
    {
        var prioritizer = new ClaimVerificationPrioritizer();
        var supported = Claim(ClaimVerificationState.Supported, verificationStrength: 0.9m);

        Assert.True(prioritizer.IsVerificationExhausted([supported], Settings));
    }
}
