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

    [Fact]
    public async Task SearchAsync_ReplaceTokenSlugifiesSectionForStaticHtmlAggregator()
    {
        // Static-HTML statutory aggregators slugify dotted section numbers in the URL path
        // (e.g. "377.60" becomes "377-60"). The reusable "{section:replace:.-}" template operation
        // must produce that slug so the authoritative page resolves and its text can be extracted.
        var descriptor=Descriptor("STATIC_AGGREGATOR",@"AGG (?<section>\d[\d.]*)","https://codes.example","/ca/ccp/{section:replace:.-}.html#{section}");
        Uri? requested=null;
        var handler=new StubHandler(request=>
        {
            requested=request.RequestUri;
            return Html("<article id=\"377.60\"><h2>Section 377.60</h2><p>A decedent's heirs may bring a wrongful death action.</p></article><article id=\"377.61\">Next</article>");
        });
        var sut=Create(descriptor,handler);

        var result=await sut.SearchAsync("Apply AGG 377.60",Configuration());

        var snippet=Assert.Single(result.Snippets);
        Assert.Equal("https://codes.example/ca/ccp/377-60.html#377.60",requested!.AbsoluteUri);
        Assert.Contains("wrongful death action",snippet.Snippet);
        Assert.Equal("RESULTS_FOUND",result.Diagnostic.OutcomeCode);
    }

    [Fact]
    public async Task SearchAsync_CivilCodeDescriptorNeverResolves377SeriesEvenWhenPatternGreedilyMatches()
    {
        // Defense-in-depth regression: California's wrongful-death / survival statutes (§ 377.x) live in
        // the Code of Civil Procedure, not the Civil Code. Even if a Civil-Code (CIV) descriptor still
        // carries a greedy pattern that captures "Cal. Civ. Code § 377.60" (e.g. a re-seeded provider that
        // lost the 0336/0338 negative lookahead), the retriever must refuse it and never fetch lawCode=CIV.
        var civ=Descriptor("CA_LEGINFO_CIV",@"\bCal\.?\s+Civ\.?\s+Code\s*§?\s*(?<section>\d[\dA-Za-z.:-]*)","https://leginfo.legislature.ca.gov","faces/codes_displaySection.xhtml?lawCode=CIV&sectionNum={section}","FULL_PAGE_TEXT");
        var handler=new StubHandler(_=>throw new InvalidOperationException("CIV descriptor must not be fetched for a § 377-series citation."));
        var sut=Create(handler,civ);

        var result=await sut.SearchAsync("Apply Cal. Civ. Code § 377.60",Configuration());

        Assert.Empty(result.Snippets);
        Assert.Equal(0,handler.RequestCount);
        Assert.NotEqual("RESULTS_FOUND",result.Diagnostic.OutcomeCode);
    }

    [Fact]
    public async Task SearchAsync_377SeriesResolvesThroughCcpDescriptorNotCivilCode()
    {
        // With both a greedy CIV descriptor and the correct CCP descriptor present, the § 377.60 citation
        // must resolve ONLY through the Code of Civil Procedure provider, producing a single authoritative
        // CCP identity rather than a contradictory "Cal. Civ. Code" one.
        var civ=Descriptor("CA_LEGINFO_CIV",@"\bCal\.?\s+Civ\.?\s+Code\s*§?\s*(?<section>\d[\dA-Za-z.:-]*)","https://leginfo.legislature.ca.gov","faces/codes_displaySection.xhtml?lawCode=CIV&sectionNum={section}","FULL_PAGE_TEXT");
        var ccp=Descriptor("CA_LEGINFO_CCP",@"\bCal\.?\s+Civ\.?\s+Code\s*§?\s*(?<section>377[\dA-Za-z.:-]*)","https://leginfo.legislature.ca.gov","faces/codes_displaySection.xhtml?lawCode=CCP&sectionNum={section}","FULL_PAGE_TEXT");
        Uri? requested=null;
        var handler=new StubHandler(request=>
        {
            requested=request.RequestUri;
            return Html("Code of Civil Procedure Section 377.60. A cause of action for the death of a person caused by the wrongful act or neglect of another may be asserted by the decedent's surviving spouse, domestic partner, children, and heirs.");
        });
        var sut=Create(handler,civ,ccp);

        var result=await sut.SearchAsync("Apply Cal. Civ. Code § 377.60",Configuration());

        var snippet=Assert.Single(result.Snippets);
        Assert.Equal("RESULTS_FOUND",result.Diagnostic.OutcomeCode);
        Assert.StartsWith("CA_LEGINFO_CCP:",snippet.SourceVersion);
        Assert.Contains("lawCode=CCP",requested!.AbsoluteUri);
        Assert.DoesNotContain("lawCode=CIV",requested.AbsoluteUri);
    }

    [Fact]
    public async Task SearchAsync_CivilCodeDescriptorStillResolvesOrdinaryCivilCodeSection()
    {
        // The 377 guard must be narrow: ordinary Civil Code citations (e.g. § 1714) must still resolve
        // through the CIV descriptor exactly as before.
        var civ=Descriptor("CA_LEGINFO_CIV",@"\bCal\.?\s+Civ\.?\s+Code\s*§?\s*(?<section>\d[\dA-Za-z.:-]*)","https://leginfo.legislature.ca.gov","faces/codes_displaySection.xhtml?lawCode=CIV&sectionNum={section}","FULL_PAGE_TEXT");
        Uri? requested=null;
        var handler=new StubHandler(request=>
        {
            requested=request.RequestUri;
            return Html("Civil Code Section 1714. Everyone is responsible, not only for the result of his or her willful acts, but also for an injury occasioned to another by his or her want of ordinary care or skill in the management of his or her property or person.");
        });
        var sut=Create(handler,civ);

        var result=await sut.SearchAsync("Apply Cal. Civ. Code § 1714",Configuration());

        var snippet=Assert.Single(result.Snippets);
        Assert.Equal("RESULTS_FOUND",result.Diagnostic.OutcomeCode);
        Assert.StartsWith("CA_LEGINFO_CIV:",snippet.SourceVersion);
        Assert.Contains("lawCode=CIV",requested!.AbsoluteUri);
        Assert.Contains("sectionNum=1714",requested.AbsoluteUri);
    }

    private static OfficialLegalAuthorityRetriever Create(LegalAuthoritySourceDescriptor descriptor,StubHandler handler) =>
        new(new HttpClient(handler),new StubRegistry([descriptor]),new StubBootstrapper(),new StubDetector(),new StubTenantAccessor(),Legal.Application.Abstractions.Services.NullErrorLogService.Instance,NullLogger<OfficialLegalAuthorityRetriever>.Instance);

    private static OfficialLegalAuthorityRetriever Create(StubHandler handler,params LegalAuthoritySourceDescriptor[] descriptors) =>
        new(new HttpClient(handler),new StubRegistry(descriptors),new StubBootstrapper(),new StubDetector(),new StubTenantAccessor(),Legal.Application.Abstractions.Services.NullErrorLogService.Instance,NullLogger<OfficialLegalAuthorityRetriever>.Instance);

    private static LegalAuthoritySourceDescriptor Descriptor(string provider,string pattern,string baseUrl,string template) =>
        new(Guid.NewGuid(),provider,"TEST", "STATUTE",pattern,baseUrl,template,"{section}","HTML_ID_SECTION",10);

    private static LegalAuthoritySourceDescriptor Descriptor(string provider,string pattern,string baseUrl,string template,string extractionStrategyCode) =>
        new(Guid.NewGuid(),provider,"NAME:CALIFORNIA", "STATUTE",pattern,baseUrl,template,null,extractionStrategyCode,10);

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
