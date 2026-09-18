namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-2 claim extraction abstraction (§14).
//
// An extractor (which may be an LLM) returns PROPOSALS only. It never produces authoritative claims;
// POLOXI registers, deduplicates (IClaimIdentityResolver), and owns the authoritative version.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

public sealed record ClaimExtractionContext
{
    public required Guid SessionId { get; init; }

    public Guid? MatterId { get; init; }

    // The generated text (interpretation / candidate explanation) to decompose into propositions.
    public required string SourceText { get; init; }

    public Guid? SourceBranchId { get; init; }
    public Guid? SourceCandidateId { get; init; }

    public string? ModelName { get; init; }
    public string? PromptRunId { get; init; }
}

public interface IClaimExtractor
{
    Task<IReadOnlyList<ClaimProposal>> ExtractAsync(
        ClaimExtractionContext context,
        CancellationToken cancellationToken);
}
