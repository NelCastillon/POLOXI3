using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Intelligence;

// Live external web-knowledge retrieval used to ground time-sensitive interpretive results
// in the Wide search pipeline. Implementations (e.g. Tavily) must be fail-soft: return an
// empty collection on any provider error so enterprise search never breaks.
public interface IExternalKnowledgeProvider
{
    Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> SearchAsync(string query,WideExternalGroundingConfiguration configuration,CancellationToken cancellationToken=default);
}
