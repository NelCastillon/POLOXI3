namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — configuration (§34).
//
// Independent feature flags so each release (EA-1..EA-5) can be enabled/disabled in isolation, plus
// tunable thresholds. Defaults stay conservative; experimental thresholds are not hard-coded until
// benchmarks (EA-6) justify them.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class EpistemicAuthoritySettings
{
    public bool UseClaimAuthorityGate { get; init; } = true;

    public bool UseMaterialClaimVerification { get; init; } = true;

    public bool UseClaimDependencyPropagation { get; init; } = true;

    public bool UseClaimReadinessBlocking { get; init; } = true;

    public bool UseOutputClaimAudit { get; init; } = true;

    // EA-6: master switch for the advisory overlay that projects the live V2 decision graph into
    // authoritative EA claims and runs readiness/output governance. Never blocks the V1/V2 decision.
    public bool UseEpistemicDecisionBridge { get; init; } = true;

    // EA-7: how strongly the epistemic readiness verdict may affect the effective decision. Advisory
    // (default) only annotates and never changes the authoritative V2 verdict. SoftGate/HardGate are
    // downgrade-only and always preserve the V2 verdict + all claims for reference.
    public EpistemicOverrideMode OverrideMode { get; init; } = EpistemicOverrideMode.Advisory;

    public int MaxVerificationActionsPerRound { get; init; } = 5;

    public int MaxOutputRepairAttempts { get; init; } = 1;

    // Minimum information value before a verification action is worth executing.
    public decimal MinimumVerificationIV { get; init; } = 0.15m;

    // Materiality at/above which a claim is decision-relevant enough to gate authority/readiness.
    public decimal MaterialityThreshold { get; init; } = 0.5m;

    // Verified-support strength required for Full (vs Limited) decision authority.
    public decimal FullAuthorityStrengthThreshold { get; init; } = 0.75m;

    // Support/contradiction strength at/above which the state machine treats a side as "adequate".
    public decimal VerificationAdequacyThreshold { get; init; } = 0.5m;

    public ClaimAuthorityContext ToAuthorityContext() => new()
    {
        MaterialityThreshold = MaterialityThreshold,
        FullAuthorityStrengthThreshold = FullAuthorityStrengthThreshold,
    };
}
