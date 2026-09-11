namespace Legal.Application.Features.Intelligence.Science;

// ── Persistent Elimination Ledger (P0 #4) ────────────────────────────────────────────────────────────
// Negative knowledge is one of POLOXI's most valuable research outputs and must not be buried under the
// newest candidate. The ledger records what has been shown NOT to work (blocked routes, refuted
// implications, non-reductions, rediscoveries) so the engine does not re-propose a closely related route
// under slightly different language across parent reopen / deepening cycles.
//
// It is deterministic and additive: entries are appended when an obligation is refuted, a claim's cited
// theorem fails to establish its implication, a reduction gate fails, or prior art is detected. It is
// designed to be persisted and rehydrated across runs so accumulated eliminations survive.
public sealed record EliminationLedger
{
    public IReadOnlyList<EliminationEntry> Entries { get; init; } = [];

    // Append entries, de-duplicating on the normalized (Kind, Subject → Consequence) signature so the same
    // negative fact is never recorded twice even if re-derived in a later cycle.
    public EliminationLedger Append(IEnumerable<EliminationEntry> newEntries)
    {
        ArgumentNullException.ThrowIfNull(newEntries);
        var seen = new HashSet<string>(Entries.Select(Signature), StringComparer.OrdinalIgnoreCase);
        var merged = new List<EliminationEntry>(Entries);
        foreach (var entry in newEntries)
        {
            if (seen.Add(Signature(entry)))
            {
                merged.Add(entry);
            }
        }

        return this with { Entries = merged };
    }

    // Whether a proposed route is already known to be eliminated (same subject → consequence signature).
    public bool IsEliminated(EliminationKind kind, string subject, string? consequence = null) =>
        Entries.Any(e => e.Kind == kind
            && string.Equals(Norm(e.Subject), Norm(subject), StringComparison.OrdinalIgnoreCase)
            && string.Equals(Norm(e.Consequence), Norm(consequence), StringComparison.OrdinalIgnoreCase));

    private static string Signature(EliminationEntry e) => $"{e.Kind}|{Norm(e.Subject)}|{Norm(e.Consequence)}";

    private static string Norm(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

// One recorded negative fact. Subject is what was tried; Consequence (optional) is what it failed to yield.
// Example: Subject="finite energy", Consequence="global regularity Q" — "finite energy ⇏ Q".
public sealed record EliminationEntry
{
    public required EliminationKind Kind { get; init; }

    public required string Subject { get; init; }

    public string? Consequence { get; init; }

    // Why it was eliminated (refutation witness, undischarged hypothesis, non-reduction, prior art, …).
    public required string Reason { get; init; }

    public string? CorrelationId { get; init; }

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public enum EliminationKind
{
    RefutedObligation,
    UnsupportedTheoremClaim,
    FailedReduction,
    BlockedStrategy,
    Rediscovery,
    Counterexample,
}
