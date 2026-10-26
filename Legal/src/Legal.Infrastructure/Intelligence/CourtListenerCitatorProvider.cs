using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Citator;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Intelligence;

// Live (external) citator provider backed by CourtListener's opinion search (v4). ADVISORY ONLY — it
// supplements, and never overrides, the DB-backed tenant treatments merged in CitatorService.
//
// How a good-law signal is derived WITHOUT fabricating one:
//  1. The authority is first LOCATED in CourtListener. If the provider cannot find the opinion at all,
//     the authority is OMITTED entirely and therefore remains "Unknown" (never silently "Good law").
//  2. Only once the authority is located, a bounded second pass searches for LATER-CITING opinions that
//     carry explicit negative-treatment language (overruled / reversed / vacated / superseded /
//     abrogated / questioned / criticized). When such citing passages are found, a conservative
//     Caution/Negative treatment is reported with the citing passage as evidence.
//  3. When the authority is located and NO negative citing passage is found, a neutral "Cited" treatment
//     (GoodLaw flag) is reported — a provider-evidenced "no negative treatment on record" signal, clearly
//     tagged COURTLISTENER so the UI shows it is externally sourced and advisory.
//
// Fail-soft: any provider/transport/timeout error returns an empty result; absence of a signal leaves
// the authority Unknown. The whole provider is gated by the DB-backed CourtListener grounding config.
public sealed class CourtListenerCitatorProvider(
    HttpClient httpClient,
    ILegalGroundingConfigurationSource configurationSource,
    ILogger<CourtListenerCitatorProvider> logger) : ICitatorProvider
{
    public const string ProviderCode = "COURTLISTENER";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Explicit negative-treatment vocabulary. Each term maps to the DB-seeded treatment code / flag so a
    // detected citing passage resolves to a real vocabulary row (never a synthesized flag).
    private static readonly (string Keyword, string TreatmentCode, string FlagLevelCode, string Polarity, string DisplayName)[] NegativeSignals =
    [
        ("overrul",     "Overruled",   "Negative", "Fatal",    "Overruled"),
        ("revers",      "Reversed",    "Negative", "Fatal",    "Reversed"),
        ("vacat",       "Vacated",     "Negative", "Fatal",    "Vacated"),
        ("supersed",    "Superseded",  "Negative", "Fatal",    "Superseded"),
        ("abrogat",     "Overruled",   "Negative", "Fatal",    "Overruled"),
        ("question",    "Questioned",  "Caution",  "Negative", "Questioned"),
        ("criticiz",    "Criticized",  "Caution",  "Negative", "Criticized"),
        ("limited by",  "Limited",     "Caution",  "Negative", "Limited"),
    ];

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<AuthorityTreatmentDto>>> GetTreatmentsAsync(
        Guid tenantId,
        IReadOnlyCollection<string> authorityIdentities,
        CancellationToken cancellationToken = default)
    {
        var empty = (IReadOnlyDictionary<string, IReadOnlyList<AuthorityTreatmentDto>>)
            new Dictionary<string, IReadOnlyList<AuthorityTreatmentDto>>(StringComparer.OrdinalIgnoreCase);

        var identities = authorityIdentities
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (identities.Length == 0)
            return empty;

        WideLegalGroundingConfiguration configuration;
        try
        {
            configuration = await configurationSource.GetConfigurationAsync(tenantId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "CITATOR-TRACE provider=COURTLISTENER outcome=CONFIG_FAILED; leaving authorities Unknown.");
            return empty;
        }

        if (!configuration.Enabled || !configuration.CourtListenerEnabled || string.IsNullOrWhiteSpace(configuration.CourtListenerBaseUrl))
            return empty;

        var baseUrl = NormalizeBaseUrl(configuration.CourtListenerBaseUrl);
        var results = new Dictionary<string, IReadOnlyList<AuthorityTreatmentDto>>(StringComparer.OrdinalIgnoreCase);

        foreach (var identity in identities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var treatments = await ResolveAuthorityAsync(identity, baseUrl, configuration, cancellationToken);
                if (treatments.Count > 0)
                    results[identity] = treatments;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Fail-soft per authority: one provider failure leaves that single authority Unknown and
                // never aborts the others.
                logger.LogWarning(exception, "CITATOR-TRACE provider=COURTLISTENER authority=\"{Authority}\" outcome=FAILED; leaving Unknown.", identity);
            }
        }

        return results;
    }

    private async Task<IReadOnlyList<AuthorityTreatmentDto>> ResolveAuthorityAsync(
        string identity, string baseUrl, WideLegalGroundingConfiguration configuration, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));

        // 1) LOCATE the authority. If it cannot be found, return nothing (stays Unknown).
        var located = await SearchFirstAsync($"{baseUrl}/api/rest/v4/search/?type=o&order_by=score%20desc&q={Uri.EscapeDataString(identity)}", configuration, timeout.Token);
        if (located is null)
        {
            logger.LogInformation("CITATOR-TRACE provider=COURTLISTENER authority=\"{Authority}\" outcome=NOT_FOUND", identity);
            return [];
        }

        var citedName = NormalizeText(located.CaseName) ?? identity;

        // 2) Bounded second pass: look for later-citing opinions carrying explicit negative-treatment
        //    language for this authority. Keyword-constrained so only a real negative passage promotes a
        //    Caution/Negative treatment.
        var negativeQuery = $"{baseUrl}/api/rest/v4/search/?type=o&order_by=score%20desc&q={Uri.EscapeDataString($"\"{citedName}\" (overruled OR reversed OR vacated OR superseded OR abrogated OR questioned OR criticized)")}";
        var citingHits = await SearchManyAsync(negativeQuery, configuration, timeout.Token);

        foreach (var hit in citingHits)
        {
            var passage = NormalizeText(BuildCitingPassage(hit));
            if (string.IsNullOrWhiteSpace(passage))
                continue;
            var lowered = passage.ToLowerInvariant();
            // Require BOTH a negative term AND the cited authority's name in the SAME passage so a generic
            // "overruled" elsewhere in the opinion cannot be mis-attributed to this authority.
            if (!NameMentioned(lowered, citedName))
                continue;
            var signal = NegativeSignals.FirstOrDefault(s => lowered.Contains(s.Keyword, StringComparison.Ordinal));
            if (signal.Keyword is null)
                continue;

            logger.LogInformation("CITATOR-TRACE provider=COURTLISTENER authority=\"{Authority}\" outcome=NEGATIVE code={Code}", identity, signal.TreatmentCode);
            return
            [
                new AuthorityTreatmentDto(
                    CitationTreatmentId: Guid.Empty,
                    CitedAuthorityIdentity: identity,
                    CitedAuthorityName: citedName,
                    CitingAuthorityName: NormalizeText(hit.CaseName),
                    CitingAuthorityRef: BuildUrl(baseUrl, hit.AbsoluteUrl),
                    TreatmentCode: signal.TreatmentCode,
                    TreatmentDisplayName: signal.DisplayName,
                    PolarityCode: signal.Polarity,
                    FlagLevelCode: signal.FlagLevelCode,
                    TreatmentDate: null,
                    Jurisdiction: NormalizeText(hit.Court),
                    SourceProviderCode: ProviderCode,
                    Notes: Truncate($"CourtListener citing passage: {passage}", 480)),
            ];
        }

        // 3) Located, no negative citing passage found → advisory "Cited" (Good Law) signal.
        logger.LogInformation("CITATOR-TRACE provider=COURTLISTENER authority=\"{Authority}\" outcome=GOOD_LAW_ADVISORY", identity);
        return
        [
            new AuthorityTreatmentDto(
                CitationTreatmentId: Guid.Empty,
                CitedAuthorityIdentity: identity,
                CitedAuthorityName: citedName,
                CitingAuthorityName: null,
                CitingAuthorityRef: BuildUrl(baseUrl, located.AbsoluteUrl),
                TreatmentCode: "Cited",
                TreatmentDisplayName: "Cited",
                PolarityCode: "Neutral",
                FlagLevelCode: "GoodLaw",
                TreatmentDate: null,
                Jurisdiction: NormalizeText(located.Court),
                SourceProviderCode: ProviderCode,
                Notes: "CourtListener located this authority with no negative treatment detected (advisory)."),
        ];
    }

    private async Task<CourtListenerSearchResult?> SearchFirstAsync(string url, WideLegalGroundingConfiguration configuration, CancellationToken cancellationToken)
        => (await SearchManyAsync(url, configuration, cancellationToken)).FirstOrDefault();

    private async Task<IReadOnlyList<CourtListenerSearchResult>> SearchManyAsync(string url, WideLegalGroundingConfiguration configuration, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        if (!string.IsNullOrWhiteSpace(configuration.CourtListenerApiToken))
            request.Headers.TryAddWithoutValidation("Authorization", $"Token {configuration.CourtListenerApiToken.Trim()}");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests || !response.IsSuccessStatusCode)
        {
            logger.LogInformation("CITATOR-TRACE provider=COURTLISTENER outcome=HTTP status={Status}", (int)response.StatusCode);
            return [];
        }

        var payload = await response.Content.ReadFromJsonAsync<CourtListenerSearchResponse>(JsonOptions, cancellationToken);
        return payload?.Results ?? [];
    }

    private static bool NameMentioned(string loweredPassage, string citedName)
    {
        // Match on the most distinctive party token(s) of the cited name so a passage must actually name
        // the authority (not merely contain a negative word).
        var tokens = citedName.ToLowerInvariant()
            .Split([' ', '.', ',', '\'', '\u2019', '-', 'v'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 4)
            .ToArray();
        return tokens.Length > 0 && tokens.Any(t => loweredPassage.Contains(t, StringComparison.Ordinal));
    }

    private static string? BuildCitingPassage(CourtListenerSearchResult result)
        => (result.Opinions?.Select(o => o.Snippet) ?? []).Append(result.Snippet).Append(result.Text)
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

    private static string BuildUrl(string baseUrl, string? absoluteUrl)
    {
        if (string.IsNullOrWhiteSpace(absoluteUrl)) return baseUrl;
        return absoluteUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? absoluteUrl : $"{baseUrl}{absoluteUrl}";
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        var apiIndex = trimmed.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
        return apiIndex > 0 ? trimmed[..apiIndex] : trimmed;
    }

    private static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var withoutMarkup = Regex.Replace(value, "<[^>]+>", " ");
        return Regex.Replace(WebUtility.HtmlDecode(withoutMarkup), @"\s+", " ").Trim();
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];

    private sealed record CourtListenerSearchResponse([property: JsonPropertyName("results")] List<CourtListenerSearchResult>? Results);

    private sealed record CourtListenerSearchResult(
        [property: JsonPropertyName("caseName")] string? CaseName,
        [property: JsonPropertyName("court")] string? Court,
        [property: JsonPropertyName("absolute_url")] string? AbsoluteUrl,
        [property: JsonPropertyName("snippet")] string? Snippet,
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("opinions")] List<CourtListenerOpinion>? Opinions);

    private sealed record CourtListenerOpinion([property: JsonPropertyName("snippet")] string? Snippet);
}
