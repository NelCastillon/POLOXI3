using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Microsoft.Extensions.Logging;

namespace Legal.Application;

// ── Optional semantic (embedding) corpus-proposition → branch attachment ─────────────────────────
// Builds a fail-soft similarity lookup used by WideCorpusEvidenceProjection as an OR path beside the
// deterministic lexical overlap gate: a paraphrased proposition the token gate misses can still attach
// when its embedding cosine clears the DB-seeded threshold. This is OFF by default and intentionally
// metered — it issues embedding calls through the governed AI provider router only when the tenant has
// enabled it. ANY failure (disabled flag, empty graph, embedding error, dimension mismatch,
// cancellation of the broader run) degrades silently to lexical-only behavior: the returned lookup is
// null, which the projection treats exactly like the pre-feature code path.
internal static class WideCorpusSemanticAttachment
{
    // Must match the passage-embedding feature code so the same embedding deployment/model is routed.
    private const string EmbeddingFeatureCode = "LEGAL_DOCUMENT_PASSAGE_EMBEDDING";

    // Build a (propositionId, branchId) → cosine lookup for the matter-corpus graph, or null when the
    // feature is disabled or no semantic signal could be produced. Never throws for non-cancellation
    // failures; callers keep the lexical-only projection path on null.
    public static async Task<Func<Guid, Guid, decimal?>?> BuildAsync(
        IAiProviderRouter aiProviderRouter,
        ILogger logger,
        Guid tenantId,
        string correlationId,
        WideConfiguration configuration,
        LegalMatterEvidenceGraphDto? graph,
        IReadOnlyCollection<WideBranchRecord> branches,
        CancellationToken cancellationToken)
    {
        if (!configuration.EnableCorpusSemanticAttachment)
            return null;
        if (graph?.Propositions is not { Count: > 0 } propositions || branches.Count == 0)
            return null;

        // Stable, de-duplicated input ordering so returned embeddings map back by index. Propositions
        // first, then branches (branch text = DisplayName + Interpretation, same signal the lexical gate
        // tokenizes). Blank texts are skipped; their ids simply never resolve a similarity (lexical-only).
        var propTexts = new List<(Guid Id, string Text)>();
        foreach (var proposition in propositions)
        {
            if (!string.IsNullOrWhiteSpace(proposition.PropositionText))
                propTexts.Add((proposition.LegalFactPropositionId, proposition.PropositionText.Trim()));
        }
        var branchTexts = new List<(Guid Id, string Text)>();
        foreach (var branch in branches)
        {
            var text = $"{branch.DisplayName} {branch.Interpretation}".Trim();
            if (!string.IsNullOrWhiteSpace(text))
                branchTexts.Add((branch.WideBranchId, text));
        }
        if (propTexts.Count == 0 || branchTexts.Count == 0)
            return null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inputs = new List<string>(propTexts.Count + branchTexts.Count);
            inputs.AddRange(propTexts.Select(item => item.Text));
            inputs.AddRange(branchTexts.Select(item => item.Text));

            var result = await aiProviderRouter.CreateEmbeddingAsync(
                tenantId, EmbeddingFeatureCode, inputs, correlationId, cancellationToken);

            var vectors = result.Embeddings.Select(embedding => embedding.ToArray()).ToArray();
            if (vectors.Length != inputs.Count)
            {
                logger.LogWarning("LEGAL-TRACE stage=corpus-semantic tenantId={TenantId} embeddingCount={Count} expected={Expected}; falling back to lexical-only", tenantId, vectors.Length, inputs.Count);
                return null;
            }

            var propVectors = new Dictionary<Guid, float[]>(propTexts.Count);
            for (var i = 0; i < propTexts.Count; i++)
                propVectors[propTexts[i].Id] = vectors[i];
            var branchVectors = new Dictionary<Guid, float[]>(branchTexts.Count);
            for (var i = 0; i < branchTexts.Count; i++)
                branchVectors[branchTexts[i].Id] = vectors[propTexts.Count + i];

            logger.LogInformation("LEGAL-TRACE stage=corpus-semantic tenantId={TenantId} propositions={Propositions} branches={Branches} embedded", tenantId, propVectors.Count, branchVectors.Count);

            return (propositionId, branchId) =>
            {
                if (propVectors.TryGetValue(propositionId, out var left) &&
                    branchVectors.TryGetValue(branchId, out var right))
                    return VectorMath.CosineSimilarity(left, right);
                return null;
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "LEGAL-TRACE stage=corpus-semantic tenantId={TenantId} embedding failed; falling back to lexical-only attachment", tenantId);
            return null;
        }
    }
}
