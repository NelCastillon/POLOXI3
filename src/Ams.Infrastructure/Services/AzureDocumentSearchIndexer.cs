using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Ams.Infrastructure.Services;

public sealed class AzureDocumentSearchIndexer : IDocumentSearchIndexer
{
    private static readonly string[] Scope=["https://search.azure.com/.default"];
    private readonly HttpClient _http;private readonly IConfiguration _configuration;private readonly TokenCredential _credential=new DefaultAzureCredential();
    public AzureDocumentSearchIndexer(HttpClient http,IConfiguration configuration){_http=http;_configuration=configuration;}
    public async Task IndexAsync(DocumentIntakeProcessingContext context,CancellationToken cancellationToken=default)
    {
        var endpoint=_configuration["DocumentSearch:Endpoint"];
        var index=_configuration["DocumentSearch:IndexName"];
        if(string.IsNullOrWhiteSpace(endpoint)||string.IsNullOrWhiteSpace(index))throw new DocumentAiProviderException("AZURE_SEARCH_NOT_CONFIGURED","DocumentSearch:Endpoint and IndexName are required.",true);
        var documentId=context.Document?.DocumentId;
        var searchDocumentId=documentId.HasValue?$"{context.Session.IntakeSessionId:N}-{documentId.Value:N}":context.Session.IntakeSessionId.ToString("N");
        var body=new{value=new[]{new Dictionary<string,object?>{{"@search.action","mergeOrUpload"},{"id",searchDocumentId},{"intakeSessionId",context.Session.IntakeSessionId.ToString("D")},{"tenantId",context.Session.TenantId.ToString("D")},{"moduleCode",context.Session.ModuleCode},{"statusCode",context.Session.StatusCode},{"documentId",documentId?.ToString("D")},{"fileName",context.Document?.FileName},{"createdDateUtc",context.Session.CreatedDateUtc}}}};
        using var request=new HttpRequestMessage(HttpMethod.Post,$"{endpoint.TrimEnd('/')}/indexes/{Uri.EscapeDataString(index)}/docs/index?api-version=2024-07-01"){Content=JsonContent.Create(body)};var key=_configuration["DocumentSearch:ApiKey"];if(!string.IsNullOrWhiteSpace(key))request.Headers.Add("api-key",key);else request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",(await _credential.GetTokenAsync(new TokenRequestContext(Scope),cancellationToken)).Token);
        using var response=await _http.SendAsync(request,cancellationToken);if(!response.IsSuccessStatusCode)throw new DocumentAiProviderException("AZURE_SEARCH_INDEX_FAILED",$"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}",(int)response.StatusCode is 408 or 429 or >=500);
    }
}
