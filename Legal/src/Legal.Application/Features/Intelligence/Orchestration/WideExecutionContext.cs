using System.Diagnostics;

namespace Legal.Application.Features.Intelligence.Orchestration;

// ── Phase 1 boundary contract (additive; not yet wired into SearchDynamicAsync) ────────────────
// Ambient, mutable execution state threaded through POLOXI Wide stages. This models the STATEFUL
// GRAPH the pipeline already is — a shared context stages read and enrich — NOT a linear pipeline
// where one stage's output is the next stage's only input. Later phases (5, 11–13) implement stages
// against this contract; Phase 1 only introduces it so nothing here changes runtime behavior yet.
public sealed class WideExecutionContext
{
    public WideExecutionContext(WideSearchRequest request, WideConfiguration configuration, Stopwatch timer)
    {
        Request = request;
        Configuration = configuration;
        Timer = timer;
    }

    public WideSearchRequest Request { get; }

    public WideConfiguration Configuration { get; }

    // The single pipeline stopwatch — stages read elapsed time for budget decisions; they never
    // reset it. Mirrors the existing SearchDynamicAsync timer semantics.
    public Stopwatch Timer { get; }

    // Running count of LLM calls, matching the existing inline accounting. A shared counter is
    // deliberate: budget/convergence decisions depend on the aggregate, not per-stage totals.
    public int LlmCallCount { get; private set; }

    public int RecordLlmCall() => ++LlmCallCount;

    public void RecordLlmCalls(int count) => LlmCallCount += count;

    // Free-form correlation carried for logging/telemetry continuity across stages.
    public string CorrelationId => Request.CorrelationId;
}
