namespace Legal.Application.Abstractions.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Media binary store (Phase 3). Keeps original media bytes PRIVATE and immutable, separate from the
// document text corpus. Supports authorized ranged reads so audio/video can be streamed with HTTP
// range requests. SQL stores only the opaque storage key returned here; bytes never enter SQL.
//
// Callers MUST authorize the tenant/matter against the owning MediaAsset before requesting any read.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface IMediaBinaryStore
{
    // Store immutable original bytes for one asset version. Returns the opaque storage key + byte length.
    Task<MediaStoreResult> StoreImmutableAsync(
        Guid tenantId,
        Guid mediaAssetId,
        Guid mediaAssetVersionId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);

    // Total byte length for the stored object (for Content-Length / range math).
    Task<long> GetLengthAsync(
        Guid tenantId, string storageKey, CancellationToken cancellationToken = default);

    // Open a read stream over the stored object. When offset/length are supplied, only that byte range
    // is returned (for HTTP range playback). The returned stream is owned by the caller.
    Task<MediaReadResult> OpenReadAsync(
        Guid tenantId, string storageKey, long? offset, long? length,
        CancellationToken cancellationToken = default);
}

public sealed record MediaStoreResult(string StorageKey, long ByteLength);

public sealed record MediaReadResult(Stream Content, long TotalLength, long RangeStart, long RangeLength);
