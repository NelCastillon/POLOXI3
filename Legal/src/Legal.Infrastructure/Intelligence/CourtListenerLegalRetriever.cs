using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Legal.Application.Features.Intelligence;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Intelligence;

// Internal legal source adapter for CourtListener case law. Aggregated by LegalRetriever.
// Fail-soft: any provider/transport error returns an empty snippet collection.
public interface ICourtListenerLegalSource
{
    Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> SearchAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default);
}

// CourtListener v4 opinion search (https://www.courtlistener.com/api/rest/v4/search/).
// An API token is optional (anonymous access is heavily rate-limited); when present it is sent as a
// "Token <token>" Authorization header, per the CourtListener API contract.
public sealed class CourtListenerLegalRetriever(HttpClient httpClient,ILogger<CourtListenerLegalRetriever> logger):ICourtListenerLegalSource
{
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> SearchAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default)
    {
        if(!configuration.CourtListenerEnabled||string.IsNullOrWhiteSpace(query)||string.IsNullOrWhiteSpace(configuration.CourtListenerBaseUrl))return [];
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));

            var baseUrl=NormalizeBaseUrl(configuration.CourtListenerBaseUrl);
            var url=$"{baseUrl}/api/rest/v4/search/?type=o&order_by=score%20desc&q={Uri.EscapeDataString(query)}";
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
                return [];
            }

            var payload=await response.Content.ReadFromJsonAsync<CourtListenerSearchResponse>(JsonOptions,timeout.Token);
            if(payload?.Results is not{Count:>0}results)
            {
                logger.LogInformation("LEGAL-TRACE stage=4-courtlistener-response outcome=NO_RESULTS status={StatusCode} query=\"{Query}\"",(int)response.StatusCode,query);
                return [];
            }

            var retrievedUtc=DateTime.UtcNow;
            var snippets=results
                .Take(configuration.MaximumSnippetsPerQuery)
                .Select(result=>new WideExternalKnowledgeSnippet(
                    query,
                    BuildTitle(result),
                    BuildUrl(baseUrl,result.AbsoluteUrl),
                    BuildSnippet(result),
                    0m,
                    retrievedUtc))
                .Where(snippet=>!string.IsNullOrWhiteSpace(snippet.Snippet))
                .ToList();
            // STAGE 4 (CourtListener response): log how many results came back and their titles so a
            // "relevant result returned but later rejected" state is distinguishable from "no results".
            logger.LogInformation("LEGAL-TRACE stage=4-courtlistener-response outcome=RESULTS status={StatusCode} rawResults={RawCount} usableSnippets={SnippetCount} query=\"{Query}\" titles=[{Titles}]",(int)response.StatusCode,results.Count,snippets.Count,query,string.Join(" | ",snippets.Take(5).Select(snippet=>snippet.Title)));
            return snippets;
        }
        catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,"LEGAL-TRACE stage=4-courtlistener-response outcome=EXCEPTION query=\"{Query}\"; continuing without this source.",query);
            return [];
        }
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
        // v4 opinion results expose highlighted snippets under "opinions"; fall back to a plain excerpt.
        var opinionSnippet=result.Opinions?.Select(item=>item.Snippet).FirstOrDefault(text=>!string.IsNullOrWhiteSpace(text));
        var text=opinionSnippet??result.Snippet??result.Text;
        if(string.IsNullOrWhiteSpace(text))return string.Empty;
        var citation=result.Citation is{Count:>0}?$" [{string.Join("; ",result.Citation)}]":string.Empty;
        return $"{text.Trim()}{citation}";
    }

    private sealed record CourtListenerSearchResponse([property:JsonPropertyName("results")]List<CourtListenerSearchResult>? Results);

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
