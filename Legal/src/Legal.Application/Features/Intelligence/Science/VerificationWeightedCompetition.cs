namespace Legal.Application.Features.Intelligence.Science;

// ── Verification-weighted candidate competition (additive; not yet wired) ──────────────────────────
// The POLOXI engine core for Mathematics. Proof strategies and counterexample (refutation) search
// compete as SIBLINGS: Prove(P) vs ∃x:¬P(x). Unlike the Research pack, the winner is not chosen by
// discovery confidence alone — VERIFICATION status dominates so a merely plausible strategy can never
// beat a deterministically verified one. This keeps discovery and verification strictly separated.
public sealed class VerificationWeightedCompetition
{
    // Verification status contributes far more weight than discovery confidence, so a verified candidate
    // outranks any number of merely promising ones.
    private static double VerificationWeight(VerificationStatus status) => status switch
    {
        VerificationStatus.Verified => 1.0,
        VerificationStatus.Conditional => 0.5,
        VerificationStatus.Unverified => 0.0,
        VerificationStatus.Refuted => -1.0,
        _ => 0.0,
    };

    // Composite score: verification dominates (weight 10), discovery confidence breaks ties (weight 1).
    public double Score(ScientificCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return (VerificationWeight(candidate.VerificationStatus) * 10d) + candidate.DiscoveryConfidence;
    }

    // Rank candidates for the current round. A refuted candidate always sinks; a verified counterexample
    // candidate wins outright because it disproves the conjecture.
    public IReadOnlyList<ScientificCandidate> Rank(IReadOnlyList<ScientificCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates
            .OrderByDescending(Score)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToArray();
    }

    // The leading candidate, or null when there are none.
    public ScientificCandidate? Leader(IReadOnlyList<ScientificCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates.Count == 0 ? null : Rank(candidates)[0];
    }

    // POLOXI next-best-action: which candidate should receive compute next? Prefer the highest-discovery
    // candidate that is still UNVERIFIED (most information to gain), because verifying it or refuting it
    // most advances the proof state. Verified/refuted candidates need no further compute.
    public ScientificCandidate? NextToInvestigate(IReadOnlyList<ScientificCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates
            .Where(c => c.VerificationStatus is VerificationStatus.Unverified or VerificationStatus.Conditional)
            .OrderByDescending(c => c.DiscoveryConfidence)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
