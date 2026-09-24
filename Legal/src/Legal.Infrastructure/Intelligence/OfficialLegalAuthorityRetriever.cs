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
    ILogger<OfficialLegalAuthorityRetriever> logger):IOfficialLegalAuthoritySource
{
    private static readonly Regex TemplateToken=new(@"\{(?<name>[A-Za-z][A-Za-z0-9_]*)(?::(?<operation>prefix):(?<argument>\d+))?\}",RegexOptions.Compiled);
    private static readonly Regex HtmlTag=new("<[^>]+>",RegexOptions.Compiled);
    private static readonly Regex WhiteSpace=new(@"\s+",RegexOptions.Compiled);

    // Built-in official California statutory sources (California Legislative Information, the state's
    // authoritative code publisher at leginfo.legislature.ca.gov). These close the coverage gap where
    // an exact California statutory citation (e.g. "California Vehicle Code § 22350") had no configured
    // adapter and returned COVERAGE_GAP. Each descriptor maps a named California code to its leginfo
    // lawCode so codes_displaySection.xhtml resolves the exact section. Registry descriptors still take
    // precedence (lower Priority wins); these seeds apply only when nothing tenant-specific matches.
    private static readonly IReadOnlyList<LegalAuthoritySourceDescriptor> BuiltInCaliforniaSources = BuildCaliforniaSources();

    private static IReadOnlyList<LegalAuthoritySourceDescriptor> BuildCaliforniaSources()
    {
        // California code name -> leginfo lawCode. Kept deterministic (no aliases/LLM); covers the
        // codes most frequently cited in litigation matters.
        var codes = new (string Name, string LawCode)[]
        {
            ("Vehicle", "VEH"), ("Civil", "CIV"), ("Penal", "PEN"), ("Probate", "PROB"),
            ("Evidence", "EVID"), ("Business and Professions", "BPC"), ("Corporations", "CORP"),
            ("Family", "FAM"), ("Government", "GOV"), ("Health and Safety", "HSC"),
            ("Insurance", "INS"), ("Labor", "LAB"), ("Code of Civil Procedure", "CCP"),
        };
        return codes.Select(code => new LegalAuthoritySourceDescriptor(
            LegalAuthoritySourceId: Guid.Empty,
            ProviderCode: $"CA_LEGINFO_{code.LawCode}",
            JurisdictionCode: "NAME:CALIFORNIA",
            AuthorityKindCode: "STATUTE",
            CitationPattern: $@"\bCal(?:ifornia|\.)?\s+{Regex.Escape(code.Name)}\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)",
            BaseUrl: "https://leginfo.legislature.ca.gov",
            DocumentUrlTemplate: $"faces/codes_displaySection.xhtml?lawCode={code.LawCode}&sectionNum={{section}}",
            SectionAnchorTemplate: null,
            ExtractionStrategyCode: "FULL_PAGE_TEXT",
            Priority: 500)).ToArray();
    }

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
            return Empty(true,"REGISTRY_FAILURE",exception.Message);
        }

        var matches=MatchDescriptors(query,descriptors);
        if(matches.Count==0)
        {
            // Fall back to the built-in official California statutory sources before attempting live
            // discovery. This closes the coverage gap for exact California code citations without a
            // tenant-specific configuration or a network bootstrap round-trip.
            matches=MatchDescriptors(query,BuiltInCaliforniaSources);
        }
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

    private List<(LegalAuthoritySourceDescriptor Descriptor,Match Citation)> MatchDescriptors(
        string query,IReadOnlyCollection<LegalAuthoritySourceDescriptor> descriptors)
    {
        var matches=new List<(LegalAuthoritySourceDescriptor Descriptor,Match Citation)>();
        foreach(var descriptor in descriptors)
        {
            try
            {
                var citation=Regex.Match(query,descriptor.CitationPattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(250));
                if(citation.Success)matches.Add((descriptor,citation));
            }
            catch(ArgumentException exception)
            {
                logger.LogWarning(exception,"Ignoring invalid citation pattern for legal authority source {ProviderCode}",descriptor.ProviderCode);
            }
        }
        return matches;
    }

    private async Task<(WideExternalKnowledgeSnippet? Snippet,string? FailureOutcome,string? FailureDetail,string? Url,int? HttpStatus)> FetchAsync(string query,LegalAuthoritySourceDescriptor descriptor,Match citation,CancellationToken timeoutToken,CancellationToken cancellationToken)
    {
        var relativeUrl=ExpandTemplate(descriptor.DocumentUrlTemplate,citation);
        if(relativeUrl is null)return (null,"EXTRACTION_EMPTY",null,null,null);
        var url=$"{descriptor.BaseUrl.TrimEnd('/')}/{relativeUrl.TrimStart('/')}";
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,url);
            request.Headers.TryAddWithoutValidation("Accept","text/html");
            request.Headers.TryAddWithoutValidation("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36");
            using var response=await httpClient.SendAsync(request,timeoutToken);
            var status=(int)response.StatusCode;
            if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                logger.LogWarning("LEGAL-TRACE stage=4-official-authority provider={Provider} outcome=ACCESS_DENIED url={Url}",descriptor.ProviderCode,url);
                return (null,"ACCESS_DENIED",$"{descriptor.ProviderCode} returned HTTP {status} for {url}.",url,status);
            }
            if(!response.IsSuccessStatusCode)
                return (null,"PROVIDER_FAILURE",$"{descriptor.ProviderCode} returned HTTP {status} for {url}.",url,status);
            var finalUri=response.RequestMessage?.RequestUri;
            if(finalUri is null||!Uri.TryCreate(descriptor.BaseUrl,UriKind.Absolute,out var baseUri)||
               !finalUri.Host.Equals(baseUri.Host,StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("LEGAL-TRACE stage=4-official-authority provider={Provider} outcome=UNTRUSTED_REDIRECT url={Url}",descriptor.ProviderCode,url);
                return (null,"UNTRUSTED_REDIRECT_FAILURE",$"{descriptor.ProviderCode} redirected off the authoritative host for {url}.",url,status);
            }
            var html=await response.Content.ReadAsStringAsync(timeoutToken);
            var text=Extract(html,descriptor,citation);
            if(string.IsNullOrWhiteSpace(text))return (null,"EXTRACTION_EMPTY",null,url,status);
            return (new(query,$"{citation.Value} ({descriptor.ProviderCode})",url,text,0m,DateTime.UtcNow)
            {
                AuthorityKind=descriptor.AuthorityKindCode,
                SourceProvider="OFFICIAL_AUTHORITY",
                SourceVersion=$"{descriptor.ProviderCode}:{descriptor.ExtractionStrategyCode}",
                ProviderIdentityVerified=true,
            },null,null,url,status);
        }
        catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,"LEGAL-TRACE stage=4-official-authority provider={Provider} outcome=PROVIDER_FAILURE",descriptor.ProviderCode);
            return (null,"PROVIDER_FAILURE",exception.Message,url,null);
        }
    }

    private static string? Extract(string html,LegalAuthoritySourceDescriptor descriptor,Match citation)
    {
        if(descriptor.ExtractionStrategyCode.Equals("FULL_PAGE_TEXT",StringComparison.OrdinalIgnoreCase))
            return WebUtility.HtmlDecode(WhiteSpace.Replace(HtmlTag.Replace(html," ")," ")).Trim();
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

    private static string? ExpandTemplate(string template,Match citation)
    {
        var invalid=false;
        var value=TemplateToken.Replace(template,token=>
        {
            var group=citation.Groups[token.Groups["name"].Value];
            if(!group.Success){invalid=true;return string.Empty;}
            var result=group.Value;
            if(token.Groups["operation"].Value.Equals("prefix",StringComparison.OrdinalIgnoreCase)&&
               int.TryParse(token.Groups["argument"].Value,out var length))
                result=result[..Math.Min(length,result.Length)];
            return Uri.EscapeDataString(result);
        });
        return invalid?null:value;
    }

    private static LegalProviderRetrievalResult Empty(bool selected,string outcome,string? detail=null,string? citation=null,string? url=null,int? httpStatus=null) =>
        new([],new("OFFICIAL_AUTHORITY",selected,outcome,0,0,detail,NormalizedCitation:citation,EndpointUrl:url,HttpStatus:httpStatus));
}
