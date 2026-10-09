using System.Net;
using System.Text.RegularExpressions;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Intelligence;

public interface IOfficialLegalAuthoritySource
{
    Task<LegalProviderRetrievalResult> SearchAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default);
}

public interface ILegalAuthoritySourceRegistry
{
    Task<IReadOnlyCollection<LegalAuthoritySourceDescriptor>> GetSourcesAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task RegisterAsync(LegalAuthoritySourceRegistration registration,CancellationToken cancellationToken=default);
}

public sealed class LegalAuthoritySourceRegistry(IIntelligenceWideRepository repository):ILegalAuthoritySourceRegistry
{
    public Task<IReadOnlyCollection<LegalAuthoritySourceDescriptor>> GetSourcesAsync(Guid tenantId,CancellationToken cancellationToken=default) =>
        repository.GetLegalAuthoritySourcesAsync(tenantId,cancellationToken);

    public Task RegisterAsync(LegalAuthoritySourceRegistration registration,CancellationToken cancellationToken=default) =>
        repository.UpsertLegalAuthoritySourceAsync(registration,cancellationToken);
}

public interface ILegalAuthoritySourceBootstrapper
{
    Task<IReadOnlyCollection<LegalAuthoritySourceDescriptor>> BootstrapAsync(LegalAuthorityDiscoveryRequest request,CancellationToken cancellationToken=default);
}

public interface ILegalJurisdictionDetector
{
    bool TryDetect(string query,out LegalJurisdictionCitation citation);
}

public sealed record LegalJurisdictionCitation(string JurisdictionCode,string AuthorityKindCode,string CitationText);

