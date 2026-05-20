using Ams.Application.Abstractions.Services;
using Ams.Infrastructure.Configuration;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Ams.Infrastructure.Services;

public sealed class AzureBlobDocumentStorageService : IDocumentStorageService
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobDocumentStorageService(IOptions<DocumentStorageOptions> options)
    {
        var storageOptions = options.Value;
        if (!string.IsNullOrWhiteSpace(storageOptions.ConnectionString))
        {
            _containerClient = new BlobContainerClient(storageOptions.ConnectionString, storageOptions.ContainerName);
        }
        else if (!string.IsNullOrWhiteSpace(storageOptions.AccountUri))
        {
            var serviceClient = new BlobServiceClient(new Uri(storageOptions.AccountUri), new DefaultAzureCredential());
            _containerClient = serviceClient.GetBlobContainerClient(storageOptions.ContainerName);
        }
        else
        {
            throw new InvalidOperationException("Document storage is not configured. Set DocumentStorage:ConnectionString or DocumentStorage:AccountUri.");
        }
    }

    public async Task<DocumentStorageUploadResult> UploadAsync(DocumentStorageUploadRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ArgumentException("FileName is required.", nameof(request));

        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobName = string.IsNullOrWhiteSpace(request.ExistingStoragePath)
            ? CreateBlobName(request.TenantId, request.FileName)
            : NormalizeStoragePath(request.ExistingStoragePath);

        var blobClient = _containerClient.GetBlobClient(blobName);
        var headers = new BlobHttpHeaders
        {
            ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType
        };

        await blobClient.UploadAsync(request.Content, new BlobUploadOptions { HttpHeaders = headers }, cancellationToken);

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        return new DocumentStorageUploadResult
        {
            StoragePath = blobName,
            FileSizeBytes = properties.Value.ContentLength,
            ContentType = properties.Value.ContentType
        };
    }

    public async Task<DocumentStorageDownloadResult?> DownloadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            return null;

        var blobClient = _containerClient.GetBlobClient(NormalizeStoragePath(storagePath));
        if (!await blobClient.ExistsAsync(cancellationToken))
            return null;

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return new DocumentStorageDownloadResult
        {
            Content = response.Value.Content,
            ContentType = response.Value.Details.ContentType,
            FileSizeBytes = response.Value.Details.ContentLength
        };
    }

    public async Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            return;

        var blobClient = _containerClient.GetBlobClient(NormalizeStoragePath(storagePath));
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    private static string CreateBlobName(Guid tenantId, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return $"tenants/{tenantId:D}/documents/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{extension}";
    }

    private static string NormalizeStoragePath(string storagePath)
    {
        if (Uri.TryCreate(storagePath, UriKind.Absolute, out var uri))
            return string.Join('/', uri.Segments.Skip(2)).TrimStart('/');

        return storagePath.TrimStart('/');
    }
}
