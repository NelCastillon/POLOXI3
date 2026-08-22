using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Services;

// Isolated "Wide" variant of the POLOXI search contract. Mirrors IIntelligenceService.SearchWithPoloxiAsync
// so /intelligence/search/poloxi_wide can evolve independently from /intelligence/search/poloxi.
public interface IIntelligenceWideService
{
    Task<PoloxiSearchResponse> SearchWithPoloxiWideAsync(PoloxiSearchRequest request,CancellationToken cancellationToken=default);
    Task<WideSearchResponse> SearchDynamicAsync(WideSearchRequest request,CancellationToken cancellationToken=default);
    // Database-backed model options for the wide-search Model dropdown (active CHAT deployments).
    Task<IReadOnlyCollection<WideModelOptionDto>> GetWideModelsAsync(Guid tenantId,CancellationToken cancellationToken=default);
}
