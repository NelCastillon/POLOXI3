using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Intelligence;

// ── Attorney Decision Input (ADI) service contracts (§19) ────────────────────────────────────────
// Orchestrates the Add-Proposition workflow (Define → Placement → Preview → Confirm) and the
// node-scoped assessment/approval/challenge/reposition operations. POLOXI remains the single
// authoritative evaluator; this service never computes a new final score.
public interface IAttorneyDecisionInputService
{
    // Define — validate + build a draft with deterministic and (when available) AI structural checks.
    Task<AttorneyInputDraft> CreateDraftAsync(Guid tenantId, Guid actorUserId, CreateAttorneyInputCommand command, CancellationToken cancellationToken = default);

    // Placement — compute the suggested midpoint and neighbor bounds for a placement position (§4).
    Task<PlacementAnalysis> AnalyzePlacementAsync(Guid tenantId, AnalyzePlacementCommand command, CancellationToken cancellationToken = default);

    // Preview — non-committing projection through the existing POLOXI scoring path (§15).
    Task<DecisionMutationPreview> PreviewAsync(Guid tenantId, Guid actorUserId, PreviewAttorneyInputCommand command, CancellationToken cancellationToken = default);

    // Confirm/Commit — persist node + assessment (+optional approval) + edges + audit + outbox in one tx (§16).
    Task<CommitResult> CommitAsync(Guid tenantId, Guid actorUserId, CommitAttorneyInputCommand command, CancellationToken cancellationToken = default);

    // Submit a node-scoped relative assessment (multi-attorney; never auto-averaged) (§5).
    Task<AttorneyRelativeAssessmentDto> SubmitAssessmentAsync(Guid tenantId, Guid actorUserId, SubmitAttorneyAssessmentCommand command, CancellationToken cancellationToken = default);

    // Approve a specific assessment as the single active Approved Matter Assessment for the node (§5).
    Task<ApprovedMatterAssessmentDto> ApproveAssessmentAsync(Guid tenantId, Guid actorUserId, ApproveMatterAssessmentCommand command, CancellationToken cancellationToken = default);

    // Raise a challenge against a node or relationship (§3).
    Task<Guid> RaiseChallengeAsync(Guid tenantId, Guid actorUserId, RaiseChallengeCommand command, CancellationToken cancellationToken = default);

    // Reposition an existing node — creates a new version and DecisionDelta (§4).
    Task<CommitResult> RepositionAsync(Guid tenantId, Guid actorUserId, RepositionNodeCommand command, CancellationToken cancellationToken = default);

    // Retract (soft-delete) a committed attorney-supplied node and queue POLOXI recompute (§16/§23).
    Task<RetractResult> RetractAsync(Guid tenantId, Guid actorUserId, RetractAttorneyInputCommand command, CancellationToken cancellationToken = default);

    // §2/§7 resolve-or-create the decision node (and ancestor chain) for a selected live Wide branch.
    Task<ResolvedBranchNode> ResolveBranchNodeAsync(Guid tenantId, Guid actorUserId, ResolveBranchNodeCommand command, CancellationToken cancellationToken = default);
}

// ── Thin adapter over the existing POLOXI evaluation path (§13, §15) ─────────────────────────────
// The adapter projects a proposed (non-committing) mutation onto the existing POLOXI scoring/IV/
// integrity infrastructure. It NEVER defines a new final formula, weight, or origin bonus.
public interface IExistingPoloxiEvaluationAdapter
{
    Task<DecisionMutationPreview> ProjectAsync(Guid tenantId, Guid actorUserId, PreviewAttorneyInputCommand command, CancellationToken cancellationToken = default);
}
