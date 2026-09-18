namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-1 core domain model (domain-independent).
//
// ClaimProposition is the authoritative, POLOXI-owned record of a proposition. The LLM produces a
// ClaimProposal (below); POLOXI registers, normalizes, and owns the authoritative ClaimProposition.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// A reference from a claim to a piece of evidence or an authority, with the relationship and strength.
public sealed record ClaimSupportRef
{
    public required Guid SupportId { get; init; }

    public required Guid ClaimId { get; init; }

    public Guid? EvidenceId { get; init; }

    public Guid? AuthorityId { get; init; }

    public required ClaimSupportRelationship Relationship { get; init; }

    // Strength of the relationship in [0,1].
    public decimal Strength { get; init; }

    // True only when the support was independently verified against the underlying source, not merely
    // asserted by the model. RetrievedSource ≠ VerifiedEvidence.
    public bool IndependentlyVerified { get; init; }

    public string? SourceLocation { get; init; }

    public string? VerificationReason { get; init; }
}

// The authoritative, POLOXI-owned proposition record.
public sealed record ClaimProposition
{
    public required Guid ClaimId { get; init; }

    public required Guid SessionId { get; init; }

    public Guid? MatterId { get; init; }

    public required string Text { get; init; }

    public required string NormalizedText { get; init; }

    public required ClaimType ClaimType { get; init; }

    public ClaimOrigin Origin { get; init; }

    // Semantic lineage back into the existing POLOXI hierarchy/candidate space.
    public Guid? SourceBranchId { get; init; }
    public Guid? SourceCandidateId { get; init; }

    public IReadOnlyList<Guid> BranchIds { get; init; } = [];
    public IReadOnlyList<Guid> CandidateIds { get; init; } = [];

    // Epistemic state.
    public ClaimVerificationState VerificationState { get; init; }

    public ClaimDecisionAuthority DecisionAuthority { get; init; }

    // Aggregate strength of verified support in [0,1].
    public decimal VerificationStrength { get; init; }

    // Decision relevance signals in [0,1].
    public decimal Materiality { get; init; }
    public decimal DecisionImpact { get; init; }
    public decimal Discrimination { get; init; }
    public decimal Uncertainty { get; init; }

    // An essential claim can block decision readiness even while ranking is unaffected.
    public bool IsEssential { get; init; }

    // Verification detail.
    public IReadOnlyList<ClaimSupportRef> SupportingEvidence { get; init; } = [];
    public IReadOnlyList<ClaimSupportRef> ContradictingEvidence { get; init; } = [];

    public string? VerificationReason { get; init; }
    public DateTimeOffset? LastVerifiedAt { get; init; }

    // Provenance.
    public string? ProposedByModel { get; init; }
    public string? PromptRunId { get; init; }
    public int Version { get; init; }
}

// A proposals-only object emitted by an IClaimExtractor. The extractor (which may be an LLM) proposes
// materiality/impact; POLOXI registers and normalizes the authoritative version.
public sealed record ClaimProposal
{
    public required string ClaimKey { get; init; }

    public required string Text { get; init; }

    public string? NormalizedText { get; init; }

    public ClaimType ClaimType { get; init; } = ClaimType.Other;

    public ClaimOrigin Origin { get; init; } = ClaimOrigin.LlmGenerated;

    public Guid? SourceBranchId { get; init; }
    public Guid? SourceCandidateId { get; init; }
    public IReadOnlyList<Guid> CandidateIds { get; init; } = [];
    public IReadOnlyList<Guid> BranchIds { get; init; } = [];

    // Proposed (advisory) decision-relevance signals in [0,1]; POLOXI owns the authoritative values.
    public decimal ProposedMateriality { get; init; }
    public decimal ProposedDecisionImpact { get; init; }
    public decimal ProposedDiscrimination { get; init; }
    public bool ProposedEssential { get; init; }

    public string? ProposedByModel { get; init; }
    public string? PromptRunId { get; init; }
}

// The authoritative, idempotent record of a verification-state transition. Feeds the existing V2.1
// dependency-propagation loop (EA-3, not implemented in this release).
public sealed record ClaimVerificationChangedEvent
{
    public required Guid EventId { get; init; }

    public required Guid ClaimId { get; init; }

    public required ClaimVerificationState PreviousState { get; init; }

    public required ClaimVerificationState NewState { get; init; }

    public IReadOnlyList<Guid> EvidenceIds { get; init; } = [];

    public IReadOnlyList<Guid> AuthorityIds { get; init; } = [];

    public required string Reason { get; init; }

    // De-dupes replays so a repeated event produces exactly one transition/propagation.
    public required string IdempotencyKey { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}
