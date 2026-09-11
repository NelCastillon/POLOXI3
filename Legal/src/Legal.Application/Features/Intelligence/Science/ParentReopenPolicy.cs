namespace Legal.Application.Features.Intelligence.Science;

// ── Automatic REOPEN_PARENT on repeated non-reduction (P1 #parent-reopening) ─────────────────────────
// The Navier–Stokes run kept transforming the representation (Q → Q_i → taxonomy → axes) because each
// descendant failed to reduce the proof obligation. POLOXI should recognise this pattern and stop
// endlessly deepening: when a run of consecutive descendants all fail to reduce (RepresentationChange,
// WeakReduction, Circular, or a refuted/blocked outcome), the engine emits REOPEN_PARENT rather than
// deepening further. This fits the existing adaptive-narrowing states (DEEPEN/DORMANT/BLOCK/REOPEN); it
// adds no new reasoning architecture.
public sealed class ParentReopenPolicy
{
    private readonly int _nonReductionThreshold;

    public ParentReopenPolicy(int nonReductionThreshold = 3)
    {
        _nonReductionThreshold = Math.Max(1, nonReductionThreshold);
    }

    // Given the recent descendant reduction verdicts (most-recent-last), decide the next narrowing action.
    // A trailing run of >= threshold non-genuine reductions triggers REOPEN_PARENT with reason
    // repeated_nonreduction; otherwise the engine may DEEPEN.
    public NarrowingDecision Decide(IReadOnlyList<ReductionVerdict> recentDescendantVerdicts)
    {
        ArgumentNullException.ThrowIfNull(recentDescendantVerdicts);

        var trailingNonReductions = 0;
        for (var i = recentDescendantVerdicts.Count - 1; i >= 0; i--)
        {
            if (recentDescendantVerdicts[i] == ReductionVerdict.GenuineReduction)
            {
                break;
            }

            trailingNonReductions++;
        }

        if (trailingNonReductions >= _nonReductionThreshold)
        {
            return new NarrowingDecision
            {
                Action = NarrowingAction.ReopenParent,
                Reason = "repeated_nonreduction",
                TrailingNonReductions = trailingNonReductions,
            };
        }

        return new NarrowingDecision
        {
            Action = NarrowingAction.Deepen,
            Reason = trailingNonReductions == 0 ? "reduction_progress" : "within_tolerance",
            TrailingNonReductions = trailingNonReductions,
        };
    }
}

public sealed record NarrowingDecision
{
    public NarrowingAction Action { get; init; }

    public required string Reason { get; init; }

    public int TrailingNonReductions { get; init; }
}

public enum NarrowingAction
{
    Deepen,
    ReopenParent,
    Block,
    Dormant,
}
