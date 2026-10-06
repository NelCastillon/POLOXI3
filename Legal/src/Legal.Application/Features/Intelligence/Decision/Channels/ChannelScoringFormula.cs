namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// ChannelScoringFormula — the SINGLE SOURCE OF TRUTH for the qualitative-to-signed δ math.
//
// Both the live scoring path (LegalChannelSignalAdapter, which feeds POLOXI recompetition) and the
// read-only Channel Scoring LPI trace (ChannelScoringLpiService) call into THIS class, so what the user
// SEES is provably the exact same math POLOXI CONSUMED. There is no second algorithm and no drift.
//
// These are NOT scores: they are the fixed, bounded δ a qualitative relation is permitted to contribute
// within POLOXI's [-1,1] signal range. POLOXI Core alone decides what that δ means for candidate ranking.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public static class ChannelScoringFormula
{
    public const double SupportMagnitude = 0.30;
    public const double QualifiedSupportMagnitude = 0.15;
    public const double ContradictionMagnitude = -0.30;

    // The qualitative effect a contribution is permitted to have on POLOXI, gated by verification.
    public enum ContributionEffect
    {
        None = 0,
        Support = 1,
        Contradict = 2,
        Reopen = 3,
    }

    public static ContributionEffect ClassifyEffect(DecisionContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);

        // A challenge reopens verification regardless of numeric support (Human Intelligence pattern).
        if (contribution.Relation == ContributionRelation.Challenges)
            return ContributionEffect.Reopen;

        // Contradiction/invalidation is meaningful evidence even against a verified opposing fact.
        if (contribution.Relation is ContributionRelation.Contradicts or ContributionRelation.Invalidates)
            return ContributionEffect.Contradict;

        // Positive support only flows for VERIFIED establishing/supporting/qualifying contributions —
        // POLOXI never manufactures support from unverified model output.
        if (contribution.VerificationState == ContributionVerificationState.Verified
            && contribution.Relation is ContributionRelation.Supports
                or ContributionRelation.Establishes
                or ContributionRelation.Qualifies)
        {
            return ContributionEffect.Support;
        }

        // ContextOnly / Insufficient / unverified support → no signal.
        return ContributionEffect.None;
    }

    // Resolves the positive support δ for a supporting contribution. The base band is the fixed
    // [QualifiedSupportMagnitude, SupportMagnitude] a qualitative relation is permitted to contribute.
    // An optional placement Magnitude in [0,1] linearly selects where inside that band the δ lands.
    public static double SupportDelta(DecisionContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);

        var ceiling = contribution.Relation == ContributionRelation.Qualifies
            ? QualifiedSupportMagnitude
            : SupportMagnitude;

        if (contribution.Magnitude is not { } magnitude)
            return ceiling;

        var position = Math.Clamp(magnitude, 0.0, 1.0);
        return QualifiedSupportMagnitude + (ceiling - QualifiedSupportMagnitude) * position;
    }

    // The δ a classified effect contributes. Reopen carries no direct δ.
    public static double DeltaFor(DecisionContribution contribution, ContributionEffect effect) => effect switch
    {
        ContributionEffect.Support => SupportDelta(contribution),
        ContributionEffect.Contradict => ContradictionMagnitude,
        _ => 0.0,
    };
}
