namespace Legal.Application.Features.Intelligence.Science;

// ── Claim ↔ Obligation certainty ceiling (P0 #1) ──────────────────────────────────────────────────
// The single most important epistemic guard: a claim may never be COMMUNICATED at a strength greater
// than the verification status of its essential obligations allows. Formally:
//
//     ReportedStrength(C) <= min over O in Essential(C) of AllowedStrength(Status(O))
//
// This is a SOFT ceiling (per product decision): the asserted strength is preserved on the claim, but
// when it outruns what its obligations support we emit a "claim outruns proof" warning that the composer
// and UI surface. Nothing here calls an LLM — it is pure deterministic policy over already-verified
// obligation statuses.
public sealed class ClaimCertaintyCeiling
{
    // Maps a deterministic obligation status to the strongest claim strength it can justify. An OPEN or
    // UNRESOLVED essential obligation caps a claim at HYPOTHESIS — it can still be ranked highly, but it
    // cannot be reported as verified/excluded/conditional.
    private static ClaimStrength AllowedStrength(ObligationStatus status) => status switch
    {
        ObligationStatus.Verified => ClaimStrength.Verified,
        ObligationStatus.Conditional => ClaimStrength.Conditional,
        ObligationStatus.Refuted => ClaimStrength.Speculative,
        ObligationStatus.Open => ClaimStrength.Hypothesis,
        ObligationStatus.Unresolved => ClaimStrength.Hypothesis,
        _ => ClaimStrength.Hypothesis,
    };

    // Evaluate every claim against its essential obligations. Returns one assessment per claim; claims with
    // no essential obligations are ceiled at STRONGLY_SUGGESTED because nothing deterministic supports them.
    public IReadOnlyList<ClaimCeilingResult> Evaluate(ProofDerivationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var obligationsById = state.Obligations
            .Where(o => !string.IsNullOrWhiteSpace(o.ObligationId))
            .GroupBy(o => o.ObligationId)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var results = new List<ClaimCeilingResult>(state.Claims.Count);
        foreach (var claim in state.Claims)
        {
            var essential = claim.EssentialObligationIds
                .Select(id => obligationsById.TryGetValue(id, out var o) ? o : null)
                .Where(o => o is not null && o.IsEssential)
                .Select(o => o!)
                .ToArray();

            // No essential obligation resolved deterministically → the claim cannot exceed a hypothesis
            // backed only by discovery/self-consistency. STRONGLY_SUGGESTED is the strongest such label.
            var ceiling = essential.Length == 0
                ? ClaimStrength.StronglySuggested
                : essential.Select(o => AllowedStrength(o.Status)).Min();

            var reported = (ClaimStrength)Math.Min((int)claim.AssertedStrength, (int)ceiling);
            var outrunsProof = claim.AssertedStrength > ceiling;

            string? warning = null;
            if (outrunsProof)
            {
                var blocking = essential
                    .Where(o => AllowedStrength(o.Status) == ceiling)
                    .Select(o => o.ObligationId)
                    .FirstOrDefault();
                warning = essential.Length == 0
                    ? $"Claim '{claim.Statement}' is asserted as {claim.AssertedStrength} but has no essential obligation verified; reported as {reported}."
                    : $"Claim '{claim.Statement}' is asserted as {claim.AssertedStrength} but essential obligation {blocking} is only {ceiling}; reported as {reported}.";
            }

            results.Add(new ClaimCeilingResult
            {
                ClaimId = claim.ClaimId,
                Statement = claim.Statement,
                AssertedStrength = claim.AssertedStrength,
                CeilingStrength = ceiling,
                ReportedStrength = reported,
                ClaimOutrunsProof = outrunsProof,
                Warning = warning,
            });
        }

        return results;
    }

    // Convenience: the soft warnings only, for the composer/UI "claim outruns proof" surface.
    public IReadOnlyList<string> Warnings(ProofDerivationState state) =>
        Evaluate(state)
            .Where(r => r.ClaimOutrunsProof && r.Warning is not null)
            .Select(r => r.Warning!)
            .ToArray();
}

// The result of applying the certainty ceiling to a single claim. ReportedStrength is what the composer
// is allowed to communicate; ClaimOutrunsProof + Warning drive the soft "claim outruns proof" surface.
public sealed record ClaimCeilingResult
{
    public required string ClaimId { get; init; }

    public required string Statement { get; init; }

    public required ClaimStrength AssertedStrength { get; init; }

    public required ClaimStrength CeilingStrength { get; init; }

    public required ClaimStrength ReportedStrength { get; init; }

    public bool ClaimOutrunsProof { get; init; }

    public string? Warning { get; init; }
}
