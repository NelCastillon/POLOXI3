namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// L2 relevance review batching by CHARACTER BUDGET (migration 0445).
//
// The previous implementation chunked the catalog by row count and then truncated the serialized
// prompt to a fixed budget, silently dropping the tail of the catalog (348/353 targets were never
// reviewed on a live matter). This helper sizes batches from the actual serialized line lengths so
// every line fits, verifies coverage of returned ids against submitted ids, and computes the retry
// set. Pure and unit-testable; it never truncates a line.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public static class L2ReviewBatcher
{
    public sealed record Batch<T>(IReadOnlyList<T> Items, int SerializedChars);

    // Splits ordered items into batches whose summed line length stays within budgetChars after
    // subtracting fixedOverheadChars (question + matter context + instructions). A single line longer than
    // the remaining budget still forms its own batch - nothing is ever dropped. maxItemsPerBatch is an
    // optional secondary cap (0 = none).
    public static IReadOnlyList<Batch<T>> Split<T>(IReadOnlyList<T> items, Func<T, string> serialize, int budgetChars, int fixedOverheadChars, int maxItemsPerBatch = 0)
    {
        var result = new List<Batch<T>>();
        if (items.Count == 0) return result;
        var available = Math.Max(500, budgetChars - Math.Max(0, fixedOverheadChars));
        var current = new List<T>();
        var currentChars = 0;
        foreach (var item in items)
        {
            var length = (serialize(item)?.Length ?? 0) + 1;
            var wouldOverflow = current.Count > 0 && (currentChars + length > available || (maxItemsPerBatch > 0 && current.Count >= maxItemsPerBatch));
            if (wouldOverflow)
            {
                result.Add(new Batch<T>(current.ToArray(), currentChars));
                current = new List<T>();
                currentChars = 0;
            }
            current.Add(item);
            currentChars += length;
        }
        if (current.Count > 0) result.Add(new Batch<T>(current.ToArray(), currentChars));
        return result;
    }

    // Coverage check: ids that were submitted but did not come back with a usable score.
    public static IReadOnlyList<string> Missing(IEnumerable<string> submitted, IReadOnlyCollection<string> returned, StringComparer? comparer = null)
    {
        var cmp = comparer ?? StringComparer.OrdinalIgnoreCase;
        var set = new HashSet<string>(returned, cmp);
        return submitted.Where(id => !set.Contains(id)).Distinct(cmp).ToArray();
    }
}
