using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Intelligence;

// Aggregates the legal source adapters (CourtListener case law + GovInfo/eCFR statutes and
// regulations + Cornell LII UCC/U.S. Code/CFR secondary source) behind the single ILegalRetriever
// contract consumed by the Wide pipeline.
// Sources run in parallel and fail-soft: one source failing never discards the other's snippets.
// The authority kind routes a specific extracted authority to the correct source: judicial opinions
// to CourtListener, statutes/regulations to GovInfo/eCFR and Cornell LII. Any (concept search) queries all.
public sealed class LegalRetriever(ICourtListenerLegalSource courtListener,IGovInfoLegalSource govInfo,ICornellLiiLegalSource cornellLii,ILogger<LegalRetriever> logger):ILegalRetriever
{
    public async Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> SearchAsync(string query,WideLegalGroundingConfiguration configuration,LegalAuthorityKind kind=LegalAuthorityKind.Any,CancellationToken cancellationToken=default)
    {
        if(!configuration.Enabled||string.IsNullOrWhiteSpace(query))return [];

        var queryCourtListener=kind is LegalAuthorityKind.Any or LegalAuthorityKind.Case;
        var queryGovInfo=kind is LegalAuthorityKind.Any or LegalAuthorityKind.Statute or LegalAuthorityKind.Regulation;
        // Cornell LII hosts the UCC (state statutory law), the U.S. Code, and the CFR, so it serves
        // the same statute/regulation routing as GovInfo/eCFR (and Any concept searches).
        var queryCornellLii=kind is LegalAuthorityKind.Any or LegalAuthorityKind.Statute or LegalAuthorityKind.Regulation;
        // STAGE 2 (provider routing): record which sources this authority kind is routed to so a
        // mis-routed authority (e.g. a Case sent only to GovInfo) is visible in the trace.
        logger.LogInformation("LEGAL-TRACE stage=2-routing kind={Kind} courtListener={CourtListener} govInfo={GovInfo} cornellLii={CornellLii} query=\"{Query}\"",kind,queryCourtListener,queryGovInfo,queryCornellLii,query);

        var courtListenerTask=queryCourtListener?courtListener.SearchAsync(query,configuration,cancellationToken):Task.FromResult<IReadOnlyCollection<WideExternalKnowledgeSnippet>>([]);
        var govInfoTask=queryGovInfo?govInfo.SearchAsync(query,configuration,cancellationToken):Task.FromResult<IReadOnlyCollection<WideExternalKnowledgeSnippet>>([]);
        var cornellLiiTask=queryCornellLii?cornellLii.SearchAsync(query,configuration,cancellationToken):Task.FromResult<IReadOnlyCollection<WideExternalKnowledgeSnippet>>([]);
        await Task.WhenAll(courtListenerTask,govInfoTask,cornellLiiTask);

        var merged=courtListenerTask.Result
            .Concat(govInfoTask.Result)
            .Concat(cornellLiiTask.Result)
            .DistinctBy(snippet=>$"{snippet.Query}\u0001{snippet.Url}",StringComparer.OrdinalIgnoreCase)
            .ToList();
        logger.LogInformation("LEGAL-TRACE stage=2-routing-result kind={Kind} courtListenerResults={CourtListenerCount} govInfoResults={GovInfoCount} cornellLiiResults={CornellLiiCount} merged={MergedCount}",kind,courtListenerTask.Result.Count,govInfoTask.Result.Count,cornellLiiTask.Result.Count,merged.Count);
        return merged;
    }
}
