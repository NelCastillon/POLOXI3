namespace Legal.Application.Features.Intelligence.Decision.Core;

public sealed class DeterministicVerificationCandidatePreScreen : IVerificationCandidatePreScreen
{
    public VerificationCandidatePreScreenResult Evaluate(
        EvidenceVerificationRequest request,
        EvidenceSourceType sourceType)
    {
        var passageAvailable = !string.IsNullOrWhiteSpace(request.SourceText);
        if (!passageAvailable)
            return new(VerificationCandidatePreScreenDisposition.RejectIrrelevant,
                "PRESCREEN_NO_CANDIDATE_PASSAGE", 0, false);

        if (sourceType == EvidenceSourceType.Unknown && string.IsNullOrWhiteSpace(request.SourceRef))
            return new(VerificationCandidatePreScreenDisposition.RejectIrrelevant,
                "PRESCREEN_SOURCE_TYPE_UNKNOWN", 0, true);

        var propositionTerms = Terms(request.Proposition);
        var sourceTerms = Terms(request.SourceText!);
        var overlap = propositionTerms.Count == 0
            ? 0
            : propositionTerms.Count(sourceTerms.Contains) / (double)propositionTerms.Count;
        if (IsWrongJurisdiction(request.Jurisdiction, request.GoverningJurisdiction))
            return new(VerificationCandidatePreScreenDisposition.RejectIrrelevant,
                "PRESCREEN_JURISDICTION_MISMATCH", overlap, true);

        if (overlap > 0)
            return new(VerificationCandidatePreScreenDisposition.AdvanceToSemantic,
                "PRESCREEN_RELEVANCE_MATCH", overlap, true);

        return new(VerificationCandidatePreScreenDisposition.UncertainRequiresDeeperScreen,
            "PRESCREEN_RELEVANCE_UNCERTAIN", overlap, true);
    }

    private static bool IsWrongJurisdiction(string? source, string? governing)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(governing))
            return false;
        var sourceTerms = Terms(source);
        var governingTerms = Terms(governing);
        return sourceTerms.Count > 0 && governingTerms.Count > 0 && !sourceTerms.Overlaps(governingTerms);
    }

    private static HashSet<string> Terms(string value) => value
        .Split([' ', '\t', '\r', '\n', '.', ',', ';', ':', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
        .Where(term => term.Length >= 4)
        .Select(term => term.ToLowerInvariant())
        .ToHashSet(StringComparer.Ordinal);
}
