namespace Legal.Web.Services.Clio;

/// <summary>
/// Strictly read-only Clio Manage API v4 client. Only GET operations are
/// exposed by design; no create/update/delete surface exists. All reads use the
/// server-side access token held in <see cref="ClioTokenStore"/>.
/// </summary>
public interface IClioReadOnlyClient
{
    /// <summary>
    /// Dynamically locates the Sapini matter (never a hardcoded id), then returns
    /// its metadata plus related contact and document metadata (no content).
    /// </summary>
    Task<ClioSapiniDiscovery> DiscoverSapiniAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads the raw bytes of a single Clio document (read-only). Used to feed
    /// content into the Judz intake pipeline; Clio is never written to.
    /// </summary>
    Task<ClioDocumentContent> DownloadDocumentAsync(long documentId, CancellationToken ct = default);
}

/// <summary>Raised when a read-only Clio operation cannot be completed.</summary>
public sealed class ClioReadException : Exception
{
    public ClioReadException(string message) : base(message) { }
    public ClioReadException(string message, Exception inner) : base(message, inner) { }
}
