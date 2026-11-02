namespace Legal.Application;

using Legal.Application.Features.Intelligence;

/// <summary>
/// Pure, deterministic follow-up heuristics shared by the Wide service and the /legal/search UI.
/// Extracted as a seam so the exact reset-phrase contract can be unit-tested without the full pipeline.
/// </summary>
public static class WideFollowUpHeuristics
{
    // Explicit thread-abandon phrases. A follow-up matching one of these starts a FRESH ROOT (no parent,
    // round 0) instead of being fused onto — or parented to — the prior thread. Kept intentionally
    // conservative to avoid false resets on normal narrowing follow-ups.
    private static readonly string[] ResetPhrases =
    [
        "disregard previous", "disregard the previous", "ignore previous", "ignore the previous",
        "forget previous", "forget the previous", "start over", "start a new", "new search",
        "new topic", "new question", "scratch that"
    ];

    /// <summary>
    /// True when the follow-up explicitly abandons the prior conversation thread.
    /// </summary>
    public static bool IsReset(string? followUpText)
    {
        if (string.IsNullOrWhiteSpace(followUpText)) return false;
        var lower = followUpText.Trim().ToLowerInvariant();
        // Standalone "reset" is matched as a whole word; the rest as substrings (they are phrases).
        if (lower == "reset") return true;
        return ResetPhrases.Any(phrase => lower.Contains(phrase));
    }

    /// <summary>
    /// Binding guard for conversation-turn results: when the proposal names a winner, ensure the primary
    /// result set actually leads with that entity. The model sometimes returns the correct winner for the new
    /// objective while leaving the items in the prior ordering; this promotes the winner-named item to
    /// rankNumber 1 (renumbering the rest) in the first result group so the headline and the ranked list can
    /// never visibly contradict each other.
    /// </summary>
    public static WideInterpretiveResultDto[] ReconcileWinner(WideInterpretiveResultDto[] results, string? winner)
    {
        if (string.IsNullOrWhiteSpace(winner) || results.Length == 0) return results;
        var primary = results[0];
        if (primary.Items.Count == 0) return results;
        var ordered = primary.Items.OrderBy(item => item.RankNumber).ToList();
        var winnerIndex = ordered.FindIndex(item => string.Equals(item.Name, winner, StringComparison.OrdinalIgnoreCase));
        // Nothing to do when the winner is absent (RESET/NARROW may legitimately drop it) or already leads.
        if (winnerIndex <= 0) return results;
        var winnerItem = ordered[winnerIndex];
        ordered.RemoveAt(winnerIndex);
        ordered.Insert(0, winnerItem);
        var renumbered = ordered.Select((item, index) => item with { RankNumber = index + 1 }).ToArray();
        results[0] = primary with { Items = renumbered };
        return results;
    }
}
