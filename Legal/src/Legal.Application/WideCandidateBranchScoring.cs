namespace Legal.Application;

// ── Candidate × Branch Cs: evidence-gated shrinkage ─────────────────────────────────────────────────
// A candidate's per-branch score (Cs) must combine what the model proposed with what the evidence
// actually confirms, WITHOUT collapsing to zero when a run admitted no evidence. We treat the model's
// per-cell proposal as a PRIOR (Lcs) and shrink it toward the branch's measured evidence support (Es)
// only in proportion to how much verified evidence actually landed on that branch:
//
//     Cs = (1 − α·g)·Lcs + α·g·Es        g = min(1, n / N*)
//
//   Lcs = LLM-proposed (metric-blended) candidate×branch score — the prior.
//   Es  = branch evidence support (0 when nothing was admitted).
//   n   = admitted-evidence count on the branch (verified sources).
//   N*  = saturation count: admitted sources that fully open the evidence gate.
//   α   = maximum authority verified evidence may exert over the prior.
//
// Properties: with NO admitted evidence g = 0 → Cs = Lcs (honest LLM-only prior, never a misleading 0).
// With strong admitted evidence the prior is pulled toward Es and can rise ABOVE or fall BELOW it,
// scaled by evidence volume. This mirrors the branch-level blend (PriorWeight·prior + EvidenceWeight·
// support) but gates on per-branch evidence volume. Pure and deterministic: no new legal reasoning.
public static class WideCandidateBranchScoring
{
    // α — maximum authority verified evidence may exert over the LLM prior (matches EvidenceWeight intent).
    public const decimal EvidenceAuthority = 0.6m;

    // N* — admitted-evidence count on a branch that fully opens the evidence gate.
    public const decimal EvidenceSaturation = 2m;

    // Evidence gate g = min(1, n / N*).
    public static decimal EvidenceGate(int admittedCount)
        => admittedCount <= 0 ? 0m : Math.Min(1m, admittedCount / EvidenceSaturation);

    // Cs = (1 − α·g)·Lcs + α·g·Es, clamped to 0..1.
    public static decimal GatedCs(decimal llmPrior, decimal evidenceSupport, int admittedCount)
    {
        var lcs = Math.Clamp(llmPrior, 0m, 1m);
        var es = Math.Clamp(evidenceSupport, 0m, 1m);
        var pull = EvidenceAuthority * EvidenceGate(admittedCount);
        return Math.Clamp((1m - pull) * lcs + pull * es, 0m, 1m);
    }
}
