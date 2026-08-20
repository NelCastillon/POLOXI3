using System.Text;
using Ams.Application.Abstractions.Services;
using Ams.Infrastructure.Configuration;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Ams.Infrastructure.Services;

public sealed class DocumentIntakePayloadStore : IDocumentIntakePayloadStore
{
    private readonly BlobContainerClient _container;

    public DocumentIntakePayloadStore(IOptions<DocumentStorageOptions> options)
    {
        var value=options.Value;
        if(!string.IsNullOrWhiteSpace(value.ConnectionString))_container=new(value.ConnectionString,value.ContainerName);
        else if(!string.IsNullOrWhiteSpace(value.AccountUri))_container=new BlobServiceClient(new Uri(value.AccountUri),new DefaultAzureCredential()).GetBlobContainerClient(value.ContainerName);
        else throw new InvalidOperationException("Document storage is not configured for intake payload retention.");
    }

    public async Task<string> SaveJsonAsync(Guid tenantId,Guid intakeSessionId,string payloadType,string json,CancellationToken cancellationToken=default)
    {
        if(tenantId==Guid.Empty||intakeSessionId==Guid.Empty)throw new ArgumentException("Tenant and intake session are required.");
        await _container.CreateIfNotExistsAsync(PublicAccessType.None,cancellationToken:cancellationToken);
        var reference=$"tenants/{tenantId:D}/document-intake/{intakeSessionId:D}/{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}-{payloadType.ToLowerInvariant()}.json";
        await using var stream=new MemoryStream(Encoding.UTF8.GetBytes(json));
        await _container.GetBlobClient(reference).UploadAsync(stream,new BlobUploadOptions{HttpHeaders=new(){ContentType="application/json"},Metadata=new Dictionary<string,string>{{"tenantId",tenantId.ToString("D")},{"intakeSessionId",intakeSessionId.ToString("D")},{"payloadType",payloadType}}},cancellationToken);
        return reference;
    }

    public async Task<string> ReadJsonAsync(Guid tenantId,Guid? intakeSessionId,string storageReference,CancellationToken cancellationToken=default)
    {
        if(tenantId==Guid.Empty)throw new ArgumentException("Tenant is required.",nameof(tenantId));
        if(string.IsNullOrWhiteSpace(storageReference))throw new ArgumentException("Storage reference is required.",nameof(storageReference));
        var normalized=storageReference.TrimStart('/');
        var expectedPrefix=intakeSessionId.HasValue
            ?$"tenants/{tenantId:D}/document-intake/{intakeSessionId.Value:D}/"
            :$"tenants/{tenantId:D}/document-intake/";
        if(!normalized.StartsWith(expectedPrefix,StringComparison.OrdinalIgnoreCase))throw new UnauthorizedAccessException("The intake payload reference does not belong to the requested tenant and session scope.");
        var response=await _container.GetBlobClient(normalized).DownloadContentAsync(cancellationToken);
        return response.Value.Content.ToString();
    }
}
