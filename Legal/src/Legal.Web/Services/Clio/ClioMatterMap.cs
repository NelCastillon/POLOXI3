using System.Collections.Concurrent;

namespace Legal.Web.Services.Clio;

/// <summary>
/// Demo-only, in-memory map from a Clio matter id to the Judz matter id that was
/// created for it. Makes the Clio "Ingest / Sync into Judz" action idempotent at
/// the matter level so repeated runs reuse the same Judz matter instead of
/// creating duplicates. Not persisted; cleared on app restart (hackathon scope).
/// </summary>
public sealed class ClioMatterMap
{
    private readonly ConcurrentDictionary<long, Guid> _map = new();

    public bool TryGet(long clioMatterId, out Guid judzMatterId) =>
        _map.TryGetValue(clioMatterId, out judzMatterId);

    public void Set(long clioMatterId, Guid judzMatterId) =>
        _map[clioMatterId] = judzMatterId;
}
