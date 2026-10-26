using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Legal.Application.Abstractions.Intelligence;
using Legal.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Legal.Infrastructure.Intelligence;

public sealed class AzureBlobLegalDocumentBinaryStore : ILegalDocumentBinaryStore
{
    private readonly BlobContainerClient _container;
    private readonly DocumentIntelligenceOptions _options;

    public AzureBlobLegalDocumentBinaryStore(IOptions<DocumentIntelligenceOptions> options)
    {
        _options = options.Value;
        _container = !string.IsNullOrWhiteSpace(_options.BlobConnectionString)
            ? new BlobContainerClient(_options.BlobConnectionString, _options.BlobContainerName)
            : new BlobServiceClient(new Uri(_options.BlobServiceUri), new DefaultAzureCredential())
                .GetBlobContainerClient(_options.BlobContainerName);
    }

    public async Task<string> StoreImmutableAsync(Guid tenantId, Guid documentId, string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        var blob = _container.GetBlobClient($"{tenantId:N}/{documentId:N}/original{extension}");
        var response = await blob.UploadAsync(content, new BlobUploadOptions
        {
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All },
            Metadata = new Dictionary<string, string>
            {
                ["tenantId"] = tenantId.ToString("N"),
                ["documentId"] = documentId.ToString("N"),
                ["originalFileName"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(fileName)))
            }
        }, cancellationToken);

        if (string.IsNullOrWhiteSpace(response.Value.VersionId))
            throw new InvalidOperationException("Azure Blob versioning is required for immutable legal-document originals.");
        var version = blob.WithVersion(response.Value.VersionId);
        var retentionExpiry = DateTimeOffset.UtcNow.AddDays(_options.BlobRetentionDays);
        await version.SetImmutabilityPolicyAsync(
            new BlobImmutabilityPolicy
            {
                ExpiresOn = retentionExpiry,
                PolicyMode = BlobImmutabilityPolicyMode.Unlocked
            }, cancellationToken: cancellationToken);
        if (_options.ApplyLegalHold)
            await version.SetLegalHoldAsync(true, cancellationToken);

        var properties = (await version.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
        if (!string.Equals(properties.VersionId, response.Value.VersionId, StringComparison.Ordinal) ||
            properties.ImmutabilityPolicy?.ExpiresOn is null ||
            properties.ImmutabilityPolicy.ExpiresOn < retentionExpiry.AddMinutes(-1))
            throw new InvalidOperationException("Azure Blob did not confirm the required immutable version retention policy.");
        if (_options.ApplyLegalHold && properties.HasLegalHold != true)
            throw new InvalidOperationException("Azure Blob did not confirm the required legal hold.");

        return version.Uri.AbsoluteUri;
    }

    public async Task<LegalDocumentReadResult> OpenReadAsync(Guid tenantId, string storageReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageReference))
            throw new ArgumentException("A storage reference is required.", nameof(storageReference));
        if (!Uri.TryCreate(storageReference, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("The stored document reference is not a valid URI.");

        // The stored reference is the versioned blob URI; resolve the blob path relative to the
        // configured container and preserve the immutable version id when present.
        var blobName = uri.AbsolutePath.TrimStart('/');
        var containerPrefix = _container.Name + "/";
        if (blobName.StartsWith(containerPrefix, StringComparison.OrdinalIgnoreCase))
            blobName = blobName[containerPrefix.Length..];

        var blob = _container.GetBlobClient(blobName);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var versionId = query["versionid"] ?? query["versionId"];
        if (!string.IsNullOrWhiteSpace(versionId))
            blob = blob.WithVersion(versionId);

        var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return new LegalDocumentReadResult(download.Value.Content, download.Value.Details.ContentLength);
    }
}