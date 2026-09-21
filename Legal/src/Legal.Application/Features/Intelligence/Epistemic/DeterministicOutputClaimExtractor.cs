using System.Text.RegularExpressions;

namespace Legal.Application.Features.Intelligence.Epistemic;

public sealed class DeterministicOutputClaimExtractor : IClaimExtractor
{
    private static readonly char[] SentenceTerminators = ['.', '!', '?'];

    public Task<IReadOnlyList<ClaimProposal>> ExtractAsync(
        ClaimExtractionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(context.SourceText))
            return Task.FromResult<IReadOnlyList<ClaimProposal>>([]);

        var claims = context.SourceText
            .Split(SentenceTerminators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Clean)
            .Where(IsMaterialAssertion)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((text, index) => new ClaimProposal
            {
                ClaimKey = $"OUTPUT:{context.SessionId:N}:{index + 1}",
                Text = text,
                ClaimType = ClaimType.Interpretive,
                Origin = ClaimOrigin.LlmGenerated,
                SourceBranchId = context.SourceBranchId,
                SourceCandidateId = context.SourceCandidateId,
                BranchIds = context.SourceBranchId is { } branchId ? [branchId] : [],
                CandidateIds = context.SourceCandidateId is { } candidateId ? [candidateId] : [],
                ProposedMateriality = 1m,
                ProposedDecisionImpact = 1m,
                ProposedEssential = true,
                ProposedByModel = context.ModelName,
                PromptRunId = context.PromptRunId,
            })
            .ToArray();

        return Task.FromResult<IReadOnlyList<ClaimProposal>>(claims);
    }

    private static string Clean(string value) =>
        Regex.Replace(value.ReplaceLineEndings(" "), @"\s+", " ").Trim(' ', '-', '•', '*', '#', ':');

    private static bool IsMaterialAssertion(string value)
    {
        if (value.Length < 12)
            return false;

        if (value.EndsWith(':') || value.StartsWith("Note:", StringComparison.OrdinalIgnoreCase))
            return false;

        return value.Any(char.IsLetter)
            && value.Contains(' ')
            && !value.Equals("Insufficient information", StringComparison.OrdinalIgnoreCase);
    }
}
