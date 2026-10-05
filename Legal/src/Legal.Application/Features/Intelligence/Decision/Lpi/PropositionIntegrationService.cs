using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision.Core;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Intelligence.Decision.Lpi;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Shared proposition-integration service (Phase 2) — the ONE insertion funnel.
//
// Flow:
//   1. Idempotency — a replayed Apply (same IntegrationContext.IdempotencyKey) returns the original
//      result without a second mutation.
//   2. Stale-hierarchy guard — reject if the hierarchy moved since the placements were reviewed.
//   3. Deterministic validation gate — failed checks PRESERVE the proposal + reason (no silent drop).
//   4. Optional advisory LPI initialization — pre-insertion ancestor scores → LpiScoreInitializer.
//      Disabled by default; CONTEXT_ONLY placements never receive a numeric initial value.
//   5. Atomic commit — proposition + placements + optional LPI record + integration op + change event
//      + outbox row committed together, so the EXISTING impact-driven reassessment is triggered.
//   6. A failed reassessment enqueue is surfaced (EvaluationFailed) so the UI never presents the old
//      ranking as if it already reflects the new proposition.
//
// POLOXI Core alone scores candidates and selects the winner. This service never does.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class PropositionIntegrationService(
    ILpiPropositionIntegrationRepository repository,
    ILpiScoreInitializer lpiInitializer,
    ILogger<PropositionIntegrationService> logger) : IPropositionIntegrationService
{
    private const int MaxAncestorDepth = 6;

    public async Task<LpiIntegrationResult> ApplyAsync(
        RetrievedProposition acceptedProposition,
        IReadOnlyList<LpiPlacementProposal> acceptedPlacements,
        LpiIntegrationContext integrationContext,
        LpiOperationKind operation = LpiOperationKind.Add,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acceptedProposition);
        ArgumentNullException.ThrowIfNull(acceptedPlacements);
        ArgumentNullException.ThrowIfNull(integrationContext);

        // 1. Idempotency — replayed commit returns the original result.
        var existing = await repository.TryGetOperationAsync(
            integrationContext.TenantId, integrationContext.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation(
                "Idempotent proposition integration replay for key {Key}; returning original result.",
                integrationContext.IdempotencyKey);
            return existing with { StatusCode = "Idempotent" };
        }

        // 2. Stale-hierarchy guard.
        var currentVersion = await repository.GetHierarchyVersionAsync(
            integrationContext.TenantId, integrationContext.DecisionMatterId, cancellationToken);
        var hierarchyCurrent = integrationContext.HierarchyRevision == 0
            || currentVersion == integrationContext.HierarchyRevision;

        // 3. Deterministic validation gate (duplication fed from accepted texts at target nodes).
        var targetNodeIds = acceptedPlacements.Select(p => p.TargetNodeId).Where(id => id != Guid.Empty).Distinct().ToArray();
        var acceptedTexts = targetNodeIds.Length == 0
            ? []
            : await repository.GetAcceptedPropositionTextsAsync(
                integrationContext.TenantId, integrationContext.DecisionMatterId, targetNodeIds, cancellationToken);

        var supersedesId = operation == LpiOperationKind.Add ? (Guid?)null : acceptedProposition.ProposalId;
        var validation = PropositionIntegrationValidator.Validate(new LpiValidationInput(
            acceptedProposition,
            acceptedPlacements,
            operation,
            hierarchyCurrent,
            acceptedTexts,
            supersedesId));

        if (!hierarchyCurrent)
        {
            var reason = "The decision hierarchy changed since these placements were reviewed. Re-review against the current revision before applying.";
            logger.LogInformation(
                "Parking stale proposition integration for matter {Matter} as {State}; preserved for attorney review.",
                integrationContext.DecisionMatterId, LpiProposalState.ReviewRequired);
            return await ParkAsync(
                acceptedProposition, acceptedPlacements, integrationContext, operation,
                LpiProposalState.ReviewRequired, reason, cancellationToken);
        }

        if (!validation.IsValid)
        {
            var reason = string.Join(" ", validation.Failures.Select(f => f.Message));
            logger.LogInformation(
                "Proposition integration validation failed for matter {Matter}; proposal preserved as {State}. {Reason}",
                integrationContext.DecisionMatterId, validation.PreservedState, reason);
            return await ParkAsync(
                acceptedProposition, acceptedPlacements, integrationContext, operation,
                validation.PreservedState, reason, cancellationToken);
        }

        // 4. Optional advisory LPI initialization (never for CONTEXT_ONLY-only placements).
        var lpiCalculation = await TryInitializeAsync(acceptedPlacements, integrationContext, cancellationToken);

        // 5. Atomic commit (proposition + placements + LPI record + op + change event + outbox).
        LpiIntegrationCommitResult commit;
        try
        {
            commit = await repository.CommitIntegrationAsync(new LpiIntegrationCommit(
                integrationContext.TenantId,
                integrationContext.ActorUserId,
                integrationContext.DecisionMatterId,
                operation,
                acceptedProposition,
                acceptedPlacements,
                lpiCalculation,
                integrationContext,
                operation == LpiOperationKind.Add ? null : acceptedProposition.ProposalId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Proposition integration commit failed for matter {Matter}.", integrationContext.DecisionMatterId);
            throw;
        }

        // 6. Surface reassessment status so the UI never shows the old ranking as current. The commit
        //    reports ReassessmentEnqueued=true ONLY when at least one accepted placement resolved to an
        //    authoritative owning session candidate (a Candidate-kind impact POLOXI Core can recompete).
        //    Zero resolved candidates => the proposition was recorded but the ranking cannot yet reflect
        //    it (e.g. the placement node has no traceable candidate lineage): report EvaluationFailed.
        var status = commit.ReassessmentEnqueued ? "EvaluationPending" : "EvaluationFailed";
        var unresolvedNote = commit.UnresolvedPlacementCount > 0
            ? $" {commit.UnresolvedPlacementCount} placement(s) could not be traced to a decision candidate and did NOT affect scoring."
            : string.Empty;
        var explanation = commit.ReassessmentEnqueued
            ? $"Proposition integrated against {commit.ResolvedCandidateCount} candidate(s). POLOXI Core reassessment was enqueued; the ranking will update when it completes.{unresolvedNote}"
            : $"Proposition integrated, but it could not be connected to any decision candidate, so reassessment was NOT enqueued. The displayed ranking does NOT yet reflect this proposition.{unresolvedNote}";

        return new LpiIntegrationResult(
            Applied: true,
            CommittedPropositionId: commit.CommittedPropositionId,
            SupersededPropositionIds: commit.SupersededPropositionIds,
            ReassessmentEnqueued: commit.ReassessmentEnqueued,
            StatusCode: status,
            Explanation: explanation);
    }

    private async Task<LpiScoreInitializerResult?> TryInitializeAsync(
        IReadOnlyList<LpiPlacementProposal> placements,
        LpiIntegrationContext context,
        CancellationToken cancellationToken)
    {
        if (!lpiInitializer.AncestorInfluenceEnabled)
            return null;

        // Only initialize a value for a placement that actually participates in scoring. CONTEXT_ONLY
        // never receives a numeric initial value.
        var scoringPlacement = placements.FirstOrDefault(p => p.Relationship != LpiRelationship.ContextOnly);
        if (scoringPlacement is null || scoringPlacement.TargetNodeId == Guid.Empty)
            return null;

        var ancestors = await repository.GetAncestorScoresAsync(
            context.TenantId, context.DecisionMatterId, scoringPlacement.TargetNodeId, MaxAncestorDepth, cancellationToken);

        var input = new LpiScoreInitializerInput(
            CandidateNodeId: scoringPlacement.TargetNodeId,
            HierarchyRevision: context.HierarchyRevision,
            PreviousNeighborScore: null,
            NextNeighborScore: null,
            PlacementFraction: scoringPlacement.PlacementFraction ?? 0.5m,
            PreInsertionAncestorScores: ancestors);

        return lpiInitializer.Compute(input);
    }

    // Park an invalid / stale / unplaceable proposition for attorney triage instead of discarding it.
    // The proposition + its proposed placements are persisted in the PRESERVED review state (ReviewRequired
    // or NeedsHierarchyReview). No change event, no candidate impact, and no reassessment is created — an
    // unreviewed proposition must never influence or appear to influence the decision ranking. The returned
    // result surfaces the parked proposition id so the UI can route the attorney to correct and resubmit it.
    private async Task<LpiIntegrationResult> ParkAsync(
        RetrievedProposition acceptedProposition,
        IReadOnlyList<LpiPlacementProposal> acceptedPlacements,
        LpiIntegrationContext integrationContext,
        LpiOperationKind operation,
        LpiProposalState preservedState,
        string reason,
        CancellationToken cancellationToken)
    {
        var reviewState = preservedState.ToString();
        var parkedId = await repository.ParkForReviewAsync(new LpiReviewPark(
            integrationContext.TenantId,
            integrationContext.ActorUserId,
            integrationContext.DecisionMatterId,
            operation,
            acceptedProposition,
            acceptedPlacements,
            integrationContext,
            reviewState,
            reason),
            cancellationToken);

        return new LpiIntegrationResult(
            Applied: false,
            CommittedPropositionId: parkedId,
            SupersededPropositionIds: [],
            ReassessmentEnqueued: false,
            StatusCode: reviewState,
            Explanation: $"Preserved for attorney review ({reviewState}); no scoring or reassessment occurred. The displayed ranking does NOT reflect this proposition. {reason}".TrimEnd());
    }
}
