using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-5 decision-readiness gate (§34, §35).
//
// A session is not "ready to decide" while an essential, decision-relevant claim remains unresolved.
// This never re-derives the rule: it reuses the authority gate's BlocksDecisionReadiness signal
// (computed for essential + material + unresolved claims) over the authoritative session claims.
//
// Gated by UseClaimReadinessBlocking so the release can be enabled/disabled in isolation. When the
// flag is off, the evaluator reports ready-with-note and never blocks.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// A single claim that blocks decision readiness, with the gate's reason.
public sealed record ReadinessBlocker
{
    public required Guid ClaimId { get; init; }

    public required string ClaimText { get; init; }

    public required ClaimVerificationState VerificationState { get; init; }

    public required string Reason { get; init; }
}

public sealed record DecisionReadinessResult
{
    public required bool IsReady { get; init; }

    public required bool Enforced { get; init; }

    public IReadOnlyList<ReadinessBlocker> Blockers { get; init; } = [];

    public IReadOnlyList<string> AuditNarrative { get; init; } = [];
}

public interface IDecisionReadinessEvaluator
{
    Task<DecisionReadinessResult> EvaluateAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

public sealed class DecisionReadinessEvaluator(
    IEpistemicClaimRepository repository,
    IClaimAuthorityGate authorityGate,
    EpistemicAuthoritySettings settings)
    : IDecisionReadinessEvaluator
{
    public async Task<DecisionReadinessResult> EvaluateAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var narrative = new List<string>();

        // §34 feature flag: readiness blocking can be disabled in isolation. When off, the session is
        // never blocked by unresolved claims — the gate simply advises.
        if (!settings.UseClaimReadinessBlocking)
        {
            narrative.Add("Readiness blocking disabled (UseClaimReadinessBlocking=false); session reported ready.");
            return new DecisionReadinessResult
            {
                IsReady = true,
                Enforced = false,
                AuditNarrative = narrative,
            };
        }

        var rows = await repository.GetClaimsForSessionAsync(sessionId, tenantId, cancellationToken);
        var context = settings.ToAuthorityContext();
        var blockers = new List<ReadinessBlocker>();

        foreach (var row in rows)
        {
            var support = await repository.GetSupportForClaimAsync(row.ClaimId, tenantId, cancellationToken);
            var claim = ClaimPersistenceMapper.ToDomain(row, support);

            var decision = authorityGate.Evaluate(claim, context);
            if (!decision.BlocksDecisionReadiness)
                continue;

            blockers.Add(new ReadinessBlocker
            {
                ClaimId = claim.ClaimId,
                ClaimText = claim.Text,
                VerificationState = claim.VerificationState,
                Reason = decision.Reason ?? "Essential, decision-relevant claim is unresolved.",
            });
        }

        var isReady = blockers.Count == 0;
        narrative.Add(isReady
            ? $"Session ready: no unresolved essential claims among {rows.Count} claim(s)."
            : $"Session NOT ready: {blockers.Count} unresolved essential claim(s) block readiness.");

        return new DecisionReadinessResult
        {
            IsReady = isReady,
            Enforced = true,
            Blockers = blockers,
            AuditNarrative = narrative,
        };
    }
}
