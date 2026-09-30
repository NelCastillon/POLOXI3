using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Persistence;

// DB-backed persistence for the POLOXI Hierarchy Execution Lineage & Authority layer (migration 0366).
// Tenant-scoped on every query/mutation. Every accepted run is stored independently and immutably;
// authority is an explicit, transactional promotion — "latest" is never implicitly authoritative.
// POLOXI remains the authoritative evaluator; this repository never computes or overrides scoring.
public interface ILegalHierarchyExecutionRepository
{
    // Lists the runs for a decision context (all runs of a contract version), newest run first.
    Task<IReadOnlyList<HierarchyExecutionSummaryDto>> GetRunsAsync(
        Guid tenantId, Guid decisionMatterId, Guid decisionContractId, int decisionContractVersion,
        CancellationToken cancellationToken = default);

    // Reads one execution header plus its ordered node lineage.
    Task<HierarchyExecutionDetailDto?> GetExecutionAsync(
        Guid tenantId, Guid hierarchyExecutionId, CancellationToken cancellationToken = default);

    // Reads the current authoritative execution pointer for a decision context (null when none).
    Task<HierarchyAuthorityDto?> GetCurrentAuthorityAsync(
        Guid tenantId, Guid decisionMatterId, Guid decisionContractId, int decisionContractVersion,
        CancellationToken cancellationToken = default);

    // Records a completed, accepted run as a COMPLETE graph in ONE transaction: the execution header
    // (assigning the next monotonic RunNumber for the decision context) plus every accepted node, every
    // materialized edge, and every APR resolution, followed by a HierarchyExecutionCompleted outbox
    // event. Node/edge/resolution ids in the record are honored as-is; the caller must validate internal
    // graph consistency before calling. Empty edge/resolution collections are persisted as-is.
    Task<HierarchyExecutionSummaryDto> RecordExecutionAsync(
        Guid tenantId, Guid userId, HierarchyExecutionRecord record,
        CancellationToken cancellationToken = default);

    // Promotes a validated execution to AUTHORITATIVE for its decision context. Transactional:
    // supersedes any prior current-authority row, inserts the new authority row, flips
    // AuthorityStatusCode (new=AUTHORITATIVE, prior=SUPERSEDED), and enqueues a HierarchyAuthorityPromoted
    // outbox event — all in one transaction that preserves the UX_Legal_DecHierAuth_Current invariant.
    // Enforces RowVersion optimistic concurrency and requires a VALID/VALID_WITH_WARNINGS execution.
    Task<PromoteHierarchyAuthorityResult> PromoteAuthorityAsync(
        Guid tenantId, Guid userId, PromoteHierarchyAuthorityCommand command,
        CancellationToken cancellationToken = default);
}

// Raised when a promotion loses the RowVersion optimistic-concurrency check.
public sealed class HierarchyExecutionConcurrencyException(string message) : Exception(message);

// Raised when a promotion is invalid for the execution's current validation/processing state.
public sealed class HierarchyExecutionStateException(string message) : Exception(message);
