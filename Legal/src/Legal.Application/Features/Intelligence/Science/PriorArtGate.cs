namespace Legal.Application.Features.Intelligence.Science;

// ── Verified-theorem → claim mapping + prior-art / novelty gate (P0 #3, P1 #novelty) ─────────────────
// Two failures observed in the Navier–Stokes run:
//   1. A known theorem (CKN) was invoked to support an EXCLUSION it did not actually establish.
//   2. Known results were rediscovered and treated like new candidate insights.
// This deterministic gate enforces that a claim citing a theorem must carry an explicit
//   theorem → hypotheses → implication → claim
// mapping whose hypotheses are all discharged (each mapped to a VERIFIED/CONDITIONAL obligation or an
// assumption node), and it flags claims whose statement matches a known prior-art result so novelty is
// never asserted for something already known.
public sealed class PriorArtGate
{
    // A record of a known theorem the solution may cite. TheoremId links from MathClaim.CitedTheoremIds.
    public sealed record KnownTheorem
    {
        public required string TheoremId { get; init; }
        public required string Name { get; init; }
        // The hypotheses the theorem REQUIRES before its conclusion may be used.
        public IReadOnlyList<string> RequiredHypotheses { get; init; } = [];
        // What the theorem actually concludes — used to check the claimed implication is really established.
        public required string Conclusion { get; init; }
    }

    // Evaluate every claim that cites a theorem. Returns one assessment per cited-theorem claim.
    public IReadOnlyList<TheoremMappingResult> EvaluateTheoremMappings(
        ProofDerivationState state,
        IReadOnlyDictionary<string, KnownTheorem> knownTheorems)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(knownTheorems);

        var obligationsById = state.Obligations
            .Where(o => !string.IsNullOrWhiteSpace(o.ObligationId))
            .GroupBy(o => o.ObligationId)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var dischargedStatements = new HashSet<string>(
            state.Obligations
                .Where(o => o.Status is ObligationStatus.Verified or ObligationStatus.Conditional)
                .Select(o => Normalize(o.Statement))
                .Concat(state.Nodes
                    .Where(n => n.NodeType is ProofNodeType.Assumption or ProofNodeType.Axiom or ProofNodeType.Definition)
                    .Select(n => Normalize(n.Statement))),
            StringComparer.Ordinal);

        var results = new List<TheoremMappingResult>();
        foreach (var claim in state.Claims.Where(c => c.CitedTheoremIds.Count > 0))
        {
            foreach (var theoremId in claim.CitedTheoremIds)
            {
                if (!knownTheorems.TryGetValue(theoremId, out var theorem))
                {
                    results.Add(new TheoremMappingResult
                    {
                        ClaimId = claim.ClaimId,
                        TheoremId = theoremId,
                        IsValidMapping = false,
                        Warning = $"Claim '{claim.Statement}' cites unknown theorem '{theoremId}'; citation cannot support the claim.",
                    });
                    continue;
                }

                var undischarged = theorem.RequiredHypotheses
                    .Where(h => !dischargedStatements.Contains(Normalize(h)))
                    .ToArray();

                var essentialResolved = claim.EssentialObligationIds
                    .Select(id => obligationsById.TryGetValue(id, out var o) ? o : null)
                    .Where(o => o is not null)
                    .All(o => o!.Status is ObligationStatus.Verified or ObligationStatus.Conditional);

                var valid = undischarged.Length == 0 && essentialResolved;
                results.Add(new TheoremMappingResult
                {
                    ClaimId = claim.ClaimId,
                    TheoremId = theoremId,
                    IsValidMapping = valid,
                    UndischargedHypotheses = undischarged,
                    Warning = valid
                        ? null
                        : undischarged.Length > 0
                            ? $"Claim '{claim.Statement}' cites {theorem.Name} but its required hypotheses are not discharged: {string.Join("; ", undischarged)}."
                            : $"Claim '{claim.Statement}' cites {theorem.Name} but its essential obligations are not verified; the implication is not established.",
                });
            }
        }

        return results;
    }

    // Flag any candidate/claim whose normalized statement matches a known prior-art result, so it is not
    // presented as a novel discovery. Returns the offending candidate ids with the matched prior art.
    public IReadOnlyList<PriorArtMatch> DetectRediscovery(
        ProofDerivationState state,
        IReadOnlyCollection<string> knownResults)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(knownResults);

        var known = knownResults.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        var matches = new List<PriorArtMatch>();
        foreach (var candidate in state.Candidates)
        {
            var name = Normalize(candidate.Name);
            var description = Normalize(candidate.Description ?? string.Empty);
            var hit = known.FirstOrDefault(k => k.Length > 0 && (name.Contains(k, StringComparison.Ordinal) || description.Contains(k, StringComparison.Ordinal)));
            if (hit is not null)
            {
                matches.Add(new PriorArtMatch
                {
                    CandidateId = candidate.Id,
                    CandidateName = candidate.Name,
                    MatchedPriorArt = hit,
                    Warning = $"Candidate '{candidate.Name}' matches known prior art; it may be a rediscovery rather than a novel result.",
                });
            }
        }

        return matches;
    }

    private static string Normalize(string value) =>
        new string((value ?? string.Empty).Trim().ToLowerInvariant().Where(c => !char.IsWhiteSpace(c) || c == ' ').ToArray());
}

public sealed record TheoremMappingResult
{
    public required string ClaimId { get; init; }
    public required string TheoremId { get; init; }
    public bool IsValidMapping { get; init; }
    public IReadOnlyList<string> UndischargedHypotheses { get; init; } = [];
    public string? Warning { get; init; }
}

public sealed record PriorArtMatch
{
    public required string CandidateId { get; init; }
    public required string CandidateName { get; init; }
    public required string MatchedPriorArt { get; init; }
    public string? Warning { get; init; }
}
