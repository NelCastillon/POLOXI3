using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-1 Claim Authority Gate invariant tests (§10–§13, §35).
//
// Pins the core boundary invariant and the deterministic authority policy. These are pure gate tests
// (no repository, no LLM): given a claim's verification state + relevance, the gate decides what the
// claim is PERMITTED to contribute to the decision.
//
// Coverage maps to the mandatory acceptance tests 1–8 from the architecture spec §35:
//   1. Fabricated authority          → no positive candidate support.
//   2. Real authority, bad holding   → (unverified proposition) no positive support.
//   3. Correct holding, wrong apply  → (unverified application) no positive support.
//   4. Unsupported material fact      → zero positive support.
//   5. Unsupported immaterial fact    → zero positive support, no unnecessary readiness block.
//   6. Contradicted claim             → negative signal + may influence competition.
//   7. Conflicting credible evidence  → DISPUTED carries uncertainty, no positive support.
//   8. No evidence either way         → UNVERIFIED, never treated as CONTRADICTED.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class ClaimAuthorityGateTests
{
    private static readonly ClaimAuthorityContext Context = new()
    {
        MaterialityThreshold = 0.5m,
        FullAuthorityStrengthThreshold = 0.75m,
    };

    private static ClaimProposition Claim(
        ClaimVerificationState state,
        decimal materiality = 0.9m,
        decimal verificationStrength = 0m,
        bool essential = false,
        ClaimType type = ClaimType.Factual,
        decimal contradictingStrength = 0m) => new()
    {
        ClaimId = Guid.NewGuid(),
        SessionId = Guid.NewGuid(),
        Text = "claim",
        NormalizedText = "claim",
        ClaimType = type,
        Origin = ClaimOrigin.LlmGenerated,
        VerificationState = state,
        VerificationStrength = verificationStrength,
        Materiality = materiality,
        DecisionImpact = 0.6m,
        Discrimination = 0.5m,
        Uncertainty = 0.5m,
        IsEssential = essential,
        ContradictingEvidence = contradictingStrength <= 0m
            ? []
            : [BuildContradiction(contradictingStrength)],
    };

    private static ClaimSupportRef BuildContradiction(decimal strength) => new()
    {
        SupportId = Guid.NewGuid(),
        ClaimId = Guid.NewGuid(),
        Relationship = ClaimSupportRelationship.Contradicts,
        Strength = strength,
    };

    // ── Test 1: Fabricated authority → no positive candidate support. ──
    // A claim the model asserts but nothing has verified sits in an unverified state; the gate must
    // grant NO positive contribution and NO decision authority, regardless of model confidence.
    [Theory]
    [InlineData(ClaimVerificationState.Proposed)]
    [InlineData(ClaimVerificationState.VerificationRequired)]
    [InlineData(ClaimVerificationState.VerificationInProgress)]
    [InlineData(ClaimVerificationState.Unverified)]
    public void UnverifiedClaim_GrantsNoPositiveAuthority(ClaimVerificationState state)
    {
        var decision = new ClaimAuthorityGate().Evaluate(Claim(state), Context);

        Assert.Equal(ClaimDecisionAuthority.None, decision.DecisionAuthority);
        Assert.Equal(0m, decision.AllowedPositiveContribution);
        Assert.True(decision.RequiresFurtherVerification);
    }

    // ── Test 2 & 3: real authority but an unverified holding / unverified application. ──
    // In the claim decomposition, "authority exists" (C1) may be Supported while "authority contains the
    // holding" (C2) or "holding supports this candidate" (C3) remain Unverified. The unverified links
    // must yield zero positive support so the candidate cannot inherit authority through a broken chain.
    [Fact]
    public void UnverifiedHoldingOrApplicationLink_ContributesNothingPositive()
    {
        var holdingLink = new ClaimAuthorityGate().Evaluate(
            Claim(ClaimVerificationState.Unverified, type: ClaimType.SourceContent), Context);
        var applicationLink = new ClaimAuthorityGate().Evaluate(
            Claim(ClaimVerificationState.Unverified, type: ClaimType.Inferential), Context);

        Assert.Equal(0m, holdingLink.AllowedPositiveContribution);
        Assert.Equal(0m, applicationLink.AllowedPositiveContribution);
        Assert.Equal(ClaimDecisionAuthority.None, holdingLink.DecisionAuthority);
        Assert.Equal(ClaimDecisionAuthority.None, applicationLink.DecisionAuthority);
    }

    // ── Test 4: unsupported material fact → zero positive support, and it blocks readiness when essential. ──
    [Fact]
    public void UnsupportedMaterialEssentialFact_ZeroSupport_BlocksReadiness()
    {
        var decision = new ClaimAuthorityGate().Evaluate(
            Claim(ClaimVerificationState.Unverified, materiality: 0.95m, essential: true), Context);

        Assert.Equal(0m, decision.AllowedPositiveContribution);
        Assert.True(decision.BlocksDecisionReadiness);
    }

    // ── Test 5: unsupported IMMATERIAL fact → zero positive support but no unnecessary readiness block. ──
    [Fact]
    public void UnsupportedImmaterialFact_ZeroSupport_DoesNotBlockReadiness()
    {
        var decision = new ClaimAuthorityGate().Evaluate(
            Claim(ClaimVerificationState.Unverified, materiality: 0.1m, essential: false), Context);

        Assert.Equal(0m, decision.AllowedPositiveContribution);
        Assert.False(decision.BlocksDecisionReadiness);
    }

    // ── Test 6: contradicted claim → negative signal, may influence competition, no positive support. ──
    [Fact]
    public void ContradictedClaim_ProducesNegativeSignal_NoPositiveSupport()
    {
        var decision = new ClaimAuthorityGate().Evaluate(
            Claim(ClaimVerificationState.Contradicted, essential: true, contradictingStrength: 0.8m), Context);

        Assert.Equal(0m, decision.AllowedPositiveContribution);
        Assert.True(decision.AllowedNegativeContribution > 0m);
        Assert.True(decision.MayInfluenceCompetition);
        Assert.True(decision.BlocksDecisionReadiness);
    }

    // ── Test 7: conflicting credible evidence → DISPUTED carries uncertainty, no positive support. ──
    [Fact]
    public void DisputedClaim_NoPositiveSupport_RequiresVerification()
    {
        var decision = new ClaimAuthorityGate().Evaluate(
            Claim(ClaimVerificationState.Disputed, essential: true, contradictingStrength: 0.6m), Context);

        Assert.Equal(ClaimDecisionAuthority.Limited, decision.DecisionAuthority);
        Assert.Equal(0m, decision.AllowedPositiveContribution);
        Assert.True(decision.RequiresFurtherVerification);
    }

    // ── Test 8: absence of evidence is UNVERIFIED, never CONTRADICTED (Unverified != Contradicted). ──
    // An unverified claim must NOT emit a negative signal; only an actually contradicted claim does.
    [Fact]
    public void AbsenceOfEvidence_IsUnverified_NotContradicted()
    {
        var unverified = new ClaimAuthorityGate().Evaluate(Claim(ClaimVerificationState.Unverified), Context);

        Assert.Equal(0m, unverified.AllowedPositiveContribution);
        Assert.Equal(0m, unverified.AllowedNegativeContribution);
    }

    // ── Positive-path control: only a Supported, strong, material claim earns FULL authority. ──
    [Fact]
    public void SupportedStrongMaterialClaim_EarnsFullAuthority()
    {
        var decision = new ClaimAuthorityGate().Evaluate(
            Claim(ClaimVerificationState.Supported, materiality: 0.9m, verificationStrength: 0.85m), Context);

        Assert.Equal(ClaimDecisionAuthority.Full, decision.DecisionAuthority);
        Assert.True(decision.AllowedPositiveContribution > 0m);
        Assert.False(decision.BlocksDecisionReadiness);
    }

    // ── Supported but weak/immaterial → only LIMITED authority (reduced positive contribution). ──
    [Fact]
    public void SupportedWeakClaim_EarnsOnlyLimitedAuthority()
    {
        var decision = new ClaimAuthorityGate().Evaluate(
            Claim(ClaimVerificationState.Supported, materiality: 0.9m, verificationStrength: 0.4m), Context);

        Assert.Equal(ClaimDecisionAuthority.Limited, decision.DecisionAuthority);
        Assert.True(decision.AllowedPositiveContribution > 0m);
        Assert.True(decision.AllowedPositiveContribution < 0.4m);
    }

    // ── Core boundary invariant, stated directly: Authority=None ⇒ PositiveContribution=0. ──
    [Theory]
    [InlineData(ClaimVerificationState.Proposed)]
    [InlineData(ClaimVerificationState.VerificationRequired)]
    [InlineData(ClaimVerificationState.VerificationInProgress)]
    [InlineData(ClaimVerificationState.Unverified)]
    [InlineData(ClaimVerificationState.Unverifiable)]
    public void CoreInvariant_NoAuthority_ImpliesNoPositiveContribution(ClaimVerificationState state)
    {
        var decision = new ClaimAuthorityGate().Evaluate(Claim(state), Context);

        if (decision.DecisionAuthority == ClaimDecisionAuthority.None)
            Assert.Equal(0m, decision.AllowedPositiveContribution);
    }
}
