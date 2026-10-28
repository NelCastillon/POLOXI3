namespace Legal.Application.Features.Intelligence.Decision;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// Deterministic expected-information-value score for one Wide L2 parent (replaces the former
// WideL2PriorityReview LLM round-trip). HIGHER means decompose-first. This is a PROCESSING-ORDER
// score only: it never includes or excludes a parent, and it spends no model call.
//
//   + Relevance            matter-funnel relevance (0..1): how much the parent matters to the matter.
//   + EvidenceGap          remaining uncertainty: a relevant parent with FEW mapped passages / no
//                          document present carries more unresolved information than a saturated one.
//   + Discrimination       decisiveness: required-coverage parents and those with retrieval evidence
//                          that can separate competing candidates are pulled forward.
//   + EvidenceAvailability cheap yield: some retrieval evidence already present means a decomposition
//                          call is likely to return supported children rather than empty gaps.
//   + Novelty              DB prior confidence as a mild novelty/importance signal.
//   - Redundancy           penalty for parents already saturated with mapped passages (little new info).
//
// Weights are DB-backed (Intelligence.SearchWide.Criterion*). The result is bounded to [0,1].
// Pure and unit-testable.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public static class WideInfoValueScorer
{
    // Nominal mapped-passage count at which a parent is treated as fully saturated for coverage.
    public const decimal SaturationPassages = 8m;
    // Evidence count treated as full "availability".
    public const int EvidenceAvailabilityCeiling = 20;

    public static decimal Score(
        decimal relevance,
        int mappedPassageCount,
        bool documentPresent,
        bool requiredCoverage,
        int evidenceCount,
        decimal priorConfidence,
        WideConfiguration configuration)
    {
        relevance = Math.Clamp(relevance, 0m, 1m);
        var coverage = Math.Clamp(Math.Max(mappedPassageCount, 0) / SaturationPassages, 0m, 1m);
        var evidenceGap = 1m - coverage;
        // Decisiveness: required coverage is the strongest deterministic proxy; a parent that already has
        // retrieval evidence but is NOT yet saturated is the pivotal, worth-resolving-first case.
        var discrimination = (requiredCoverage ? 0.6m : 0m) + (evidenceCount > 0 ? 0.4m * evidenceGap : 0m);
        var evidenceAvailability = Math.Clamp(Math.Min(Math.Max(evidenceCount, 0), EvidenceAvailabilityCeiling) / (decimal)EvidenceAvailabilityCeiling, 0m, 1m);
        var novelty = Math.Clamp(priorConfidence, 0m, 1m);
        // A document already present with full coverage is redundant to re-decompose first.
        var redundancy = documentPresent ? coverage : coverage * 0.5m;

        var score =
              configuration.CriterionRankingImpactWeight * relevance
            + configuration.CriterionUncertaintyWeight * evidenceGap
            + configuration.CriterionDiscriminationWeight * discrimination
            + configuration.CriterionEvidenceAvailabilityWeight * evidenceAvailability
            + configuration.CriterionNoveltyWeight * novelty
            - configuration.CriterionRedundancyPenalty * redundancy;

        return Math.Clamp(score, 0m, 1m);
    }
}
