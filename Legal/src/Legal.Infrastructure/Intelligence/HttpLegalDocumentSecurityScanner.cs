using System.Net.Http.Json;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Legal.Infrastructure.Intelligence;

public sealed class HttpLegalDocumentSecurityScanner(HttpClient httpClient, IOptions<DocumentIntelligenceOptions> options) : ILegalDocumentSecurityScanner
{
    private readonly DocumentIntelligenceOptions _options = options.Value;

    public async Task<LegalDocumentSecurityScanResult> ScanAsync(string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(_options.MalwareScanEndpoint, UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("DocumentIntelligence:MalwareScanEndpoint must be configured before document intake can run.");

        using var requestContent = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new(contentType);
        requestContent.Add(streamContent, "file", Path.GetFileName(fileName));
        using var response = await httpClient.PostAsync(endpoint, requestContent, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ScanResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("The malware scanner returned no status.");
        return string.IsNullOrWhiteSpace(result.Status)
            ? throw new InvalidDataException("The malware scanner returned an empty status.")
            : new LegalDocumentSecurityScanResult(result.Status.Trim().ToUpperInvariant());
    }

    private sealed record ScanResponse(string Status);
}
