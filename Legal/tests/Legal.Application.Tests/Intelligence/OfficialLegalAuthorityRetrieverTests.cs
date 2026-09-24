using System.Net;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence;
using Legal.Infrastructure.Intelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class OfficialLegalAuthorityRetrieverTests
{
    [Fact]
    public async Task SearchAsync_UsesDescriptorPatternAndTemplateWithoutProviderSpecificCode()
    {
        var descriptor=Descriptor("EXAMPLE_OFFICIAL",@"EX (?<volume>\d+) § (?<section>\d{3})","https://law.example","/v{volume}/chapter-{section:prefix:1}.html#{section}");
        var handler=new StubHandler(request=>
        {
            Assert.Equal("https://law.example/v7/chapter-4.html#456",request.RequestUri!.AbsoluteUri);
            return Html("<article id=\"456\"><h2>Section 456</h2><p>Controlling official text.</p></article><article id=\"457\">Next</article>");
        });
        var sut=Create(descriptor,handler);

        var result=await sut.SearchAsync("Apply EX 7 § 456",Configuration());

        var snippet=Assert.Single(result.Snippets);
        Assert.Equal("OFFICIAL_AUTHORITY",snippet.SourceProvider);
        Assert.StartsWith("EXAMPLE_OFFICIAL:",snippet.SourceVersion);
        Assert.Contains("Controlling official text",snippet.Snippet);
        Assert.Equal("RESULTS_FOUND",result.Diagnostic.OutcomeCode);
    }

    [Fact]
    public async Task SearchAsync_SameResolverSupportsDifferentJurisdictionDescriptor()
    {
        var descriptor=Descriptor("SECOND_JURISDICTION",@"YZ-(?<book>\d+)-(?<section>\d+)","https://codes.example","/book/{book}/sections/{section}");
        var handler=new StubHandler(_=>Html("<div id='88'>A second jurisdiction's official rule.</div><div id='89'>Next</div>"));
        var sut=Create(descriptor,handler);

        var result=await sut.SearchAsync("See YZ-12-88",Configuration());

        var snippet=Assert.Single(result.Snippets);
        Assert.Equal("https://codes.example/book/12/sections/88",snippet.Url);
        Assert.Equal("OFFICIAL_AUTHORITY",snippet.SourceProvider);
        Assert.StartsWith("SECOND_JURISDICTION:",snippet.SourceVersion);
    }

    [Fact]
    public async Task SearchAsync_WhenNoDescriptorMatches_ReturnsCoverageOutcomeWithoutHttpRequest()
    {
        var handler=new StubHandler(_=>throw new InvalidOperationException("HTTP should not be called."));
        var sut=Create(Descriptor("EXAMPLE",@"EX (?<section>\d+)","https://law.example","/{section}"),handler);

        var result=await sut.SearchAsync("No exact citation is present",Configuration());

        Assert.Empty(result.Snippets);
        Assert.Equal("NO_CITATIONS_RESOLVED",result.Diagnostic.OutcomeCode);
        Assert.Equal(0,handler.RequestCount);
    }

    [Fact]
    public async Task SearchAsync_WhenMatchedCitationIsForbidden_SurfacesAccessDeniedNotNoResults()
    {
        var descriptor=Descriptor("EXAMPLE",@"EX (?<section>\d+)","https://law.example","/{section}");
        var handler=new StubHandler(_=>new HttpResponseMessage(HttpStatusCode.Forbidden));
        var sut=Create(descriptor,handler);

        var result=await sut.SearchAsync("Apply EX 456",Configuration());

        Assert.Empty(result.Snippets);
        Assert.Equal("ACCESS_DENIED",result.Diagnostic.OutcomeCode);
        Assert.Equal(1,handler.RequestCount);
    }

    [Fact]
    public async Task SearchAsync_WhenMatchedCitationExtractsNothing_SurfacesExtractionOutcomeNotNoResults()
    {
        var descriptor=Descriptor("EXAMPLE",@"EX (?<section>\d+)","https://law.example","/{section}");
        var handler=new StubHandler(_=>Html("<article id=\"999\">Unrelated section.</article>"));
        var sut=Create(descriptor,handler);

        var result=await sut.SearchAsync("Apply EX 456",Configuration());

        Assert.Empty(result.Snippets);
        Assert.Equal("EXTRACTION_EMPTY",result.Diagnostic.OutcomeCode);
    }

    [Fact]
    public async Task SearchAsync_ExactCaliforniaStatute_ResolvesViaBuiltInLeginfoSourceWhenRegistryHasNoMatch()
    {
        // Regression: an exact California statutory citation (California Vehicle Code § 22350) had no
        // configured adapter and returned COVERAGE_GAP. The built-in leginfo sources must resolve it to
        // the authoritative California Legislative Information host without any tenant configuration.
        var unrelated=Descriptor("EXAMPLE",@"EX (?<section>\d+)","https://law.example","/{section}");
        Uri? requested=null;
        var handler=new StubHandler(request=>
        {
            requested=request.RequestUri;
            return Html("California Vehicle Code Section 22350. No person shall drive a vehicle at a speed greater than is reasonable or prudent.");
        });
        var sut=Create(unrelated,handler);

        var result=await sut.SearchAsync("Apply California Vehicle Code § 22350",Configuration());

        var snippet=Assert.Single(result.Snippets);
        Assert.Equal("RESULTS_FOUND",result.Diagnostic.OutcomeCode);
        Assert.StartsWith("CA_LEGINFO_VEH:",snippet.SourceVersion);
        Assert.Contains("leginfo.legislature.ca.gov",requested!.AbsoluteUri);
        Assert.Contains("lawCode=VEH",requested.AbsoluteUri);
        Assert.Contains("sectionNum=22350",requested.AbsoluteUri);
    }

    private static OfficialLegalAuthorityRetriever Create(LegalAuthoritySourceDescriptor descriptor,StubHandler handler) =>
        new(new HttpClient(handler),new StubRegistry([descriptor]),new StubBootstrapper(),new StubDetector(),new StubTenantAccessor(),NullLogger<OfficialLegalAuthorityRetriever>.Instance);

    private static LegalAuthoritySourceDescriptor Descriptor(string provider,string pattern,string baseUrl,string template) =>
        new(Guid.NewGuid(),provider,"TEST", "STATUTE",pattern,baseUrl,template,"{section}","HTML_ID_SECTION",10);

    private static WideLegalGroundingConfiguration Configuration() =>
        new(true,3,5,24,15,true,"https://court.example","",true,"https://gov.example","","https://ecfr.example",true,"https://lii.example");

    private static HttpResponseMessage Html(string value) => new(HttpStatusCode.OK){Content=new StringContent(value)};

    private sealed class StubRegistry(IReadOnlyCollection<LegalAuthoritySourceDescriptor> descriptors):ILegalAuthoritySourceRegistry
    {
        public Task<IReadOnlyCollection<LegalAuthoritySourceDescriptor>> GetSourcesAsync(Guid tenantId,CancellationToken cancellationToken=default) => Task.FromResult(descriptors);
        public Task RegisterAsync(LegalAuthoritySourceRegistration registration,CancellationToken cancellationToken=default) => Task.CompletedTask;
    }

    private sealed class StubBootstrapper:ILegalAuthoritySourceBootstrapper
    {
        public Task<IReadOnlyCollection<LegalAuthoritySourceDescriptor>> BootstrapAsync(LegalAuthorityDiscoveryRequest request,CancellationToken cancellationToken=default) => Task.FromResult<IReadOnlyCollection<LegalAuthoritySourceDescriptor>>([]);
    }

    private sealed class StubDetector:ILegalJurisdictionDetector
    {
        public bool TryDetect(string query,out LegalJurisdictionCitation citation)
        {
            citation=new(string.Empty,string.Empty,string.Empty);
            return false;
        }
    }

    private sealed class StubTenantAccessor:IEpistemicTenantAccessor
    {
        public Guid? TenantId{get;}=Guid.NewGuid();
    }

    private sealed class StubHandler(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler
    {
        public int RequestCount{get;private set;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            RequestCount++;
            var result=response(request);
            result.RequestMessage=request;
            return Task.FromResult(result);
        }
    }
}
