namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-1 core enums (domain-independent).
//
// The invariant enforced by this layer:  LLMGenerated ≠ POLOXIAuthorized.
// A generated proposition is never automatically an authoritative fact, evidence, authority, or
// decision support. The LLM proposes; POLOXI governs what those proposals may influence.
// These enums MUST stay domain-neutral — legal specializations live alongside, never here.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// The domain-neutral nature of a claimed proposition. Legal packs map these into richer semantics.
public enum ClaimType
{
    Factual,
    Numeric,
    Temporal,
    Attribution,
    Quotation,
    SourceIdentity,
    SourceContent,
    Causal,
    Interpretive,
    Inferential,
    Predictive,
    Procedural,
    Other
}

// Where a claim came from. Origin does NOT determine truth — a UserProvided claim can still be
// Unverified, and an LlmGenerated claim can still become Supported after verification.
public enum ClaimOrigin
{
    LlmGenerated,
    UserProvided,
    RetrievedSource,
    DerivedInference,
    SystemGenerated,
    Imported
}

// Epistemic verification state. UNVERIFIED is deliberately distinct from CONTRADICTED:
//   AbsenceOfSupport ≠ EvidenceOfFalsity.
public enum ClaimVerificationState
{
    Proposed,
    VerificationRequired,
    VerificationInProgress,
    Supported,
    Contradicted,
    Disputed,
    Unverified,
    Unverifiable
}

// Permission to influence a decision. Distinct from verification state: a Supported but low-quality,
// non-material claim may carry less authority than a Supported controlling, essential one.
public enum ClaimDecisionAuthority
{
    None,
    Limited,
    Full
}

// How a piece of evidence/authority relates to a claim.
public enum ClaimSupportRelationship
{
    Supports,
    Contradicts,
    Corroborates,
    Qualifies
}
