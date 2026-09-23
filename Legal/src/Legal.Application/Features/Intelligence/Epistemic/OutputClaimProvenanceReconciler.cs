namespace Legal.Application.Features.Intelligence.Epistemic;

public sealed class OutputClaimProvenanceReconciler : IOutputClaimProvenanceReconciler
{
    public IReadOnlyList<ClaimProposal> Reconcile(
        IReadOnlyList<ClaimProposal> extractedClaims,
        IReadOnlyList<ClaimProposition> authoritativePropositions,
        IReadOnlyList<ComposerClaimProvenance> composerProvenance)
    {
        var provenanceByKey = composerProvenance
            .GroupBy(p => p.ClaimKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return extractedClaims.Select(claim =>
        {
            provenanceByKey.TryGetValue(claim.ClaimKey, out var provenance);
            var proposition = ResolveProposition(claim, provenance, authoritativePropositions);
            if (proposition is null)
                return claim with
                {
                    IsMaterial = true,
                    MappingState = ClaimMappingState.Unmapped,
                    MappingReasonCode = "OUTPUT_CLAIM_UNMAPPED",
                };

            var withinScope = IsWithinScope(claim.Text, proposition.Text);
            return claim with
            {
                IsMaterial = true,
                SourcePropositionId = proposition.ClaimId,
                EvidenceAttachmentIds = provenance?.EvidenceAttachmentIds ?? [],
                DecisionEvidenceIds = provenance?.DecisionEvidenceIds
                    ?? proposition.SupportingEvidence.Where(e => e.EvidenceId.HasValue).Select(e => e.EvidenceId!.Value).Distinct().ToArray(),
                MappingState = withinScope ? ClaimMappingState.Mapped : ClaimMappingState.ScopeExceeded,
                MappingReasonCode = withinScope ? "OUTPUT_CLAIM_SCOPE_VALIDATED" : "OUTPUT_CLAIM_EXCEEDS_PROPOSITION_SCOPE",
            };
        }).ToArray();
    }

    private static ClaimProposition? ResolveProposition(
        ClaimProposal claim,
        ComposerClaimProvenance? provenance,
        IReadOnlyList<ClaimProposition> propositions)
    {
        if (provenance?.SourcePropositionId is { } id)
            return propositions.FirstOrDefault(p => p.ClaimId == id);

        return propositions
            .Select(p => new { Proposition = p, Score = Coverage(claim.Text, p.Text) })
            .Where(x => x.Score >= 0.70)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Proposition)
            .FirstOrDefault();
    }

    private static bool IsWithinScope(string claim, string proposition) => Coverage(claim, proposition) >= 0.70;

    private static double Coverage(string claim, string proposition)
    {
        var claimTerms = Terms(claim);
        var propositionTerms = Terms(proposition);
        return claimTerms.Count == 0 ? 0 : claimTerms.Count(propositionTerms.Contains) / (double)claimTerms.Count;
    }

    private static HashSet<string> Terms(string value) => value
        .Split([' ', '\t', '\r', '\n', '.', ',', ';', ':', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
        .Where(term => term.Length >= 4)
        .Select(term => term.ToLowerInvariant())
        .ToHashSet(StringComparer.Ordinal);
}