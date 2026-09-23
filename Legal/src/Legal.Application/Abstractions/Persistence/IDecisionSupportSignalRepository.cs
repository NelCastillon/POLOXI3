using Legal.Application.Features.Intelligence.Epistemic;

namespace Legal.Application.Abstractions.Persistence;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Verified Decision Signals — persistence contract (frozen slice-1 design).
//
// Persists verified decision-support signals into the POLOXI schema. These records never duplicate
// dependency-graph edges; they live in POLOXI.Legal_DecisionSupportSignal keyed by decision session.
// ─────────────────────────────────────────────────────────────────────────────────────────────
public interface IDecisionSupportSignalRepository
{
    // Inserts or updates a support signal by SignalId.
    Task UpsertAsync(DecisionSupportSignalPersistence signal, CancellationToken cancellationToken = default);

    // Returns the non-deleted signals for a decision session, ordered deterministically.
    Task<IReadOnlyList<DecisionSupportSignalPersistence>> GetBySessionAsync(
        Guid decisionSessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
