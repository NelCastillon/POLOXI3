using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Application.Features.Intelligence.Decision.Lpi;

namespace Legal.Application.Abstractions.Persistence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Persistence for the shared LPI proposition-integration funnel (Phase 2).
//
// Backs POLOXI.Legal_RetrievedProposition / Legal_PropositionNodeLink / Legal_LpiCalculation /
// Legal_PropositionIntegrationOp (migration 0399). All writes for one Apply are committed in a single
// transaction together with the reassessment change-event + outbox row so an accepted insertion and
// its reassessment trigger are atomic. No scoring happens here — POLOXI Core owns outcome.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface ILpiPropositionIntegrationRepository
{
    // Idempotency: return the prior operation result when the key was already committed.
    Task<LpiIntegrationResult?> TryGetOperationAsync(
        Guid tenantId, string idempotencyKey, CancellationToken cancellationToken = default);

    // Current max hierarchy version for the matter (stale-review guard, mirrors the ADI path).
    Task<long> GetHierarchyVersionAsync(
        Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);

    // Normalized texts of propositions already accepted at the given target nodes — feeds the pure
    // duplication check in the validator without the validator touching the DB.
    Task<IReadOnlyCollection<string>> GetAcceptedPropositionTextsAsync(
        Guid tenantId, Guid decisionMatterId, IReadOnlyCollection<Guid> targetNodeIds,
        CancellationToken cancellationToken = default);

    // Pre-insertion ancestor scores for an optional LPI initialization, nearest-first (Depth 1 = parent).
    Task<IReadOnlyList<LpiAncestorScore>> GetAncestorScoresAsync(
        Guid tenantId, Guid decisionMatterId, Guid parentNodeId, int maxDepth,
        CancellationToken cancellationToken = default);

    // Atomic commit: proposition + placements + optional LPI calculation + integration op + change
    // event + outbox row. Returns the committed proposition id and the enqueued change-event id.
    Task<LpiIntegrationCommitResult> CommitIntegrationAsync(
        LpiIntegrationCommit commit, CancellationToken cancellationToken = default);

    // Park a proposition that FAILED the validation gate (or a stale-hierarchy guard) for attorney
    // triage instead of discarding it: the proposition + its placements are persisted with the preserved
    // review state (ReviewRequired | NeedsHierarchyReview) and an integration-op row carrying the
    // idempotency key, with NO change event and NO reassessment. The spec guarantees the proposal is
    // ALWAYS preserved — this is where that promise is kept. Idempotent via the op's unique key.
    Task<Guid> ParkForReviewAsync(
        LpiReviewPark park, CancellationToken cancellationToken = default);

    // Pending review queue: retrieved propositions for a matter that still await attorney action (any
    // non-terminal state — Extracted | PlacementProposed | ReviewRequired | NeedsHierarchyReview), with
    // their proposed placements. Feeds the Retrieved Proposition Review panel.
    Task<IReadOnlyList<LpiReviewItem>> GetPendingReviewItemsAsync(
        Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);

    // Load ONE review item (proposition + its proposed placements) so the accept path can reconstruct the
    // RetrievedProposition + placements and route them through the shared integration funnel.
    Task<LpiReviewItem?> GetReviewItemAsync(
        Guid tenantId, Guid retrievedPropositionId, CancellationToken cancellationToken = default);

    // Terminal reject: mark a parked proposition (and its links) Rejected with the reviewer reason. No
    // change event, no reassessment — the proposition is preserved but will not influence the ranking.
    Task RejectReviewItemAsync(
        Guid tenantId, Guid actorUserId, Guid retrievedPropositionId, string reason,
        CancellationToken cancellationToken = default);

    // Load ONE accepted proposition (plus its accepted placements) so the revise/withdraw lifecycle can
    // reconstruct the prior contribution. Returns null when the id is not an accepted, non-deleted row.
    Task<LpiReviewItem?> GetAcceptedPropositionAsync(
        Guid tenantId, Guid retrievedPropositionId, CancellationToken cancellationToken = default);

    // Withdraw an ACCEPTED proposition: mark it (and its links) Withdrawn — never deleted — and atomically
    // emit a matter change event with INVERTED candidate impacts (a prior SUPPORTS retraction WEAKENS the
    // owning candidate, a prior CONTRADICTS retraction STRENGTHENS it, QUALIFIES reopens evaluation) plus a
    // Withdraw integration op, so the EXISTING impact-driven POLOXI recompetition runs without this
    // proposition. Idempotent via the op's unique key. Returns whether a reassessment was enqueued.
    Task<LpiIntegrationCommitResult> WithdrawAcceptedAsync(
        LpiWithdrawCommit commit, CancellationToken cancellationToken = default);
}

