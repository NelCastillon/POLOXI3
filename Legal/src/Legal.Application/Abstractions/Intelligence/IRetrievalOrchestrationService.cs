using Legal.Application.Features.Intelligence.Decision.Lpi;

namespace Legal.Application.Abstractions.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// IRetrievalOrchestrationService — the Document-Retrieval DRIVER (Phase 2, fan-out half).
//
// Where IRetrievalPropositionExtractionService processes ONE passage, the orchestrator enumerates the
// passages worth processing for a matter and feeds each through the shared extraction→park seam. It
// supports the two complementary retrieval modes from the spec:
//
//   * ConditionDirected  — find passages that could SUPPORT / CONTRADICT / QUALIFY the matter's open
//                          hierarchy conditions. Relevance SELECTS passages; it never assigns support.
//   * DocumentDirected   — scan document passages for material information that may not fit the current
//                          hierarchy (a new defense, party, or outcome) so attorneys can extend it.
//
// It loads the authoritative hierarchy once (for the {{CONTEXT}} JSON + node resolver), selects passages,
// and parks every extracted proposition for attorney review. It NEVER scores, applies, or ranks — POLOXI
// Core remains the sole competition authority and acceptance flows through the shared funnel.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface IRetrievalOrchestrationService
{
    // Run one retrieval pass for a matter and park every extracted proposition for review. Returns a
    // batch summary (passages scanned, propositions extracted/parked, per-passage status).
    Task<RetrievalOrchestrationResult> RunAsync(
        RetrievalOrchestrationRequest request, CancellationToken cancellationToken = default);
}

// Everything the orchestrator needs to drive one retrieval pass. Revisions/scoring-config are carried
// so parked propositions and later acceptance share the manual ADI path's identity contract.
public sealed record RetrievalOrchestrationRequest(
    Guid TenantId,
    Guid ActorUserId,
    Guid MatterId,
    LpiRetrievalMode Mode,
    string DecisionQuestion,
    long DecisionContractRevision,
    long CandidateSetRevision,
    long HierarchyRevision,
    string ScoringConfigurationVersion,
    string CorrelationId,
    // ConditionDirected: free-text relevance query selecting passages for the open conditions.
    // DocumentDirected: optional scope filter; when null/empty every matter document passage is scanned.
    string? RetrievalQuery = null,
    // Optional: restrict a document-directed pass to specific document versions. Empty = all.
    IReadOnlyList<Guid>? DocumentVersionIds = null,
    // Upper bound on passages processed in one pass so the operation stays observable/bounded.
    int MaxPassages = 50);

// Outcome of one orchestration pass. Items preserve per-passage extraction results (never discarded).
public sealed record RetrievalOrchestrationResult(
    int PassagesScanned,
    int PropositionsExtracted,
    int ItemsParked,
    IReadOnlyList<RetrievalOrchestrationPassage> Passages,
    string StatusCode,          // Completed | NoHierarchy | NoPassages | Failed
    string? FailureReason);

// Per-passage result of a pass: which passage was processed and how the extraction resolved.
public sealed record RetrievalOrchestrationPassage(
    Guid DocumentVersionId,
    Guid? PassageId,
    string SourceLocator,
    int PropositionsExtracted,
    int ItemsParked,
    string StatusCode,          // Extracted | NoPropositions | ExtractionFailed
    string? FailureReason);
