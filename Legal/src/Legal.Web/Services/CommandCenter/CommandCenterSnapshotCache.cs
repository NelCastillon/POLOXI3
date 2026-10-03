using System.Collections.Concurrent;

namespace Legal.Web.Services.CommandCenter;

/// <summary>
/// Demo-reliability cache for composed <see cref="MatterIntelligenceSnapshot"/> values.
/// After a successful, data-rich build the snapshot is retained in memory so that a
/// later Clio/API outage can still render the last good Command Center instead of an
/// empty page. Not persisted; cleared on app restart (hackathon scope). Security is
/// unaffected — the provider-safe projection is always re-derived at render time.
/// </summary>
public sealed class CommandCenterSnapshotCache
{
    private readonly ConcurrentDictionary<Guid, MatterIntelligenceSnapshot> _cache = new();

    public bool TryGet(Guid matterId, out MatterIntelligenceSnapshot snapshot) =>
        _cache.TryGetValue(matterId, out snapshot!);

    public void Set(Guid matterId, MatterIntelligenceSnapshot snapshot) =>
        _cache[matterId] = snapshot;
}
