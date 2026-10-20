namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Per-matter Evidence Scope admission (migration 0445). HIGH IMPACT: the only Domain Pack L2 -> L3 gate.
//
// Inclusion-first with evidence integrity:
//   * DocumentPresent means a DIRECT classification of an uploaded document; it is independent of passage
//     mapping and of lexical name/keyword matching.
//   * Mapped passages admit only above a floor (MinimumMappedPassages) or when a single passage is
//     decisive (its mapping score >= DecisivePassageScore).
//   * Score-only admission requires the LLM review to have actually scored the target when review is
//     enabled; a target the review never scored is REVIEW_FAILED (dormant, never auto-admitted) unless a
//     deterministic positive signal admits it.
//   * Targets with no positive signal fall into PENDING_REVIEW (dormant) when their blended score sits in
//     [PendingReviewFloor, OutThreshold) and OUT_OF_SCOPE below that.
// Pure and unit-testable. USER decisions are applied by the repository, never here.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public static class MatterEvidenceScopeDecider
{
    public sealed record Inputs(
        bool DirectDocument,
        bool IsRequiredCoverage,
        bool IsPrioritized,
        int MappedPassageCount,
        decimal MaxPassageScore,
        bool MatterTypeMatch,
        decimal? BlendedScore,
        decimal? LlmScore,
        bool ReviewEnabled,
        bool ReviewUnresolved,
        // 0447: passages whose classified EvidenceTypeCode IS this target (method EVIDENCE_TYPE). Keyword/lexical
        // passage hits are NOT direct and can never override an explicit LLM inapplicability finding.
        int DirectPassageCount = 0);

    public sealed record Thresholds(
        decimal OutThreshold,
        decimal PendingReviewFloor,
        int MinimumMappedPassages,
        decimal DecisivePassageScore,
        // 0448: LLM score <= this is a clear inapplicability finding. Only concrete reopening triggers (PRIORITIZE,
        // matter-type match, required coverage, direct document / direct passage) keep the target PENDING_REVIEW.
        // Keyword/lexical passage hits and the blended score do not.
        decimal ZeroScoreCeiling = 0.05m);

    public sealed record Decision(string StatusCode, string Reason);

    public static Decision Decide(Inputs input, Thresholds thresholds)
    {
        if (input.DirectDocument) return new(MatterEvidenceScopeStatusCodes.InScope, "Uploaded document directly classified as this target");
        if (input.IsRequiredCoverage) return new(MatterEvidenceScopeStatusCodes.InScope, "Required coverage (mandatory decision gate)");
        if (input.DirectPassageCount > 0)
            return new(MatterEvidenceScopeStatusCodes.InScope, $"{input.DirectPassageCount} document passage(s) classified as this evidence type");

        // 0447 arbitration: a valid LLM score below the out-threshold is an explicit inapplicability finding. Only
        // the exceptions above (direct document, mandatory coverage, directly classified passages) admit against
        // it. Keyword/lexical passage hits, PRIORITIZE, matter-type match and blended score are ORDERING signals
        // and leave the target visible and attorney-decidable as PENDING_REVIEW with an auditable reason.
        var llmRejected = input.ReviewEnabled && input.LlmScore is { } l && l < thresholds.OutThreshold;
        var competingSignal = input.IsPrioritized || input.MatterTypeMatch || input.MappedPassageCount > 0 || (input.BlendedScore is { } bs && bs >= thresholds.OutThreshold);
        if (llmRejected && competingSignal)
        {
            // 0448 zero-score cleanup: at/below the ceiling the model found the target clearly inapplicable; only a
            // concrete reopening trigger (PRIORITIZE / matter-type; required + direct handled above) keeps it pending.
            var zeroScore = input.LlmScore is { } z && z <= thresholds.ZeroScoreCeiling;
            var reopenTrigger = input.IsPrioritized || input.MatterTypeMatch;
            if (zeroScore && !reopenTrigger)
                return new(MatterEvidenceScopeStatusCodes.OutOfScope, $"LLM relevance {input.LlmScore:0.00} <= {thresholds.ZeroScoreCeiling:0.00} (clearly inapplicable); {(input.MappedPassageCount > 0 ? $"{input.MappedPassageCount} keyword/lexical passage hit(s) and " : string.Empty)}blended score are not reopening triggers");

            var hint = input.IsPrioritized ? "PRIORITIZE rule fired but orders only; "
                : input.MappedPassageCount > 0 ? $"{input.MappedPassageCount} keyword/lexical passage hit(s) order only; "
                : input.MatterTypeMatch ? "matter-type match orders only; " : string.Empty;
            return new(MatterEvidenceScopeStatusCodes.PendingReview, $"LLM relevance {input.LlmScore:0.00} < {thresholds.OutThreshold:0.00} ({hint}no document, classified-passage or mandatory-coverage exception)");
        }

        if (input.MappedPassageCount >= Math.Max(1, thresholds.MinimumMappedPassages))
            return new(MatterEvidenceScopeStatusCodes.InScope, $"{input.MappedPassageCount} distinct document passages mapped (floor {thresholds.MinimumMappedPassages})");
        if (input.MappedPassageCount > 0 && input.MaxPassageScore >= thresholds.DecisivePassageScore)
            return new(MatterEvidenceScopeStatusCodes.InScope, $"Single decisive passage (score {input.MaxPassageScore:0.00} >= {thresholds.DecisivePassageScore:0.00})");

        if (input.IsPrioritized && (!input.ReviewEnabled || input.LlmScore is not null))
            return new(MatterEvidenceScopeStatusCodes.InScope, "PRIORITIZE relevance rule fired on a specific matter signal and LLM review did not reject it");
        if (input.MatterTypeMatch && (!input.ReviewEnabled || input.LlmScore is not null))
            return new(MatterEvidenceScopeStatusCodes.InScope, "Matter type match and LLM review did not reject it");

        var scoreAdmits = input.BlendedScore is { } s && s >= thresholds.OutThreshold && (!input.ReviewEnabled || input.LlmScore is not null);
        if (scoreAdmits) return new(MatterEvidenceScopeStatusCodes.InScope, $"Blended relevance {input.BlendedScore:0.00} >= {thresholds.OutThreshold:0.00} with LLM review");

        if (input.ReviewEnabled && input.ReviewUnresolved && input.LlmScore is null)
            return new(MatterEvidenceScopeStatusCodes.ReviewFailed, "L2 relevance review did not return a valid score after bounded retries; held back (never auto-admitted)");

        if (input.IsPrioritized || input.MatterTypeMatch)
            return new(MatterEvidenceScopeStatusCodes.PendingReview, "Deterministic signal present but target was not scored by the LLM review; attorney review");
        if (input.ReviewEnabled && input.LlmScore is { } zl && zl <= thresholds.ZeroScoreCeiling)
            return new(MatterEvidenceScopeStatusCodes.OutOfScope, $"LLM relevance {zl:0.00} <= {thresholds.ZeroScoreCeiling:0.00} (clearly inapplicable) and no reopening trigger");
        if (input.MappedPassageCount > 0)
            return new(MatterEvidenceScopeStatusCodes.PendingReview, $"Only {input.MappedPassageCount} weak passage(s) mapped (max score {input.MaxPassageScore:0.00}); attorney review");
        if (input.BlendedScore is { } b && b >= thresholds.PendingReviewFloor)
            return new(MatterEvidenceScopeStatusCodes.PendingReview, $"Blended relevance {b:0.00} in review band [{thresholds.PendingReviewFloor:0.00}, {thresholds.OutThreshold:0.00})");
        return new(MatterEvidenceScopeStatusCodes.OutOfScope, "No positive matter signal");
    }
}
