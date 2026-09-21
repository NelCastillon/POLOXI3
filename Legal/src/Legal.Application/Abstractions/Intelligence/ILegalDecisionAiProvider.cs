using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;

namespace Legal.Application.Abstractions.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Self-contained AI + retrieval abstractions for the POLOXI Legal Decision module. These are
// deliberately separate from the /legal/search IAiProvider / ILegalRetriever surfaces so the
// decision module owns its own architecture and can evolve independently. The LLM only proposes
// candidate semantics; POLOXI Core owns the authoritative decision state (§3).
// ─────────────────────────────────────────────────────────────────────────────────────────────

public interface ILegalDecisionAiProvider
{
    Task<DecisionAiResult> GenerateAsync(DecisionAiRequest request, CancellationToken cancellationToken = default);
}

public sealed record DecisionAiRequest(
    DecisionModelRouteDto Route,
    string FeatureCode,
    string SystemPrompt,
    string UserPrompt,
    string? OutputSchemaJson,
    string CorrelationId);

public sealed record DecisionAiResult(
    string Content,
    string? StructuredOutputJson,
    int InputTokenCount,
    int OutputTokenCount,
    string ProviderRequestId,
    TimeSpan Duration);

public interface ILegalDecisionRetriever
{
    // Decision-directed retrieval (§13): optimize expected decision change, not raw document relevance.
    Task<IReadOnlyCollection<DecisionRetrievedSource>> RetrieveAsync(DecisionRetrievalRequest request, CancellationToken cancellationToken = default);
}

public sealed record DecisionRetrievalRequest(string ContextCode, string Objective, int MaximumResults);

public sealed record DecisionRetrievedSource(string SourceRef, string Title, string Snippet)
{
    public EvidenceSourceType? SourceType { get; init; }
    public string? Jurisdiction { get; init; }
    public DateOnly? AuthorityDate { get; init; }
    public string? SourceProvider { get; init; }
    public string? SourceVersion { get; init; }
    public bool ProviderIdentityVerified { get; init; }
}
