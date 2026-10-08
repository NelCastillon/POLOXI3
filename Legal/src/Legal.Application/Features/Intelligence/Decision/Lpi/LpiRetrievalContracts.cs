namespace Legal.Application.Features.Intelligence.Decision.Lpi;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// LPI Document-Retrieval integration contracts (Phase 2).
//
// These records carry a RETRIEVED, atomic proposition from an immutable document passage through
// hierarchy placement, attorney review, and the SHARED proposition-integration service. Retrieval
// supplies new propositions; it NEVER assigns outcome support, scores candidates, or picks winners.
// POLOXI Core remains the sole competition authority; LpiScoreInitializer provides only an advisory,
// attorney-reviewed initial value (disabled by default).
//
// Design invariants mirrored from the ADI (manual) path so BOTH entry points share one service:
//   * Exact source provenance is preserved (DocumentVersionId + SourceLocator + SourceText).
//   * Attribution / assertion type is retained (reported ≠ established fact).
//   * Relationship is qualitative only: SUPPORTS | CONTRADICTS | QUALIFIES | CONTEXT_ONLY.
//   * Unplaceable propositions are flagged NEEDS_HIERARCHY_REVIEW — never force-fit to lexical match.
//   * Idempotency keys derive from stable operation identity, not raw proposition text.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// Which complementary retrieval mode produced a query/proposition.
public enum LpiRetrievalMode
{
    // Search for information addressing an unresolved or changed hierarchy condition.
    ConditionDirected,

    // Detect material information that does not fit the current hierarchy (new defense, party, outcome).
    DocumentDirected,

    // Proposition originated from the Media & Machine Evidence channel (image/audio/structured extraction
    // or manual annotation), anchored to an exact media source region/interval — NOT a document retrieval.
    // MediaDirected is the generic channel marker; the sub-type members below record the exact media kind.
    MediaDirected,

    // Media & Machine Evidence sub-type provenance, derived from the evidence anchor type. These keep the
    // exact media origin visible end-to-end (review queue, DCI trace) without changing scoring behavior.
    MediaImageDirected,
    MediaAudioDirected,
    MediaVideoDirected,
    MediaStructuredDirected,

    // Proposition originated from the LegalAuthority channel: a VERIFIED statute/regulation/case-law
    // evidence item matched to an authoritative hierarchy node. Parked for attorney review like any other
    // source; POLOXI Core still scores it only after acceptance through the shared integration funnel.
    LegalAuthorityDirected
}

// Qualitative relationship a proposition bears to a hierarchy node. Never a score.
public enum LpiRelationship
{
    Supports,
    Contradicts,
    Qualifies,
    ContextOnly
}

// How a source expresses a proposition. Preserved verbatim; a reported assertion is NOT promoted to
// an established fact, and an authority citation does NOT establish the asserted holding.
public enum LpiAssertionType
{
    Asserts,        // a source asserts something
    Reports,        // a person reports something ("patient reports pain")
    Documents,      // a record documents an observation
    StatesLaw,      // an authority states a legal proposition
    Infers          // an inference is proposed
}

// Lifecycle state of a retrieved proposition proposal. Failed validation PRESERVES the proposal and
// reason rather than discarding it.
public enum LpiProposalState
{
    Extracted,
    PlacementProposed,
    ReviewRequired,
    NeedsHierarchyReview,
    Accepted,
    Rejected,
    Superseded,
    Withdrawn
}

// A single condition-directed retrieval query: everything the stateless engine needs to find passages
// that could SUPPORT, CONTRADICT, or QUALIFY a specific hierarchy condition. Relevance selects passages;
// it does not assign outcome support.
public sealed record LpiRetrievalQuery(
    Guid MatterId,
    LpiRetrievalMode Mode,
    string DecisionQuestion,
    Guid? TargetNodeId,
    string? TargetNodeText,
    IReadOnlyList<string> AncestorContext,
    string SupportCriteria,                 // what would support the condition
    string ContradictCriteria,             // what would contradict it
    string QualifyCriteria,                // what would qualify it
    IReadOnlyList<string> MatterEntities,
    IReadOnlyList<string> ApplicableDates,
    IReadOnlyList<string> SourceBoundaries);

// An atomic proposition extracted from a retrieved passage, retaining exact source location. This is
// the illustrative internal contract from the specification.
public sealed record RetrievedProposition(
    Guid ProposalId,
    Guid MatterId,
    Guid DocumentVersionId,
    string SourceLocator,
    string SourceText,
    string PropositionText,
    LpiAssertionType AssertionType,
    string? AttributedTo,
    DateTimeOffset? EffectiveAt);

// A proposed placement of a proposition at a hierarchy node (at any depth), with a single qualitative
// relationship and an explanatory rationale. PlacementFraction is only set from an explicit reviewed
// interpolation position — NEVER inferred from display order.
public sealed record LpiPlacementProposal(
    Guid ProposalId,
    Guid HierarchyRevisionId,
    Guid TargetNodeId,
    Guid? LeftNeighborId,
    Guid? RightNeighborId,
    decimal? PlacementFraction,
    LpiRelationship Relationship,
    string Rationale);

// A proposition plus its candidate placements and current review state, as surfaced to the review UI.
public sealed record RetrievedPropositionReviewItem(
    RetrievedProposition Proposition,
    IReadOnlyList<LpiPlacementProposal> Placements,
    LpiProposalState State,
    string? ReviewNote,
    IReadOnlyList<Guid> AffectedCandidateIds);

// The identifying context for an integration operation. Mirrors the spec's IntegrationContext so both
// the manual ADI path and the retrieval path flow through one service with one idempotency contract.
public sealed record LpiIntegrationContext(
    Guid TenantId,
    Guid ActorUserId,
    Guid DecisionMatterId,
    long DecisionContractRevision,
    long CandidateSetRevision,
    long HierarchyRevision,
    Guid? SourceDocumentVersionId,
    Guid ReviewerUserId,
    string ScoringConfigurationVersion,
    string IdempotencyKey);

// The outcome of an ApplyAsync integration: what was inserted/superseded and whether reassessment was
// enqueued. A FAILED reassessment is reported here so the UI never shows the old ranking as current.
public sealed record LpiIntegrationResult(
    bool Applied,
    Guid? CommittedPropositionId,
    IReadOnlyList<Guid> SupersededPropositionIds,
    bool ReassessmentEnqueued,
    string StatusCode,                      // Applied | Idempotent | Rejected | EvaluationPending | EvaluationFailed
    string Explanation);
