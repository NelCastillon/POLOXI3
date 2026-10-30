using System.Text.Json;

namespace Legal.Application;

// ── Shared vector math for semantic (embedding) similarity ───────────────────────────────────────
// Pure, deterministic, allocation-light helpers used wherever embedding cosine similarity is needed
// (document-passage hybrid retrieval, corpus→branch semantic attachment, etc.). Centralized here so
// the identical normalization/clamping is reused rather than re-implemented per call site.
public static class VectorMath
{
    // Parse a JSON-encoded float[] embedding; returns null for null/blank/malformed input so callers
    // can fail-soft to a non-semantic path instead of throwing.
    public static float[]? ParseEmbedding(string? embeddingJson)
    {
        if (string.IsNullOrWhiteSpace(embeddingJson))
            return null;
        try
        {
            var vector = JsonSerializer.Deserialize<float[]>(embeddingJson);
            return vector is { Length: > 0 } ? vector : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Cosine similarity mapped from the raw [-1,1] range to [0,1] so it composes with other 0..1
    // scores. Returns null when either vector is absent, empty, dimensionally mismatched, or has zero
    // magnitude, letting callers treat "no comparable vector" distinctly from "dissimilar".
    public static decimal? CosineSimilarity(float[]? left, float[]? right)
    {
        if (left is null || right is null || left.Length == 0 || left.Length != right.Length)
            return null;
        double dot = 0, leftMagnitude = 0, rightMagnitude = 0;
        for (var index = 0; index < left.Length; index++)
        {
            dot += (double)left[index] * right[index];
            leftMagnitude += (double)left[index] * left[index];
            rightMagnitude += (double)right[index] * right[index];
        }
        if (leftMagnitude <= 0 || rightMagnitude <= 0)
            return null;
        var cosine = dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
        return (decimal)Math.Clamp((cosine + 1d) / 2d, 0d, 1d);
    }
}
