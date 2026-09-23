using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Intelligence;

public interface ILegalAuthorityDiscoveryConfiguration
{
    Task<WideExternalGroundingConfiguration> GetAsync(Guid tenantId,CancellationToken cancellationToken=default);
}

public sealed class LegalAuthorityDiscoveryConfiguration(Legal.Application.Abstractions.Persistence.IIntelligenceWideRepository repository):ILegalAuthorityDiscoveryConfiguration
{
    public Task<WideExternalGroundingConfiguration> GetAsync(Guid tenantId,CancellationToken cancellationToken=default) =>
        repository.GetExternalGroundingConfigurationAsync(tenantId,cancellationToken);
}

public sealed class LegalAuthoritySourceBootstrapper(
    HttpClient httpClient,
    IExternalKnowledgeProvider searchProvider,
    ILegalAuthorityDiscoveryConfiguration configurationProvider,
    ILegalAuthoritySourceRegistry registry,
    ILogger<LegalAuthoritySourceBootstrapper> logger):ILegalAuthoritySourceBootstrapper
{
    private static readonly Regex HtmlTag=new("<[^>]+>",RegexOptions.Compiled);
    private static readonly Regex WhiteSpace=new(@"\s+",RegexOptions.Compiled);

    public async Task<IReadOnlyCollection<LegalAuthoritySourceDescriptor>> BootstrapAsync(LegalAuthorityDiscoveryRequest request,CancellationToken cancellationToken=default)
    {
        try
        {
            var configuration=await configurationProvider.GetAsync(request.TenantId,cancellationToken);
            if(!configuration.Enabled)return [];
            var searchQuery=$"\"{request.CitationText}\" {DisplayJurisdiction(request.JurisdictionCode)} official {request.AuthorityKindCode.ToLowerInvariant()}";
            var candidates=await searchProvider.SearchAsync(searchQuery,configuration,cancellationToken);
            foreach(var candidate in candidates.OrderByDescending(item=>item.Score))
            {
                if(!TryGetTrustedGovernmentUri(candidate.Url,out var uri))continue;
                if(!await ValidateCandidateAsync(uri,request.CitationText,configuration.TimeoutSeconds,cancellationToken))continue;
                var registration=CreateRegistration(request,uri);
                await registry.RegisterAsync(registration,cancellationToken);
                logger.LogInformation("LEGAL-TRACE stage=3-authority-bootstrap outcome=REGISTERED jurisdiction={Jurisdiction} host={Host}",request.JurisdictionCode,uri.Host);
                return await registry.GetSourcesAsync(request.TenantId,cancellationToken);
            }
            logger.LogInformation("LEGAL-TRACE stage=3-authority-bootstrap outcome=NO_TRUSTED_SOURCE jurisdiction={Jurisdiction}",request.JurisdictionCode);
            return [];
        }
        catch(Exception exception)when(exception is not OperationCanceledException||!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception,"LEGAL-TRACE stage=3-authority-bootstrap outcome=DISCOVERY_FAILURE jurisdiction={Jurisdiction}",request.JurisdictionCode);
            return [];
        }
    }

    private async Task<bool> ValidateCandidateAsync(Uri uri,string citationText,int timeoutSeconds,CancellationToken cancellationToken)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        using var request=new HttpRequestMessage(HttpMethod.Get,uri);
        request.Headers.TryAddWithoutValidation("Accept","text/html");
        request.Headers.TryAddWithoutValidation("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36");
        using var response=await httpClient.SendAsync(request,timeout.Token);
        if(!response.IsSuccessStatusCode)return false;
        var finalUri=response.RequestMessage?.RequestUri;
        if(finalUri is null||!TryGetTrustedGovernmentUri(finalUri.AbsoluteUri,out _))return false;
        var mediaType=response.Content.Headers.ContentType?.MediaType;
        if(mediaType is not null&&!mediaType.Contains("html",StringComparison.OrdinalIgnoreCase)&&!mediaType.Contains("text",StringComparison.OrdinalIgnoreCase))return false;
        var html=await response.Content.ReadAsStringAsync(timeout.Token);
        var text=Normalize(WebUtility.HtmlDecode(HtmlTag.Replace(html," ")));
        var citation=Normalize(citationText);
        return citation.Length>=6&&text.Contains(citation,StringComparison.OrdinalIgnoreCase);
    }

    private static LegalAuthoritySourceRegistration CreateRegistration(LegalAuthorityDiscoveryRequest request,Uri uri)
    {
        var identity=$"{uri.Host}|{request.JurisdictionCode}|{request.AuthorityKindCode}|{request.CitationText}";
        var providerCode=$"DISCOVERED_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16]}";
        var path=uri.PathAndQuery+uri.Fragment;
        return new(providerCode,request.JurisdictionCode,request.AuthorityKindCode,
            Regex.Escape(request.CitationText),uri.GetLeftPart(UriPartial.Authority),path,null,
            "FULL_PAGE_TEXT",50,"TRUSTED_WEB_DISCOVERY",uri.AbsoluteUri,DateTime.UtcNow);
    }

    private static bool TryGetTrustedGovernmentUri(string? value,out Uri uri)
    {
        if(Uri.TryCreate(value,UriKind.Absolute,out var candidate)&&candidate.Scheme==Uri.UriSchemeHttps&&IsGovernmentHost(candidate.Host))
        {
            uri=candidate;
            return true;
        }
        uri=null!;
        return false;
    }

    private static bool IsGovernmentHost(string host)
    {
        var normalized=host.TrimEnd('.').ToLowerInvariant();
        return normalized.EndsWith(".gov",StringComparison.Ordinal)||
               normalized.EndsWith(".gov.us",StringComparison.Ordinal);
    }

    private static string DisplayJurisdiction(string code) => code.StartsWith("NAME:",StringComparison.Ordinal)?code[5..].Replace('_',' '):code;
    private static string Normalize(string value) => WhiteSpace.Replace(value," ").Trim();
}
