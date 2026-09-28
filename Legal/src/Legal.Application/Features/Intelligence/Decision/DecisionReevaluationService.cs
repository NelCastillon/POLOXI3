using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision.Core;

namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Continuous Decision Integrity — Reevaluation Service (Phase 2, steps 8-9: reevaluate + record).
//
// This is the thin orchestration shell around the pure DecisionReevaluationPlanner. After Phase 1
// (MatterChangeProcessor) has recorded the candidate-affecting impacts for a change event, this
// service:
//   1. loads the recorded impacts and the latest matter snapshot,
//   2. runs the planner (impacts → signals → authoritative DecisionRecompetition),
//   3. when the recompetition materially changed the outcome, appends a NEW superseding snapshot and
//      marks the prior snapshot's reliance as SUPERSEDED (history is never overwritten).
//
// It NEVER scores (POLOXI Core does), NEVER deletes history, and NEVER auto-approves: the new snapshot
// is created unapproved so an attorney remains in control. Current candidate/branch state is supplied
// by the caller (the decision session projection), keeping this unit-testable and side-effect scoped
// to the integrity tables. Fail-soft is the caller's responsibility, mirroring Phase 1 capture.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionReevaluationService(
    IDecisionIntegrityRepository integrityRepository)
{
    public async Task<DecisionReevaluationResult> ReevaluateChangeAsync(
        Guid tenantId,
        Guid userId,
        Guid decisionMatterId,
        Guid matterChangeEventId,
        IReadOnlyList<DecisionCandidatePersistence> candidates,
        IReadOnlyList<DecisionBranchPersistence> branches,
        DecisionCoreSettings settings,
        ISet<Guid> reopenAllowedBranchIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(branches);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(reopenAllowedBranchIds);

        var changeEvent = await integrityRepository.GetChangeEventAsync(tenantId, matterChangeEventId, cancellationToken);
        if (changeEvent is null)
            return DecisionReevaluationResult.NotApplicable(matterChangeEventId, "Change event not found.");

        var classification = changeEvent.ClassificationCode ?? MatterChangeClassification.NoMaterialImpact;
        if (string.Equals(classification, MatterChangeClassification.NoMaterialImpact, StringComparison.OrdinalIgnoreCase))
            return DecisionReevaluationResult.NotApplicable(matterChangeEventId, "No material impact to reevaluate.");

        var impacts = await integrityRepository.GetImpactsForEventAsync(tenantId, matterChangeEventId, cancellationToken);

        var plan = DecisionReevaluationPlanner.Run(
            classification, impacts, candidates, branches, settings, reopenAllowedBranchIds);

        if (!plan.ReevaluationOccurred)
            return DecisionReevaluationResult.NotApplicable(matterChangeEventId, "No candidate-affecting impacts.");

        var latestSnapshot = await integrityRepository.GetLatestMatterSnapshotAsync(tenantId, decisionMatterId, cancellationToken);
        var result = plan.Result;

        // Only append a superseding snapshot when POLOXI Core actually changed the outcome. A recompetition
        // that left the ranking and margins unchanged is recorded as "reevaluated, no change" (the Phase 1
        // review task/reliance flag already stands).
        var outcomeChanged =
            result.WinnerChanged
            || Math.Abs(result.CurrentMargin - result.PreviousMargin) > 1e-6
            || Math.Abs(result.CurrentEntropy - result.PreviousEntropy) > 1e-6;

        // Affected-structure counts for the Decision Change Intelligence delta (from the recorded impacts).
        var affectedPropositionCount = impacts.Count(i => string.Equals(i.AffectedKindCode, DecisionImpactKind.Proposition, StringComparison.OrdinalIgnoreCase));
        var affectedCandidateCount = impacts.Count(i => string.Equals(i.AffectedKindCode, DecisionImpactKind.Candidate, StringComparison.OrdinalIgnoreCase));
        var previousWinner = result.PreviousWinnerId is { } pwid
            ? candidates.FirstOrDefault(c => c.DecisionCandidateId == pwid)
            : null;

        if (!outcomeChanged)
        {
            // Even when the ranking held, record a first-class "what changed" delta so the attorney
            // timeline is complete: a material change WAS reevaluated; POLOXI Core left the outcome intact.
            await RecordDeltaAsync(
                tenantId, userId, decisionMatterId, matterChangeEventId, classification,
                DecisionDeltaKind.ReevaluatedNoChange, changeEvent.SourceLabel,
                fromSnapshotId: latestSnapshot?.DecisionSnapshotId, toSnapshotId: null,
                winnerChanged: false,
                previousWinnerId: result.PreviousWinnerId, currentWinnerId: result.CurrentWinnerId,
                previousWinnerLabel: previousWinner?.DisplayName, currentWinnerLabel: previousWinner?.DisplayName,
                previousMargin: result.PreviousMargin, currentMargin: result.CurrentMargin,
                previousEntropy: result.PreviousEntropy, currentEntropy: result.CurrentEntropy,
                affectedPropositionCount, affectedCandidateCount,
                previousReadinessCode: latestSnapshot?.ReadinessStatusCode, currentReadinessCode: latestSnapshot?.ReadinessStatusCode,
                attorneyReviewRequired: false,
                summary: "Material change reevaluated; the leading outcome and margins were unchanged.",
                requiredAction: "No decision change. The existing review task (if any) still stands for attorney awareness.",
                cancellationToken);

            return new DecisionReevaluationResult(
                matterChangeEventId, ReevaluationPerformed: true, OutcomeChanged: false,
                NewSnapshotId: null, SupersededSnapshotId: latestSnapshot?.DecisionSnapshotId,
                PreviousWinnerId: result.PreviousWinnerId, CurrentWinnerId: result.CurrentWinnerId,
                Reason: "Reevaluated; outcome unchanged.");
        }

        var winner = result.CurrentWinnerId is { } wid
            ? candidates.FirstOrDefault(c => c.DecisionCandidateId == wid)
            : null;

        var snapshotNumber = await integrityRepository.GetNextSnapshotNumberAsync(tenantId, decisionMatterId, cancellationToken);
        var newSnapshotId = Guid.NewGuid();
        await integrityRepository.CreateSnapshotAsync(new DecisionSnapshotPersistence(
            newSnapshotId, decisionMatterId, latestSnapshot?.DecisionSessionId, snapshotNumber,
            Title: winner?.DisplayName ?? latestSnapshot?.Title ?? "Reevaluated decision",
            PropositionStatement: latestSnapshot?.PropositionStatement,
            ReadinessStatusCode: latestSnapshot?.ReadinessStatusCode ?? "REEVALUATED",
            RelianceStatusCode: DecisionRelianceStatus.Current,
            RelianceReason: $"Reevaluated after change event {matterChangeEventId:N} ({classification}); "
                            + (result.WinnerChanged ? "winner changed." : "candidate margins updated."),
            IsAttorneyApproved: false, ApprovedByUserId: null, ApprovedDateUtc: null,
            SupersededBySnapshotId: null,
            EvidenceSummaryJson: null,
            EvaluatedDateUtc: DateTime.UtcNow, tenantId, userId), cancellationToken);

        // Mark the prior snapshot as superseded (append-only history: the row is not deleted).
        if (latestSnapshot is not null)
            await integrityRepository.UpdateSnapshotRelianceAsync(
                tenantId, userId, latestSnapshot.DecisionSnapshotId, DecisionRelianceStatus.Superseded,
                $"Superseded by snapshot {snapshotNumber} after reevaluation of change event {matterChangeEventId:N}.",
                cancellationToken);

        // Record the first-class Decision Change Intelligence delta: the durable, traceable answer to
        // "what changed, why does it matter, and what should I examine?" A winner change always warrants
        // attorney review; a margin-only shift is flagged for review when the prior decision was approved.
        var deltaKind = result.WinnerChanged ? DecisionDeltaKind.WinnerChanged : DecisionDeltaKind.MarginShifted;
        var reviewRequired = result.WinnerChanged || (latestSnapshot?.IsAttorneyApproved ?? false);
        await RecordDeltaAsync(
            tenantId, userId, decisionMatterId, matterChangeEventId, classification,
            deltaKind, changeEvent.SourceLabel,
            fromSnapshotId: latestSnapshot?.DecisionSnapshotId, toSnapshotId: newSnapshotId,
            winnerChanged: result.WinnerChanged,
            previousWinnerId: result.PreviousWinnerId, currentWinnerId: result.CurrentWinnerId,
            previousWinnerLabel: previousWinner?.DisplayName, currentWinnerLabel: winner?.DisplayName,
            previousMargin: result.PreviousMargin, currentMargin: result.CurrentMargin,
            previousEntropy: result.PreviousEntropy, currentEntropy: result.CurrentEntropy,
            affectedPropositionCount, affectedCandidateCount,
            previousReadinessCode: latestSnapshot?.ReadinessStatusCode,
            currentReadinessCode: latestSnapshot?.ReadinessStatusCode ?? "REEVALUATED",
            attorneyReviewRequired: reviewRequired,
            summary: result.WinnerChanged
                ? $"Leading outcome changed from \u201C{previousWinner?.DisplayName ?? "—"}\u201D to \u201C{winner?.DisplayName ?? "—"}\u201D after new evidence."
                : "New evidence shifted the margin between competing outcomes; the leading outcome held.",
            requiredAction: result.WinnerChanged
                ? "Review the new leading outcome and confirm whether the revised analysis should replace the prior conclusion."
                : "Review whether the narrowed/widened margin changes case strategy or settlement posture.",
            cancellationToken);

        return new DecisionReevaluationResult(
            matterChangeEventId, ReevaluationPerformed: true, OutcomeChanged: true,
            NewSnapshotId: newSnapshotId, SupersededSnapshotId: latestSnapshot?.DecisionSnapshotId,
            PreviousWinnerId: result.PreviousWinnerId, CurrentWinnerId: result.CurrentWinnerId,
            Reason: result.WinnerChanged ? "Winner changed after reevaluation." : "Candidate margins changed after reevaluation.");
    }

    // Persists one immutable first-class DecisionDelta. Fail-soft: a delta is an audit/read-model
    // artifact; a persistence hiccup here must never fail the authoritative reevaluation above.
    private async Task RecordDeltaAsync(
        Guid tenantId, Guid userId, Guid decisionMatterId, Guid matterChangeEventId, string classification,
        string deltaKind, string? changeSourceLabel, Guid? fromSnapshotId, Guid? toSnapshotId,
        bool winnerChanged, Guid? previousWinnerId, Guid? currentWinnerId,
        string? previousWinnerLabel, string? currentWinnerLabel,
        double previousMargin, double currentMargin, double previousEntropy, double currentEntropy,
        int affectedPropositionCount, int affectedCandidateCount,
        string? previousReadinessCode, string? currentReadinessCode,
        bool attorneyReviewRequired, string summary, string requiredAction,
        CancellationToken cancellationToken)
    {
        await integrityRepository.CreateDecisionDeltaAsync(new DecisionDeltaPersistence(
            DecisionDeltaId: Guid.NewGuid(),
            DecisionMatterId: decisionMatterId,
            MatterChangeEventId: matterChangeEventId,
            FromSnapshotId: fromSnapshotId,
            ToSnapshotId: toSnapshotId,
            DeltaKindCode: deltaKind,
            ClassificationCode: classification,
            Summary: summary,
            ChangeSourceLabel: changeSourceLabel,
            WinnerChanged: winnerChanged,
            PreviousWinnerId: previousWinnerId,
            CurrentWinnerId: currentWinnerId,
            PreviousWinnerLabel: previousWinnerLabel,
            CurrentWinnerLabel: currentWinnerLabel,
            PreviousMargin: previousMargin,
            CurrentMargin: currentMargin,
            PreviousEntropy: previousEntropy,
            CurrentEntropy: currentEntropy,
            AffectedPropositionCount: affectedPropositionCount,
            AffectedCandidateCount: affectedCandidateCount,
            AffectedEvidenceCount: 0,
            InformationValueDelta: null,
            FrontierChanged: null,
            PreviousReadinessCode: previousReadinessCode,
            CurrentReadinessCode: currentReadinessCode,
            AttorneyReviewRequired: attorneyReviewRequired,
            RequiredAction: requiredAction,
            DetailJson: null,
            OccurredDateUtc: DateTime.UtcNow,
            TenantId: tenantId,
            ActorUserId: userId), cancellationToken);
    }
}

// Structured outcome of a Phase 2 reevaluation attempt for one change event.
public sealed record DecisionReevaluationResult(
    Guid MatterChangeEventId,
    bool ReevaluationPerformed,
    bool OutcomeChanged,
    Guid? NewSnapshotId,
    Guid? SupersededSnapshotId,
    Guid? PreviousWinnerId,
    Guid? CurrentWinnerId,
    string Reason)
{
    public static DecisionReevaluationResult NotApplicable(Guid changeEventId, string reason)
        => new(changeEventId, ReevaluationPerformed: false, OutcomeChanged: false,
            NewSnapshotId: null, SupersededSnapshotId: null,
            PreviousWinnerId: null, CurrentWinnerId: null, reason);
}