// A parked retrieved proposition plus its proposed placements, as surfaced to the review queue and used
// to reconstruct the integration payload on accept.
public sealed record LpiReviewItem(
    Guid RetrievedPropositionId,
    Guid DecisionMatterId,
    Guid DocumentVersionId,
    string SourceLocator,
    string SourceText,
    string PropositionText,
    string AssertionTypeCode,
    string? AttributedTo,
    DateTimeOffset? EffectiveAt,
    string StateCode,
    string? StateReason,
    IReadOnlyList<LpiReviewPlacement> Placements,
    // Provenance mode stored at park time (ConditionDirected | DocumentDirected | MediaDirected). Carried
    // through on accept so the committed proposition keeps its original channel instead of being relabeled.
    string RetrievalModeCode = nameof(LpiRetrievalMode.ConditionDirected));

public sealed record LpiReviewPlacement(
    Guid TargetNodeId,
    Guid? LeftNeighborId,
    Guid? RightNeighborId,
    decimal? PlacementFraction,
    string RelationshipCode,
    string Rationale,
    long HierarchyRevision);

// A proposition whose integration was blocked (invalid or stale) but which must be preserved for review.
public sealed record LpiReviewPark(
    Guid TenantId,
    Guid ActorUserId,
    Guid DecisionMatterId,
    LpiOperationKind Operation,
    RetrievedProposition Proposition,
    IReadOnlyList<LpiPlacementProposal> Placements,
    LpiIntegrationContext Context,
    string ReviewStateCode,     // LpiProposalState: ReviewRequired | NeedsHierarchyReview
    string ReviewReason,
    // Provenance mode persisted to Legal_RetrievedProposition.RetrievalModeCode. Defaults to
    // ConditionDirected to preserve existing document-retrieval parking; media parks pass MediaDirected.
    string RetrievalModeCode = nameof(LpiRetrievalMode.ConditionDirected));

// A fully-prepared, validated commit payload assembled by the integration service.
public sealed record LpiIntegrationCommit(
    Guid TenantId,
    Guid ActorUserId,
    Guid DecisionMatterId,
    LpiOperationKind Operation,
    RetrievedProposition Proposition,
    IReadOnlyList<LpiPlacementProposal> Placements,
    LpiScoreInitializerResult? LpiCalculation,      // null when LPI disabled or not applicable
    LpiIntegrationContext Context,
    Guid? SupersedesPropositionId,
    // Provenance mode persisted to Legal_RetrievedProposition.RetrievalModeCode. Defaults to
    // ConditionDirected to preserve existing document-retrieval commits; media commits pass MediaDirected.
    string RetrievalModeCode = nameof(LpiRetrievalMode.ConditionDirected));

// A withdrawal payload: the accepted proposition being retracted plus the placements whose contribution
// must be inverted so POLOXI Core recompetes without it. History is preserved (state Withdrawn).
public sealed record LpiWithdrawCommit(
    Guid TenantId,
    Guid ActorUserId,
    Guid DecisionMatterId,
    Guid RetrievedPropositionId,
    IReadOnlyList<LpiPlacementProposal> Placements,
    LpiIntegrationContext Context,
    string Reason);

public sealed record LpiIntegrationCommitResult(
    Guid CommittedPropositionId,
    IReadOnlyList<Guid> SupersededPropositionIds,
    Guid ChangeEventId,
    bool ReassessmentEnqueued,
    // How many accepted, scoring placements resolved to an authoritative owning session candidate
    // (their Candidate-kind Legal_DecisionImpact rows drive the POLOXI recompetition), and how many
    // scoring placements could NOT be traced to a candidate (unresolved lineage). A commit that
    // produced zero candidate impacts has nothing for POLOXI Core to recompete, so the caller must
    // NOT present the old ranking as if it already reflects the proposition.
    int ResolvedCandidateCount = 0,
    int UnresolvedPlacementCount = 0);
