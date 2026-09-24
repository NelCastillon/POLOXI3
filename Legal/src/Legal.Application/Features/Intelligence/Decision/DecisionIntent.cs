namespace Legal.Application.Features.Intelligence.Decision;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// R3 — Decision Intent (decision ownership).
//
// A Matter carries several DISTINCT decision-role fields that must never collapse into one another:
//   • RequestedDisposition — what the USER wants (an objective / requested relief).
//   • CurrentOutcome       — a recorded/SUPPLIED assertion about what has happened (provenance-tagged).
//   • verified court order  — evidence of what was actually ordered, once obtained and verified by Core.
//   • candidate outcome     — a possible answer proposed for comparison during competition.
//   • final decision        — the result AUTHORIZED after Core evaluation + readiness checks.
//
// DecisionIntent decides whether a run should EVALUATE competing dispositions (the supplied
// CurrentOutcome is only one input, never the conclusion) or IMPLEMENT/DRAFT an already-specified
// disposition (the supplied disposition is the governing premise).
//
// Critical rule: on an EVALUATE run a supplied CurrentOutcome MUST NOT become a HARD constraint /
// fixed legal conclusion. It stays a SUPPLIED Matter assertion with provenance and competes like any
// other candidate outcome.
// ────────────────────────────────────────────────────────────────────────────────────────────────

public enum DecisionIntent
{
    // The task asks WHICH disposition should be reached — genuine competing outcome candidates must be
    // generated and competed. Supplied CurrentOutcome is an input, not the answer.
    Evaluate = 0,

    // The task asks to draft/implement an ALREADY-specified disposition — the supplied disposition is
    // the governing premise and no competition over the outcome is required.
    ImplementDraft = 1,
}

// Deterministic, zero-LLM guard that validates a proposed intent against explicit user intent,
// execution signals, and Matter provenance. Stage 0 may PROPOSE an intent from the query text, but
// this guard is authoritative: it never lets a supplied CurrentOutcome silently drive the run to a
// conclusion unless the user explicitly asked to implement/draft that outcome.
public static class DecisionIntentResolver
{
    // Verb signals in the ORIGINAL user question that unambiguously request drafting/implementing an
    // already-decided disposition (not asking which disposition to reach).
    private static readonly string[] ImplementDraftSignals =
    [
        "draft", "implement", "prepare the order", "write the order", "write the disposition",
        "generate the disposition", "produce the order", "memorialize", "reduce to writing",
    ];

    // Verb signals that unambiguously ask the system to CHOOSE / recommend a disposition.
    private static readonly string[] EvaluateSignals =
    [
        "which disposition", "what disposition", "should we", "should the court", "recommend",
        "evaluate", "assess", "what is the best", "what outcome", "decide whether", "advise",
    ];

    public readonly record struct Resolution(DecisionIntent Intent, string Reason, bool CurrentOutcomeIsHardConstraint);

    // Resolves the authoritative intent.
    //   proposedIntent  — the Stage 0 LLM proposal (may be null when unavailable).
    //   originalQuestion — the verbatim user question (deterministic override source).
    // Precedence: explicit user verb signals > Stage 0 proposal > safe default (Evaluate).
    // A supplied CurrentOutcome is treated as a HARD constraint ONLY on an ImplementDraft run.
    public static Resolution Resolve(DecisionIntent? proposedIntent, string? originalQuestion)
    {
        var text = (originalQuestion ?? string.Empty).ToLowerInvariant();

        var asksImplement = ImplementDraftSignals.Any(signal => text.Contains(signal, StringComparison.Ordinal));
        var asksEvaluate = EvaluateSignals.Any(signal => text.Contains(signal, StringComparison.Ordinal));

        // Explicit user intent wins. When BOTH appear (rare), evaluation is the safer, non-committal
        // choice because it never silently adopts a supplied outcome as the conclusion.
        if (asksEvaluate)
            return new(DecisionIntent.Evaluate, "User question explicitly asks which disposition to reach.", false);
        if (asksImplement)
            return new(DecisionIntent.ImplementDraft, "User question explicitly asks to draft/implement a specified disposition.", true);

        // No explicit signal — honor the Stage 0 proposal if present, but a proposed ImplementDraft
        // still only makes CurrentOutcome authoritative because the model read an already-specified
        // disposition; otherwise default to the safe evaluation path.
        return proposedIntent switch
        {
            DecisionIntent.ImplementDraft => new(DecisionIntent.ImplementDraft, "Stage 0 proposed an implement/draft task for a specified disposition.", true),
            DecisionIntent.Evaluate => new(DecisionIntent.Evaluate, "Stage 0 proposed an evaluation task.", false),
            _ => new(DecisionIntent.Evaluate, "No explicit intent signal; defaulting to evaluation so a supplied outcome is never adopted as the conclusion.", false),
        };
    }
}
