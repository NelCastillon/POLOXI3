namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────
// 0448 Post-L2 evidence-aware routing. HIGH IMPACT: decides which participating L2 targets receive an L3
// atomic-proposition pass THIS run. It never changes ScopeStatusCode (relevance) - it computes four
// independent attributes on top of it:
//   * Evidence availability : PRESENT (classified document) | REFERENCED (classified passage only) | ABSENT.
//                              Keyword/lexical passage hits, LLM score, blended score and DocumentPresent
//                              fuzzy flags NEVER establish availability.
//   * Verification status   : VERIFIED only when a VERIFIED source assertion is anchored to evidence of this
//                              type; UNVERIFIED when evidence exists but none is verified; UNKNOWN when absent.
//   * Execution priority    : IMMEDIATE / NORMAL run L3 now; DEFERRED = relevant, evidence ABSENT, relevance
//                              below the immediate threshold -> acquisition queue (no model call this run);
//                              DORMANT = scope status does not participate in L3.
//   * Reopen                : a DEFERRED target is re-routed automatically on the next run once a classified
//                              document or passage arrives (availability changes), so deferral is never final.
// Concepts, required coverage and PRIORITIZE targets are never deferred. None of these attributes is LPI
// support: only verified source assertions aligned to propositions contribute (DocumentEvidenceChannel).
// Pure and unit-testable.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────
public static class MatterEvidenceRouting
{
    public sealed record Inputs(
        string TargetKindCode,
        string ScopeStatusCode,
        bool ParticipatesInL3,
        bool DirectDocument,
        int DirectPassageCount,
        int VerifiedAssertionCount,
        bool IsRequiredCoverage,
        bool IsPrioritized,
        decimal? LlmScore,
        decimal? BlendedScore);

    public sealed record Thresholds(decimal ImmediateRelevance, bool EvidenceAwareRoutingEnabled);

    public sealed record Route(string AvailabilityCode, string VerificationCode, string PriorityCode, string Reason)
    {
        public bool RunsL3 => PriorityCode is MatterEvidenceExecutionPriorityCodes.Immediate or MatterEvidenceExecutionPriorityCodes.Normal;
        public bool IsDeferred => PriorityCode == MatterEvidenceExecutionPriorityCodes.Deferred;
    }

    public static Route Decide(Inputs input, Thresholds thresholds)
    {
        var isConcept = string.Equals(input.TargetKindCode, "CONCEPT", StringComparison.OrdinalIgnoreCase);

        var availability = isConcept ? MatterEvidenceAvailabilityCodes.NotApplicable
            : input.DirectDocument ? MatterEvidenceAvailabilityCodes.Present
            : input.DirectPassageCount > 0 ? MatterEvidenceAvailabilityCodes.Referenced
            : MatterEvidenceAvailabilityCodes.Absent;

        var verification = input.VerifiedAssertionCount > 0 ? MatterEvidenceVerificationCodes.Verified
            : availability is MatterEvidenceAvailabilityCodes.Present or MatterEvidenceAvailabilityCodes.Referenced ? MatterEvidenceVerificationCodes.Unverified
            : MatterEvidenceVerificationCodes.Unknown;

        if (!input.ParticipatesInL3)
            return new(availability, verification, MatterEvidenceExecutionPriorityCodes.Dormant, $"Scope status {input.ScopeStatusCode} does not participate in L3");

        if (isConcept)
            return new(availability, verification, MatterEvidenceExecutionPriorityCodes.Immediate, "Legal concept: analytical relevance is not gated on document availability");
        if (input.IsRequiredCoverage)
            return new(availability, verification, MatterEvidenceExecutionPriorityCodes.Immediate, "Required coverage (mandatory decision gate)");
        if (input.IsPrioritized)
            return new(availability, verification, MatterEvidenceExecutionPriorityCodes.Immediate, "PRIORITIZE relevance rule fired on a specific matter signal");
        if (availability is MatterEvidenceAvailabilityCodes.Present or MatterEvidenceAvailabilityCodes.Referenced)
            return new(availability, verification, MatterEvidenceExecutionPriorityCodes.Immediate, $"Evidence {availability.ToLowerInvariant()} ({verification.ToLowerInvariant()}): enters L3 extraction");

        // ABSENT evidence: relevance decides immediate acquisition-oriented L3 vs deferral.
        var relevance = input.LlmScore ?? input.BlendedScore;
        if (relevance is { } r && r >= thresholds.ImmediateRelevance)
            return new(availability, verification, MatterEvidenceExecutionPriorityCodes.Immediate, $"Evidence absent but relevance {r:0.00} >= {thresholds.ImmediateRelevance:0.00}: acquisition-critical, enters L3 now");

        if (!thresholds.EvidenceAwareRoutingEnabled)
            return new(availability, verification, MatterEvidenceExecutionPriorityCodes.Normal, $"Evidence absent, relevance {relevance?.ToString("0.00") ?? "n/a"} below immediate threshold; evidence-aware routing disabled so L3 runs");

        return new(availability, verification, MatterEvidenceExecutionPriorityCodes.Deferred, $"Evidence absent and relevance {relevance?.ToString("0.00") ?? "n/a"} < {thresholds.ImmediateRelevance:0.00}: routed to acquisition; no L3 call this run; reopens when a classified document or passage arrives");
    }
}
