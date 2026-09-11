namespace Legal.Application.Features.Intelligence.Orchestration;

// ── Phase 1 boundary contract (additive; not yet wired) ───────────────────────────────────────
// The result of a single stage execution. Encodes the pipeline's existing FAIL-SOFT semantics as a
// first-class value: advisory stages (grounding, ABV, challenge, information value) may fail without
// aborting the run. A skipped/failed stage carries a reason for the audit trail rather than throwing.
public sealed record StageResult<T>
{
    private StageResult(StageOutcome outcome, T? value, string? reasonCode, string? message)
    {
        Outcome = outcome;
        Value = value;
        ReasonCode = reasonCode;
        Message = message;
    }

    public StageOutcome Outcome { get; }

    public T? Value { get; }

    public string? ReasonCode { get; }

    public string? Message { get; }

    public bool IsSuccess => Outcome == StageOutcome.Completed;

    public static StageResult<T> Completed(T value) => new(StageOutcome.Completed, value, null, null);

    // Advisory stage did not run (gated off, preconditions unmet). Non-fatal by design.
    public static StageResult<T> Skipped(string reasonCode, string? message = null) =>
        new(StageOutcome.Skipped, default, reasonCode, message);

    // Advisory stage failed soft — the pipeline continues with whatever state already exists.
    public static StageResult<T> FailedSoft(string reasonCode, string? message = null) =>
        new(StageOutcome.FailedSoft, default, reasonCode, message);
}

public enum StageOutcome
{
    Completed,
    Skipped,
    FailedSoft,
}
