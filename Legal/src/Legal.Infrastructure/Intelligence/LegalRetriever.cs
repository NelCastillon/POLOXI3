using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Intelligence;

// Aggregates the legal source adapters (CourtListener case law + GovInfo/eCFR statutes and
// regulations + Cornell LII UCC/U.S. Code/CFR secondary source) behind the single ILegalRetriever
// contract consumed by the Wide pipeline.
// Sources run in parallel and fail-soft: one source failing never discards the other's snippets.
// The authority kind routes a specific extracted authority to the correct source: judicial opinions
// to CourtListener, statutes/regulations to GovInfo/eCFR and Cornell LII. Any (concept search) queries all.
public sealed class LegalRetriever(ICourtListenerLegalSource courtListener,IGovInfoLegalSource govInfo,ICornellLiiLegalSource cornellLii,IOfficialLegalAuthoritySource officialAuthority,ILogger<LegalRetriever> logger):ILegalRetriever
{
    public Task<LegalRetrievalResult> SearchScopedAsync(LegalProviderSearchRequest request,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default) =>
        SearchWithDiagnosticsAsync(request.Query,configuration,request.AuthorityKind,request.AuthorityScope,cancellationToken);

    public async Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> SearchAsync(string query,WideLegalGroundingConfiguration configuration,LegalAuthorityKind kind=LegalAuthorityKind.Any,CancellationToken cancellationToken=default)
        => (await SearchWithDiagnosticsAsync(query, configuration, kind, cancellationToken)).Snippets;

    public async Task<LegalRetrievalResult> SearchWithDiagnosticsAsync(string query,WideLegalGroundingConfiguration configuration,LegalAuthorityKind kind=LegalAuthorityKind.Any,CancellationToken cancellationToken=default)
        =>await SearchWithDiagnosticsAsync(query,configuration,kind,null,cancellationToken);

    private async Task<LegalRetrievalResult> SearchWithDiagnosticsAsync(string query,WideLegalGroundingConfiguration configuration,LegalAuthorityKind kind,LegalAuthorityScope? scope,CancellationToken cancellationToken)
    {
        if(!configuration.Enabled||string.IsNullOrWhiteSpace(query))
            return new([], [new("LEGAL_GROUNDING", false, configuration.Enabled ? "EMPTY_QUERY" : "DISABLED", 0, 0)]);

        var queryCourtListener=kind is LegalAuthorityKind.Any or LegalAuthorityKind.Case;
        var federalCoverage=scope is null
            ||scope.AuthorityRoleCode==LegalAuthorityRoles.FederalApplyingStateLaw
            ||scope.CourtSystem?.Contains("Federal",StringComparison.OrdinalIgnoreCase)==true
            ||scope.GoverningLaw?.Contains("Federal",StringComparison.OrdinalIgnoreCase)==true
            ||scope.GoverningLaw?.Contains("United States",StringComparison.OrdinalIgnoreCase)==true;
        var stateSettlement=scope?.IssueScopeCode==LegalAuthorityIssueScopes.SettlementEnforcement&&!federalCoverage;
        var queryGovInfo=!stateSettlement&&federalCoverage
            &&kind is LegalAuthorityKind.Any or LegalAuthorityKind.Statute or LegalAuthorityKind.Regulation;
        // Cornell LII hosts the UCC (state statutory law), the U.S. Code, and the CFR, so it serves
        // the same statute/regulation routing as GovInfo/eCFR (and Any concept searches).
        var queryCornellLii=!stateSettlement&&kind is LegalAuthorityKind.Any or LegalAuthorityKind.Statute or LegalAuthorityKind.Regulation;
        var queryOfficialAuthority=kind is LegalAuthorityKind.Any or LegalAuthorityKind.Statute or LegalAuthorityKind.Regulation;
        // STAGE 2 (provider routing): record which sources this authority kind is routed to so a
        // mis-routed authority (e.g. a Case sent only to GovInfo) is visible in the trace.
        logger.LogInformation("LEGAL-TRACE stage=2-routing kind={Kind} issueScope={IssueScope} federalCoverage={FederalCoverage} courtListener={CourtListener} govInfo={GovInfo} cornellLii={CornellLii} query=\"{Query}\"",kind,scope?.IssueScopeCode,federalCoverage,queryCourtListener,queryGovInfo,queryCornellLii,query);

        var officialAuthorityResult=queryOfficialAuthority
            ?await officialAuthority.SearchAsync(query,configuration,cancellationToken)
            :Skipped("OFFICIAL_AUTHORITY");
        if(officialAuthorityResult.Snippets.Count>0)
        {
            logger.LogInformation("LEGAL-TRACE stage=2-exact-authority-result provider={Provider} outcome={Outcome} results={Count}",officialAuthorityResult.Diagnostic.ProviderCode,officialAuthorityResult.Diagnostic.OutcomeCode,officialAuthorityResult.Snippets.Count);
            return new(officialAuthorityResult.Snippets,
            [
                officialAuthorityResult.Diagnostic,
                new("COURTLISTENER",false,"SKIPPED_EXACT_AUTHORITY_RESOLVED",0,0),
                new("GOVINFO_ECFR",false,"SKIPPED_EXACT_AUTHORITY_RESOLVED",0,0),
                new("CORNELL_LII",false,"SKIPPED_EXACT_AUTHORITY_RESOLVED",0,0),
            ]);
        }

        var courtListenerTask=queryCourtListener?courtListener.SearchAsync(new LegalProviderSearchRequest(query,kind,scope),configuration,cancellationToken):Task.FromResult(Skipped("COURTLISTENER"));
        var govInfoTask=queryGovInfo?govInfo.SearchAsync(query,configuration,cancellationToken):Task.FromResult(CoverageSkipped("GOVINFO_ECFR",scope));
        var cornellLiiTask=queryCornellLii?cornellLii.SearchAsync(query,configuration,cancellationToken):Task.FromResult(CoverageSkipped("CORNELL_LII",scope));
        await Task.WhenAll(courtListenerTask,govInfoTask,cornellLiiTask);

        var merged=courtListenerTask.Result.Snippets
            .Concat(govInfoTask.Result.Snippets)
            .Concat(cornellLiiTask.Result.Snippets)
            .DistinctBy(snippet=>$"{snippet.Query}\u0001{snippet.Url}",StringComparer.OrdinalIgnoreCase)
            .ToList();
        logger.LogInformation("LEGAL-TRACE stage=2-routing-result kind={Kind} courtListenerResults={CourtListenerCount} govInfoResults={GovInfoCount} cornellLiiResults={CornellLiiCount} merged={MergedCount}",kind,courtListenerTask.Result.Snippets.Count,govInfoTask.Result.Snippets.Count,cornellLiiTask.Result.Snippets.Count,merged.Count);
        var providers = new[]
        {
            courtListenerTask.Result.Diagnostic,
            govInfoTask.Result.Diagnostic,
            cornellLiiTask.Result.Diagnostic,
            officialAuthorityResult.Diagnostic,
        };
        return new(merged, providers);
    }

    private static LegalProviderRetrievalResult Skipped(string provider) =>
        new([],new(provider,false,"SKIPPED_BY_AUTHORITY_KIND",0,0));

    private static LegalProviderRetrievalResult CoverageSkipped(string provider,LegalAuthorityScope? scope) =>
        scope is null?Skipped(provider):new([],new(provider,false,"COVERAGE_GAP_SCOPE",0,0,
            $"{provider} does not cover {scope.IssueScopeCode} under {scope.GoverningLaw??"the resolved legal scope"}."));
}
