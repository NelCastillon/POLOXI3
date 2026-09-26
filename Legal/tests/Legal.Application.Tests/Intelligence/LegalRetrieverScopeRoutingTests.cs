using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Infrastructure.Intelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class LegalRetrieverScopeRoutingTests
{
    [Fact]
    public async Task StateSettlementScope_DoesNotRouteToFederalRegulatoryProviders()
    {
        var court=new CourtSource();
        var govInfo=new ProviderSource("GOVINFO_ECFR");
        var cornell=new ProviderSource("CORNELL_LII");
        var official=new ProviderSource("OFFICIAL_AUTHORITY");
        var retriever=new LegalRetriever(court,govInfo,cornell,official,Legal.Application.Abstractions.Services.NullErrorLogService.Instance,NullLogger<LegalRetriever>.Instance);
        var scope=new LegalAuthorityScope
        {
            IssueScopeCode=LegalAuthorityIssueScopes.SettlementEnforcement,
            AuthorityRoleCode=LegalAuthorityRoles.Controlling,
            GoverningLaw="New York",
            CourtSystem="United States – State",
            StatusCode=LegalAuthorityScopeStatuses.PartiallyResolved,
        };

        var result=await retriever.SearchScopedAsync(
            new LegalProviderSearchRequest("New York settlement enforcement",LegalAuthorityKind.Any,scope),Configuration());

        Assert.Equal(1,court.CallCount);
        Assert.Equal(0,govInfo.CallCount);
        Assert.Equal(0,cornell.CallCount);
        Assert.Equal(1,official.CallCount);
        Assert.Contains(result.Providers,provider=>provider.ProviderCode=="GOVINFO_ECFR"&&provider.OutcomeCode=="COVERAGE_GAP_SCOPE");
        Assert.Contains(result.Providers,provider=>provider.ProviderCode=="CORNELL_LII"&&provider.OutcomeCode=="COVERAGE_GAP_SCOPE");
    }

    private static WideLegalGroundingConfiguration Configuration() => new(
        true,3,5,1,10,true,"https://www.courtlistener.com",string.Empty,
        false,string.Empty,string.Empty,string.Empty,false,string.Empty);

    private sealed class CourtSource:ICourtListenerLegalSource
    {
        public int CallCount { get; private set; }
        public Task<LegalProviderRetrievalResult> SearchAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default) =>
            SearchAsync(new LegalProviderSearchRequest(query,LegalAuthorityKind.Case,null),configuration,cancellationToken);
        public Task<LegalProviderRetrievalResult> SearchAsync(LegalProviderSearchRequest request,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default)
        {
            CallCount++;
            return Task.FromResult(new LegalProviderRetrievalResult([],new("COURTLISTENER",true,"NO_RESULTS",0,0)));
        }
    }

    private sealed class ProviderSource(string provider):IGovInfoLegalSource,ICornellLiiLegalSource,IOfficialLegalAuthoritySource
    {
        public int CallCount { get; private set; }
        public Task<LegalProviderRetrievalResult> SearchAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default)
        {
            CallCount++;
            return Task.FromResult(new LegalProviderRetrievalResult([],new(provider,true,"NO_RESULTS",0,0)));
        }
    }
}
