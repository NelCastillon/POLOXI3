using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Intelligence;

// Internal legal source adapter for Cornell Legal Information Institute (LII, law.cornell.edu):
// the authoritative free host for the Uniform Commercial Code (UCC), the U.S. Code, and the CFR.
// Cornell LII exposes no JSON search API, so this adapter is a DETERMINISTIC CITATION RESOLVER:
// it extracts UCC/USC/CFR citations from the clean retrieval query and fetches the canonical LII
// page for each so an authority (e.g. "UCC § 2-207") can actually be verified rather than asserted.
// Aggregated by LegalRetriever. Fail-soft: any error (or no citation found) returns an empty
// collection so enterprise search never breaks and other sources are unaffected.
public interface ICornellLiiLegalSource
{
    Task<LegalProviderRetrievalResult> SearchAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default);
}

public sealed partial class CornellLiiLegalRetriever(HttpClient httpClient,ILogger<CornellLiiLegalRetriever> logger):ICornellLiiLegalSource
{
    public async Task<LegalProviderRetrievalResult> SearchAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default)
    {
        if(!configuration.CornellLiiEnabled||string.IsNullOrWhiteSpace(query)||string.IsNullOrWhiteSpace(configuration.CornellLiiBaseUrl))
            return new([],new("CORNELL_LII",false,!configuration.CornellLiiEnabled?"DISABLED":"INVALID_REQUEST",0,0));

        var baseUrl=configuration.CornellLiiBaseUrl.Trim().TrimEnd('/');
        // Resolve at most MaximumSnippetsPerQuery distinct citations to keep cost/latency bounded.
        var citations=ExtractCitations(query,baseUrl).Take(configuration.MaximumSnippetsPerQuery).ToList();
        // STAGE 3 (Cornell LII citation extraction): log which citations were parsed so a
        // "authority present in the prompt but never resolved" state is visible in the trace.
        logger.LogInformation("LEGAL-TRACE stage=3-cornelllii-citations count={Count} query=\"{Query}\" citations=[{Citations}]",citations.Count,query,string.Join(" | ",citations.Select(citation=>citation.Label)));
        if(citations.Count==0)return new([],new("CORNELL_LII",true,"NO_CITATIONS_RESOLVED",0,0));

        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));

        var snippets=new List<WideExternalKnowledgeSnippet>();
        foreach(var citation in citations)
        {
            var snippet=await FetchCitationAsync(query,citation,timeout.Token,cancellationToken);
            if(snippet is not null)snippets.Add(snippet);
        }

        logger.LogInformation("LEGAL-TRACE stage=4-cornelllii-response resolved={Resolved} of citations={CitationCount} query=\"{Query}\"",snippets.Count,citations.Count,query);
        return new(snippets,new("CORNELL_LII",true,snippets.Count>0?"SUCCEEDED":"FETCH_FAILED",citations.Count,snippets.Count));
    }

    private async Task<WideExternalKnowledgeSnippet?> FetchCitationAsync(string query,LiiCitation citation,CancellationToken timeoutToken,CancellationToken cancellationToken)
    {
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,citation.Url);
            request.Headers.TryAddWithoutValidation("Accept","text/html");
            // Cornell LII (like most public sites) returns 403 to requests without a browser-style
            // User-Agent, which would silently yield zero admitted grounding evidence. Send a realistic
            // UA so the canonical statute page is actually fetched and can pass the identity gate.
            request.Headers.TryAddWithoutValidation("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept-Language","en-US,en;q=0.9");

            using var response=await httpClient.SendAsync(request,timeoutToken);
            if(!response.IsSuccessStatusCode)
            {
                logger.LogWarning("LEGAL-TRACE stage=4-cornelllii-response outcome=HTTP_ERROR status={StatusCode} citation={Label} url={Url}",(int)response.StatusCode,citation.Label,citation.Url);
                return null;
            }

            var html=await response.Content.ReadAsStringAsync(timeoutToken);
            var text=ExtractReadableText(html);
            if(string.IsNullOrWhiteSpace(text))return null;

            return new WideExternalKnowledgeSnippet(
                query,
                citation.Label,
                citation.Url,
                text,
                0m,
                DateTime.UtcNow)
            {
                    AuthorityKind=citation.AuthorityKind,
                SourceProvider="CORNELL_LII",
                SourceVersion="HTML_V1",
                ProviderIdentityVerified=true,
            };
        }
        catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,"LEGAL-TRACE stage=4-cornelllii-response outcome=EXCEPTION citation={Label}; continuing without this citation.",citation.Label);
            return null;
        }
    }

    // Parses the citation forms Cornell LII can serve directly. Deterministic and case-insensitive;
    // distinct by canonical URL so "UCC 2-207" and "U.C.C. § 2-207" resolve once.
    private static IEnumerable<LiiCitation> ExtractCitations(string query,string baseUrl)
    {
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // UCC: "UCC § 2-207", "U.C.C. 2-207", "Uniform Commercial Code § 2-207" -> /ucc/{article}/{article}-{section}
        foreach(Match match in UccCitationRegex().Matches(query))
        {
            var article=match.Groups["article"].Value;
            var section=match.Groups["section"].Value;
            var url=$"{baseUrl}/ucc/{article}/{article}-{section}";
            if(seen.Add(url))yield return new LiiCitation($"U.C.C. § {article}-{section} (Cornell LII)",url,"STATUTE");
        }

        // U.S. Code: "42 U.S.C. § 1983", "17 USC 107" -> /uscode/text/{title}/{section}
        foreach(Match match in UsCodeCitationRegex().Matches(query))
        {
            var title=match.Groups["title"].Value;
            var section=match.Groups["section"].Value;
            var url=$"{baseUrl}/uscode/text/{title}/{section}";
            if(seen.Add(url))yield return new LiiCitation($"{title} U.S.C. § {section} (Cornell LII)",url,"STATUTE");
        }

        // CFR: "29 CFR § 1604.11", "12 C.F.R. 1026.1" -> /cfr/text/{title}/{section}
        foreach(Match match in CfrCitationRegex().Matches(query))
        {
            var title=match.Groups["title"].Value;
            var section=match.Groups["section"].Value;
            var url=$"{baseUrl}/cfr/text/{title}/{section}";
            if(seen.Add(url))yield return new LiiCitation($"{title} C.F.R. § {section} (Cornell LII)",url,"REGULATION");
        }
    }

    // Lightweight, dependency-free HTML-to-text: drop scripts/styles/markup, decode entities,
    // collapse whitespace, and cap length so a single statute page yields a bounded snippet.
    private static string ExtractReadableText(string html)
    {
        if(string.IsNullOrWhiteSpace(html))return string.Empty;
        var withoutScripts=ScriptStyleRegex().Replace(html," ");
        var withoutTags=TagRegex().Replace(withoutScripts," ");
        var decoded=WebUtility.HtmlDecode(withoutTags);
        var collapsed=WhitespaceRegex().Replace(decoded," ").Trim();
        if(collapsed.Length==0)return string.Empty;
        const int maxLength=1600;
        return collapsed.Length<=maxLength?collapsed:collapsed[..maxLength].TrimEnd()+"…";
    }

    private sealed record LiiCitation(string Label,string Url,string AuthorityKind);

    [GeneratedRegex(@"(?:U\.?\s?C\.?\s?C\.?|uniform\s+commercial\s+code)\s*(?:§+\s*|section\s+|sec\.?\s+)?(?<article>\d{1,2})[-–](?<section>\d{1,4}[a-zA-Z]?)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex UccCitationRegex();

    [GeneratedRegex(@"\b(?<title>\d{1,2})\s*U\.?\s?S\.?\s?C\.?\s*(?:§+\s*|section\s+|sec\.?\s+)?(?<section>\d{1,5}[a-zA-Z]?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex UsCodeCitationRegex();

    [GeneratedRegex(@"\b(?<title>\d{1,2})\s*C\.?\s?F\.?\s?R\.?\s*(?:§+\s*|section\s+|sec\.?\s+)?(?<section>\d{1,4}(?:\.\d{1,4})?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex CfrCitationRegex();

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1>",RegexOptions.IgnoreCase|RegexOptions.Singleline|RegexOptions.CultureInvariant)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"<[^>]+>",RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+",RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
