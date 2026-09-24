using System.Net;
using System.Text;
using Legal.Application.Features.Intelligence;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Infrastructure.Intelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class CourtListenerLegalRetrieverTests
{
    [Fact]
    public async Task SearchAsync_RejectsCaptionOnlyResult()
    {
        using var client = new HttpClient(new JsonHandler("""
            {"results":[{"caseName":"Smith v. Jones","court":"Delaware Supreme Court","absolute_url":"/opinion/1/","snippet":"Smith v. Jones","opinions":[]}]}
            """));
        var retriever = new CourtListenerLegalRetriever(client, NullLogger<CourtListenerLegalRetriever>.Instance);

        var result = await retriever.SearchAsync("Delaware negligence", Configuration());

        Assert.Empty(result.Snippets);
        Assert.Equal("PARSE_REJECTED", result.Diagnostic.OutcomeCode);
    }

    [Fact]
    public async Task SearchAsync_RetainsSubstantiveOpinionAndReturnedCourtJurisdiction()
    {
        using var client = new HttpClient(new JsonHandler("""
            {"results":[{"caseName":"Smith v. Jones","court":"Delaware Supreme Court","absolute_url":"/opinion/1/","snippet":"<mark>We hold that comparative negligence reduces recovery according to the claimant's percentage of fault under the governing Delaware statute.</mark>","citation":["123 A.3d 456"],"opinions":[]}]}
            """));
        var retriever = new CourtListenerLegalRetriever(client, NullLogger<CourtListenerLegalRetriever>.Instance);

        var result = await retriever.SearchAsync("Delaware comparative negligence", Configuration());

        var snippet = Assert.Single(result.Snippets);
        Assert.Equal("Delaware Supreme Court", snippet.Jurisdiction);
        Assert.DoesNotContain("<mark>", snippet.Snippet);
        Assert.Contains("123 A.3d 456", snippet.Snippet);
    }

    [Fact]
    public async Task ControllingLane_ResolvesAndAppliesProviderCourtFilter()
    {
        var handler=new RoutingHandler();
        using var client=new HttpClient(handler);
        var retriever=new CourtListenerLegalRetriever(client,NullLogger<CourtListenerLegalRetriever>.Instance);
        var scope=new LegalAuthorityScope
        {
            IssueScopeCode=LegalAuthorityIssueScopes.SubstantiveLaw,
            AuthorityRoleCode=LegalAuthorityRoles.Controlling,
            GoverningLaw="Delaware",
            StatusCode=LegalAuthorityScopeStatuses.PartiallyResolved,
        };

        var result=await retriever.SearchAsync(new LegalProviderSearchRequest("comparative negligence",LegalAuthorityKind.Case,scope),Configuration());

        Assert.Single(result.Snippets);
        Assert.Contains("court=del",handler.SearchRequest!.Query);
    }

    [Fact]
    public async Task ControllingLane_UsesCourtCatalogWhenBroadJurisdictionSearchHasNoMatches()
    {
        var handler=new RoutingHandler(
            courtsJson:"{\"results\":[]}",
            catalogJson:"{\"results\":[{\"slug\":\"ny\",\"full_name\":\"New York Court of Appeals\",\"short_name\":\"N.Y.\",\"jurisdiction\":\"New York\"}]}",
            searchJson:"{\"results\":[{\"caseName\":\"Sharma v. Example\",\"court\":\"New York Court of Appeals\",\"absolute_url\":\"/opinion/2/\",\"snippet\":\"We hold that a settlement agreement is enforced according to established New York contract principles and the parties' manifested assent.\",\"opinions\":[]}]}");
        using var client=new HttpClient(handler);
        var retriever=new CourtListenerLegalRetriever(client,NullLogger<CourtListenerLegalRetriever>.Instance);
        var scope=new LegalAuthorityScope
        {
            IssueScopeCode=LegalAuthorityIssueScopes.SettlementEnforcement,
            AuthorityRoleCode=LegalAuthorityRoles.Controlling,
            GoverningLaw="New York",
            StatusCode=LegalAuthorityScopeStatuses.PartiallyResolved,
        };

        var result=await retriever.SearchAsync(new LegalProviderSearchRequest("settlement enforcement",LegalAuthorityKind.Case,scope),Configuration());

        Assert.Single(result.Snippets);
        Assert.Contains("court=ny",handler.SearchRequest!.Query);
        Assert.True(handler.CatalogRequested);
    }

    [Fact]
    public async Task ControllingLane_UnsupportedCourtScope_DoesNotRunUnrestrictedSearch()    {
        var handler=new RoutingHandler(courtsJson:"{\"results\":[]}");
        using var client=new HttpClient(handler);
        var retriever=new CourtListenerLegalRetriever(client,NullLogger<CourtListenerLegalRetriever>.Instance);
        var scope=new LegalAuthorityScope
        {
            AuthorityRoleCode=LegalAuthorityRoles.Controlling,
            GoverningLaw="Unsupported Jurisdiction",
            StatusCode=LegalAuthorityScopeStatuses.PartiallyResolved,
        };

        var result=await retriever.SearchAsync(new LegalProviderSearchRequest("negligence",LegalAuthorityKind.Case,scope),Configuration());

        Assert.Empty(result.Snippets);
        Assert.Equal("RETRIEVAL_SCOPE_UNSUPPORTED",result.Diagnostic.OutcomeCode);
        Assert.Null(handler.SearchRequest);
    }

    [Fact]
    public async Task ControllingLane_RejectsReturnedCourtOutsideResolvedCourtSet()
    {
        var handler=new RoutingHandler(searchJson:"{\"results\":[{\"caseName\":\"Smith v. Jones\",\"court\":\"Supreme Court of Ohio\",\"absolute_url\":\"/opinion/1/\",\"snippet\":\"We hold that comparative negligence reduces recovery according to proven causal fault under Delaware law.\",\"opinions\":[]}]}");
        using var client=new HttpClient(handler);
        var retriever=new CourtListenerLegalRetriever(client,NullLogger<CourtListenerLegalRetriever>.Instance);
        var scope=new LegalAuthorityScope
        {
            AuthorityRoleCode=LegalAuthorityRoles.Controlling,
            GoverningLaw="Delaware",
            StatusCode=LegalAuthorityScopeStatuses.PartiallyResolved,
        };

        var result=await retriever.SearchAsync(new LegalProviderSearchRequest("comparative negligence",LegalAuthorityKind.Case,scope),Configuration());

        Assert.Empty(result.Snippets);
        Assert.Equal("FILTER_REJECTED_SCOPE_MISMATCH",result.Diagnostic.OutcomeCode);
        Assert.Contains("court=del",handler.SearchRequest!.Query);
    }

    [Fact]
    public async Task PersuasiveLane_IsExplicitlyAllowedToSearchWithoutCourtFilter()
    {
        var handler=new RoutingHandler();
        using var client=new HttpClient(handler);
        var retriever=new CourtListenerLegalRetriever(client,NullLogger<CourtListenerLegalRetriever>.Instance);
        var scope=new LegalAuthorityScope
        {
            AuthorityRoleCode=LegalAuthorityRoles.Persuasive,
            GoverningLaw="Delaware",
            StatusCode=LegalAuthorityScopeStatuses.PartiallyResolved,
        };

        await retriever.SearchAsync(new LegalProviderSearchRequest("comparative negligence",LegalAuthorityKind.Case,scope),Configuration());

        Assert.NotNull(handler.SearchRequest);
        Assert.DoesNotContain("court=",handler.SearchRequest!.Query);
    }

    [Fact]
    public async Task ControllingLane_TranslatesCourtCaptionToSovereignProviderFilter()
    {
        // A court/forum caption is not resolvable directly or from the catalog, but the enclosing
        // US-state sovereign IS a provider-native jurisdiction, so a valid court filter is produced.
        var handler=new SovereignRoutingHandler();
        using var client=new HttpClient(handler);
        var retriever=new CourtListenerLegalRetriever(client,NullLogger<CourtListenerLegalRetriever>.Instance);
        var scope=new LegalAuthorityScope
        {
            AuthorityRoleCode=LegalAuthorityRoles.Controlling,
            GoverningLaw="Superior Court of California, County of Los Angeles",
            CourtOrForum="Superior Court of California, County of Los Angeles",
            StatusCode=LegalAuthorityScopeStatuses.PartiallyResolved,
        };

        var result=await retriever.SearchAsync(new LegalProviderSearchRequest("unsafe speed negligence",LegalAuthorityKind.Case,scope),Configuration());

        Assert.Single(result.Snippets);
        Assert.Contains("court=cal",handler.SearchRequest!.Query);
    }

    [Fact]
    public async Task ControllingLane_TranslatesFederalCircuitCaptionToProviderFilter()
    {
        // No US-state sovereign resolves, but the caption names a federal forum. The federal
        // jurisdiction translation ("Ninth Circuit") yields a provider-native court filter instead
        // of RETRIEVAL_SCOPE_UNSUPPORTED.
        var handler=new FederalRoutingHandler();
        using var client=new HttpClient(handler);
        var retriever=new CourtListenerLegalRetriever(client,NullLogger<CourtListenerLegalRetriever>.Instance);
        var scope=new LegalAuthorityScope
        {
            AuthorityRoleCode=LegalAuthorityRoles.Controlling,
            CourtSystem="Federal",
            GoverningLaw="United States Court of Appeals for the Ninth Circuit",
            CourtOrForum="United States Court of Appeals for the Ninth Circuit",
            StatusCode=LegalAuthorityScopeStatuses.PartiallyResolved,
        };

        var result=await retriever.SearchAsync(new LegalProviderSearchRequest("federal preemption",LegalAuthorityKind.Case,scope),Configuration());

        Assert.Single(result.Snippets);
        Assert.Contains("court=ca9",handler.SearchRequest!.Query);
    }

    [Fact]
    public async Task ControllingLane_ResolvesCourtFromSourceCourt_NotFromGoverningLawSovereign()
    {
        // Regression: citation-expansion previously sent the governing-law sovereign ("California")
        // to the courts endpoint as if it were a court identifier. Court-slug resolution must instead
        // consume the SOURCE COURT caption; the sovereign remains the jurisdiction, never the court id.
        var handler=new SovereignRoutingHandler();
        using var client=new HttpClient(handler);
        var retriever=new CourtListenerLegalRetriever(client,NullLogger<CourtListenerLegalRetriever>.Instance);
        var scope=new LegalAuthorityScope
        {
            AuthorityRoleCode=LegalAuthorityRoles.Controlling,
            GoverningLaw="California",
            SourceCourt="Superior Court of California, County of Los Angeles",
            StatusCode=LegalAuthorityScopeStatuses.PartiallyResolved,
        };

        var result=await retriever.SearchAsync(new LegalProviderSearchRequest("authorities citing Cal. Veh. Code § 22350",LegalAuthorityKind.Case,scope),Configuration());

        Assert.Single(result.Snippets);
        Assert.Contains("court=cal",handler.SearchRequest!.Query);
    }

    private static WideLegalGroundingConfiguration Configuration() => new(
        true, 3, 5, 1, 10, true, "https://www.courtlistener.com", string.Empty,
        false, string.Empty, string.Empty, string.Empty, false, string.Empty);

    private sealed class JsonHandler(string payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                RequestMessage = request,
            });
    }

    private sealed class RoutingHandler(string? courtsJson=null,string? searchJson=null,string? catalogJson=null):HttpMessageHandler
    {
        public Uri? SearchRequest { get; private set; }
        public bool CatalogRequested { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            var isCourts=request.RequestUri!.AbsolutePath.Contains("/courts/",StringComparison.OrdinalIgnoreCase);
            if(!isCourts)SearchRequest=request.RequestUri;
            CatalogRequested|=isCourts&&request.RequestUri.Query.Contains("page_size",StringComparison.OrdinalIgnoreCase);
            var payload=isCourts
                ?CatalogRequested?catalogJson??courtsJson??"{\"results\":[]}":courtsJson??"{\"results\":[{\"slug\":\"del\",\"full_name\":\"Delaware Supreme Court\",\"short_name\":\"Del.\",\"jurisdiction\":\"Delaware\"}]}"
                :searchJson??"{\"results\":[{\"caseName\":\"Smith v. Jones\",\"court\":\"Delaware Supreme Court\",\"absolute_url\":\"/opinion/1/\",\"snippet\":\"We hold that comparative negligence reduces recovery according to proven causal fault under Delaware law.\",\"opinions\":[]}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content=new StringContent(payload,Encoding.UTF8,"application/json"),
                RequestMessage=request,
            });
        }
    }

    // Court captions and the raw jurisdiction are not resolvable, but a search for the extracted
    // sovereign ("California") returns a provider-native court (slug "cal"). This proves the caption
    // -> sovereign provider translation, not a court-name alias.
    private sealed class SovereignRoutingHandler:HttpMessageHandler
    {
        public Uri? SearchRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            var uri=request.RequestUri!;
            var isCourts=uri.AbsolutePath.Contains("/courts/",StringComparison.OrdinalIgnoreCase);
            string payload;
            if(isCourts)
                payload=uri.Query.Contains("California",StringComparison.OrdinalIgnoreCase)
                    ?"{\"results\":[{\"slug\":\"cal\",\"full_name\":\"Supreme Court of California\",\"short_name\":\"Cal.\",\"jurisdiction\":\"California\"}]}"
                    :"{\"results\":[]}";
            else
            {
                SearchRequest=uri;
                payload="{\"results\":[{\"caseName\":\"People v. Example\",\"court\":\"Supreme Court of California\",\"absolute_url\":\"/opinion/9/\",\"snippet\":\"We hold that a violation of the Vehicle Code establishing unsafe speed supports negligence under California law.\",\"opinions\":[]}]}";
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content=new StringContent(payload,Encoding.UTF8,"application/json"),
                RequestMessage=request,
            });
        }
    }

    // No US-state sovereign resolves ("United States Court of Appeals for the Ninth Circuit"), but a
    // search for the federal jurisdiction ("Ninth Circuit") returns a provider-native federal court
    // (slug "ca9"). This proves the caption -> federal-jurisdiction provider translation.
    private sealed class FederalRoutingHandler:HttpMessageHandler
    {
        public Uri? SearchRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            var uri=request.RequestUri!;
            var isCourts=uri.AbsolutePath.Contains("/courts/",StringComparison.OrdinalIgnoreCase);
            string payload;
            if(isCourts)
                payload=uri.Query.Contains("Circuit",StringComparison.OrdinalIgnoreCase)
                    ?"{\"results\":[{\"slug\":\"ca9\",\"full_name\":\"Court of Appeals for the Ninth Circuit\",\"short_name\":\"9th Cir.\",\"jurisdiction\":\"Federal\"}]}"
                    :"{\"results\":[]}";
            else
            {
                SearchRequest=uri;
                payload="{\"results\":[{\"caseName\":\"United States v. Example\",\"court\":\"Court of Appeals for the Ninth Circuit\",\"absolute_url\":\"/opinion/7/\",\"snippet\":\"We hold that the state statute is preempted where compliance with both federal and state law is impossible under governing federal law.\",\"opinions\":[]}]}";
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content=new StringContent(payload,Encoding.UTF8,"application/json"),
                RequestMessage=request,
            });
        }
    }
}
