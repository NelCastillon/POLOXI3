namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-1 Claim Authority Gate (§10–§13).
//
// Given a claim + context, decides what that claim is PERMITTED to influence. This is deterministic
// (not prompt-based). Core invariants enforced here:
//
//   Authority(c)=NONE            ⇒ PositiveDecisionContribution(c)=0
//   VerificationState ∈ {Proposed, VerificationRequired, VerificationInProgress, Unverified}
//                                ⇒ PositiveDecisionContribution(c)=0
//   Contradiction is NOT erased  — it may still produce a NEGATIVE impact signal.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// Context supplied by POLOXI when evaluating a claim (policy thresholds live in settings).
public sealed record ClaimAuthorityContext
{
    public decimal MaterialityThreshold { get; init; }

    // Minimum verified support strength required before a claim may be granted Full authority.
    public decimal FullAuthorityStrengthThreshold { get; init; } = 0.75m;
}

public sealed record ClaimAuthorityDecision
{
    public required Guid ClaimId { get; init; }

    public required ClaimVerificationState VerificationState { get; init; }

    public required ClaimDecisionAuthority DecisionAuthority { get; init; }

    public decimal AllowedPositiveContribution { get; init; }

    public decimal AllowedNegativeContribution { get; init; }

    public bool MayInfluenceCompetition { get; init; }

    public bool BlocksDecisionReadiness { get; init; }

    public bool RequiresFurtherVerification { get; init; }

    public string? Reason { get; init; }
}

public interface IClaimAuthorityGate
{
    ClaimAuthorityDecision Evaluate(ClaimProposition claim, ClaimAuthorityContext context);
}

public sealed class ClaimAuthorityGate : IClaimAuthorityGate
{
    public ClaimAuthorityDecision Evaluate(ClaimProposition claim, ClaimAuthorityContext context)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(context);

        return claim.VerificationState switch
        {
            // No positive support may flow from an unverified / in-flight / proposed claim.
            ClaimVerificationState.Proposed or
            ClaimVerificationState.VerificationRequired or
            ClaimVerificationState.VerificationInProgress or
            ClaimVerificationState.Unverified =>
                Unauthorized(claim, context, requiresFurtherVerification: true,
                    reason: "Unsupported/unverified claim cannot positively support any candidate."),

            // Contradiction carries no positive support but IS meaningful negative evidence.
            ClaimVerificationState.Contradicted =>
                new ClaimAuthorityDecision
                {
                    ClaimId = claim.ClaimId,
                    VerificationState = claim.VerificationState,
                    DecisionAuthority = ClaimDecisionAuthority.Limited,
                    AllowedPositiveContribution = 0m,
                    AllowedNegativeContribution = NegativeSignal(claim),
                    MayInfluenceCompetition = true,
                    BlocksDecisionReadiness = claim.IsEssential,
                    RequiresFurtherVerification = false,
                    Reason = "Contradicted claim contributes negative impact, not positive support.",
                },

            // Genuinely two-sided: limited, frontier-eligible, blocks readiness if essential.
            ClaimVerificationState.Disputed =>
                new ClaimAuthorityDecision
                {
                    ClaimId = claim.ClaimId,
                    VerificationState = claim.VerificationState,
                    DecisionAuthority = ClaimDecisionAuthority.Limited,
                    AllowedPositiveContribution = 0m,
                    AllowedNegativeContribution = NegativeSignal(claim),
                    MayInfluenceCompetition = true,
                    BlocksDecisionReadiness = claim.IsEssential,
                    RequiresFurtherVerification = true,
                    Reason = "Disputed claim carries uncertainty; eligible for the decision frontier.",
                },

            // The only state that may grant positive decision authority.
            ClaimVerificationState.Supported => EvaluateSupported(claim, context),

            // Cannot be verified either way; no positive support, policy decides readiness elsewhere.
            ClaimVerificationState.Unverifiable =>
                Unauthorized(claim, context, requiresFurtherVerification: false,
                    reason: "Claim is unverifiable; it cannot positively support a candidate."),

            _ => Unauthorized(claim, context, requiresFurtherVerification: true,
                    reason: "Unknown verification state; treated as unauthorized."),
        };
    }

    private static ClaimAuthorityDecision EvaluateSupported(ClaimProposition claim, ClaimAuthorityContext context)
    {
        var material = claim.Materiality >= context.MaterialityThreshold;
        var strong = claim.VerificationStrength >= context.FullAuthorityStrengthThreshold;

        // Full authority requires strong verified support AND material relevance; otherwise Limited.
        var authority = strong && material ? ClaimDecisionAuthority.Full : ClaimDecisionAuthority.Limited;

        var positive = authority == ClaimDecisionAuthority.Full
            ? claim.VerificationStrength
            : claim.VerificationStrength * 0.5m;

        return new ClaimAuthorityDecision
        {
            ClaimId = claim.ClaimId,
            VerificationState = claim.VerificationState,
            DecisionAuthority = authority,
            AllowedPositiveContribution = Clamp(positive),
            AllowedNegativeContribution = 0m,
            MayInfluenceCompetition = true,
            BlocksDecisionReadiness = false,
            RequiresFurtherVerification = authority == ClaimDecisionAuthority.Limited && claim.IsEssential,
            Reason = authority == ClaimDecisionAuthority.Full
                ? "Supported, material claim with strong verified support: full decision authority."
                : "Supported claim with limited strength/materiality: limited decision authority.",
        };
    }

    private static ClaimAuthorityDecision Unauthorized(
        ClaimProposition claim,
        ClaimAuthorityContext context,
        bool requiresFurtherVerification,
        string reason) => new()
        {
            ClaimId = claim.ClaimId,
            VerificationState = claim.VerificationState,
            DecisionAuthority = ClaimDecisionAuthority.None,
            AllowedPositiveContribution = 0m,
            AllowedNegativeContribution = 0m,
            MayInfluenceCompetition = false,
            // An essential, decision-relevant but unresolved claim blocks readiness even at authority=None.
            BlocksDecisionReadiness = claim.IsEssential && claim.Materiality >= context.MaterialityThreshold,
            RequiresFurtherVerification = requiresFurtherVerification,
            Reason = reason,
        };

    // Negative impact scales with how decision-relevant the (contradicted/disputed) claim is.
    private static decimal NegativeSignal(ClaimProposition claim) =>
        Clamp(Math.Max(claim.ContradictingEvidenceStrength(), claim.DecisionImpact));

    private static decimal Clamp(decimal value) => value < 0m ? 0m : value > 1m ? 1m : value;
}

internal static class ClaimPropositionExtensions
{
    public static decimal ContradictingEvidenceStrength(this ClaimProposition claim) =>
        claim.ContradictingEvidence.Count == 0 ? 0m : claim.ContradictingEvidence.Max(e => e.Strength);
}
