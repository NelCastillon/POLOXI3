using Legal.Application.Abstractions.Intelligence;
using Legal.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Legal.Infrastructure.Intelligence;

// Filesystem media store. Writes original bytes once (CreateNew) under a tenant/asset/version path and
// serves authorized ranged reads for playback. Storage key is the relative path under the media root.
public sealed class FileSystemMediaBinaryStore(IOptions<DocumentIntelligenceOptions> options) : IMediaBinaryStore
{
    private readonly string _root = ResolveRoot(options.Value.MediaStorageRoot);

    public async Task<MediaStoreResult> StoreImmutableAsync(
        Guid tenantId, Guid mediaAssetId, Guid mediaAssetVersionId, string fileName, Stream content,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName));
        var relative = Path.Combine(tenantId.ToString("N"), mediaAssetId.ToString("N"), $"{mediaAssetVersionId:N}{extension}");
        var fullPath = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using (var destination = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await content.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }
        var length = new FileInfo(fullPath).Length;
        return new MediaStoreResult(relative.Replace('\\', '/'), length);
    }

    public Task<long> GetLengthAsync(Guid tenantId, string storageKey, CancellationToken cancellationToken = default)
        => Task.FromResult(new FileInfo(Resolve(tenantId, storageKey)).Length);

    public Task<MediaReadResult> OpenReadAsync(
        Guid tenantId, string storageKey, long? offset, long? length, CancellationToken cancellationToken = default)
    {
        var path = Resolve(tenantId, storageKey);
        var total = new FileInfo(path).Length;
        var start = Math.Clamp(offset ?? 0, 0, total);
        var count = length is { } l ? Math.Clamp(l, 0, total - start) : total - start;

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (start > 0)
            stream.Seek(start, SeekOrigin.Begin);
        var ranged = new RangeLimitedStream(stream, count);
        return Task.FromResult(new MediaReadResult(ranged, total, start, count));
    }

    private string Resolve(Guid tenantId, string storageKey)
    {
        // Enforce tenant isolation: the stored key MUST begin with the caller's tenant partition.
        var normalized = storageKey.Replace('\\', '/').TrimStart('/');
        var tenantPrefix = tenantId.ToString("N") + "/";
        if (!normalized.StartsWith(tenantPrefix, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("The media storage key does not belong to the requesting tenant.");
        var fullPath = Path.GetFullPath(Path.Combine(_root, normalized));
        if (!fullPath.StartsWith(Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("The media storage key resolves outside the media root.");
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The requested media object was not found.");
        return fullPath;
    }

    private static string ResolveRoot(string configuredRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
            throw new InvalidOperationException("DocumentIntelligence:MediaStorageRoot is required.");
        return Path.GetFullPath(configuredRoot, AppContext.BaseDirectory);
    }

    // Caps the number of bytes readable from an underlying stream so a range read never over-serves.
    private sealed class RangeLimitedStream(Stream inner, long remaining) : Stream
    {
        private long _remaining = remaining;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining <= 0) return 0;
            var toRead = (int)Math.Min(count, _remaining);
            var read = inner.Read(buffer, offset, toRead);
            _remaining -= read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_remaining <= 0) return 0;
            var toRead = (int)Math.Min(buffer.Length, _remaining);
            var read = await inner.ReadAsync(buffer[..toRead], cancellationToken);
            _remaining -= read;
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
