using System.Net;
using System.Text;
using Ams.Knowledge.Application.Features.Knowledge;
using Ams.Web.Services;
using Xunit;

namespace Ams.Application.Tests;

public sealed class KnowledgeApiClientTests
{
    [Fact]
    public async Task QueryMethods_UseKnowledgeRoutesWithoutTenantQueryParameters()
    {
        var requests = new List<CapturedRequest>();
        var handler = new StubHandler(requests,
            Json(HttpStatusCode.OK, "{\"activeSchemes\":0,\"publishedConcepts\":0,\"searchableLabels\":0,\"approvedMappings\":0,\"pendingMappingReviews\":0,\"draftChangeRequests\":0,\"failedImports\":0}"),
            Json(HttpStatusCode.OK, "{\"items\":[],\"totalCount\":0,\"pageNumber\":1,\"pageSize\":25}"));
        var client = CreateClient(handler);

        await client.GetKnowledgeDashboardAsync();
        await client.SearchKnowledgeConceptsAsync(searchTerm: "auto", pageSize: 25);

        Assert.Collection(requests,
            request => Assert.Equal("api/knowledge/dashboard", request.Path),
            request =>
            {
                Assert.StartsWith("api/knowledge/concepts?", request.Path, StringComparison.Ordinal);
                Assert.DoesNotContain("tenantId", request.Path, StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public async Task ReviewAndPublish_PreserveRowVersionTokens()
    {
        var requests = new List<CapturedRequest>();
        var handler = new StubHandler(requests, new(HttpStatusCode.NoContent), new(HttpStatusCode.NoContent));
        var client = CreateClient(handler);
        var reviewId = Guid.NewGuid();
        var mappingId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();
        var rowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        await client.ReviewKnowledgeMappingAsync(reviewId, new(Guid.Empty, reviewId, mappingId, "APPROVED", "Verified", "correlation", Guid.Empty, rowVersion));
        await client.PublishKnowledgeAsync(publicationId, new(Guid.Empty, publicationId, "PUBLISHED", "Release", "correlation", Guid.Empty, rowVersion));

        Assert.Equal($"api/knowledge/reviews/{reviewId}/decision", requests[0].Path);
        Assert.Equal($"api/knowledge/publications/{publicationId}/publish", requests[1].Path);
        Assert.All(requests, request => Assert.Contains(Convert.ToBase64String(rowVersion), request.Body));
    }

    [Fact]
    public async Task CrudMethods_UseEntityRoutesAndKeepTenantContextOutOfUrls()
    {
        var requests = new List<CapturedRequest>();
        var handler = new StubHandler(requests,
            Json(HttpStatusCode.OK, $"{{\"id\":\"{Guid.NewGuid()}\"}}"),
            new(HttpStatusCode.NoContent),
            new(HttpStatusCode.NoContent));
        var client = CreateClient(handler);
        var tenantId = Guid.NewGuid();
        var publicationId = Guid.NewGuid();
        var rowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        await client.CreateKnowledgePublicationAsync(new(tenantId, "REL-1", "Release 1", "1.0", "DRAFT", "Create release", "correlation", Guid.NewGuid()));
        await client.UpdateKnowledgePublicationAsync(publicationId, new(tenantId, publicationId, "Release 1", "1.1", "DRAFT", "Update release", "correlation", Guid.NewGuid(), rowVersion));
        await client.DeleteKnowledgePublicationAsync(publicationId, new(tenantId, publicationId, "Delete release", "correlation", Guid.NewGuid(), rowVersion));

        Assert.Collection(requests,
            request => Assert.Equal("api/knowledge/publications", request.Path),
            request => Assert.Equal($"api/knowledge/publications/{publicationId}", request.Path),
            request => Assert.Equal($"api/knowledge/publications/{publicationId}", request.Path));
        Assert.All(requests, request => Assert.DoesNotContain("tenantId", request.Path, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(Convert.ToBase64String(rowVersion), requests[1].Body);
        Assert.Contains(Convert.ToBase64String(rowVersion), requests[2].Body);
    }

    private static ApiClient CreateClient(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://ams.test/") });

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed record CapturedRequest(string Path, string Body);

    private sealed class StubHandler(List<CapturedRequest> requests, params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _index;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add(new(request.RequestUri!.PathAndQuery.TrimStart('/'), request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return responses[_index++];
        }
    }
}
