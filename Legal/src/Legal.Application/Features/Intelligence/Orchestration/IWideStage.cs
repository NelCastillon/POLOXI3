namespace Legal.Application.Features.Intelligence.Orchestration;

// ── Phase 1 boundary contract (additive; not yet wired) ───────────────────────────────────────
// A discrete, addressable unit of POLOXI Wide work (e.g. query-contract extraction, hierarchy
// proposal, grounding, information value, convergence). A stage reads/enriches the shared
// WideExecutionContext and returns a fail-soft StageResult.
//
// DESIGN INVARIANT: stages are NOT meant to be run in a blind sequential foreach. The orchestrator
// (see IWideExecutionOrchestrator) owns a stateful graph — it decides which stage runs next based on
// current context (uncertainty, information value, convergence, budget). This interface only defines
// the unit of work; it deliberately does not imply ordering.
public interface IWideStage<TInput, TOutput>
{
    // Stable identifier for telemetry, audit, and routing decisions.
    string StageCode { get; }

    Task<StageResult<TOutput>> ExecuteAsync(
        TInput input,
        WideExecutionContext context,
        CancellationToken cancellationToken);
}
