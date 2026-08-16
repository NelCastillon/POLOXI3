using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;
using Azure.Core;

namespace Ams.Infrastructure.Services;

public sealed class AzureDocumentIntelligenceOcrProvider(HttpClient http, IDocumentOcrRouteRepository routeRepository, TokenCredential credential) : IDocumentOcrProvider
{
    private static readonly string[] Scope=["https://cognitiveservices.azure.com/.default"];

    public async Task<DocumentOcrResult> AnalyzeAsync(DocumentOcrRequest request,CancellationToken cancellationToken=default)
    {
        var route=await routeRepository.GetRouteAsync(request.TenantId,cancellationToken)??throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_NOT_CONFIGURED","No active database-backed Document Intelligence route is configured for this tenant.",true);var endpoint=ResolveEndpoint(route.Endpoint);EnsureConfigured(route,endpoint);var clock=Stopwatch.StartNew();
        endpoint=endpoint.TrimEnd('/');
        var uri=$"{endpoint}/documentintelligence/documentModels/{Uri.EscapeDataString(route.ModelId)}:analyze?api-version={Uri.EscapeDataString(route.ApiVersion)}";
        if(!request.Content.CanSeek)throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_CONTENT_NOT_SEEKABLE","Document Intelligence analysis requires a seekable, buffered content stream.",false);
        request.Content.Position=0;
        using var message=new HttpRequestMessage(HttpMethod.Post,uri){Content=new StreamContent(request.Content)};message.Content.Headers.ContentType=new MediaTypeHeaderValue(request.ContentType);
        await AuthorizeAsync(message,route,cancellationToken);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);timeout.CancelAfter(TimeSpan.FromSeconds(route.TimeoutSeconds));
        using var response=await http.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
        if(response.StatusCode!=(System.Net.HttpStatusCode)202)throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_SUBMIT_FAILED",await ErrorAsync(response,cancellationToken),IsRetryable(response.StatusCode));
        var operation=response.Headers.TryGetValues("Operation-Location",out var operationLocations)?operationLocations.FirstOrDefault():response.Headers.Location?.ToString();
        if(string.IsNullOrWhiteSpace(operation))throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_OPERATION_MISSING","Azure Document Intelligence did not return an operation location.",true);
        string json;while(true){await Task.Delay(TimeSpan.FromSeconds(1),timeout.Token);using var poll=new HttpRequestMessage(HttpMethod.Get,operation);await AuthorizeAsync(poll,route,timeout.Token);using var result=await http.SendAsync(poll,timeout.Token);json=await result.Content.ReadAsStringAsync(timeout.Token);if(!result.IsSuccessStatusCode)throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_POLL_FAILED",json,IsRetryable(result.StatusCode));using var root=JsonDocument.Parse(json);var status=root.RootElement.GetProperty("status").GetString();if(string.Equals(status,"succeeded",StringComparison.OrdinalIgnoreCase))break;if(string.Equals(status,"failed",StringComparison.OrdinalIgnoreCase))throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_ANALYSIS_FAILED",json,false);}
        clock.Stop();using var document=JsonDocument.Parse(json);var analyze=document.RootElement.GetProperty("analyzeResult");var content=analyze.TryGetProperty("content",out var text)?text.GetString()??string.Empty:string.Empty;var pages=new List<DocumentOcrPage>();if(analyze.TryGetProperty("pages",out var pageArray))foreach(var page in pageArray.EnumerateArray()){var number=page.GetProperty("pageNumber").GetInt32();var fields=new List<DocumentOcrField>();if(page.TryGetProperty("words",out var words))foreach(var word in words.EnumerateArray())fields.Add(new("word",word.GetProperty("content").GetString(),word.TryGetProperty("confidence",out var wordConfidence)?wordConfidence.GetDecimal():null,word.TryGetProperty("polygon",out var polygon)?polygon.GetRawText():null));pages.Add(new(number,string.Join(' ',fields.Select(x=>x.Value)),fields,[]));}
        var confidence=pages.SelectMany(x=>x.Fields).Where(x=>x.Confidence.HasValue).Select(x=>x.Confidence!.Value).DefaultIfEmpty().Average();return new("AZURE_DOCUMENT_INTELLIGENCE",route.ModelId,content,pages,confidence==0?null:confidence,json,clock.ElapsedMilliseconds);
    }

    private static string ResolveEndpoint(string endpoint){if(!endpoint.StartsWith("env://",StringComparison.OrdinalIgnoreCase))return endpoint;var variable=endpoint["env://".Length..].Trim();var value=string.IsNullOrWhiteSpace(variable)?null:Environment.GetEnvironmentVariable(variable);if(string.IsNullOrWhiteSpace(value))throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_ENDPOINT_MISSING",$"Document Intelligence endpoint environment variable '{variable}' is not configured.",true);return value;}
    private static void EnsureConfigured(DocumentOcrRoute route,string endpoint){if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||uri.Scheme!=Uri.UriSchemeHttps)throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_NOT_CONFIGURED","The database-backed Document Intelligence endpoint must resolve to an absolute HTTPS URI.",true);if(!string.IsNullOrWhiteSpace(route.CredentialReference)&&!route.CredentialReference.StartsWith("env://",StringComparison.OrdinalIgnoreCase))throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_CREDENTIAL_INVALID","Document Intelligence credentials must use env://VARIABLE_NAME or managed identity.",false);}
    private async Task AuthorizeAsync(HttpRequestMessage message,DocumentOcrRoute route,CancellationToken token){if(!string.IsNullOrWhiteSpace(route.CredentialReference)){var variable=route.CredentialReference["env://".Length..].Trim();var key=string.IsNullOrWhiteSpace(variable)?null:Environment.GetEnvironmentVariable(variable);if(string.IsNullOrWhiteSpace(key))throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_SECRET_MISSING",$"Document Intelligence credential environment variable '{variable}' is not configured.",true);message.Headers.Add("Ocp-Apim-Subscription-Key",key);}else message.Headers.Authorization=new("Bearer",(await credential.GetTokenAsync(new TokenRequestContext(Scope),token)).Token);}
    private static bool IsRetryable(System.Net.HttpStatusCode status)=>(int)status is 408 or 429 or >=500;
    private static async Task<string> ErrorAsync(HttpResponseMessage response,CancellationToken token)=>$"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(token)}";
}

public sealed class DocumentAiProviderException(string code,string message,bool retryable):Exception(message){public string Code{get;}=code;public bool Retryable{get;}=retryable;}
