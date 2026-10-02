using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Persistence;

// Persistence for the Attorney Decision Input (Human Intelligence) surface.
// Reads back the canonical hierarchy + assessments; writes commit attorney mutations transactionally
// (node + placement + assessment + optional approval + edges + audit + outbox) with concurrency guards.
public interface IAttorneyDecisionInputRepository
{
    Task<MatterHumanIntelligenceDto> GetMatterHumanIntelligenceAsync(
        Guid tenantId,
        Guid matterId,
        CancellationToken cancellationToken = default);

    // Returns the immediate ordered sibling values under a parent/candidate scope at a given level (§4).
    Task<IReadOnlyList<(Guid NodeId, string NodeText, decimal? Value)>> GetSiblingValuesAsync(
        Guid tenantId, Guid matterId, Guid candidateNodeId, Guid? parentNodeId, int nodeLevel,
        CancellationToken cancellationToken = default);

    // Duplicate candidates by canonical-key / text similarity within the same matter (§8).
    Task<IReadOnlyList<AttorneyDuplicateCandidateDto>> FindDuplicateCandidatesAsync(
        Guid tenantId, Guid matterId, string nodeText, CancellationToken cancellationToken = default);

    // Current max hierarchy version for the matter (optimistic concurrency guard, §16/§23).
    Task<long> GetHierarchyVersionAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    // Idempotency check — returns a prior CommitResult if the idempotency key was already committed.
    Task<CommitResult?> TryGetCommittedAsync(Guid tenantId, Guid idempotencyKey, CancellationToken cancellationToken = default);

    // §16 transactional commit: node + placement + assessment (+approval) + edges + audit + outbox.
    Task<CommitResult> CommitAttorneyInputAsync(
        Guid tenantId, Guid actorUserId, CommitAttorneyInputCommand command, string canonicalKey, string placementKey,
        CancellationToken cancellationToken = default);

    Task<AttorneyRelativeAssessmentDto> SubmitAssessmentAsync(
        Guid tenantId, Guid actorUserId, SubmitAttorneyAssessmentCommand command, CancellationToken cancellationToken = default);

    Task<ApprovedMatterAssessmentDto> ApproveAssessmentAsync(
        Guid tenantId, Guid actorUserId, ApproveMatterAssessmentCommand command, CancellationToken cancellationToken = default);

    Task<Guid> RaiseChallengeAsync(
        Guid tenantId, Guid actorUserId, RaiseChallengeCommand command, CancellationToken cancellationToken = default);

    Task<CommitResult> RepositionAsync(
        Guid tenantId, Guid actorUserId, RepositionNodeCommand command, CancellationToken cancellationToken = default);

    // §2/§7 resolve-or-create the decision node (and its ancestor chain) for a selected Wide branch.
    // Idempotent on (TenantId, MatterId, SourceWideBranchId); returns the resolved/created node + L1 scope.
    Task<ResolvedBranchNode> ResolveBranchNodeAsync(
        Guid tenantId, Guid actorUserId, ResolveBranchNodeCommand command, CancellationToken cancellationToken = default);
}
