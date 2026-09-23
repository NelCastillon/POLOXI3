using System.Net;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Legal.Infrastructure.Intelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class LegalAuthorityBootstrapTests
{
    [Fact]
    public async Task BootstrapAsync_ValidGovernmentCandidate_IsRegisteredAndReturned()
    {
        var registry=new RecordingRegistry();
        var configuration=new StubConfiguration();
        var search=new StubSearchProvider([new("query","Official Code","https://code.example.gov/books/4/section/22","result",1m,DateTime.UtcNow)]);
        var handler=new StubHandler(_=>Html("<main>Exampleland Code section 22 controlling text</main>"));
        var sut=new LegalAuthoritySourceBootstrapper(new HttpClient(handler),search,configuration,registry,NullLogger<LegalAuthoritySourceBootstrapper>.Instance);
        var request=new LegalAuthorityDiscoveryRequest(Guid.NewGuid(),"Exampleland Code section 22","NAME:EXAMPLELAND","STATUTE","Exampleland Code section 22");

        var descriptors=await sut.BootstrapAsync(request);

        var registration=Assert.Single(registry.Registrations);
        Assert.Equal("NAME:EXAMPLELAND",registration.JurisdictionCode);
        Assert.Equal("FULL_PAGE_TEXT",registration.ExtractionStrategyCode);
        Assert.Single(descriptors);
    }

    [Fact]
    public async Task BootstrapAsync_RejectsHostThatOnlyContainsGovernmentLabel()
    {
        var registry=new RecordingRegistry();
        var handler=new StubHandler(_=>new(HttpStatusCode.OK)
        {
            Content=new StringContent("Delaware Code 8 § 141")
        });
        var search=new StubSearchProvider([new("query","Official Code","https://courts.gov.example.com/title8/141","result",1m,DateTime.UtcNow)]);
        var bootstrapper=new LegalAuthoritySourceBootstrapper(new HttpClient(handler),search,new StubConfiguration(),registry,NullLogger<LegalAuthoritySourceBootstrapper>.Instance);

        var result=await bootstrapper.BootstrapAsync(new(Guid.NewGuid(),"Delaware Code 8 § 141","NAME:DELAWARE","STATUTE","Delaware Code 8 § 141"));

        Assert.Empty(result);
        Assert.Equal(0,handler.RequestCount);
        Assert.Empty(registry.Registrations);
    }

    [Fact]
    public async Task BootstrapAsync_RejectsRedirectOutsideGovernmentBoundary()
    {
        var registry=new RecordingRegistry();
        var handler=new StubHandler(request=>
        {
            request.RequestUri=new Uri("https://example.com/title8/141");
            return new(HttpStatusCode.OK)
            {
                Content=new StringContent("Delaware Code 8 § 141")
            };
        });
        var search=new StubSearchProvider([new("query","Official Code","https://delcode.delaware.gov/title8/141","result",1m,DateTime.UtcNow)]);
        var bootstrapper=new LegalAuthoritySourceBootstrapper(new HttpClient(handler),search,new StubConfiguration(),registry,NullLogger<LegalAuthoritySourceBootstrapper>.Instance);

        var result=await bootstrapper.BootstrapAsync(new(Guid.NewGuid(),"Delaware Code 8 § 141","NAME:DELAWARE","STATUTE","Delaware Code 8 § 141"));

        Assert.Empty(result);
        Assert.Equal(1,handler.RequestCount);
        Assert.Empty(registry.Registrations);
    }

    [Fact]
    public async Task BootstrapAsync_NonGovernmentCandidate_IsRejectedWithoutFetchOrRegistration()
    {
        var registry=new RecordingRegistry();
        var search=new StubSearchProvider([new("query","Unofficial","https://law.example.com/code/22","result",1m,DateTime.UtcNow)]);
        var handler=new StubHandler(_=>throw new InvalidOperationException("Untrusted source must not be fetched."));
        var sut=new LegalAuthoritySourceBootstrapper(new HttpClient(handler),search,new StubConfiguration(),registry,NullLogger<LegalAuthoritySourceBootstrapper>.Instance);

        var result=await sut.BootstrapAsync(new(Guid.NewGuid(),"Exampleland Code section 22","NAME:EXAMPLELAND","STATUTE","Exampleland Code section 22"));

        Assert.Empty(result);
        Assert.Empty(registry.Registrations);
        Assert.Equal(0,handler.RequestCount);
    }

    [Fact]
    public async Task Retriever_MissingCoverage_BootstrapsAndResolvesInSameRun()
    {
        var descriptor=new LegalAuthoritySourceDescriptor(Guid.NewGuid(),"DISCOVERED","NAME:EXAMPLELAND","STATUTE",RegexEscape("Exampleland Code section 22"),"https://code.example.gov","/section/22",null,"FULL_PAGE_TEXT",50);
        var registry=new RecordingRegistry();
        var bootstrapper=new StubBootstrapper([descriptor]);
        var detector=new StubDetector(new("NAME:EXAMPLELAND","STATUTE","Exampleland Code section 22"));
        var handler=new StubHandler(_=>Html("<main>Exampleland Code section 22 controlling text</main>"));
        var sut=new OfficialLegalAuthorityRetriever(new HttpClient(handler),registry,bootstrapper,detector,new StubTenantAccessor(),NullLogger<OfficialLegalAuthorityRetriever>.Instance);

        var result=await sut.SearchAsync("Apply Exampleland Code section 22",LegalConfiguration());

        Assert.Single(result.Snippets);
        Assert.Equal(1,bootstrapper.CallCount);
        Assert.Equal("RESULTS_FOUND",result.Diagnostic.OutcomeCode);
    }

    private static string RegexEscape(string value)=>System.Text.RegularExpressions.Regex.Escape(value);
    private static HttpResponseMessage Html(string value)=>new(HttpStatusCode.OK){Content=new StringContent(value)};
    private static WideLegalGroundingConfiguration LegalConfiguration()=>new(true,3,5,24,15,true,"https://court.example","",true,"https://gov.example","","https://ecfr.example",true,"https://lii.example");

    private sealed class RecordingRegistry:ILegalAuthoritySourceRegistry
    {
        public List<LegalAuthoritySourceRegistration> Registrations{get;}=[];
        public Task<IReadOnlyCollection<LegalAuthoritySourceDescriptor>> GetSourcesAsync(Guid tenantId,CancellationToken cancellationToken=default)
        {
            IReadOnlyCollection<LegalAuthoritySourceDescriptor> descriptors=Registrations.Select(item=>new LegalAuthoritySourceDescriptor(Guid.NewGuid(),item.ProviderCode,item.JurisdictionCode,item.AuthorityKindCode,item.CitationPattern,item.BaseUrl,item.DocumentUrlTemplate,item.SectionAnchorTemplate,item.ExtractionStrategyCode,item.Priority)).ToList();
            return Task.FromResult(descriptors);
        }
        public Task RegisterAsync(LegalAuthoritySourceRegistration registration,CancellationToken cancellationToken=default){Registrations.Add(registration);return Task.CompletedTask;}
    }

    private sealed class StubSearchProvider(IReadOnlyCollection<WideExternalKnowledgeSnippet> results):IExternalKnowledgeProvider
    {
        public Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> SearchAsync(string query,WideExternalGroundingConfiguration configuration,CancellationToken cancellationToken=default)=>Task.FromResult(results);
    }

    private sealed class StubConfiguration:ILegalAuthorityDiscoveryConfiguration
    {
        public Task<WideExternalGroundingConfiguration> GetAsync(Guid tenantId,CancellationToken cancellationToken=default)=>Task.FromResult(new WideExternalGroundingConfiguration(true,"TEST","key",1,5,1,15));
    }

    private sealed class StubBootstrapper(IReadOnlyCollection<LegalAuthoritySourceDescriptor>? descriptors=null):ILegalAuthoritySourceBootstrapper
    {
        public int CallCount{get;private set;}
        public Task<IReadOnlyCollection<LegalAuthoritySourceDescriptor>> BootstrapAsync(LegalAuthorityDiscoveryRequest request,CancellationToken cancellationToken=default){CallCount++;return Task.FromResult(descriptors??[]);}
    }
    private sealed class StubDetector(LegalJurisdictionCitation? result=null):ILegalJurisdictionDetector
    {
        public bool TryDetect(string query,out LegalJurisdictionCitation citation){citation=result??new(string.Empty,string.Empty,string.Empty);return result is not null;}
    }
    private sealed class StubTenantAccessor:Legal.Application.Abstractions.Services.IEpistemicTenantAccessor{public Guid? TenantId{get;}=Guid.NewGuid();}
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
