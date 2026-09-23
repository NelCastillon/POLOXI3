using Legal.Application.Features.Intelligence;
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


    async Task<DecisionRetrievalResult> RetrieveWithDiagnosticsAsync(DecisionRetrievalRequest request, CancellationToken cancellationToken = default)
    {
        var sources = await RetrieveAsync(request, cancellationToken);
        return new(sources, sources.Count, 0, 0, []);
    }

    Task<LegalAuthorityRetrievalResult> RetrieveResearchNeedAsync(
        LegalResearchRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new LegalAuthorityRetrievalResult(
            new LegalSearchPlan(
                Guid.Empty,
                request.ResearchNeed.DecisionResearchNeedId,
                request.DecisionSessionId,
                request.TenantId,
                request.ResearchNeed.PropositionToResolve ?? string.Empty,
                request.Jurisdiction,
                request.AuthorityCutoffDate,
                []),
            request.ResearchNeed.DecisionResearchNeedId,
            LegalRetrievalOutcome.CoverageGap,
            [],
            []));
}

public sealed record DecisionRetrievalResult(
    IReadOnlyCollection<DecisionRetrievedSource> Sources,
    int RawResultCount,
    int AuthorityKindFilteredCount,
    int AuthorityDateFilteredCount,
    IReadOnlyCollection<LegalProviderRetrievalDiagnostic> Providers);

public sealed record DecisionRetrievalRequest(string ContextCode, string Objective, int MaximumResults)
{
    public Guid TenantId { get; init; }
    public string? Jurisdiction { get; init; }
    public IReadOnlyCollection<string> AuthorityKinds { get; init; } = [];
    public DateTime? AuthorityCutoffDate { get; init; }
}

public sealed record DecisionRetrievedSource(string SourceRef, string Title, string Snippet)
{
    public EvidenceSourceType? SourceType { get; init; }
    public string? Jurisdiction { get; init; }
    public DateOnly? AuthorityDate { get; init; }
    public string? SourceProvider { get; init; }
    public string? SourceVersion { get; init; }
    public bool ProviderIdentityVerified { get; init; }
    public Guid AtomicPropositionId { get; init; }
    public Guid LegalSearchPlanId { get; init; }
    public Guid? NormalizedAuthorityId { get; init; }
    public string? NormalizedAuthorityIdentity { get; init; }
    public string? PassageIdentity { get; init; }
    public decimal PropositionSelectionScore { get; init; }
    public int PropositionSelectionRank { get; init; }
}
