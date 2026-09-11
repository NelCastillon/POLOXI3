namespace Legal.Application.Features.Intelligence.Science;

// ── Multidimensional epistemic state (P0 #2) ──────────────────────────────────────────────────────
// A single "confidence" number is misleading: "StronglySuggested + Discovery 1.0 + Unverified" reads as
// certainty when it is not. POLOXI separates FOUR orthogonal dimensions so research priority can never be
// confused with mathematical verification:
//
//   R = ResearchPriority        — how much this direction deserves compute next (0..1)
//   E = EvidenceSupport         — how much informal/self-consistency evidence backs it (0..1)
//   V = MathematicalVerification— deterministic proof strength (0..1); ONLY this authorizes truth
//   F = FalsificationCoverage   — how thoroughly refutation/counterexample search was attempted (0..1)
//
// A result of R=0.91 E=0.52 V=0.08 F=0.61 is far more honest than "StronglySuggested — Confidence 1".
public readonly record struct EpistemicState
{
    public double ResearchPriority { get; init; }

    public double EvidenceSupport { get; init; }

    public double MathematicalVerification { get; init; }

    public double FalsificationCoverage { get; init; }

    public static EpistemicState Empty => new();

    public EpistemicState(double researchPriority, double evidenceSupport, double mathematicalVerification, double falsificationCoverage)
    {
        ResearchPriority = Clamp(researchPriority);
        EvidenceSupport = Clamp(evidenceSupport);
        MathematicalVerification = Clamp(mathematicalVerification);
        FalsificationCoverage = Clamp(falsificationCoverage);
    }

    // A coarse categorical label derived ONLY from the verification dimension, so the UI can show a state
    // like VIABLE/UNVERIFIED without conflating it with research priority.
    public string StateLabel =>
        MathematicalVerification >= 0.999 ? "VERIFIED"
        : MathematicalVerification <= double.Epsilon && FalsificationCoverage > 0d ? "VIABLE / UNVERIFIED"
        : MathematicalVerification <= double.Epsilon ? "UNVERIFIED"
        : "PARTIALLY VERIFIED";

    private static double Clamp(double value) => value < 0d ? 0d : value > 1d ? 1d : value;
}
