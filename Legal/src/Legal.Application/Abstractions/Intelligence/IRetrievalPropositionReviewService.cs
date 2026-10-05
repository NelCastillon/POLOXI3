using Legal.Application.Features.Intelligence.Decision.Lpi;

namespace Legal.Application.Abstractions.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Retrieved Proposition Review seam (Phase 2, acceptance half).
//
// Bridges the parked review queue (ILpiPropositionIntegrationRepository) to the SHARED insertion funnel
// (IPropositionIntegrationService.ApplyAsync). On accept, it reconstructs the RetrievedProposition and
// its accepted placements from the persisted review item, rebuilds the LpiIntegrationContext (carrying
// revisions, reviewer, scoring-config version, and a stable idempotency key), and routes them through
// the EXISTING funnel — never a second scoring engine. On reject it only updates state.
//
// The returned LpiIntegrationResult carries the reassessment status (Applied | Idempotent | Rejected |
// EvaluationPending | EvaluationFailed) so the Outcome screen never presents the old ranking as if it
// already reflects an accepted proposition.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface IRetrievalPropositionReviewService
{
    // The attorney review queue for a matter: every parked, non-terminal retrieved proposition.
    Task<IReadOnlyList<LpiReviewItemView>> GetPendingAsync(
        Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);

    // Accept a reviewed proposition (optionally restricting to a subset of its proposed placements) and
    // route it through the shared integration funnel. Returns the funnel result including reassessment
    // status. Rejects the request if the proposition is not in a reviewable state or has no usable
    // placement.
    Task<LpiIntegrationResult> AcceptAsync(
        LpiReviewAcceptRequest request, CancellationToken cancellationToken = default);

    // Terminal reject: preserve the proposition but mark it Rejected with the reviewer reason.
    Task RejectAsync(
        LpiReviewRejectRequest request, CancellationToken cancellationToken = default);

    // Revise an ACCEPTED proposition: supersede it with a corrected proposition/placements through the
    // shared funnel. The prior accepted proposition is preserved (state Superseded) and history is kept;
    // the correction drives a fresh impact-driven reassessment. POLOXI Core alone re-scores.
    Task<LpiIntegrationResult> ReviseAsync(
        LpiReviewReviseRequest request, CancellationToken cancellationToken = default);

    // Withdraw an ACCEPTED proposition: retract its contribution through the shared funnel so POLOXI Core
    // recompetes without it. The source record is preserved (state Withdrawn); nothing is deleted.
    Task<LpiIntegrationResult> WithdrawAsync(
        LpiReviewWithdrawRequest request, CancellationToken cancellationToken = default);
}

// Review-queue projection of one parked proposition, for the Retrieved Proposition Review panel.
public sealed record LpiReviewItemView(
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
    IReadOnlyList<LpiReviewPlacementView> Placements,
    // Provenance channel (ConditionDirected | DocumentDirected | MediaDirected) so the review panel can
    // show whether a parked proposition came from document retrieval or the Media & Machine Evidence channel.
    string RetrievalModeCode = nameof(LpiRetrievalMode.ConditionDirected));

public sealed record LpiReviewPlacementView(
    Guid TargetNodeId,
    Guid? LeftNeighborId,
    Guid? RightNeighborId,
    decimal? PlacementFraction,
    string RelationshipCode,
    string Rationale);

// Attorney acceptance. AcceptedPlacementTargetNodeIds optionally narrows to a reviewed subset of the
// proposed placements; when null/empty, all proposed placements are accepted. Revisions/scoring-config
// come from the current decision state and are supplied by the caller.
public sealed record LpiReviewAcceptRequest(
    Guid TenantId,
    Guid ReviewerUserId,
    Guid RetrievedPropositionId,
    long DecisionContractRevision,
    long CandidateSetRevision,
    long HierarchyRevision,
    string ScoringConfigurationVersion,
    IReadOnlyList<Guid>? AcceptedPlacementTargetNodeIds = null);

public sealed record LpiReviewRejectRequest(
    Guid TenantId,
    Guid ReviewerUserId,
    Guid RetrievedPropositionId,
    string Reason);

// Attorney revision of an accepted proposition. The corrected text/placements replace a prior accepted
// proposition (identified by SupersedesPropositionId). Corrected placements are supplied explicitly so
// the attorney can change the node/relationship; when omitted the original placements are reused.
public sealed record LpiReviewReviseRequest(
    Guid TenantId,
    Guid ReviewerUserId,
    Guid SupersedesPropositionId,
    string PropositionText,
    long DecisionContractRevision,
    long CandidateSetRevision,
    long HierarchyRevision,
    string ScoringConfigurationVersion,
    IReadOnlyList<LpiReviewPlacementEdit>? Placements = null,
    string? Reason = null);

// Attorney withdrawal of an accepted proposition. Retracts its contribution; the source is preserved.
public sealed record LpiReviewWithdrawRequest(
    Guid TenantId,
    Guid ReviewerUserId,
    Guid RetrievedPropositionId,
    long DecisionContractRevision,
    long CandidateSetRevision,
    long HierarchyRevision,
    string ScoringConfigurationVersion,
    string Reason);

// A reviewed placement edit supplied with a revision (node + qualitative relationship + optional
// neighbor/fraction). Relationship is one of SUPPORTS | CONTRADICTS | QUALIFIES | CONTEXT_ONLY.
public sealed record LpiReviewPlacementEdit(
    Guid TargetNodeId,
    string RelationshipCode,
    Guid? LeftNeighborId = null,
    Guid? RightNeighborId = null,
    decimal? PlacementFraction = null,
    string Rationale = "");
