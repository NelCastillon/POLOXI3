using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Intelligence;

// Classifies the kind of legal authority a grounding query targets so retrieval can be routed to the
// authoritative source: judicial opinions to case-law providers, statutes/regulations to legislative
// and regulatory providers. Any = no specific authority (concept search) — query every source.
public enum LegalAuthorityKind
{
    Any=0,
    Case=1,
    Statute=2,
    Regulation=3
}

public sealed record LegalProviderRetrievalDiagnostic(
    string ProviderCode,
    bool Selected,
    string OutcomeCode,
    int RawResultCount,
    int ReturnedCount,
    string? Detail = null,
    // Optional retrieval-integration telemetry populated by the source adapters so a diagnostic
    // harness can distinguish provider configuration, query translation, HTTP/API access, response
    // parsing, and filtering failures without altering routing/reasoning. Left null when not applicable.
    string? NormalizedCitation = null,
    string? EndpointUrl = null,
    int? HttpStatus = null,
    IReadOnlyCollection<string>? SourceCourtIds = null);

public sealed record LegalProviderRetrievalResult(
    IReadOnlyCollection<WideExternalKnowledgeSnippet> Snippets,
    LegalProviderRetrievalDiagnostic Diagnostic);

public sealed record LegalRetrievalResult(
    IReadOnlyCollection<WideExternalKnowledgeSnippet> Snippets,
    IReadOnlyCollection<LegalProviderRetrievalDiagnostic> Providers);

public sealed record LegalProviderSearchRequest(
    string Query,
    LegalAuthorityKind AuthorityKind,
    LegalAuthorityScope? AuthorityScope);

// Live legal-source retrieval used to ground the Wide pipeline when the LEGAL search context is
// selected. Implementations retrieve from authoritative legal sources (e.g. CourtListener case law
// and GovInfo/eCFR statutes and regulations) and must be fail-soft: return an empty collection on
// any provider error so enterprise search never breaks. The optional authority kind lets callers
// route a specific extracted authority to the correct source instead of querying every source.
public interface ILegalRetriever
{
    Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> SearchAsync(string query,WideLegalGroundingConfiguration configuration,LegalAuthorityKind kind=LegalAuthorityKind.Any,CancellationToken cancellationToken=default);

    Task<LegalRetrievalResult> SearchScopedAsync(LegalProviderSearchRequest request,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default) =>
        SearchWithDiagnosticsAsync(request.Query,configuration,request.AuthorityKind,cancellationToken);

    async Task<LegalRetrievalResult> SearchWithDiagnosticsAsync(string query,WideLegalGroundingConfiguration configuration,LegalAuthorityKind kind=LegalAuthorityKind.Any,CancellationToken cancellationToken=default)
    {
        var snippets = await SearchAsync(query, configuration, kind, cancellationToken);
        return new(snippets, [new("AGGREGATE", true, snippets.Count > 0 ? "SUCCEEDED" : "NO_RESULTS", snippets.Count, snippets.Count)]);
    }
}