public sealed class OfficialLegalAuthorityRetriever(
    HttpClient httpClient,
    ILegalAuthoritySourceRegistry registry,
    ILegalAuthoritySourceBootstrapper bootstrapper,
    ILegalJurisdictionDetector jurisdictionDetector,
    IEpistemicTenantAccessor tenantAccessor,
    ILegalCuratedStatuteStore curatedStatuteStore,
    IErrorLogService errorLog,
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    ILogger<OfficialLegalAuthorityRetriever> logger):IOfficialLegalAuthoritySource
{
    private static readonly Regex TemplateToken=new(@"\{(?<name>[A-Za-z][A-Za-z0-9_]*)(?::(?<operation>prefix|pad|replace):(?<argument>[^}]+))?\}",RegexOptions.Compiled);
    private static readonly Regex HtmlTag=new("<[^>]+>",RegexOptions.Compiled);
    private static readonly Regex ScriptOrStyleBlock=new(@"<(script|style)\b[^>]*>.*?</\1>",RegexOptions.Compiled|RegexOptions.IgnoreCase|RegexOptions.Singleline);
    private static readonly Regex WhiteSpace=new(@"\s+",RegexOptions.Compiled);
    public async Task<LegalProviderRetrievalResult> SearchAsync(string query,WideLegalGroundingConfiguration configuration,CancellationToken cancellationToken=default)
    {
        if(string.IsNullOrWhiteSpace(query))return Empty(false,"INVALID_QUERY");
        var tenantId=tenantAccessor.TenantId;
        if(tenantId is null||tenantId==Guid.Empty)return Empty(false,"TENANT_UNAVAILABLE");

        IReadOnlyCollection<LegalAuthoritySourceDescriptor> descriptors;
        try
        {
            descriptors=await registry.GetSourcesAsync(tenantId.Value,cancellationToken);
        }
        catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,"LEGAL-TRACE stage=3-official-authority outcome=REGISTRY_FAILURE");
            await errorLog.LogAsync("OfficialLegalAuthorityRetriever",exception,"SearchAsync/GetSources",severityCode:"Warning",contextJson:$"{{\"outcome\":\"REGISTRY_FAILURE\"}}",cancellationToken:cancellationToken);
            return Empty(true,"REGISTRY_FAILURE",exception.Message);
        }

        var matches=MatchDescriptors(query,descriptors);
        if(matches.Count==0&&jurisdictionDetector.TryDetect(query,out var jurisdictionCitation))
        {
            var discovered=await bootstrapper.BootstrapAsync(new(
                tenantId.Value,query,jurisdictionCitation.JurisdictionCode,
                jurisdictionCitation.AuthorityKindCode,jurisdictionCitation.CitationText),cancellationToken);
            if(discovered.Count>0)matches=MatchDescriptors(query,discovered);
        }
        if(matches.Count==0)return Empty(true,descriptors.Count==0?"COVERAGE_GAP":"NO_CITATIONS_RESOLVED");

        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.TimeoutSeconds));
        // A matching descriptor means the citation resolved to an authoritative document; therefore an
        // empty result is not the same as "no such authority". Track the strongest technical failure
        // encountered while fetching so systemic provider problems (access denied, untrusted redirect,
        // transport failure) surface as a technical outcome instead of being masked as NO_RESULTS.
        string? failureOutcome=null;
        string? failureDetail=null;
        string? failureCitation=null;
        string? failureUrl=null;
        int? failureStatus=null;
        foreach(var candidate in matches.OrderBy(match=>match.Descriptor.Priority).Take(configuration.MaximumSnippetsPerQuery))
        {
            var fetched=await FetchAsync(query,candidate.Descriptor,candidate.Citation,timeout.Token,cancellationToken);
            if(fetched.Snippet is not null)
                return new([fetched.Snippet],new(candidate.Descriptor.ProviderCode,true,"RESULTS_FOUND",1,1,
                    NormalizedCitation:candidate.Citation.Value,EndpointUrl:fetched.Url,HttpStatus:fetched.HttpStatus));
            if(fetched.FailureOutcome is not null&&OutcomeSeverity(fetched.FailureOutcome)>OutcomeSeverity(failureOutcome))
            {
                failureOutcome=fetched.FailureOutcome;
                failureDetail=fetched.FailureDetail;
                failureCitation=candidate.Citation.Value;
                failureUrl=fetched.Url;
                failureStatus=fetched.HttpStatus;
            }
        }
        return failureOutcome is not null
            ?Empty(true,failureOutcome,failureDetail,failureCitation,failureUrl,failureStatus)
            :Empty(true,"NO_RESULTS");
    }

    // Higher wins when several candidates fail differently: a systemic technical failure is more
    // decision-relevant than an extraction miss, so it must not be overwritten by a later NO_RESULTS.
    private static int OutcomeSeverity(string? outcome)=>outcome switch
    {
        "ACCESS_DENIED"=>4,
        "PROVIDER_FAILURE"=>3,
        "UNTRUSTED_REDIRECT_FAILURE"=>2,
        "EXTRACTION_EMPTY"=>1,
        _=>0,
    };

    // Tracks the last time a host session was warmed so repeated citations in one run do not re-navigate
    // the site root on every fetch. The shared primary handler keeps a cookie jar alive across requests,
    // so a warm-up is only needed once per host per short window (handler lifetime is ~2 minutes).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,DateTimeOffset> HostSessionWarmedUtc=new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan HostSessionTtl=TimeSpan.FromMinutes(1);

    // Establishes a cookie-bearing session by navigating the host root before fetching a section page.
    // Several official statutory hosts (e.g. California leginfo, a JSF app) now reject a cold section GET
    // that lacks the JSESSIONID / WAF cookie a browser collects on its first page load. The response body
    // is intentionally discarded; only the cookies it sets (captured by the shared CookieContainer) matter.
    // Fail-soft: any warm-up error is swallowed so the real fetch still runs and reports its own outcome.
    private async Task WarmUpHostSessionAsync(string baseUrl,CancellationToken cancellationToken)
    {
        if(!Uri.TryCreate(baseUrl,UriKind.Absolute,out var baseUri))return;
        var hostKey=baseUri.Host;
        if(HostSessionWarmedUtc.TryGetValue(hostKey,out var warmedAt)&&DateTimeOffset.UtcNow-warmedAt<HostSessionTtl)return;
        try
        {
            using var warmUp=new HttpRequestMessage(HttpMethod.Get,baseUri.GetLeftPart(UriPartial.Authority)+"/");
            warmUp.Headers.TryAddWithoutValidation("Accept","text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            warmUp.Headers.TryAddWithoutValidation("Accept-Language","en-US,en;q=0.9");
            warmUp.Headers.TryAddWithoutValidation("Accept-Encoding","gzip, deflate, br");
            warmUp.Headers.TryAddWithoutValidation("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            warmUp.Headers.TryAddWithoutValidation("sec-ch-ua","\"Chromium\";v=\"124\", \"Google Chrome\";v=\"124\", \"Not-A.Brand\";v=\"99\"");
            warmUp.Headers.TryAddWithoutValidation("sec-ch-ua-mobile","?0");
            warmUp.Headers.TryAddWithoutValidation("sec-ch-ua-platform","\"Windows\"");
            warmUp.Headers.TryAddWithoutValidation("Sec-Fetch-Site","none");
            warmUp.Headers.TryAddWithoutValidation("Sec-Fetch-Mode","navigate");
            warmUp.Headers.TryAddWithoutValidation("Sec-Fetch-User","?1");
            warmUp.Headers.TryAddWithoutValidation("Sec-Fetch-Dest","document");
            warmUp.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests","1");
            using var response=await httpClient.SendAsync(warmUp,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
            HostSessionWarmedUtc[hostKey]=DateTimeOffset.UtcNow;
            logger.LogInformation("LEGAL-TRACE stage=4-official-authority outcome=SESSION_WARMUP host={Host} status={Status}",hostKey,(int)response.StatusCode);
        }
        catch(Exception exception)when(exception is not OperationCanceledException)
        {
            // Record the attempt timestamp anyway so a persistently unreachable root does not make every
            // citation pay for a doomed warm-up; the real fetch still proceeds and reports the true outcome.
            HostSessionWarmedUtc[hostKey]=DateTimeOffset.UtcNow;
            logger.LogWarning(exception,"LEGAL-TRACE stage=4-official-authority outcome=SESSION_WARMUP_FAILED host={Host}",hostKey);
        }
    }

    private List<(LegalAuthoritySourceDescriptor Descriptor,Match Citation)> MatchDescriptors(
        string query,IReadOnlyCollection<LegalAuthoritySourceDescriptor> descriptors)
    {
        var matches=new List<(LegalAuthoritySourceDescriptor Descriptor,Match Citation)>();
        foreach(var descriptor in descriptors)
        {
            try
            {
                var citation=Regex.Match(query,descriptor.CitationPattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(250));
                if(!citation.Success)continue;
                if(IsCaliforniaCivilCode377Miscitation(descriptor,citation))
                {
                    // Defense-in-depth guard independent of DB pattern drift: California's wrongful-death /
                    // survival statutes (§§ 377.10-377.62, including § 377.60) live in the Code of Civil
                    // Procedure, NOT the Civil Code. A CIV-worded descriptor that still captures a 377-series
                    // section (e.g. because a re-seeded or newly registered Civil-Code provider lacks the
                    // 0336/0338 negative lookahead) must be refused so the citation resolves only through the
                    // CCP descriptor and never surfaces a contradictory "Cal. Civ. Code § 377.x" identity.
                    logger.LogWarning("LEGAL-TRACE stage=3-official-authority provider={Provider} outcome=CIV_377_ROUTING_GUARD section={Section}",descriptor.ProviderCode,citation.Groups["section"].Value);
                    continue;
                }
                matches.Add((descriptor,citation));
            }
            catch(ArgumentException exception)
            {
                logger.LogWarning(exception,"Ignoring invalid citation pattern for legal authority source {ProviderCode}",descriptor.ProviderCode);
            }
        }
        return matches;
    }

    // Returns true when a Civil-Code (CIV) descriptor has captured a California § 377-series section. The
    // 377-series is a Code of Civil Procedure family, so a CIV descriptor matching it is always a
    // miscitation-routing error. "Civil Code" is detected from the provider code (…_CIV / contains CIV) or
    // a "lawCode=CIV" document template; Code of Civil Procedure providers (…_CCP / lawCode=CCP) are never
    // treated as CIV, so this guard never suppresses the correct CCP resolution.
    private static bool IsCaliforniaCivilCode377Miscitation(LegalAuthoritySourceDescriptor descriptor,Match citation)
    {
        var section=citation.Groups["section"].Success?citation.Groups["section"].Value.Trim():string.Empty;
        if(!section.StartsWith("377",StringComparison.OrdinalIgnoreCase))return false;
        if(section.Length>3&&section[3] is not ('.' or ':' or '-'))return false;
        var provider=descriptor.ProviderCode??string.Empty;
        var isCcp=provider.EndsWith("_CCP",StringComparison.OrdinalIgnoreCase)
            ||provider.Contains("CCP",StringComparison.OrdinalIgnoreCase)
            ||descriptor.DocumentUrlTemplate.Contains("lawCode=CCP",StringComparison.OrdinalIgnoreCase);
        if(isCcp)return false;
        var isCiv=provider.EndsWith("_CIV",StringComparison.OrdinalIgnoreCase)
            ||provider.Contains("CIV",StringComparison.OrdinalIgnoreCase)
            ||descriptor.DocumentUrlTemplate.Contains("lawCode=CIV",StringComparison.OrdinalIgnoreCase);
        return isCiv;
    }

    private async Task<(WideExternalKnowledgeSnippet? Snippet,string? FailureOutcome,string? FailureDetail,string? Url,int? HttpStatus)> FetchAsync(string query,LegalAuthoritySourceDescriptor descriptor,Match citation,CancellationToken timeoutToken,CancellationToken cancellationToken)
    {
        // CURATED_STORE descriptors carry no live host: statute text is served from the proxy-free DB
        // store (POLOXI.Legal_AuthorityStatuteText). These rows are seeded WEB-FIRST (higher Priority
        // number) so a live source is always attempted before this fallback. Resolve the section from the
        // citation's named group and return a verified snippet with the stored provenance.
        if(descriptor.ExtractionStrategyCode.Equals("CURATED_STORE",StringComparison.OrdinalIgnoreCase))
            return await FetchCuratedAsync(query,descriptor,citation,cancellationToken);
        var relativeUrl=ExpandTemplate(descriptor.DocumentUrlTemplate,citation);
        if(relativeUrl is null)return (null,"EXTRACTION_EMPTY",null,null,null);
        var url=$"{descriptor.BaseUrl.TrimEnd('/')}/{relativeUrl.TrimStart('/')}";
        var isJsonApi=descriptor.ExtractionStrategyCode.Equals("JSON_NYSENATE",StringComparison.OrdinalIgnoreCase);
        if(isJsonApi)
        {
            // The NY Senate Open Legislation API is the only government-host source that returns live New
            // York statute text from blocked networks (nysenate.gov HTML and Justia both 403). It requires a
            // free API key, appended as a query-string parameter from configuration so the key is never
            // stored in the descriptor row. Missing key => explicit, actionable failure (not a silent 401).
            var apiKey=configuration["Legal:OfficialAuthority:NySenateApiKey"];
            if(string.IsNullOrWhiteSpace(apiKey))
            {
                logger.LogWarning("LEGAL-TRACE stage=4-official-authority provider={Provider} outcome=PROVIDER_FAILURE detail=MISSING_API_KEY",descriptor.ProviderCode);
                return (null,"PROVIDER_FAILURE","Legal:OfficialAuthority:NySenateApiKey is not configured; NY Senate API requires a key.",url,null);
            }
            url=$"{url}{(url.Contains('?')?"&":"?")}key={Uri.EscapeDataString(apiKey)}";
        }
        try
        {
            if(!isJsonApi)
                await WarmUpHostSessionAsync(descriptor.BaseUrl,timeoutToken);
            using var request=new HttpRequestMessage(HttpMethod.Get,url);
            if(isJsonApi)
            {
                // The NY Senate Open Legislation endpoint is a JSON API, not an HTML document host. Request
                // JSON explicitly and skip the browser-navigation fingerprint used to defeat HTML WAFs.
                request.Headers.TryAddWithoutValidation("Accept","application/json");
                request.Headers.TryAddWithoutValidation("Accept-Encoding","gzip, deflate, br");
                request.Headers.TryAddWithoutValidation("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            }
            else
            {
            // Egress hardening (Option 1): present a complete, consistent browser fingerprint. A bare
            // User-Agent + Accept pair is a classic bot signature that Justia/FindLaw-class WAFs answer with
            // HTTP 403. Sending the same Accept-Language / Accept-Encoding / Sec-Fetch / client-hint headers a
            // real Chrome navigation emits (and matching the UA's Chrome major version) lets a legitimate
            // document GET through. Decompression is enabled on the primary handler so Accept-Encoding is honest.
            request.Headers.TryAddWithoutValidation("Accept","text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language","en-US,en;q=0.9");
            request.Headers.TryAddWithoutValidation("Accept-Encoding","gzip, deflate, br");
            request.Headers.TryAddWithoutValidation("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("sec-ch-ua","\"Chromium\";v=\"124\", \"Google Chrome\";v=\"124\", \"Not-A.Brand\";v=\"99\"");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile","?0");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-platform","\"Windows\"");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site","none");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode","navigate");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-User","?1");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest","document");
            request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests","1");
            // A real navigation to a section page arrives from the site itself; presenting the host root as
            // Referer matches the cookie established by the warm-up and satisfies session-origin WAF checks.
            request.Headers.TryAddWithoutValidation("Referer",descriptor.BaseUrl);
            }
            using var response=await httpClient.SendAsync(request,timeoutToken);
            var status=(int)response.StatusCode;
            // Never surface the API key in logs, failure details, or the persisted snippet URL.
            var safeUrl=isJsonApi?Regex.Replace(url,@"([?&])key=[^&]*","$1key=***"):url;
            if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                logger.LogWarning("LEGAL-TRACE stage=4-official-authority provider={Provider} outcome=ACCESS_DENIED url={Url}",descriptor.ProviderCode,safeUrl);
                return (null,"ACCESS_DENIED",$"{descriptor.ProviderCode} returned HTTP {status} for {safeUrl}.",safeUrl,status);
            }
            if(!response.IsSuccessStatusCode)
                return (null,"PROVIDER_FAILURE",$"{descriptor.ProviderCode} returned HTTP {status} for {safeUrl}.",safeUrl,status);
            var finalUri=response.RequestMessage?.RequestUri;
            if(finalUri is null||!Uri.TryCreate(descriptor.BaseUrl,UriKind.Absolute,out var baseUri)||
               !finalUri.Host.Equals(baseUri.Host,StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("LEGAL-TRACE stage=4-official-authority provider={Provider} outcome=UNTRUSTED_REDIRECT url={Url}",descriptor.ProviderCode,safeUrl);
                return (null,"UNTRUSTED_REDIRECT_FAILURE",$"{descriptor.ProviderCode} redirected off the authoritative host for {safeUrl}.",safeUrl,status);
            }
            var html=await response.Content.ReadAsStringAsync(timeoutToken);
            var text=Extract(html,descriptor,citation);
            if(string.IsNullOrWhiteSpace(text))return (null,"EXTRACTION_EMPTY",null,safeUrl,status);
            // Compose ONE canonical authority identity from the descriptor the citation actually resolved
            // to, NOT the raw matched citation text. A "Cal. Civ. Code § 377.60" miscitation routed to the
            // CCP descriptor must be reported as the Code of Civil Procedure authority it really is, so the
            // display title, jurisdiction, and source version all agree on a single authority identity and
            // no obsolete Civil-Code wording survives from stale retrieval text.
            var canonicalTitle=BuildCanonicalAuthorityTitle(descriptor,citation);
            return (new(query,canonicalTitle,safeUrl,text,0m,DateTime.UtcNow)
            {
                AuthorityKind=descriptor.AuthorityKindCode,
                SourceProvider="OFFICIAL_AUTHORITY",
                SourceVersion=$"{descriptor.ProviderCode}:{descriptor.ExtractionStrategyCode}",
                Jurisdiction=NormalizeJurisdictionLabel(descriptor.JurisdictionCode),
                ProviderIdentityVerified=true,
            },null,null,safeUrl,status);
        }
        catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,"LEGAL-TRACE stage=4-official-authority provider={Provider} outcome=PROVIDER_FAILURE",descriptor.ProviderCode);
            await errorLog.LogAsync("OfficialLegalAuthorityRetriever",exception,$"Fetch/{descriptor.ProviderCode}",severityCode:"Warning",contextJson:$"{{\"outcome\":\"PROVIDER_FAILURE\",\"provider\":\"{descriptor.ProviderCode}\"}}",cancellationToken:cancellationToken);
            return (null,"PROVIDER_FAILURE",exception.Message,url,null);
        }
    }

    // Serve statute text from the proxy-free curated store for a CURATED_STORE descriptor. The section is
    // the citation's named "section" group (normalized the same way the descriptor patterns capture it).
    // A hit yields a verified snippet whose identity/provenance mirror live-retrieved evidence: canonical
    // title from the descriptor, the stored authoritative SourceUrl, VerifiedDateUtc, and SourceLabel. A
    // miss returns EXTRACTION_EMPTY so the aggregator reports no result rather than fabricating one.
    private async Task<(WideExternalKnowledgeSnippet? Snippet,string? FailureOutcome,string? FailureDetail,string? Url,int? HttpStatus)> FetchCuratedAsync(string query,LegalAuthoritySourceDescriptor descriptor,Match citation,CancellationToken cancellationToken)
    {
        var section=citation.Groups["section"].Success?citation.Groups["section"].Value.Trim():string.Empty;
        if(section.Length==0)return (null,"EXTRACTION_EMPTY",null,null,null);
        var tenantId=tenantAccessor.TenantId;
        if(tenantId is null||tenantId==Guid.Empty)return (null,"EXTRACTION_EMPTY",null,null,null);
        try
        {
            var curated=await curatedStatuteStore.GetAsync(tenantId.Value,descriptor.ProviderCode,descriptor.JurisdictionCode,section,cancellationToken);
            if(curated is null)
            {
                logger.LogInformation("LEGAL-TRACE stage=4-official-authority provider={Provider} outcome=CURATED_MISS section={Section}",descriptor.ProviderCode,section);
                return (null,"EXTRACTION_EMPTY",null,curated?.SourceUrl,null);
            }
            var canonicalTitle=BuildCanonicalAuthorityTitle(descriptor,citation);
            logger.LogInformation("LEGAL-TRACE stage=4-official-authority provider={Provider} outcome=CURATED_HIT section={Section}",descriptor.ProviderCode,section);
            return (new(query,canonicalTitle,curated.SourceUrl,curated.StatuteText,0m,curated.VerifiedDateUtc)
            {
                AuthorityKind=descriptor.AuthorityKindCode,
                SourceProvider="OFFICIAL_AUTHORITY",
                SourceVersion=$"{descriptor.ProviderCode}:CURATED_STORE",
                Jurisdiction=NormalizeJurisdictionLabel(descriptor.JurisdictionCode),
                ProviderIdentityVerified=true,
            },null,null,curated.SourceUrl,null);
        }
        catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,"LEGAL-TRACE stage=4-official-authority provider={Provider} outcome=PROVIDER_FAILURE detail=CURATED_STORE",descriptor.ProviderCode);
            await errorLog.LogAsync("OfficialLegalAuthorityRetriever",exception,$"FetchCurated/{descriptor.ProviderCode}",severityCode:"Warning",contextJson:$"{{\"outcome\":\"PROVIDER_FAILURE\",\"provider\":\"{descriptor.ProviderCode}\"}}",cancellationToken:cancellationToken);
            return (null,"PROVIDER_FAILURE",exception.Message,null,null);
        }
    }

    // Deterministically compose the single canonical authority identity for an admitted snippet from the
    // descriptor the citation resolved to. The captured section number is taken from the regex's named
    // "section" group when present (so a Civil-Code-worded 377.x miscitation still surfaces the section),
    // and the code label is derived from the provider code — never from the raw miscited wording. Result
    // form: "California Code of Civil Procedure § 377.60 (CA_LEGINFO_CCP)".
    private static string BuildCanonicalAuthorityTitle(LegalAuthoritySourceDescriptor descriptor,Match citation)
    {
        var jurisdiction=NormalizeJurisdictionLabel(descriptor.JurisdictionCode);
        var codeLabel=CanonicalCodeLabel(descriptor.ProviderCode,descriptor.AuthorityKindCode);
        var section=citation.Groups["section"].Success?citation.Groups["section"].Value.Trim():string.Empty;
        var sectionPart=section.Length>0?$" § {section}":string.Empty;
        return $"{jurisdiction} {codeLabel}{sectionPart} ({descriptor.ProviderCode})".Replace("  "," ").Trim();
    }

    // Map a provider code (e.g. CA_LEGINFO_CCP, CA_LEGINFO_CIV) to its authoritative code name. The code
    // family is the trailing token after the last underscore. Falls back to the authority-kind label so
    // an unknown provider still yields a coherent identity rather than the raw miscitation.
    private static string CanonicalCodeLabel(string providerCode,string authorityKindCode)
    {
        var family=providerCode.Split('_').LastOrDefault()?.ToUpperInvariant()??string.Empty;
        return family switch
        {
            "CCP"=>"Code of Civil Procedure",
            "CIV"=>"Civil Code",
            "PEN"=>"Penal Code",
            "PROB"=>"Probate Code",
            "VEH"=>"Vehicle Code",
            "BPC"=>"Business and Professions Code",
            "HSC"=>"Health and Safety Code",
            "LAB"=>"Labor Code",
            "INS"=>"Insurance Code",
            "GOV"=>"Government Code",
            "EVID"=>"Evidence Code",
            "WIC"=>"Welfare and Institutions Code",
            _=>string.Equals(authorityKindCode,"STATUTE",StringComparison.OrdinalIgnoreCase)?"Statute":"Authority",
        };
    }

    // Turn a stored jurisdiction code (e.g. "NAME:CALIFORNIA") into a display label ("California").
    private static string NormalizeJurisdictionLabel(string jurisdictionCode)
    {
        if(string.IsNullOrWhiteSpace(jurisdictionCode))return string.Empty;
        var raw=jurisdictionCode.Contains(':')?jurisdictionCode[(jurisdictionCode.IndexOf(':')+1)..]:jurisdictionCode;
        raw=raw.Trim();
        if(raw.Length==0)return string.Empty;
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.ToLowerInvariant());
    }

    // Minimum number of characters a FULL_PAGE_TEXT statutory body must contain after chrome removal to
    // be admitted as evidence. leginfo returns a JSF "shell" page (search box + navigation only, no
    // statute) for sections that do not exist under the requested lawCode; that shell decodes to a few
    // words of chrome. A real statute section decodes to hundreds of characters of operative text.
    private const int MinimumStatutoryBodyLength=120;

    // leginfo emits the operative statute text AFTER this sentinel ("Search Phrase: Code Text <body>").
    // On a shell page nothing meaningful follows it, so splitting on the last occurrence isolates the
    // real body and lets the length guard reject empty shells deterministically.
    private const string LeginfoBodySentinel="Code Text";

    private static string? Extract(string html,LegalAuthoritySourceDescriptor descriptor,Match citation)
    {
        if(descriptor.ExtractionStrategyCode.Equals("JSON_NYSENATE",StringComparison.OrdinalIgnoreCase))
            return ExtractNySenateJson(html);
        if(descriptor.ExtractionStrategyCode.Equals("FULL_PAGE_TEXT",StringComparison.OrdinalIgnoreCase))
            return ExtractFullPageText(html);
        if(!descriptor.ExtractionStrategyCode.Equals("HTML_ID_SECTION",StringComparison.OrdinalIgnoreCase))return null;
        var anchor=ExpandTemplate(descriptor.SectionAnchorTemplate??string.Empty,citation);
        if(string.IsNullOrWhiteSpace(anchor))return null;
        var start=Regex.Match(html,$"<[^>]+\\bid\\s*=\\s*['\"]{Regex.Escape(anchor)}['\"][^>]*>",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(250));
        if(!start.Success)return null;
        var next=Regex.Match(html[(start.Index+start.Length)..],"<[^>]+\\bid\\s*=\\s*['\"][^'\"]+['\"][^>]*>",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(250));
        var length=next.Success?start.Length+next.Index:Math.Min(html.Length-start.Index,12000);
        var fragment=html.Substring(start.Index,Math.Min(length,html.Length-start.Index));
        return WebUtility.HtmlDecode(WhiteSpace.Replace(HtmlTag.Replace(fragment," ")," ")).Trim();
    }

    // Strips markup/script, then rejects JSF shell pages that carry no statutory body. When the leginfo
    // "Code Text" sentinel is present we keep only what follows the LAST occurrence (the operative
    // statute), which is empty on a shell page. The length guard then discards chrome-only pages so they
    // are never admitted as evidence (returns null => caller reports EXTRACTION_EMPTY).
    private static string? ExtractFullPageText(string html)
    {
        var withoutScripts=ScriptOrStyleBlock.Replace(html," ");
        var decoded=WebUtility.HtmlDecode(WhiteSpace.Replace(HtmlTag.Replace(withoutScripts," ")," ")).Trim();
        if(decoded.Length==0)return null;
        var sentinelIndex=decoded.LastIndexOf(LeginfoBodySentinel,StringComparison.OrdinalIgnoreCase);
        var body=sentinelIndex>=0?decoded[(sentinelIndex+LeginfoBodySentinel.Length)..].Trim():decoded;
        return body.Length>=MinimumStatutoryBodyLength?body:null;
    }

    // Parses the NY Senate Open Legislation API JSON envelope:
    //   { "success": true, "result": { "docType":"SECTION", "title":"...", "text":"..." } }
    // The operative statute lives in result.text; title prefixes it for readability. Unsuccessful
    // envelopes (success=false, or a non-SECTION/empty-text result for a nonexistent section) yield null
    // so the caller reports EXTRACTION_EMPTY and never admits a shell as evidence.
    private static string? ExtractNySenateJson(string json)
    {
        try
        {
            using var document=System.Text.Json.JsonDocument.Parse(json);
            var root=document.RootElement;
            if(!root.TryGetProperty("success",out var success)||success.ValueKind!=System.Text.Json.JsonValueKind.True)
                return null;
            if(!root.TryGetProperty("result",out var result)||result.ValueKind!=System.Text.Json.JsonValueKind.Object)
                return null;
            var text=result.TryGetProperty("text",out var textElement)&&textElement.ValueKind==System.Text.Json.JsonValueKind.String
                ?textElement.GetString():null;
            if(string.IsNullOrWhiteSpace(text))return null;
            var title=result.TryGetProperty("title",out var titleElement)&&titleElement.ValueKind==System.Text.Json.JsonValueKind.String
                ?titleElement.GetString():null;
            var body=WhiteSpace.Replace(text," ").Trim();
            var composed=string.IsNullOrWhiteSpace(title)?body:$"{title.Trim()}\n\n{body}";
            return composed.Length>=MinimumStatutoryBodyLength?composed:null;
        }
        catch(System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string? ExpandTemplate(string template,Match citation)
    {
        var invalid=false;
        var value=TemplateToken.Replace(template,token=>
        {
            var group=citation.Groups[token.Groups["name"].Value];
            if(!group.Success){invalid=true;return string.Empty;}
            var result=group.Value;
            var operation=token.Groups["operation"].Value;
            var argument=token.Groups["argument"].Value;
            if(operation.Equals("prefix",StringComparison.OrdinalIgnoreCase)&&
               int.TryParse(argument,out var length))
                result=result[..Math.Min(length,result.Length)];
            else if(operation.Equals("pad",StringComparison.OrdinalIgnoreCase)&&
               int.TryParse(argument,out var width))
                result=result.PadLeft(width,'0');
            // replace maps one character to a replacement (e.g. "{section:replace:.-}" turns "377.60" into
            // "377-60" for static-HTML aggregators that slugify section numbers). Argument = <from><to...>:
            // the first character is replaced by the remaining substring (may be empty to delete it).
            else if(operation.Equals("replace",StringComparison.OrdinalIgnoreCase)&&argument.Length>=1)
                result=result.Replace(argument[0].ToString(),argument[1..]);
            return Uri.EscapeDataString(result);
        });
        return invalid?null:value;
    }

    private static LegalProviderRetrievalResult Empty(bool selected,string outcome,string? detail=null,string? citation=null,string? url=null,int? httpStatus=null) =>
        new([],new("OFFICIAL_AUTHORITY",selected,outcome,0,0,detail,NormalizedCitation:citation,EndpointUrl:url,HttpStatus:httpStatus));
}
