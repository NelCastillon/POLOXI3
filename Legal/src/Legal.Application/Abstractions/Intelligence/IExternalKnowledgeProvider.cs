using Legal.Application.Features.Intelligence;

namespace Legal.Application.Abstractions.Intelligence;

// Live external web-knowledge retrieval used to ground time-sensitive interpretive results
// in the Wide search pipeline. Implementations (e.g. Tavily) must be fail-soft: return an
// empty collection on any provider error so enterprise search never breaks.
public interface IExternalKnowledgeProvider
{
    Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> SearchAsync(string query,WideExternalGroundingConfiguration configuration,CancellationToken cancellationToken=default);

    // Fail-soft providers return [] on error, which is indistinguishable from "nothing found". This
    // exposes the most recent provider-level failure (e.g. HTTP 401/429) so the pipeline can tell the
    // user WHY grounding produced zero evidence instead of showing a silent 0%. Null when the last
    // call succeeded or no call has been made.
    string? LastFailureReason=>null;
}
