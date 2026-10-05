using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Decision.Core;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Continuous Decision Integrity — Reevaluation Planner (Phase 2, step 8: targeted reevaluation).
//
// Phase 1 (MatterChangeProcessor) records WHICH candidates a validated source change affects, as
// Legal_DecisionImpact rows. This planner is the deterministic bridge from those recorded impacts to
// the ONE authoritative scorer: it translates candidate-affecting impacts into domain-neutral
// DecisionBranchSignals and runs DecisionRecompetition. The planner never scores — DecisionRecompetition
// (POLOXI Core) still owns the composite / entropy / margin / winner math.
//
// Pure and side-effect free (no DB): the caller (a worker/service) loads the impacts + current
// candidate/branch state, calls Plan(...), then persists the resulting Result as a new snapshot. This
// is the piece the end-to-end trace test previously stubbed with hand-built signals.
//
// Direction is deterministic. For legacy MatterChangeProcessor impacts (which share one event-level
// classification) a MATERIAL_CONTRADICTION weakens the affected candidates and any other matched
// material/potential change strengthens them. For LPI retrieval impacts, the owning-candidate NET
// direction is resolved upstream through the dependency/defeating-edge lineage and encoded per-impact
// (CurrentStateCode = Strengthened | Weakened), so ONE source change can strengthen one candidate while
// weakening another. Magnitude is severity-scaled. History is never mutated; only a fresh recompeted
// state is returned.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public static class DecisionReevaluationPlanner
{
    // Severity-scaled support magnitudes applied to the dependency-backed candidate dimensions.
    private const double MaterialMagnitude = 0.30;
    private const double PotentialMagnitude = 0.12;

    public sealed record Plan(
        IReadOnlyList<DecisionBranchSignal> Signals,
        DecisionRecompetition.Result Result)
    {
        public bool ReevaluationOccurred => Signals.Count > 0;
    }

    // Translate recorded impacts into signals and recompete. reopenAllowedBranchIds bounds which
    // branches may be reopened (loop-safety), matching DecisionRecompetition's contract.
    public static Plan Run(
        string classificationCode,
        IReadOnlyCollection<DecisionImpactDto> impacts,
        IReadOnlyList<DecisionCandidatePersistence> candidates,
        IReadOnlyList<DecisionBranchPersistence> branches,
        DecisionCoreSettings settings,
        ISet<Guid> reopenAllowedBranchIds)
    {
        ArgumentNullException.ThrowIfNull(impacts);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(branches);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(reopenAllowedBranchIds);

        var signals = BuildSignals(classificationCode, impacts, candidates);
        var result = DecisionRecompetition.Run(candidates, branches, signals, settings, reopenAllowedBranchIds);
        return new Plan(signals, result);
    }

    // Deterministic mapping: one signed candidate signal per distinct affected candidate impact.
    public static IReadOnlyList<DecisionBranchSignal> BuildSignals(
        string classificationCode,
        IReadOnlyCollection<DecisionImpactDto> impacts,
        IReadOnlyList<DecisionCandidatePersistence> candidates)
    {
        if (impacts.Count == 0 || candidates.Count == 0)
            return [];

        // Default (fallback) direction from the event-level classification: a material contradiction
        // weakens the affected candidates; any other matched material/potential change strengthens them.
        // This preserves the original MatterChangeProcessor path, where every candidate impact of one
        // event shares the event's single classification sign.
        var classificationIsContradiction = string.Equals(
            classificationCode, MatterChangeClassification.MaterialContradiction, StringComparison.OrdinalIgnoreCase);

        var byCode = candidates
            .GroupBy(c => c.CandidateCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DecisionCandidateId, StringComparer.OrdinalIgnoreCase);

        var signals = new List<DecisionBranchSignal>();
        var seen = new HashSet<Guid>();

        foreach (var impact in impacts)
        {
            if (!string.Equals(impact.AffectedKindCode, DecisionImpactKind.Candidate, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!byCode.TryGetValue(impact.AffectedKey, out var candidateId) || !seen.Add(candidateId))
                continue;

            // PER-CANDIDATE polarity: a single source change can STRENGTHEN one candidate while
            // WEAKENING another (supporting a defense condition weakens the opposing outcome). The
            // owning-candidate direction is resolved upstream (dependency/defeating-edge lineage) and
            // encoded on the impact's CurrentStateCode. Fall back to the event classification sign when
            // the impact carries no explicit direction (legacy MatterChangeProcessor impacts).
            //
            // A QUALIFIES placement carries the neutral RequiresEvaluation state: it conditions/narrows
            // the outcome and must NOT be given a fabricated sign. It still triggers reevaluation (so the
            // change is recorded and the candidate's branch is reopened for attorney evaluation), but with
            // a ZERO support delta so the ranking is never biased by an unevaluated qualifier.
            if (IsRequiresEvaluation(impact.CurrentStateCode))
            {
                signals.Add(new DecisionBranchSignal(
                    DecisionBranchSignalKinds.SupportChanged,
                    BranchId: null,
                    CandidateId: candidateId,
                    SupportDelta: 0.0,
                    ReopenRequested: true,
                    ReasonCode: "IMPACT_CANDIDATE_REQUIRES_EVALUATION"));
                continue;
            }

            var directionSign = ResolveDirectionSign(impact.CurrentStateCode, classificationIsContradiction);

            var isMaterial = string.Equals(
                impact.ImpactSeverityCode, DecisionImpactSeverity.Material, StringComparison.OrdinalIgnoreCase);
            var magnitude = isMaterial ? MaterialMagnitude : PotentialMagnitude;

            signals.Add(new DecisionBranchSignal(
                DecisionBranchSignalKinds.SupportChanged,
                BranchId: null,
                CandidateId: candidateId,
                SupportDelta: directionSign * magnitude,
                ReopenRequested: isMaterial,
                ReasonCode: directionSign < 0 ? "IMPACT_CANDIDATE_WEAKENED" : "IMPACT_CANDIDATE_STRENGTHENED"));
        }

        return signals;
    }

    // Per-impact direction tokens written by the lineage resolver. These are NOT fixed relationship
    // signs: they are the already-resolved NET effect on a specific candidate after accounting for the
    // qualitative relationship AND the dependency path (e.g. a DEFEATING/defense condition inverts it).
    private const string Weakened = "Weakened";
    private const string Strengthened = "Strengthened";

    // Neutral state written for a QUALIFIES placement: the qualifier conditions/narrows the outcome and
    // has no direction, so it produces a zero-delta reopen signal (evaluation required) rather than a
    // fabricated strengthen/weaken sign.
    private const string RequiresEvaluation = "RequiresEvaluation";

    private static bool IsRequiresEvaluation(string? currentStateCode)
        => string.Equals(currentStateCode, RequiresEvaluation, StringComparison.OrdinalIgnoreCase);

    private static double ResolveDirectionSign(string? currentStateCode, bool classificationIsContradiction)
    {
        if (string.Equals(currentStateCode, Weakened, StringComparison.OrdinalIgnoreCase))
            return -1.0;
        if (string.Equals(currentStateCode, Strengthened, StringComparison.OrdinalIgnoreCase))
            return 1.0;
        return classificationIsContradiction ? -1.0 : 1.0;
    }
}
