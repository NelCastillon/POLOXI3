using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Features.Intelligence;
using Microsoft.Extensions.Logging;

namespace Ams.Infrastructure.Intelligence;

// Tavily live web-search provider for grounding time-sensitive Wide interpretive results.
// Fail-soft by design: any provider/transport error returns an empty snippet collection
// so enterprise search continues without external grounding.
public sealed class TavilyExternalKnowledgeProvider(HttpClient httpClient,ILogger<TavilyExternalKnowledgeProvider> logger):IExternalKnowledgeProvider
{
    private const string EndpointUrl="https://api.tavily.com/search";

    public async Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> SearchAsync(string query,WideExternalGroundingConfiguration configuration,CancellationToken cancellationToken=default)
    {
        if(!configuration.Enabled||string.IsNullOrWhiteSpace(configuration.ApiKey)||string.IsNullOrWhiteSpace(query))return [];
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));

            using var request=new HttpRequestMessage(HttpMethod.Post,EndpointUrl);
            request.Headers.Authorization=new("Bearer",configuration.ApiKey);
            request.Content=JsonContent.Create(new TavilySearchRequest(query,"basic",configuration.MaximumSnippetsPerQuery));

            using var response=await httpClient.SendAsync(request,timeout.Token);
            if(!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Tavily search returned {StatusCode} for external grounding query.",(int)response.StatusCode);
                return [];
            }

            var payload=await response.Content.ReadFromJsonAsync<TavilySearchResponse>(JsonOptions,timeout.Token);
            if(payload?.Results is not{Count:>0}results)return [];

            var retrievedUtc=DateTime.UtcNow;
            return results
                .Where(result=>!string.IsNullOrWhiteSpace(result.Content))
                .Take(configuration.MaximumSnippetsPerQuery)
                .Select(result=>new WideExternalKnowledgeSnippet(query,result.Title??string.Empty,result.Url??string.Empty,result.Content!,Math.Clamp(result.Score,0,1),retrievedUtc))
                .ToList();
        }
        catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,"Tavily external grounding call failed; continuing without live grounding.");
            return [];
        }
    }

    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);

    private sealed record TavilySearchRequest(
        [property:JsonPropertyName("query")]string Query,
        [property:JsonPropertyName("search_depth")]string SearchDepth,
        [property:JsonPropertyName("max_results")]int MaxResults);

    private sealed record TavilySearchResponse([property:JsonPropertyName("results")]List<TavilySearchResult>? Results);

    private sealed record TavilySearchResult(
        [property:JsonPropertyName("title")]string? Title,
        [property:JsonPropertyName("url")]string? Url,
        [property:JsonPropertyName("content")]string? Content,
        [property:JsonPropertyName("score")]decimal Score);
}
