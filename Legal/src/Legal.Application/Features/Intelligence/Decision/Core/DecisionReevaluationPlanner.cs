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
// Direction is deterministic from the change classification: a MATERIAL_CONTRADICTION weakens the
// affected candidates; a matched supporting change (POTENTIAL_IMPACT / NEW_MATERIAL_FACT) strengthens
// them. Magnitude is severity-scaled. History is never mutated; only a fresh recompeted state is returned.
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

        // A material contradiction weakens the candidates that relied on the now-contested proposition;
        // any other material/potential change that matched an existing proposition adds support to them.
        var isContradiction = string.Equals(
            classificationCode, MatterChangeClassification.MaterialContradiction, StringComparison.OrdinalIgnoreCase);
        var directionSign = isContradiction ? -1.0 : 1.0;

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

            var isMaterial = string.Equals(
                impact.ImpactSeverityCode, DecisionImpactSeverity.Material, StringComparison.OrdinalIgnoreCase);
            var magnitude = isMaterial ? MaterialMagnitude : PotentialMagnitude;

            signals.Add(new DecisionBranchSignal(
                DecisionBranchSignalKinds.SupportChanged,
                BranchId: null,
                CandidateId: candidateId,
                SupportDelta: directionSign * magnitude,
                ReopenRequested: isMaterial,
                ReasonCode: isContradiction ? "IMPACT_MATERIAL_CONTRADICTION" : "IMPACT_SUPPORT_CHANGED"));
        }

        return signals;
    }
}
