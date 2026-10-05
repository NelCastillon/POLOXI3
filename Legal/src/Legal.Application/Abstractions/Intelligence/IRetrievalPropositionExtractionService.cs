using Legal.Application.Features.Intelligence.Decision.Lpi;

namespace Legal.Application.Abstractions.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Document-Retrieval EXTRACTION seam (Phase 2, sending half).
//
// This is the retrieval-side counterpart to IPropositionIntegrationService (the receiving funnel). It
// runs the DB-backed DECISION_EXTRACTION_V1 prompt over ONE retrieved passage and the current decision
// hierarchy, parses the strict-JSON atomic propositions + PROPOSED qualitative placements, and PARKS
// each as an attorney review item in a preserved review state. It NEVER scores candidates, never picks
// a winner, never auto-applies. Acceptance flows through the SHARED IPropositionIntegrationService so
// the manual (ADI) path and the retrieval path converge on one scoring funnel and one reassessment.
//
// Invariants:
//   * Exact source provenance preserved (DocumentVersionId + SourceLocator + SourceText).
//   * Attribution / assertion type retained (a reported assertion is NOT promoted to an established fact).
//   * Relationship is qualitative only: SUPPORTS | CONTRADICTS | QUALIFIES | CONTEXT_ONLY.
//   * needsHierarchyReview (or an unmappable enum) parks as NeedsHierarchyReview — never force-fit.
//   * Extraction output is review-only: it produces no change event, no impact, no reassessment.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface IRetrievalPropositionExtractionService
{
    // Extract atomic propositions from one retrieved passage and park them as review items. Returns a
    // summary of what was extracted and persisted (ids + per-item state). Does not apply or score.
    Task<RetrievalExtractionResult> ExtractAndParkAsync(
        RetrievalExtractionRequest request, CancellationToken cancellationToken = default);
}

// One retrieved source passage plus the hierarchy context the stateless extraction engine needs. The
// engine is stateless, so the caller supplies the full relevant context on every call.
public sealed record RetrievalExtractionRequest(
    Guid TenantId,
    Guid ActorUserId,
    Guid MatterId,
    Guid DocumentVersionId,
    long DecisionContractRevision,
    long CandidateSetRevision,
    long HierarchyRevision,
    string ScoringConfigurationVersion,
    string CorrelationId,
    // {{QUERY}} — the decision question.
    string DecisionQuestion,
    // {{CONTEXT}} — candidates and nodes JSON (hierarchy context the LLM matches placements against).
    string HierarchyContextJson,
    // {{ARTIFACT}} — the retrieved passage with its documentVersionId + locator JSON.
    string SourcePassage,
    string SourceLocator);

// Outcome of an extraction pass. Each parked item is preserved for attorney review (never discarded).
public sealed record RetrievalExtractionResult(
    int PropositionsExtracted,
    int ItemsParked,
    IReadOnlyList<RetrievalExtractedItem> Items,
    string StatusCode,          // Extracted | NoPropositions | ExtractionFailed
    string? FailureReason);

// A single extracted-and-parked proposition: the parked proposition id, its preserved review state, and
// how many placements the engine proposed.
public sealed record RetrievalExtractedItem(
    Guid ParkedPropositionId,
    LpiProposalState State,
    string PropositionText,
    int ProposedPlacementCount,
    bool NeedsHierarchyReview);
