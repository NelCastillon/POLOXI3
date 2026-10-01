using Legal.Application.Features.Intelligence;

namespace Legal.Web.Services;

/// <summary>
/// Scoped (per Blazor Server circuit) cache for the most recent Personal Injury
/// Decision 2 Overview result, keyed by matter.
///
/// The Overview page only populates its candidate display in-memory when the user
/// clicks "Disambiguate &amp; Answer". Navigating away (for example into Candidate
/// Full Analysis) and back builds a brand-new component instance whose
/// <c>_response</c> is null, so the display appeared blank. Caching the last
/// response here lets the Overview restore exactly what the user was viewing when
/// they return, without re-running the (expensive) pipeline.
///
/// This is deliberately circuit-scoped working memory, not a persistence layer:
/// it survives in-app navigation within the same session but is naturally
/// discarded when the circuit ends.
/// </summary>
public sealed class DecisionOverviewStateCache
{
    private readonly Dictionary<Guid, WideSearchResponse> _byMatter = new();

    /// <summary>Stores (or replaces) the latest Overview response for a matter.</summary>
    public void Save(Guid matterId, WideSearchResponse response)
    {
        if (matterId == Guid.Empty || response is null) return;
        _byMatter[matterId] = response;
    }

    /// <summary>Returns the cached Overview response for a matter, or null if none.</summary>
    public WideSearchResponse? TryGet(Guid matterId)
        => matterId != Guid.Empty && _byMatter.TryGetValue(matterId, out var response) ? response : null;

    /// <summary>Clears the cached response for a matter (for example on an explicit new run reset).</summary>
    public void Clear(Guid matterId)
        => _byMatter.Remove(matterId);
}
