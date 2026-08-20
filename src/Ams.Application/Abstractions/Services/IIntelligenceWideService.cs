using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Services;

// Isolated "Wide" variant of the EPH search contract. Mirrors IIntelligenceService.SearchWithEphAsync
// so /intelligence/search/eph_wide can evolve independently from /intelligence/search/eph.
public interface IIntelligenceWideService
{
    Task<EphSearchResponse> SearchWithEphWideAsync(EphSearchRequest request,CancellationToken cancellationToken=default);
    Task<WideSearchResponse> SearchDynamicAsync(WideSearchRequest request,CancellationToken cancellationToken=default);
}
