using Legal.Application.Features.Intelligence.Epistemic;

namespace Legal.Application.Abstractions.Persistence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-1 persistence contract (§32).
//
// Persists authoritative claim propositions, their support edges, and idempotent verification events
// into the POLOXI schema. Graph relationships continue to live in the existing dependency-graph
// tables; these records never duplicate graph edges.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// A flat persistence row for a claim proposition (mirrors POLOXI.Legal_ClaimProposition).
public sealed record ClaimPropositionPersistence(
    Guid ClaimId,
    Guid DecisionSessionId,
    Guid? MatterId,
    string Text,
    string NormalizedText,
    string ClaimTypeCode,
    string ClaimOriginCode,
    string VerificationStateCode,
    string DecisionAuthorityCode,
    decimal VerificationStrength,
    decimal Materiality,
    decimal DecisionImpact,
    decimal Discrimination,
    decimal Uncertainty,
    bool IsEssential,
    Guid? SourceBranchId,
    Guid? SourceCandidateId,
    string? ProposedByModel,
    string? PromptRunId,
    string? VerificationReason,
    int Version,
    Guid TenantId,
    Guid? ActorUserId);

// A flat persistence row for a claim support edge (mirrors POLOXI.Legal_ClaimSupport).
public sealed record ClaimSupportPersistence(
    Guid ClaimSupportId,
    Guid ClaimId,
    Guid? EvidenceId,
    Guid? AuthorityId,
    string RelationshipCode,
    decimal Strength,
    bool IndependentlyVerified,
    string? SourceLocation,
    string? VerificationReason,
    Guid TenantId,
    Guid? ActorUserId);

// A flat persistence row for a verification-state transition (mirrors POLOXI.Legal_ClaimVerificationEvent).
public sealed record ClaimVerificationEventPersistence(
    Guid EventId,
    Guid ClaimId,
    string PreviousStateCode,
    string NewStateCode,
    string Reason,
    string? EvidenceIdsJson,
    string? AuthorityIdsJson,
    string IdempotencyKey,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid? ActorUserId);

public interface IEpistemicClaimRepository
{
    Task UpsertClaimAsync(ClaimPropositionPersistence claim, CancellationToken cancellationToken = default);

    Task ReplaceSupportAsync(
        Guid claimId,
        IReadOnlyList<ClaimSupportPersistence> support,
        Guid tenantId,
        Guid? actorUserId,
        CancellationToken cancellationToken = default);

    // Records a verification event idempotently. Returns true when a NEW event row was written, false
    // when the idempotency key already existed (so callers run propagation exactly once).
    Task<bool> TryRecordVerificationEventAsync(
        ClaimVerificationEventPersistence verificationEvent,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClaimPropositionPersistence>> GetClaimsForSessionAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ClaimPropositionPersistence?> GetClaimAsync(
        Guid claimId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    // Returns the active (non-deleted) support edges for a claim so a rehydrated proposition can be
    // populated with its supporting/contradicting evidence.
    Task<IReadOnlyList<ClaimSupportPersistence>> GetSupportForClaimAsync(
        Guid claimId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
