namespace Legal.Application.Abstractions.Persistence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-7 governance verdict persistence contract (§34, §35).
//
// Persists the non-destructive governance verdict overlay (mirrors POLOXI.Legal_DecisionGovernanceVerdict).
// It never mutates the V2 verdict or the claims; it records a per-session snapshot + EA outcome so the
// original result and all involved claims stay visible for reference.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// A flat persistence row for a decision governance verdict.
public sealed record DecisionGovernanceVerdictPersistence(
    Guid GovernanceVerdictId,
    Guid DecisionSessionId,
    Guid? MatterId,
    string OverrideModeCode,
    bool V2ReadinessSatisfied,
    bool EaReadinessReady,
    bool EaReadinessEnforced,
    bool OutputClean,
    bool EffectiveReadinessSatisfied,
    bool OverrideApplied,
    int ProjectedClaimCount,
    int AuthorizedClaimCount,
    int BlockerCount,
    int ViolationCount,
    string? BlockersJson,
    string? ViolationsJson,
    string? InvolvedClaimsJson,
    string? NarrativeJson,
    Guid TenantId,
    Guid? ActorUserId);

public interface IDecisionGovernanceRepository
{
    // Idempotent upsert of the active governance verdict for a session (one active row per session).
    Task UpsertVerdictAsync(
        DecisionGovernanceVerdictPersistence verdict,
        CancellationToken cancellationToken = default);

    // The active (non-deleted) governance verdict for a session, or null if none recorded.
    Task<DecisionGovernanceVerdictPersistence?> GetVerdictForSessionAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
