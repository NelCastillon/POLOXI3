using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Intelligence;

// Internal legal source adapter for CourtListener case law. Aggregated by LegalRetriever.
// Fail-soft: any provider/transport error returns an empty snippet collection.
public interface ICourtListenerLegalSource
{
    Task<LegalProviderRetrievalResult> SearchAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default);
    Task<LegalProviderRetrievalResult> SearchAsync(LegalProviderSearchRequest request,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default) =>
        SearchAsync(request.Query,configuration,cancellationToken);
}

// CourtListener v4 opinion search (https://www.courtlistener.com/api/rest/v4/search/).
// An API token is optional (anonymous access is heavily rate-limited); when present it is sent as a
// "Token <token>" Authorization header, per the CourtListener API contract.
public sealed class CourtListenerLegalRetriever(HttpClient httpClient,ILogger<CourtListenerLegalRetriever> logger):ICourtListenerLegalSource
{
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);

    public async Task<LegalProviderRetrievalResult> SearchAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default)
        =>await SearchCoreAsync(query,configuration,null,cancellationToken);

    public async Task<LegalProviderRetrievalResult> SearchAsync(LegalProviderSearchRequest request,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default)
    {
        var scope=request.AuthorityScope;
        if(scope is null)return new([],new("COURTLISTENER",true,"REQUIRED_AUTHORITY_SCOPE_MISSING",0,0));
        if(scope.AuthorityRoleCode==LegalAuthorityRoles.Persuasive)
            return await SearchCoreAsync(request.Query,configuration,null,cancellationToken);

        // Court-slug resolution is driven by the SOURCE COURT (the actual court/forum caption), not by
        // the governing-law sovereign. Sending a bare sovereign such as "California" to the courts
        // endpoint (as citation-expansion previously did) cannot resolve a provider-native court id.
        // Prefer the explicit source court, then the court/forum caption, and only fall back to the
        // governing-law jurisdiction when no court caption is available. ResolveCourtFilterAsync still
        // extracts the enclosing sovereign from a caption so state jurisdictions remain supported.
        var scopeValue=FirstNonEmpty(scope.SourceCourt,scope.CourtOrForum,scope.GoverningLaw);
        if(string.IsNullOrWhiteSpace(scopeValue))
            return new([],new("COURTLISTENER",true,
                scope.IssueScopeCode==LegalAuthorityIssueScopes.ProceduralLaw
                    ?"REQUIRED_COURT_OR_FORUM_MISSING":"REQUIRED_GOVERNING_LAW_MISSING",0,0));

        var eligibleCourts=await ResolveCourtFilterAsync(scopeValue,scope,configuration,cancellationToken);
        if(eligibleCourts.Count==0)
        {
            // A composite/free-text scope (e.g. the matter breadcrumb "United States - State · Delaware
            // · District Court") may not map to any provider-native court slug. Rather than collapsing
            // the whole round into RETRIEVAL_SCOPE_UNSUPPORTED (which upstream reports as
            // RETRIEVAL_NO_RESULTS and hard-stops the research loop), degrade gracefully to an
            // UNFILTERED case-law search so genuinely retrievable evidence is still returned. The
            // diagnostic detail preserves the unresolved scope for observability.
            logger.LogInformation(
                "LEGAL-TRACE stage=2-courtlistener-scope outcome=UNFILTERED_FALLBACK scope=\"{Scope}\"",scopeValue);
            var fallback=await SearchCoreAsync(request.Query,configuration,null,cancellationToken);
            var diagnostic=fallback.Diagnostic with
            {
                Detail=$"Court filter unresolved for '{scopeValue}'; used unfiltered case-law search. {fallback.Diagnostic.Detail}".Trim()
            };
            return fallback with { Diagnostic=diagnostic };
        }
        return await SearchCoreAsync(request.Query,configuration,eligibleCourts,cancellationToken);
    }

    private async Task<LegalProviderRetrievalResult> SearchCoreAsync(string query,WideLegalGroundingConfiguration configuration,IReadOnlyCollection<CourtListenerCourt>? eligibleCourts,CancellationToken cancellationToken)
    {
        if(!configuration.CourtListenerEnabled||string.IsNullOrWhiteSpace(query)||string.IsNullOrWhiteSpace(configuration.CourtListenerBaseUrl))
            return new([],new("COURTLISTENER",false,!configuration.CourtListenerEnabled?"DISABLED":"INVALID_REQUEST",0,0));
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));

            var baseUrl=NormalizeBaseUrl(configuration.CourtListenerBaseUrl);
            var sourceCourtIds=eligibleCourts is{Count:>0}
                ?eligibleCourts.Select(court=>court.Slug).Where(slug=>!string.IsNullOrWhiteSpace(slug)).Select(slug=>slug!).ToArray():[];
            var courtQuery=sourceCourtIds.Length>0
                ?$"&court={Uri.EscapeDataString(string.Join(',',sourceCourtIds))}":string.Empty;
            var url=$"{baseUrl}/api/rest/v4/search/?type=o&order_by=score%20desc&q={Uri.EscapeDataString(query)}{courtQuery}";
            // STAGE 3 (CourtListener query): log the exact clean query sent so we can confirm a named
            // authority (e.g. "Raffles v Wichelhaus") is searched, not the whole user prompt.
            logger.LogInformation("LEGAL-TRACE stage=3-courtlistener-query query=\"{Query}\" url={Url}",query,url);
            using var request=new HttpRequestMessage(HttpMethod.Get,url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            // CourtListener rejects requests without a User-Agent; send a realistic one so anonymous
            // (token-less) searches still return results instead of an error.
            request.Headers.TryAddWithoutValidation("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            if(!string.IsNullOrWhiteSpace(configuration.CourtListenerApiToken))
                request.Headers.TryAddWithoutValidation("Authorization",$"Token {configuration.CourtListenerApiToken.Trim()}");

            using var response=await httpClient.SendAsync(request,timeout.Token);
            if(!response.IsSuccessStatusCode)
            {
                logger.LogWarning("LEGAL-TRACE stage=4-courtlistener-response outcome=HTTP_ERROR status={StatusCode} query=\"{Query}\" url={RequestUrl}",(int)response.StatusCode,query,url);
                return new([],new("COURTLISTENER",true,"HTTP_ERROR",0,0,$"HTTP {(int)response.StatusCode}",EndpointUrl:url,HttpStatus:(int)response.StatusCode,SourceCourtIds:sourceCourtIds));
            }

            var payload=await response.Content.ReadFromJsonAsync<CourtListenerSearchResponse>(JsonOptions,timeout.Token);
            if(payload?.Results is not{Count:>0}results)
            {
                logger.LogInformation("LEGAL-TRACE stage=4-courtlistener-response outcome=NO_RESULTS status={StatusCode} query=\"{Query}\"",(int)response.StatusCode,query);
                return new([],new("COURTLISTENER",true,"NO_RESULTS",0,0,EndpointUrl:url,HttpStatus:(int)response.StatusCode,SourceCourtIds:sourceCourtIds));
            }

            var retrievedUtc=DateTime.UtcNow;
            var eligibleResults=eligibleCourts is{Count:>0}
                ?results.Where(result=>ResultMatchesEligibleCourt(result,eligibleCourts)).ToList()
                :results;
            var snippets=eligibleResults
                .Select(result=>new WideExternalKnowledgeSnippet(
                    query,
                    BuildTitle(result),
                    BuildUrl(baseUrl,result.AbsoluteUrl),
                    BuildSnippet(result),
                    0m,
                    retrievedUtc)
                {
                    AuthorityKind="CASE_LAW",
                    SourceProvider="COURTLISTENER",
                    SourceVersion="API_V4",
                    Jurisdiction=NormalizeText(result.Court),
                    ProviderIdentityVerified=true,
                })
                .Where(snippet=>!string.IsNullOrWhiteSpace(snippet.Snippet))
                .Take(configuration.MaximumSnippetsPerQuery)
                .ToList();
            // STAGE 4 (CourtListener response): log how many results came back and their titles so a
            // "relevant result returned but later rejected" state is distinguishable from "no results".
            logger.LogInformation("LEGAL-TRACE stage=4-courtlistener-response outcome=RESULTS status={StatusCode} rawResults={RawCount} usableSnippets={SnippetCount} query=\"{Query}\" titles=[{Titles}]",(int)response.StatusCode,results.Count,snippets.Count,query,string.Join(" | ",snippets.Take(5).Select(snippet=>snippet.Title)));
            var outcome=snippets.Count>0?"SUCCEEDED"
                :eligibleCourts is{Count:>0}&&results.Count>0&&eligibleResults.Count==0?"FILTER_REJECTED_SCOPE_MISMATCH":"PARSE_REJECTED";
            return new(snippets,new("COURTLISTENER",true,outcome,results.Count,snippets.Count,
                snippets.Count>0?null:outcome=="FILTER_REJECTED_SCOPE_MISMATCH"
                    ?"Provider results did not identify an eligible court from the enforced source scope."
                    :"Provider returned results, but no usable opinion passage could be extracted.",
                EndpointUrl:url,HttpStatus:(int)response.StatusCode,SourceCourtIds:sourceCourtIds));
        }
        catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,"LEGAL-TRACE stage=4-courtlistener-response outcome=EXCEPTION query=\"{Query}\"; continuing without this source.",query);
            return new([],new("COURTLISTENER",true,exception is OperationCanceledException?"TIMED_OUT":"FAILED",0,0,exception.GetType().Name));
        }
    }

    private async Task<IReadOnlyCollection<CourtListenerCourt>> ResolveCourtFilterAsync(
        string scopeValue,LegalAuthorityScope scope,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken)
    {
        if(!configuration.CourtListenerEnabled||string.IsNullOrWhiteSpace(configuration.CourtListenerBaseUrl))return [];
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));
            var baseUrl=NormalizeBaseUrl(configuration.CourtListenerBaseUrl);
            var federalLane=scope.AuthorityRoleCode==LegalAuthorityRoles.FederalApplyingStateLaw
                ||scope.CourtSystem?.Contains("Federal",StringComparison.OrdinalIgnoreCase)==true;
            var searchUrl=$"{baseUrl}/api/rest/v4/courts/?search={Uri.EscapeDataString(scopeValue)}";
            var courts=await FetchCourtsAsync(searchUrl,configuration,timeout.Token);
            var matches=SelectEligibleCourts(courts,scopeValue,scope,federalLane);
            if(matches.Count>0)return matches;

            var catalogUrl=$"{baseUrl}/api/rest/v4/courts/?page_size=1000";
            courts=await FetchCourtsAsync(catalogUrl,configuration,timeout.Token);
            matches=SelectEligibleCourts(courts,scopeValue,scope,federalLane);
            if(matches.Count>0)return matches;

            // Provider-native scope translation: the incoming value may be a court/forum caption that
            // CourtListener cannot resolve directly (e.g. "Superior Court of California, County of Los
            // Angeles"). Fall back to the enclosing US-state sovereign, which the provider DOES expose
            // as a jurisdiction, so a valid court filter is produced instead of SCOPE_UNSUPPORTED.
            var sovereign=LegalJurisdictionScope.ExtractSovereign(scopeValue);
            if(!string.IsNullOrWhiteSpace(sovereign)&&!sovereign.Equals(scopeValue,StringComparison.OrdinalIgnoreCase))
            {
                var sovereignUrl=$"{baseUrl}/api/rest/v4/courts/?search={Uri.EscapeDataString(sovereign)}";
                var sovereignCourts=await FetchCourtsAsync(sovereignUrl,configuration,timeout.Token);
                matches=SelectEligibleCourts(sovereignCourts,sovereign,scope,federalLane);
                if(matches.Count>0)return matches;
                var sovereignCatalog=SelectEligibleCourts(courts,sovereign,scope,federalLane);
                if(sovereignCatalog.Count>0)return sovereignCatalog;
            }

            // Provider-native FEDERAL scope translation: when no US-state sovereign resolves, the
            // caption may name a federal forum (e.g. "United States Court of Appeals for the Ninth
            // Circuit"). CourtListener exposes the circuit/appellate courts as jurisdictions, so
            // translate the caption to the federal jurisdiction term and resolve on the federal lane.
            var federal=LegalJurisdictionScope.ExtractFederalJurisdiction(scopeValue);
            if(!string.IsNullOrWhiteSpace(federal)&&!federal.Equals(scopeValue,StringComparison.OrdinalIgnoreCase))
            {
                var federalUrl=$"{baseUrl}/api/rest/v4/courts/?search={Uri.EscapeDataString(federal)}";
                var federalCourts=await FetchCourtsAsync(federalUrl,configuration,timeout.Token);
                matches=SelectEligibleCourts(federalCourts,federal,scope,federalLane:true);
                if(matches.Count>0)return matches;
                return SelectEligibleCourts(courts,federal,scope,federalLane:true);
            }

            // Provider-native BREADCRUMB scope translation: the incoming value may be a composite
            // display breadcrumb (e.g. "United States - State · Delaware · District Court") assembled
            // from separate court dimensions. CourtListener cannot resolve the whole breadcrumb, so
            // resolve each delimited segment independently (longest first, so "Delaware" resolves the
            // state jurisdiction) against the already-fetched catalog before reporting SCOPE_UNSUPPORTED.
            foreach(var segment in SplitScopeSegments(scopeValue))
            {
                var segmentMatches=SelectEligibleCourts(courts,segment,scope,federalLane);
                if(segmentMatches.Count>0)return segmentMatches;
                var segmentSovereign=LegalJurisdictionScope.ExtractSovereign(segment);
                if(!string.IsNullOrWhiteSpace(segmentSovereign))
                {
                    var segmentSovereignMatches=SelectEligibleCourts(courts,segmentSovereign,scope,federalLane);
                    if(segmentSovereignMatches.Count>0)return segmentSovereignMatches;
                }
            }
            return matches;
        }

        catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,"CourtListener court-scope resolution failed for {ScopeValue}.",scopeValue);
            return [];
        }
    }

    private async Task<IReadOnlyCollection<CourtListenerCourt>> FetchCourtsAsync(
        string url,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36");
        if(!string.IsNullOrWhiteSpace(configuration.CourtListenerApiToken))
            request.Headers.TryAddWithoutValidation("Authorization",$"Token {configuration.CourtListenerApiToken.Trim()}");
        using var response=await httpClient.SendAsync(request,cancellationToken);
        if(!response.IsSuccessStatusCode)return [];
        var payload=await response.Content.ReadFromJsonAsync<CourtListenerCourtResponse>(JsonOptions,cancellationToken);
        return payload?.Results??[];
    }

    private static IReadOnlyCollection<CourtListenerCourt> SelectEligibleCourts(
        IReadOnlyCollection<CourtListenerCourt> courts,string scopeValue,LegalAuthorityScope scope,bool federalLane) => courts
                .Where(court=>!string.IsNullOrWhiteSpace(court.Slug))
                .Where(court=>CourtMatchesScope(court,scopeValue,federalLane))
                .Where(court=>CourtMatchesLevel(court,scope.CourtLevel))
                .DistinctBy(court=>court.Slug!.Trim(),StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();

    private static bool CourtMatchesLevel(CourtListenerCourt court,string? courtLevel) =>
        string.IsNullOrWhiteSpace(courtLevel)
        ||$"{court.FullName} {court.ShortName}".Contains(courtLevel,StringComparison.OrdinalIgnoreCase);

    private static bool ResultMatchesEligibleCourt(CourtListenerSearchResult result,IReadOnlyCollection<CourtListenerCourt> eligibleCourts)
    {
        var returned=NormalizeText(result.Court);
        if(string.IsNullOrWhiteSpace(returned))return false;
        return eligibleCourts.Any(court=>
        {
            var fullName=NormalizeText(court.FullName);
            var shortName=NormalizeText(court.ShortName);
            return !string.IsNullOrWhiteSpace(fullName)
                    &&(returned.Equals(fullName,StringComparison.OrdinalIgnoreCase)
                        ||returned.Contains(fullName,StringComparison.OrdinalIgnoreCase))
                ||!string.IsNullOrWhiteSpace(shortName)
                    &&returned.Equals(shortName,StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool CourtMatchesScope(CourtListenerCourt court,string scopeValue,bool federalLane)
    {
        var identity=$"{court.FullName} {court.ShortName} {court.Jurisdiction}";
        var courtJurisdiction=court.Jurisdiction?.Trim();
        var scopeMatches=identity.Contains(scopeValue,StringComparison.OrdinalIgnoreCase)
            ||!string.IsNullOrWhiteSpace(courtJurisdiction)
                &&scopeValue.Contains(courtJurisdiction,StringComparison.OrdinalIgnoreCase);
        if(!scopeMatches)return false;
        var federal=identity.Contains("Federal",StringComparison.OrdinalIgnoreCase)
            ||identity.Contains("District Court",StringComparison.OrdinalIgnoreCase)
            ||identity.Contains("Circuit",StringComparison.OrdinalIgnoreCase);
        return federalLane==federal;
    }

    // Guards against a misconfigured base URL that already includes the API path
    // (e.g. "https://www.courtlistener.com/api/rest/v4"), which would otherwise produce a
    // doubled path such as ".../api/rest/v4/api/rest/v4/search/" and a 404 from CourtListener.
    private static string NormalizeBaseUrl(string baseUrl)
    {
        var trimmed=baseUrl.Trim().TrimEnd('/');
        var apiIndex=trimmed.IndexOf("/api/",StringComparison.OrdinalIgnoreCase);
        return apiIndex>0?trimmed[..apiIndex]:trimmed;
    }

    // Returns the first non-blank candidate, trimmed. Used to select the court-slug resolution source
    // (source court, then caption, then governing-law jurisdiction) without collapsing distinct fields.
    private static string? FirstNonEmpty(params string?[] candidates) =>
        candidates.Select(value=>value?.Trim()).FirstOrDefault(value=>!string.IsNullOrWhiteSpace(value));

    // Splits a composite display breadcrumb (e.g. "United States - State · Delaware · District Court")
    // into its individual scope segments so each can be resolved against CourtListener independently.
    // Longest segments first so a specific jurisdiction ("Delaware") is preferred over a generic label.
    private static IReadOnlyList<string> SplitScopeSegments(string scopeValue) =>
        scopeValue
            .Split(['·','|','/','-',','],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Where(segment=>!string.IsNullOrWhiteSpace(segment))
            .Where(segment=>!segment.Equals(scopeValue,StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(segment=>segment.Length)
            .ToArray();

    private static string BuildTitle(CourtListenerSearchResult result)
    {
        var name=result.CaseName ?? "CourtListener opinion";
        return string.IsNullOrWhiteSpace(result.Court)?name:$"{name} ({result.Court})";
    }

    private static string BuildUrl(string baseUrl,string? absoluteUrl)
    {
        if(string.IsNullOrWhiteSpace(absoluteUrl))return baseUrl;
        return absoluteUrl.StartsWith("http",StringComparison.OrdinalIgnoreCase)?absoluteUrl:$"{baseUrl}{absoluteUrl}";
    }

    private static string BuildSnippet(CourtListenerSearchResult result)
    {
        var candidates=(result.Opinions?.Select(item=>item.Snippet)??[])
            .Append(result.Snippet)
            .Append(result.Text)
            .Select(NormalizeText)
            .Where(text=>IsSubstantivePassage(text,result.CaseName));
        var text=candidates.FirstOrDefault();
        if(text is null)return string.Empty;
        var citation=result.Citation is{Count:>0}?$" [{string.Join("; ",result.Citation)}]":string.Empty;
        return $"{text}{citation}";
    }

    private static string? NormalizeText(string? value)
    {
        if(string.IsNullOrWhiteSpace(value))return null;
        var withoutMarkup=Regex.Replace(value,"<[^>]+>"," ");
        return Regex.Replace(WebUtility.HtmlDecode(withoutMarkup),@"\s+"," ").Trim();
    }

    private static bool IsSubstantivePassage(string? text,string? caseName)
    {
        if(string.IsNullOrWhiteSpace(text))return false;
        var words=Regex.Matches(text,@"\b[\p{L}\p{N}][\p{L}\p{N}'’-]*\b").Count;
        if(words<12)return false;
        var normalizedCase=NormalizeText(caseName);
        return string.IsNullOrWhiteSpace(normalizedCase)
            || !text.Equals(normalizedCase,StringComparison.OrdinalIgnoreCase);
    }

    private sealed record CourtListenerSearchResponse([property:JsonPropertyName("results")]List<CourtListenerSearchResult>? Results);

    private sealed record CourtListenerCourtResponse([property:JsonPropertyName("results")]List<CourtListenerCourt>? Results);

    private sealed record CourtListenerCourt(
        [property:JsonPropertyName("slug")]string? Slug,
        [property:JsonPropertyName("full_name")]string? FullName,
        [property:JsonPropertyName("short_name")]string? ShortName,
        [property:JsonPropertyName("jurisdiction")]string? Jurisdiction);

    private sealed record CourtListenerSearchResult(
        [property:JsonPropertyName("caseName")]string? CaseName,
        [property:JsonPropertyName("court")]string? Court,
        [property:JsonPropertyName("absolute_url")]string? AbsoluteUrl,
        [property:JsonPropertyName("snippet")]string? Snippet,
        [property:JsonPropertyName("text")]string? Text,
        [property:JsonPropertyName("citation")]List<string>? Citation,
        [property:JsonPropertyName("opinions")]List<CourtListenerOpinion>? Opinions);

    private sealed record CourtListenerOpinion([property:JsonPropertyName("snippet")]string? Snippet);
}
