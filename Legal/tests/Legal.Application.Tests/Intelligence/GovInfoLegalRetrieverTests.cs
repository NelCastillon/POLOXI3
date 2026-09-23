using System.Net;
using System.Text;
using Legal.Application.Features.Intelligence;
using Legal.Infrastructure.Intelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class GovInfoLegalRetrieverTests
{
    [Fact]
    public async Task SearchAsync_DoesNotClassifyTransportationResearchAsStatute()
    {
        using var client=new HttpClient(new Handler(request=>request.Method==HttpMethod.Post
            ?"{\"results\":[{\"title\":\"Human Factors Assessment of Pedestrian Roadway Crossing Behavior\",\"teaser\":\"Transportation safety research\",\"resultLink\":\"https://www.govinfo.gov/app/details/GOVPUB-TD2-PURL-gpo123\",\"collectionCode\":\"GOVPUB\"}]}"
            :"{\"results\":[]}"));
        var retriever=new GovInfoLegalRetriever(client,NullLogger<GovInfoLegalRetriever>.Instance);

        var result=await retriever.SearchAsync("Delaware comparative negligence",Configuration());

        Assert.Empty(result.Snippets);
    }

    [Fact]
    public async Task SearchAsync_RetainsIdentifiedUnitedStatesCodePublication()
    {
        using var client=new HttpClient(new Handler(request=>request.Method==HttpMethod.Post
            ?"{\"results\":[{\"title\":\"United States Code Title 42\",\"teaser\":\"Official statutory text\",\"resultLink\":\"https://www.govinfo.gov/app/details/USCODE-2023-title42\",\"collectionCode\":\"USCODE\"}]}"
            :"{\"results\":[]}"));
        var retriever=new GovInfoLegalRetriever(client,NullLogger<GovInfoLegalRetriever>.Instance);

        var result=await retriever.SearchAsync("42 U.S.C. 1983",Configuration());

        var snippet=Assert.Single(result.Snippets);
        Assert.Equal("STATUTE",snippet.AuthorityKind);
    }

    private static WideLegalGroundingConfiguration Configuration()=>new(
        true,3,5,1,10,false,string.Empty,string.Empty,true,"https://api.govinfo.gov","key",
        "https://www.ecfr.gov",false,string.Empty);

    private sealed class Handler(Func<HttpRequestMessage,string> payload):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content=new StringContent(payload(request),Encoding.UTF8,"application/json"),
                RequestMessage=request,
            });
    }
}