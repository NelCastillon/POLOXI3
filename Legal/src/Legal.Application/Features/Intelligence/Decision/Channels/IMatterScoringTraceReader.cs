namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// IMatterScoringTraceReader — read-only access to a matter's latest Wide execution scoring trace.
//
// Returns the authoritative hierarchy levels (L1..Ln), named branches/candidates, and the exact
// persisted scoring values from POLOXI.Legal_Wide* for the matter's most recent Wide execution.
// Fail-soft: returns MatterScoringTraceReadModel.Empty(matterId) when the matter has no Wide
// execution. POLOXI Core remains the sole scorer; this only explains the persisted inputs.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public interface IMatterScoringTraceReader
{
    Task<MatterScoringTraceReadModel> GetLatestForMatterAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);
}
