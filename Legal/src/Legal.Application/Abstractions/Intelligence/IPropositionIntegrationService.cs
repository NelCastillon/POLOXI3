using Legal.Application.Features.Intelligence.Decision.Lpi;

namespace Legal.Application.Abstractions.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Shared proposition-integration seam (Phase 2).
//
// BOTH entry points — the manual Attorney Decision Input (ADI) path and the Document-Retrieval path —
// flow through this ONE service so there is a single insertion funnel, one idempotency contract, and
// one reassessment trigger. The service inserts a reviewed atomic proposition at its accepted
// placement(s), optionally records the advisory LPI initial value, and enqueues the EXISTING
// impact-driven reassessment. It NEVER scores candidates or selects a winner — POLOXI Core owns that.
//
// Invariants enforced by implementations:
//   * Idempotency on IntegrationContext.IdempotencyKey — a replayed Apply returns the original result.
//   * Stale-hierarchy guard — reject if the hierarchy moved since the placements were reviewed.
//   * CONTEXT_ONLY placements add no numeric support.
//   * Accepted insertion + outbox reassessment event committed atomically.
//   * A failed reassessment is reported (EvaluationFailed) so the UI never shows the old ranking as
//     if it already reflects the new proposition.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface IPropositionIntegrationService
{
    // Apply one reviewed proposition at one or more accepted placements. The operation kind selects the
    // lifecycle intent: Add inserts a new accepted proposition; Revise supersedes a prior accepted
    // proposition with a corrected one; Withdraw retracts a prior accepted proposition's contribution.
    // The context carries revisions, reviewer, and the stable idempotency key. POLOXI Core alone scores.
    Task<LpiIntegrationResult> ApplyAsync(
        RetrievedProposition acceptedProposition,
        IReadOnlyList<LpiPlacementProposal> acceptedPlacements,
        LpiIntegrationContext integrationContext,
        LpiOperationKind operation = LpiOperationKind.Add,
        CancellationToken cancellationToken = default,
        string retrievalModeCode = nameof(LpiRetrievalMode.ConditionDirected));
}
