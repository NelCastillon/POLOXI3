namespace Legal.Application.Features.Intelligence.Decision.Core;

public sealed class DeterministicVerificationCandidatePreScreen : IVerificationCandidatePreScreen
{
    public VerificationCandidatePreScreenResult Evaluate(
        EvidenceVerificationRequest request,
        EvidenceSourceType sourceType)
    {
        var passageAvailable = !string.IsNullOrWhiteSpace(request.SourceText);
        if (!passageAvailable)
            return new(false, "PRESCREEN_NO_CANDIDATE_PASSAGE", 0, false);

        var propositionTerms = Terms(request.Proposition);
        var sourceTerms = Terms(request.SourceText!);
        var overlap = propositionTerms.Count == 0
            ? 0
            : propositionTerms.Count(sourceTerms.Contains) / (double)propositionTerms.Count;
        var sourceCompatible = sourceType != EvidenceSourceType.Unknown
            || !string.IsNullOrWhiteSpace(request.SourceRef);

        return new(
            sourceCompatible && overlap > 0,
            sourceCompatible ? "PRESCREEN_RELEVANCE" : "PRESCREEN_SOURCE_TYPE_UNKNOWN",
            overlap,
            true);
    }

    private static HashSet<string> Terms(string value) => value
        .Split([' ', '\t', '\r', '\n', '.', ',', ';', ':', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
        .Where(term => term.Length >= 4)
        .Select(term => term.ToLowerInvariant())
        .ToHashSet(StringComparer.Ordinal);
}
