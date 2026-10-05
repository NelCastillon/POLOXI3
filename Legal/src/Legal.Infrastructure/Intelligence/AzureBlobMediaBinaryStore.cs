using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Legal.Application.Abstractions.Intelligence;
using Legal.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Legal.Infrastructure.Intelligence;

// Azure Blob media store. Writes original bytes once with immutable version retention (mirrors the
// document binary store) and serves authorized ranged reads for playback. Bytes stay private; delivery
// is proxied through an authorized endpoint rather than a public URL.
public sealed class AzureBlobMediaBinaryStore : IMediaBinaryStore
{
    private readonly BlobContainerClient _container;
    private readonly DocumentIntelligenceOptions _options;

    public AzureBlobMediaBinaryStore(IOptions<DocumentIntelligenceOptions> options)
    {
        _options = options.Value;
        _container = !string.IsNullOrWhiteSpace(_options.BlobConnectionString)
            ? new BlobContainerClient(_options.BlobConnectionString, _options.MediaBlobContainerName)
            : new BlobServiceClient(new Uri(_options.BlobServiceUri), new DefaultAzureCredential())
                .GetBlobContainerClient(_options.MediaBlobContainerName);
    }

    public async Task<MediaStoreResult> StoreImmutableAsync(
        Guid tenantId, Guid mediaAssetId, Guid mediaAssetVersionId, string fileName, Stream content,
        CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        var key = $"{tenantId:N}/{mediaAssetId:N}/{mediaAssetVersionId:N}{extension}";
        var blob = _container.GetBlobClient(key);
        var response = await blob.UploadAsync(content, new BlobUploadOptions
        {
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            Metadata = new Dictionary<string, string>
            {
                ["tenantId"] = tenantId.ToString("N"),
                ["mediaAssetId"] = mediaAssetId.ToString("N"),
                ["mediaAssetVersionId"] = mediaAssetVersionId.ToString("N"),
                ["originalFileName"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(fileName)))
            }
        }, cancellationToken);

        if (!string.IsNullOrWhiteSpace(response.Value.VersionId))
        {
            var version = blob.WithVersion(response.Value.VersionId);
            var retentionExpiry = DateTimeOffset.UtcNow.AddDays(_options.BlobRetentionDays);
            await version.SetImmutabilityPolicyAsync(
                new BlobImmutabilityPolicy { ExpiresOn = retentionExpiry, PolicyMode = BlobImmutabilityPolicyMode.Unlocked },
                cancellationToken: cancellationToken);
            if (_options.ApplyLegalHold)
                await version.SetLegalHoldAsync(true, cancellationToken);
        }

        var properties = (await blob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
        return new MediaStoreResult(key, properties.ContentLength);
    }

    public async Task<long> GetLengthAsync(Guid tenantId, string storageKey, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId, storageKey);
        var blob = _container.GetBlobClient(storageKey);
        var properties = (await blob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
        return properties.ContentLength;
    }

    public async Task<MediaReadResult> OpenReadAsync(
        Guid tenantId, string storageKey, long? offset, long? length, CancellationToken cancellationToken = default)
    {
        EnsureTenant(tenantId, storageKey);
        var blob = _container.GetBlobClient(storageKey);
        var total = (await blob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value.ContentLength;
        var start = Math.Clamp(offset ?? 0, 0, total);
        var count = length is { } l ? Math.Clamp(l, 0, total - start) : total - start;

        var range = new HttpRange(start, count);
        var download = await blob.DownloadStreamingAsync(new BlobDownloadOptions { Range = range }, cancellationToken);
        return new MediaReadResult(download.Value.Content, total, start, count);
    }

    private static void EnsureTenant(Guid tenantId, string storageKey)
    {
        var tenantPrefix = tenantId.ToString("N") + "/";
        if (!storageKey.Replace('\\', '/').StartsWith(tenantPrefix, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("The media storage key does not belong to the requesting tenant.");
    }
}
