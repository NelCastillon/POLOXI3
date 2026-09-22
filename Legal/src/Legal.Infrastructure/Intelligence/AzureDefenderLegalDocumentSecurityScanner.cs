using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Legal.Infrastructure.Intelligence;

public sealed class AzureDefenderLegalDocumentSecurityScanner : ILegalDocumentSecurityScanner
{
    private const string ScanResultTag = "Malware Scanning scan result";
    private readonly BlobContainerClient _container;
    private readonly DocumentIntelligenceOptions _options;

    public AzureDefenderLegalDocumentSecurityScanner(IOptions<DocumentIntelligenceOptions> options)
    {
        _options = options.Value;
        _container = !string.IsNullOrWhiteSpace(_options.BlobConnectionString)
            ? new BlobContainerClient(_options.BlobConnectionString, _options.DefenderScanContainerName)
            : new BlobServiceClient(new Uri(_options.BlobServiceUri), new Azure.Identity.DefaultAzureCredential())
                .GetBlobContainerClient(_options.DefenderScanContainerName);
    }

    public async Task<LegalDocumentSecurityScanResult> ScanAsync(string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var blob = _container.GetBlobClient($"pending/{Guid.NewGuid():N}/{Path.GetFileName(fileName)}");
        await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.DefenderScanTimeoutSeconds));
        try
        {
            while (true)
            {
                var tags = await blob.GetTagsAsync(cancellationToken: timeout.Token);
                if (tags.Value.Tags.TryGetValue(ScanResultTag, out var result))
                {
                    if (result.Equals("No threats found", StringComparison.OrdinalIgnoreCase))
                    {
                        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
                        return new LegalDocumentSecurityScanResult("CLEAN");
                    }

                    if (result.Contains("malicious", StringComparison.OrdinalIgnoreCase) || result.Contains("threat", StringComparison.OrdinalIgnoreCase))
                    {
                        await MarkQuarantinedAsync(blob, "MALICIOUS", cancellationToken);
                        return new LegalDocumentSecurityScanResult("MALICIOUS", blob.Uri.AbsoluteUri);
                    }

                    await MarkQuarantinedAsync(blob, "INCONCLUSIVE", cancellationToken);
                    return new LegalDocumentSecurityScanResult("INCONCLUSIVE", blob.Uri.AbsoluteUri);
                }

                await Task.Delay(TimeSpan.FromSeconds(_options.DefenderScanPollSeconds), timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await MarkQuarantinedAsync(blob, "SCAN_TIMEOUT", CancellationToken.None);
            return new LegalDocumentSecurityScanResult("SCAN_TIMEOUT", blob.Uri.AbsoluteUri);
        }
    }

    private static Task MarkQuarantinedAsync(BlobClient blob, string status, CancellationToken cancellationToken) =>
        blob.SetMetadataAsync(new Dictionary<string, string>
        {
            ["quarantineStatus"] = status,
            ["quarantinedDateUtc"] = DateTime.UtcNow.ToString("O")
        }, cancellationToken: cancellationToken);
}