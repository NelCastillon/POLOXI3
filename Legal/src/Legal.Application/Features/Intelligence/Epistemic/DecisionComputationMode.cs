namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — decision computation mode (§2, §12).
//
// POLOXI scores Candidate × Branch competition TWICE with the SAME math but DIFFERENT admissible
// inputs:
//
//   Provisional   — the first pass over LLM-proposed candidate/branch signals. These scores are
//                   NAVIGATION signals only: they tell POLOXI what currently looks important enough
//                   to investigate/verify. They are never authoritative conclusions.
//
//   Authoritative — the second pass, run AFTER epistemic verification + the authority gate. Only
//                   authorized signals (produced by IEpistemicDecisionSignalMapper) may contribute
//                   positive support. Unverified/unsupported claims contribute zero positive support
//                   here — the EA core invariant.
//
// The enum is a label carried through scoring/telemetry/persistence so the two passes are
// distinguishable; it does not create a second scoring engine.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public enum DecisionComputationMode
{
    // First-pass provisional scoring over LLM-proposed signals (navigation only).
    Provisional = 0,

    // Second-pass authoritative scoring over epistemically-authorized signals only.
    Authoritative = 1,
}

public static class DecisionComputationModes
{
    public const string ProvisionalCode = "PROVISIONAL";
    public const string AuthoritativeCode = "AUTHORITATIVE";

    public static string ToCode(DecisionComputationMode mode) => mode switch
    {
        DecisionComputationMode.Provisional => ProvisionalCode,
        DecisionComputationMode.Authoritative => AuthoritativeCode,
        _ => ProvisionalCode,
    };
}
