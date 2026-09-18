namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-2 decision-directed verification (§16–§17, §28).
//
// Claim verification is added as ANOTHER action type in POLOXI's existing Information-Value machinery
// rather than a competing framework. POLOXI can compare "investigate another branch" vs "verify this
// critical claim" on the same VIV scale, and always verify the highest-value claim first.
//
// Research-exhaustion invariant: never declare exhausted while a high-value executable verification
// action remains (VIV > MinimumVerificationIV).
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// A candidate verification action for one claim, scored by its expected decision impact.
public sealed record ClaimVerificationAction(
    Guid ClaimId,
    decimal InformationValue,
    bool Executable,
    string Reason);

public interface IClaimVerificationPrioritizer
{
    // Verification Information Value for a single claim (0..1).
    decimal ComputeInformationValue(ClaimProposition claim);

    // Ordered, executable verification actions worth running this round (highest VIV first).
    IReadOnlyList<ClaimVerificationAction> Prioritize(
        IReadOnlyList<ClaimProposition> claims,
        EpistemicAuthoritySettings settings);

    // Research-exhaustion guard: true only when NO executable verification action clears the bar.
    bool IsVerificationExhausted(
        IReadOnlyList<ClaimProposition> claims,
        EpistemicAuthoritySettings settings);
}

public sealed class ClaimVerificationPrioritizer : IClaimVerificationPrioritizer
{
    // States for which running verification can still change the epistemic/decision picture.
    private static bool NeedsVerification(ClaimVerificationState state) => state is
        ClaimVerificationState.Proposed or
        ClaimVerificationState.VerificationRequired or
        ClaimVerificationState.VerificationInProgress or
        ClaimVerificationState.Unverified or
        ClaimVerificationState.Disputed;

    public decimal ComputeInformationValue(ClaimProposition claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (!NeedsVerification(claim.VerificationState))
            return 0m;

        // VIV = f(Materiality, Uncertainty, DecisionImpact, Discrimination). Essential claims get a
        // bounded boost so an unresolved essential claim rises to the top of the queue.
        var baseValue =
            (claim.Materiality * 0.35m) +
            (claim.Uncertainty * 0.20m) +
            (claim.DecisionImpact * 0.30m) +
            (claim.Discrimination * 0.15m);

        if (claim.IsEssential)
            baseValue = Math.Min(1m, baseValue + 0.15m);

        return Clamp(baseValue);
    }

    public IReadOnlyList<ClaimVerificationAction> Prioritize(
        IReadOnlyList<ClaimProposition> claims,
        EpistemicAuthoritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(settings);

        return claims
            .Select(c => new ClaimVerificationAction(
                c.ClaimId,
                ComputeInformationValue(c),
                Executable: NeedsVerification(c.VerificationState),
                Reason: c.IsEssential
                    ? "Essential unresolved claim — highest priority to resolve."
                    : "Unresolved claim with decision-relevant information value."))
            .Where(a => a.Executable && a.InformationValue >= settings.MinimumVerificationIV)
            .OrderByDescending(a => a.InformationValue)
            .ThenBy(a => a.ClaimId)
            .Take(settings.MaxVerificationActionsPerRound)
            .ToArray();
    }

    public bool IsVerificationExhausted(
        IReadOnlyList<ClaimProposition> claims,
        EpistemicAuthoritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(settings);

        return !claims.Any(c =>
            NeedsVerification(c.VerificationState) &&
            ComputeInformationValue(c) >= settings.MinimumVerificationIV);
    }

    private static decimal Clamp(decimal value) => value < 0m ? 0m : value > 1m ? 1m : value;
}
