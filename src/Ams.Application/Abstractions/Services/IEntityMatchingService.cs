using Ams.Application.Features.SearchMatching;

namespace Ams.Application.Abstractions.Services;

public interface IEntityMatchingService
{
    Task<EntityMatchResult> FindMatchesAsync(EntityMatchRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchMatchResult>> SearchAsync(EnterpriseFuzzySearchRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchMatchResult>> SearchFastAsync(EnterpriseFuzzySearchRequest request, CancellationToken cancellationToken = default);
    Task<EntityMatchResult> FindModuleMatchesAsync(ModuleMatchRequest request, CancellationToken cancellationToken = default);
}
