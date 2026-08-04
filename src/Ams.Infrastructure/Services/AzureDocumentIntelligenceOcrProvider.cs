using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;
using Ams.Infrastructure.Configuration;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace Ams.Infrastructure.Services;

public sealed class AzureDocumentIntelligenceOcrProvider : IDocumentOcrProvider
{
    private static readonly string[] Scope=["https://cognitiveservices.azure.com/.default"];
    private readonly HttpClient _http;
    private readonly DocumentAiOptions _options;
    private readonly TokenCredential _credential=new DefaultAzureCredential();

    public AzureDocumentIntelligenceOcrProvider(HttpClient http,IOptions<DocumentAiOptions> options){_http=http;_options=options.Value;_http.Timeout=TimeSpan.FromSeconds(Math.Max(30,_options.RequestTimeoutSeconds));}

    public async Task<DocumentOcrResult> AnalyzeAsync(DocumentOcrRequest request,CancellationToken cancellationToken=default)
    {
        EnsureConfigured();var clock=Stopwatch.StartNew();
        var endpoint=_options.DocumentIntelligenceEndpoint.TrimEnd('/');
        var uri=$"{endpoint}/documentintelligence/documentModels/{Uri.EscapeDataString(_options.DocumentIntelligenceModelId)}:analyze?api-version={Uri.EscapeDataString(_options.DocumentIntelligenceApiVersion)}";
        using var message=new HttpRequestMessage(HttpMethod.Post,uri){Content=new StreamContent(request.Content)};message.Content.Headers.ContentType=new MediaTypeHeaderValue(request.ContentType);
        await AuthorizeAsync(message,cancellationToken);
        using var response=await _http.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
        if(response.StatusCode!=(System.Net.HttpStatusCode)202)throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_SUBMIT_FAILED",await ErrorAsync(response,cancellationToken),IsRetryable(response.StatusCode));
        var operation=response.Headers.Location?.ToString()??throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_OPERATION_MISSING","Azure Document Intelligence did not return an operation location.",true);
        string json;while(true){await Task.Delay(TimeSpan.FromSeconds(1),cancellationToken);using var poll=new HttpRequestMessage(HttpMethod.Get,operation);await AuthorizeAsync(poll,cancellationToken);using var result=await _http.SendAsync(poll,cancellationToken);json=await result.Content.ReadAsStringAsync(cancellationToken);if(!result.IsSuccessStatusCode)throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_POLL_FAILED",json,IsRetryable(result.StatusCode));using var root=JsonDocument.Parse(json);var status=root.RootElement.GetProperty("status").GetString();if(string.Equals(status,"succeeded",StringComparison.OrdinalIgnoreCase))break;if(string.Equals(status,"failed",StringComparison.OrdinalIgnoreCase))throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_ANALYSIS_FAILED",json,false);}
        clock.Stop();using var document=JsonDocument.Parse(json);var analyze=document.RootElement.GetProperty("analyzeResult");var content=analyze.TryGetProperty("content",out var text)?text.GetString()??string.Empty:string.Empty;var pages=new List<DocumentOcrPage>();if(analyze.TryGetProperty("pages",out var pageArray))foreach(var page in pageArray.EnumerateArray()){var number=page.GetProperty("pageNumber").GetInt32();var fields=new List<DocumentOcrField>();if(page.TryGetProperty("words",out var words))foreach(var word in words.EnumerateArray())fields.Add(new("word",word.GetProperty("content").GetString(),word.TryGetProperty("confidence",out var wordConfidence)?wordConfidence.GetDecimal():null,word.TryGetProperty("polygon",out var polygon)?polygon.GetRawText():null));pages.Add(new(number,string.Join(' ',fields.Select(x=>x.Value)),fields,[]));}
        var confidence=pages.SelectMany(x=>x.Fields).Where(x=>x.Confidence.HasValue).Select(x=>x.Confidence!.Value).DefaultIfEmpty().Average();return new("AZURE_DOCUMENT_INTELLIGENCE",_options.DocumentIntelligenceModelId,content,pages,confidence==0?null:confidence,json,clock.ElapsedMilliseconds);
    }

    private void EnsureConfigured(){if(string.IsNullOrWhiteSpace(_options.DocumentIntelligenceEndpoint))throw new DocumentAiProviderException("DOCUMENT_INTELLIGENCE_NOT_CONFIGURED","DocumentAi:DocumentIntelligenceEndpoint is required.",true);}
    private async Task AuthorizeAsync(HttpRequestMessage message,CancellationToken token){if(!string.IsNullOrWhiteSpace(_options.DocumentIntelligenceApiKey))message.Headers.Add("Ocp-Apim-Subscription-Key",_options.DocumentIntelligenceApiKey);else message.Headers.Authorization=new("Bearer",(await _credential.GetTokenAsync(new TokenRequestContext(Scope),token)).Token);}
    private static bool IsRetryable(System.Net.HttpStatusCode status)=>(int)status is 408 or 429 or >=500;
    private static async Task<string> ErrorAsync(HttpResponseMessage response,CancellationToken token)=>$"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(token)}";
}

public sealed class DocumentAiProviderException(string code,string message,bool retryable):Exception(message){public string Code{get;}=code;public bool Retryable{get;}=retryable;}
