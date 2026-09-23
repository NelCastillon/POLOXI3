namespace Legal.Application.Features.Intelligence.Orchestration;

// ── Phase 1 boundary contract (additive; not yet wired) ───────────────────────────────────────
// Owns the POLOXI Wide state machine. The orchestrator is authoritative over stage sequencing,
// branch lifecycle transitions, adaptive narrowing, candidate competition, and convergence — it
// drives a STATEFUL GRAPH, not a fixed linear pipeline.
//
// Phase 1 introduces this contract only. The current authoritative implementation remains
// IntelligenceWideService.SearchDynamicAsync; a concrete orchestrator will be introduced in a later
// phase and must reproduce the existing behavior verified by the Phase 0 golden-master baseline
// before it can replace the inline pipeline.
public interface IWideExecutionOrchestrator
{
    Task<WideSearchResponse> ExecuteAsync(
        WideSearchRequest request,
        CancellationToken cancellationToken);
}
