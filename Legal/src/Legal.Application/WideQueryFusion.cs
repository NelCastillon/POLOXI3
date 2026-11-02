namespace Legal.Application;

/// <summary>
/// Pure, deterministic composition of a continuation (clarification / free-text follow-up) query.
/// A follow-up answer is fused onto the parent's original question as an added constraint so the
/// POLOXI pipeline reweights the existing interpretations instead of restarting blind on the bare
/// follow-up text. Extracted as a seam so the exact fusion contract can be unit-tested without the
/// full Wide pipeline.
/// </summary>
public static class WideQueryFusion
{
    /// <summary>
    /// Collapses runs of whitespace and trims, matching the Wide service query normalization.
    /// </summary>
    public static string NormalizeQuery(string query)
        => string.Join(' ', (query ?? string.Empty).Trim().Split((char[]?)null, System.StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Fuses a clarification/follow-up answer onto the original query.
    /// When <paramref name="clarificationAnswer"/> is null/whitespace the original query is returned
    /// unchanged. When a <paramref name="clarificationTarget"/> is supplied it prefixes the constraint
    /// (e.g. <c>target: answer</c>). The result is <c>"{original} ({constraint})"</c>.
    /// </summary>
    public static string Compose(string originalQuery, string? clarificationTarget, string? clarificationAnswer)
    {
        var baseQuery = NormalizeQuery(originalQuery);
        if (string.IsNullOrWhiteSpace(clarificationAnswer))
            return baseQuery;

        var constraint = string.IsNullOrWhiteSpace(clarificationTarget)
            ? NormalizeQuery(clarificationAnswer)
            : $"{clarificationTarget.Trim()}: {NormalizeQuery(clarificationAnswer)}";

        return $"{baseQuery} ({constraint})";
    }
}
