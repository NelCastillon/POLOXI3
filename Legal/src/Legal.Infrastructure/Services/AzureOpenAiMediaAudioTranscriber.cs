using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision.Media;
using Legal.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Legal.Infrastructure.Services;

// Azure OpenAI Whisper-backed audio transcriber (AUDIO). Supported only when endpoint + key + deployment
// are configured; otherwise IsSupported is false and TranscribeAsync returns CapabilityUnavailable so the
// reviewer annotates manually. Produces TIME-ALIGNED segments (verbose_json). Plain-text extraction is
// never substituted for timestamped transcription; no timestamps or speaker labels are fabricated.
public sealed class AzureOpenAiMediaAudioTranscriber(
    HttpClient httpClient,
    IOptions<DocumentIntelligenceOptions> options,
    ILogger<AzureOpenAiMediaAudioTranscriber> logger) : IMediaAudioTranscriber
{
    private readonly DocumentIntelligenceOptions _options = options.Value;
    private const string Version = "azure-openai-whisper/1";

    public bool IsSupported =>
        !string.IsNullOrWhiteSpace(_options.MediaTranscriptionEndpoint) &&
        !string.IsNullOrWhiteSpace(_options.MediaTranscriptionApiKey) &&
        !string.IsNullOrWhiteSpace(_options.MediaTranscriptionDeployment);

    public async Task<MediaAudioTranscriptionResult> TranscribeAsync(
        Guid tenantId, MediaAssetVersion version, Stream audioContent, string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return Unavailable("Timestamped audio transcription is not configured; annotate intervals manually.");

        try
        {
            var uri = $"{_options.MediaTranscriptionEndpoint.TrimEnd('/')}/openai/deployments/{_options.MediaTranscriptionDeployment}/audio/transcriptions?api-version={_options.MediaTranscriptionApiVersion}";

            using var form = new MultipartFormDataContent();
            using var fileContent = new StreamContent(audioContent);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(fileContent, "file", $"{version.MediaAssetVersionId:N}.audio");
            form.Add(new StringContent("verbose_json"), "response_format");
            form.Add(new StringContent("segment"), "timestamp_granularities[]");

            using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = form };
            request.Headers.Add("api-key", _options.MediaTranscriptionApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Media transcription failed {Status} for version {VersionId}: {Body}", response.StatusCode, version.MediaAssetVersionId, body);
                return Failed($"HTTP_{(int)response.StatusCode}", "Transcription call failed; annotate intervals manually.");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var segments = new List<MediaTranscriptSegment>();
            if (json.TryGetProperty("segments", out var segmentArray) && segmentArray.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var segment in segmentArray.EnumerateArray())
                {
                    var text = segment.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(text)) { index++; continue; }
                    var startMs = ToMs(segment, "start");
                    var endMs = ToMs(segment, "end");
                    // Whisper does not diarize; speaker label stays null (tentative, reviewer assigns).
                    segments.Add(new MediaTranscriptSegment(startMs, endMs, text.Trim(), null, $"seg-{index}"));
                    index++;
                }
            }

            var coverage = segments.Count == 0
                ? "No speech segments detected."
                : $"Transcribed {segments.Count} segment(s); speaker labels unassigned (no diarization).";
            return new MediaAudioTranscriptionResult(MediaProcessorOutcome.Produced, segments, coverage, Version, null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Media transcription errored for version {VersionId}", version.MediaAssetVersionId);
            return Failed(ex.GetType().Name, "Transcription errored; annotate intervals manually.");
        }
    }

    private static long ToMs(JsonElement segment, string property)
        => segment.TryGetProperty(property, out var value) && value.TryGetDouble(out var seconds)
            ? (long)Math.Round(seconds * 1000)
            : 0;

    private static MediaAudioTranscriptionResult Unavailable(string note) =>
        new(MediaProcessorOutcome.CapabilityUnavailable, Array.Empty<MediaTranscriptSegment>(), note, Version, "CAPABILITY_UNAVAILABLE", note);

    private static MediaAudioTranscriptionResult Failed(string code, string message) =>
        new(MediaProcessorOutcome.Failed, Array.Empty<MediaTranscriptSegment>(), null, Version, code, message);
}
