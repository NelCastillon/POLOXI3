using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Intelligence;

// Internal legal source adapter for federal statutory/regulatory material: GovInfo Search Service
// (official federal publications, incl. CFR editions) plus eCFR (current regulation text).
// Aggregated by LegalRetriever. Fail-soft: any error returns an empty snippet collection.
public interface IGovInfoLegalSource
{
    Task<LegalProviderRetrievalResult> SearchAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default);
}

public sealed class GovInfoLegalRetriever(HttpClient httpClient,ILogger<GovInfoLegalRetriever> logger):IGovInfoLegalSource
{
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);

    public async Task<LegalProviderRetrievalResult> SearchAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default)
    {
        if(!configuration.GovInfoEnabled||string.IsNullOrWhiteSpace(query))
            return new([],new("GOVINFO_ECFR",false,!configuration.GovInfoEnabled?"DISABLED":"INVALID_REQUEST",0,0));
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));

        // GovInfo and eCFR are related but distinct sources; each fails soft independently so one
        // outage never discards the other's results. Both are capped by MaximumSnippetsPerQuery.
        // They run in parallel: neither call depends on the other, and running them sequentially
        // doubled the worst-case latency of this source under slow government endpoints.
        var govInfoTask=SearchGovInfoAsync(query,configuration,timeout.Token,cancellationToken);
        var ecfrTask=SearchEcfrAsync(query,configuration,timeout.Token,cancellationToken);
        await Task.WhenAll(govInfoTask,ecfrTask);
        var raw=govInfoTask.Result.Concat(ecfrTask.Result).ToList();
        var snippets=raw.Take(configuration.MaximumSnippetsPerQuery).ToList();
        return new(snippets,new("GOVINFO_ECFR",true,snippets.Count>0?"SUCCEEDED":"NO_RESULTS",raw.Count,snippets.Count));
    }

    private async Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> SearchGovInfoAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken timeoutToken,CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(configuration.GovInfoBaseUrl)||string.IsNullOrWhiteSpace(configuration.GovInfoApiKey))return [];
        try
        {
            var baseUrl=configuration.GovInfoBaseUrl.TrimEnd('/');
            var url=$"{baseUrl}/search?api_key={Uri.EscapeDataString(configuration.GovInfoApiKey.Trim())}";
            using var request=new HttpRequestMessage(HttpMethod.Post,url);
            request.Content=JsonContent.Create(new GovInfoSearchRequest(query,configuration.MaximumSnippetsPerQuery,"*","default",true,[new GovInfoSort("relevancy","DESC")]));

            using var response=await httpClient.SendAsync(request,timeoutToken);
            if(!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GovInfo search returned {StatusCode} for legal grounding query.",(int)response.StatusCode);
                return [];
            }

            var payload=await response.Content.ReadFromJsonAsync<GovInfoSearchResponse>(JsonOptions,timeoutToken);
            if(payload?.Results is not{Count:>0}results)return [];

            var retrievedUtc=DateTime.UtcNow;
            return results
                .Where(IsStatutoryPublication)
                .Take(configuration.MaximumSnippetsPerQuery)
                .Select(result=>new WideExternalKnowledgeSnippet(
                    query,
                    result.Title!.Trim(),
                    result.ResultLink??baseUrl,
                    string.IsNullOrWhiteSpace(result.Teaser)?result.Title!.Trim():result.Teaser!.Trim(),
                    0m,
                    retrievedUtc)
                {
                    AuthorityKind="STATUTE",
                    SourceProvider="GOVINFO",
                    SourceVersion="SEARCH_API_V1",
                    ProviderIdentityVerified=true,
                })
                .ToList();
        }
        catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,"GovInfo legal grounding call failed; continuing without this source.");
            return [];
        }
    }

    private static bool IsStatutoryPublication(GovInfoSearchResult result)
    {
        if(string.IsNullOrWhiteSpace(result.Title)||string.IsNullOrWhiteSpace(result.ResultLink))return false;
        var collection=result.CollectionCode?.Trim();
        if(collection is not null)
            return collection.Equals("USCODE",StringComparison.OrdinalIgnoreCase)
                || collection.Equals("PLAW",StringComparison.OrdinalIgnoreCase)
                || collection.Equals("STATUTE",StringComparison.OrdinalIgnoreCase);

        var identity=$"{result.Title} {result.ResultLink}";
        return identity.Contains("United States Code",StringComparison.OrdinalIgnoreCase)
            || identity.Contains("Public Law",StringComparison.OrdinalIgnoreCase)
            || identity.Contains("Statutes at Large",StringComparison.OrdinalIgnoreCase)
            || identity.Contains("/uscode/",StringComparison.OrdinalIgnoreCase)
            || identity.Contains("/plaw/",StringComparison.OrdinalIgnoreCase)
            || identity.Contains("/statute/",StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> SearchEcfrAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken timeoutToken,CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(configuration.EcfrBaseUrl))return [];
        try
        {
            var baseUrl=configuration.EcfrBaseUrl.TrimEnd('/');
            var url=$"{baseUrl}/api/search/v1/results?per_page={configuration.MaximumSnippetsPerQuery}&query={Uri.EscapeDataString(query)}";
            using var request=new HttpRequestMessage(HttpMethod.Get,url);

            using var response=await httpClient.SendAsync(request,timeoutToken);
            if(!response.IsSuccessStatusCode)
            {
                logger.LogWarning("eCFR search returned {StatusCode} for legal grounding query.",(int)response.StatusCode);
                return [];
            }

            var payload=await response.Content.ReadFromJsonAsync<EcfrSearchResponse>(JsonOptions,timeoutToken);
            if(payload?.Results is not{Count:>0}results)return [];

            var retrievedUtc=DateTime.UtcNow;
            return results
                .Take(configuration.MaximumSnippetsPerQuery)
                .Select(result=>new WideExternalKnowledgeSnippet(
                    query,
                    BuildEcfrTitle(result),
                    baseUrl,
                    result.FullTextExcerpt?.Trim()??string.Empty,
                    0m,
                    retrievedUtc)
                {
                    AuthorityKind="REGULATION",
                    SourceProvider="ECFR",
                    SourceVersion="SEARCH_API_V1",
                    ProviderIdentityVerified=true,
                })
                .Where(snippet=>!string.IsNullOrWhiteSpace(snippet.Snippet))
                .ToList();
        }
        catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,"eCFR legal grounding call failed; continuing without this source.");
            return [];
        }
    }

    private static string BuildEcfrTitle(EcfrSearchResult result)
    {
        var heading=result.HierarchyHeadings is{Count:>0}?string.Join(" \u203a ",result.HierarchyHeadings.Values.Where(text=>!string.IsNullOrWhiteSpace(text))):null;
        return string.IsNullOrWhiteSpace(heading)?"eCFR current regulation":$"eCFR: {heading}";
    }

    private sealed record GovInfoSearchRequest(
        [property:JsonPropertyName("query")]string Query,
        [property:JsonPropertyName("pageSize")]int PageSize,
        [property:JsonPropertyName("offsetMark")]string OffsetMark,
        [property:JsonPropertyName("resultLevel")]string ResultLevel,
        [property:JsonPropertyName("historical")]bool Historical,
        [property:JsonPropertyName("sorts")]List<GovInfoSort> Sorts);

    private sealed record GovInfoSort(
        [property:JsonPropertyName("field")]string Field,
        [property:JsonPropertyName("sortOrder")]string SortOrder);

    private sealed record GovInfoSearchResponse([property:JsonPropertyName("results")]List<GovInfoSearchResult>? Results);

    private sealed record GovInfoSearchResult(
        [property:JsonPropertyName("title")]string? Title,
        [property:JsonPropertyName("teaser")]string? Teaser,
        [property:JsonPropertyName("resultLink")]string? ResultLink,
        [property:JsonPropertyName("collectionCode")]string? CollectionCode);

    private sealed record EcfrSearchResponse([property:JsonPropertyName("results")]List<EcfrSearchResult>? Results);

    // eCFR returns hierarchy_headings as an OBJECT keyed by hierarchy level (title/subtitle/chapter/part/...),
    // not a JSON array, so it deserializes into a dictionary. Values may be null for absent levels.
    private sealed record EcfrSearchResult(
        [property:JsonPropertyName("full_text_excerpt")]string? FullTextExcerpt,
        [property:JsonPropertyName("hierarchy_headings")]Dictionary<string,string?>? HierarchyHeadings);
}
