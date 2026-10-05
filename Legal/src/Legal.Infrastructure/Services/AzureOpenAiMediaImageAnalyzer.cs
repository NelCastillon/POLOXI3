using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision.Media;
using Legal.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Legal.Infrastructure.Services;

// Azure OpenAI vision-backed image analyzer (PHOTO). Supported only when endpoint + key + deployment
// are configured; otherwise IsSupported is false and AnalyzeAsync returns CapabilityUnavailable so the
// reviewer annotates manually. Observations and regions are taken strictly from the model response;
// nothing is fabricated when the capability is absent or the call fails.
public sealed class AzureOpenAiMediaImageAnalyzer(
    HttpClient httpClient,
    IOptions<DocumentIntelligenceOptions> options,
    ILogger<AzureOpenAiMediaImageAnalyzer> logger) : IMediaImageAnalyzer
{
    private readonly DocumentIntelligenceOptions _options = options.Value;
    private const string Version = "azure-openai-vision/1";

    public bool IsSupported =>
        !string.IsNullOrWhiteSpace(_options.MediaVisionEndpoint) &&
        !string.IsNullOrWhiteSpace(_options.MediaVisionApiKey) &&
        !string.IsNullOrWhiteSpace(_options.MediaVisionDeployment);

    public async Task<MediaImageAnalysisResult> AnalyzeAsync(
        Guid tenantId, MediaAssetVersion version, Stream imageContent, string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return Unavailable("Image analysis is not configured; annotate observations manually.");

        try
        {
            using var buffer = new MemoryStream();
            await imageContent.CopyToAsync(buffer, cancellationToken);
            var base64 = Convert.ToBase64String(buffer.ToArray());
            var mime = string.IsNullOrWhiteSpace(version.MetadataJson) ? "image/jpeg" : "image/jpeg";
            var dataUrl = $"data:{mime};base64,{base64}";

            var uri = $"{_options.MediaVisionEndpoint.TrimEnd('/')}/openai/deployments/{_options.MediaVisionDeployment}/chat/completions?api-version={_options.MediaVisionApiVersion}";
            var payload = new
            {
                messages = new object[]
                {
                    new { role = "system", content = "You analyze a single still image for legal evidence review. Report only directly observable objects and legible text as discrete observations, each with an approximate normalized bounding region (x,y,width,height in [0,1]) when locatable. Do not infer identity, speed, causation, fault, or authenticity. Return strict JSON: {\"observations\":[{\"description\":string,\"normX\":number,\"normY\":number,\"normWidth\":number,\"normHeight\":number}],\"coverageNote\":string}. Omit region fields when a region cannot be located." },
                    new { role = "user", content = new object[]
                        {
                            new { type = "text", text = "List observable content in this image." },
                            new { type = "image_url", image_url = new { url = dataUrl } }
                        }
                    }
                },
                temperature = 0,
                response_format = new { type = "json_object" }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(payload) };
            request.Headers.Add("api-key", _options.MediaVisionApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Media vision analysis failed {Status} for version {VersionId}: {Body}", response.StatusCode, version.MediaAssetVersionId, body);
                return Failed($"HTTP_{(int)response.StatusCode}", "Image analysis call failed; annotate manually.");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
            var parsed = JsonNode.Parse(content)!;

            var observations = new List<MediaImageObservation>();
            if (parsed["observations"] is JsonArray array)
            {
                foreach (var node in array)
                {
                    if (node is null) continue;
                    var description = node["description"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(description)) continue;
                    observations.Add(new MediaImageObservation(
                        description,
                        TryDecimal(node["normX"]), TryDecimal(node["normY"]),
                        TryDecimal(node["normWidth"]), TryDecimal(node["normHeight"])));
                }
            }

            var coverage = parsed["coverageNote"]?.GetValue<string>() ?? "Single still analyzed; OCR limited to legible text.";
            return new MediaImageAnalysisResult(MediaProcessorOutcome.Produced, observations, coverage, Version, null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Media vision analysis errored for version {VersionId}", version.MediaAssetVersionId);
            return Failed(ex.GetType().Name, "Image analysis errored; annotate manually.");
        }
    }

    private static decimal? TryDecimal(JsonNode? node)
        => node is not null && decimal.TryParse(node.ToString(), out var value) ? value : null;

    private static MediaImageAnalysisResult Unavailable(string note) =>
        new(MediaProcessorOutcome.CapabilityUnavailable, Array.Empty<MediaImageObservation>(), note, Version, "CAPABILITY_UNAVAILABLE", note);

    private static MediaImageAnalysisResult Failed(string code, string message) =>
        new(MediaProcessorOutcome.Failed, Array.Empty<MediaImageObservation>(), null, Version, code, message);
}
