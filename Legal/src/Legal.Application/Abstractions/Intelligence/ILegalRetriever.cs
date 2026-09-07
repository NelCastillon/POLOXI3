using Legal.Application.Features.Intelligence;

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

// Live legal-source retrieval used to ground the Wide pipeline when the LEGAL search context is
// selected. Implementations retrieve from authoritative legal sources (e.g. CourtListener case law
// and GovInfo/eCFR statutes and regulations) and must be fail-soft: return an empty collection on
// any provider error so enterprise search never breaks. The optional authority kind lets callers
// route a specific extracted authority to the correct source instead of querying every source.
public interface ILegalRetriever
{
    Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> SearchAsync(string query,WideLegalGroundingConfiguration configuration,LegalAuthorityKind kind=LegalAuthorityKind.Any,CancellationToken cancellationToken=default);
}
