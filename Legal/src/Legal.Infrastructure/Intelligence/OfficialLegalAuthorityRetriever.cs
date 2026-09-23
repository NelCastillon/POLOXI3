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
        foreach(var candidate in matches.OrderBy(match=>match.Descriptor.Priority).Take(configuration.MaximumSnippetsPerQuery))
        {
            var result=await FetchAsync(query,candidate.Descriptor,candidate.Citation,timeout.Token,cancellationToken);
            if(result is not null)return new([result],new(candidate.Descriptor.ProviderCode,true,"RESULTS_FOUND",1,1));
        }
        return Empty(true,"NO_RESULTS");
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
                if(citation.Success)matches.Add((descriptor,citation));
            }
            catch(ArgumentException exception)
            {
                logger.LogWarning(exception,"Ignoring invalid citation pattern for legal authority source {ProviderCode}",descriptor.ProviderCode);
            }
        }
        return matches;
    }

    private async Task<WideExternalKnowledgeSnippet?> FetchAsync(string query,LegalAuthoritySourceDescriptor descriptor,Match citation,CancellationToken timeoutToken,CancellationToken cancellationToken)
    {
        var relativeUrl=ExpandTemplate(descriptor.DocumentUrlTemplate,citation);
        if(relativeUrl is null)return null;
        var url=$"{descriptor.BaseUrl.TrimEnd('/')}/{relativeUrl.TrimStart('/')}";
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,url);
            request.Headers.TryAddWithoutValidation("Accept","text/html");
            request.Headers.TryAddWithoutValidation("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36");
            using var response=await httpClient.SendAsync(request,timeoutToken);
            if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                logger.LogWarning("LEGAL-TRACE stage=4-official-authority provider={Provider} outcome=ACCESS_DENIED url={Url}",descriptor.ProviderCode,url);
                return null;
            }
            if(!response.IsSuccessStatusCode)return null;
            var finalUri=response.RequestMessage?.RequestUri;
            if(finalUri is null||!Uri.TryCreate(descriptor.BaseUrl,UriKind.Absolute,out var baseUri)||
               !finalUri.Host.Equals(baseUri.Host,StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("LEGAL-TRACE stage=4-official-authority provider={Provider} outcome=UNTRUSTED_REDIRECT url={Url}",descriptor.ProviderCode,url);
                return null;
            }
            var html=await response.Content.ReadAsStringAsync(timeoutToken);
            var text=Extract(html,descriptor,citation);
            if(string.IsNullOrWhiteSpace(text))return null;
            return new(query,$"{citation.Value} ({descriptor.ProviderCode})",url,text,0m,DateTime.UtcNow)
            {
                AuthorityKind=descriptor.AuthorityKindCode,
                SourceProvider="OFFICIAL_AUTHORITY",
                SourceVersion=$"{descriptor.ProviderCode}:{descriptor.ExtractionStrategyCode}",
                ProviderIdentityVerified=true,
            };
        }
        catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,"LEGAL-TRACE stage=4-official-authority provider={Provider} outcome=PROVIDER_FAILURE",descriptor.ProviderCode);
            return null;
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

    private static LegalProviderRetrievalResult Empty(bool selected,string outcome,string? detail=null) =>
        new([],new("OFFICIAL_AUTHORITY",selected,outcome,0,0,detail));
}
