namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-7 override mode (§34, §35).
//
// Governs HOW strongly the epistemic layer's readiness verdict may affect the effective decision.
// The override is always downgrade-only (EA can withhold readiness, never manufacture it) — consistent
// with the layer invariant LLMGenerated ≠ POLOXIAuthorized. No mode ever mutates or deletes the
// authoritative V2 verdict or the underlying claims; those remain visible for reference in every mode.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public enum EpistemicOverrideMode
{
    // Annotate only. The effective verdict always equals the V2 verdict; EA never changes it (default).
    Advisory,

    // EA may downgrade a V2 "ready" verdict to NOT-ready when it finds an essential unresolved claim,
    // but never upgrades. The V2 verdict is preserved as reference.
    SoftGate,

    // The EA readiness verdict becomes the effective verdict (still downgrade-only). V2 kept as reference.
    HardGate,
}

public static class EpistemicOverrideModes
{
    public const string Advisory = "Advisory";
    public const string SoftGate = "SoftGate";
    public const string HardGate = "HardGate";

    public static string ToCode(EpistemicOverrideMode mode) => mode switch
    {
        EpistemicOverrideMode.SoftGate => SoftGate,
        EpistemicOverrideMode.HardGate => HardGate,
        _ => Advisory,
    };

    // Tolerant parse; unknown/empty falls back to the safest mode (Advisory).
    public static EpistemicOverrideMode Parse(string? code) => (code ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "softgate" => EpistemicOverrideMode.SoftGate,
        "hardgate" => EpistemicOverrideMode.HardGate,
        _ => EpistemicOverrideMode.Advisory,
    };
}
