namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Contracts for the POLOXI Hierarchy Execution Lineage & Authority layer (schema migration 0366).
//
// A HierarchyExecution is one immutable POLOXI hierarchy RUN. The same Decision Contract may produce
// materially different but valid hierarchies on separate runs, so every accepted execution is stored
// independently and exactly one validated execution is explicitly promoted as AUTHORITATIVE per
// decision context. These records are read/DTO shapes and deterministic write commands only — POLOXI
// remains the authoritative evaluator and nothing here computes or overrides scoring.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// Summary row for the Runs list. Carries the three orthogonal lifecycle dimensions separately.
public sealed record HierarchyExecutionSummaryDto(
    Guid HierarchyExecutionId,
    Guid DecisionMatterId,
    Guid DecisionContractId,
    int DecisionContractVersion,
    int RunNumber,
    string RunTypeCode,
    string ProcessingStatusCode,
    string ValidationStatusCode,
    string AuthorityStatusCode,
    string? ModelCode,
    string PromptCode,
    int PromptVersion,
    string AlgorithmVersion,
    int NodeCount,
    int MaxDepth,
    DateTime StartedDateUtc,
    DateTime? CompletedDateUtc,
    byte[] RowVersion);

// One node in a run's hierarchy. Depth never determines role — NodeRoleCode is explicit.
public sealed record HierarchyNodeDto(
    Guid HierarchyNodeId,
    Guid? ParentHierarchyNodeId,
    int Depth,
    int DisplayOrder,
    string NodeTypeCode,
    string NodeRoleCode,
    string? Title,
    string Statement,
    string BranchStateCode,
    bool ContinueNarrowing,
    string? StopReasonCode,
    decimal? Confidence,
    string? CapabilityCode,
    string OriginCode);

// Full execution detail: the run header plus its ordered node lineage.
public sealed record HierarchyExecutionDetailDto(
    HierarchyExecutionSummaryDto Execution,
    IReadOnlyList<HierarchyNodeDto> Nodes);

// The current authoritative execution pointer for a decision context (null when none promoted yet).
public sealed record HierarchyAuthorityDto(
    Guid DecisionHierarchyAuthorityId,
    Guid DecisionMatterId,
    Guid DecisionContractId,
    int DecisionContractVersion,
    Guid HierarchyExecutionId,
    int RunNumber,
    string AuthorityReasonCode,
    DateTime EffectiveDateUtc);

// Command to record a completed, accepted run's header. Node/edge/APR writes are added by later phases.
public sealed record RecordHierarchyExecutionCommand(
    Guid DecisionMatterId,
    Guid DecisionContractId,
    int DecisionContractVersion,
    string RunTypeCode,
    string ProcessingStatusCode,
    string ValidationStatusCode,
    string? ModelCode,
    string? ModelVersion,
    string PromptCode,
    int PromptVersion,
    string AlgorithmVersion,
    string? ConfigurationVersion,
    string InputSnapshotHash);

// Command to promote a validated execution to authoritative for its decision context.
public sealed record PromoteHierarchyAuthorityCommand(
    Guid HierarchyExecutionId,
    byte[] RowVersion,
    string AuthorityReasonCode);

// Result of a promotion: the new authority pointer plus the execution it superseded (if any).
public sealed record PromoteHierarchyAuthorityResult(
    HierarchyAuthorityDto Authority,
    Guid? SupersededExecutionId);

// ── Full-graph write contract ───────────────────────────────────────────────────────────────────
// A HierarchyExecutionRecord is the complete accepted run submitted for atomic persistence: the
// execution header plus every accepted node, every materialized edge, and every APR resolution.
// Node/edge/resolution ids are assigned by the caller so the graph is internally referential before
// it reaches the repository; the application layer validates internal consistency BEFORE the write
// transaction and the DB enforces referential integrity as the final guard. Empty edge/resolution
// collections are legitimate for a valid accepted run and must be persisted as-is (never synthesized).

// One accepted node. Depth never determines role — NodeRoleCode is explicit. ParentNodeId references
// another node in the SAME record (null only for roots).
public sealed record HierarchyNodeRecord(
    Guid NodeId,
    Guid? ParentNodeId,
    int Depth,
    int DisplayOrder,
    string NodeTypeCode,
    string NodeRoleCode,
    string? Title,
    string Statement,
    string? SearchText,
    string BranchStateCode,
    bool ContinueNarrowing,
    string? StopReasonCode,
    decimal? Confidence,
    string? CapabilityCode,
    string OriginCode,
    string? OriginPromptCode,
    int? OriginPromptVersion,
    string? OriginModelCode,
    string? SemanticHash);

// One materialized non-parent structural edge between two nodes in the SAME record.
public sealed record HierarchyEdgeRecord(
    Guid FromNodeId,
    Guid ToNodeId,
    string EdgeTypeCode,
    string StateCode);

// One APR (Atomic Proposition Resolution) resolution for a proposition-like node in the SAME record.
public sealed record PropositionResolutionRecord(
    Guid NodeId,
    int ResolutionRevision,
    string AtomicityStateCode,
    bool ContextResolved,
    bool ParentFidelityPassed,
    bool IndependentlyTestable,
    bool MaterialSplitRemaining,
    string ResolutionStateCode,
    int ResolutionDepth,
    string ResolutionMethodCode,
    string? StopReasonCode);

// The complete accepted run submitted for atomic persistence.
public sealed record HierarchyExecutionRecord(
    RecordHierarchyExecutionCommand Header,
    IReadOnlyList<HierarchyNodeRecord> Nodes,
    IReadOnlyList<HierarchyEdgeRecord> Edges,
    IReadOnlyList<PropositionResolutionRecord> Resolutions);
