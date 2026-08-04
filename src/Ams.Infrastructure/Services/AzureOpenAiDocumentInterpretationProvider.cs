using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;
using Ams.Infrastructure.Configuration;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace Ams.Infrastructure.Services;

public sealed class AzureOpenAiDocumentInterpretationProvider : IDocumentInterpretationProvider
{
    private static readonly string[] Scope=["https://cognitiveservices.azure.com/.default"];
    private readonly HttpClient _http;private readonly DocumentAiOptions _options;private readonly TokenCredential _credential=new DefaultAzureCredential();
    public AzureOpenAiDocumentInterpretationProvider(HttpClient http,IOptions<DocumentAiOptions> options){_http=http;_options=options.Value;_http.Timeout=TimeSpan.FromSeconds(Math.Max(30,_options.RequestTimeoutSeconds));}

    public async Task<DocumentInterpretationResult> InterpretAsync(DocumentInterpretationRequest request,CancellationToken cancellationToken=default)
    {
        EnsureConfigured();var clock=Stopwatch.StartNew();var endpoint=_options.AzureOpenAiEndpoint.TrimEnd('/');var uri=$"{endpoint}/openai/deployments/{Uri.EscapeDataString(_options.AzureOpenAiDeployment)}/chat/completions?api-version={Uri.EscapeDataString(_options.AzureOpenAiApiVersion)}";
        using var schema=JsonDocument.Parse(request.OutputSchemaJson);var body=new{messages=new[]{new{role="system",content=request.SystemPrompt},new{role="user",content=$"Module: {request.ModuleCode}\nCorrelation: {request.CorrelationId}\nOCR JSON:\n{request.OcrJson}"}},temperature=0,response_format=new{type="json_schema",json_schema=new{name=request.PromptCode.Replace('.','_').ToLowerInvariant(),strict=true,schema=schema.RootElement}}};
        using var message=new HttpRequestMessage(HttpMethod.Post,uri){Content=JsonContent.Create(body)};await AuthorizeAsync(message,cancellationToken);using var response=await _http.SendAsync(message,cancellationToken);var json=await response.Content.ReadAsStringAsync(cancellationToken);if(!response.IsSuccessStatusCode)throw new DocumentAiProviderException("AZURE_OPENAI_REQUEST_FAILED",$"{(int)response.StatusCode}: {json}",(int)response.StatusCode is 408 or 429 or >=500);
        clock.Stop();using var envelope=JsonDocument.Parse(json);var content=envelope.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()??throw new DocumentAiProviderException("AZURE_OPENAI_EMPTY_OUTPUT","Azure OpenAI returned no structured output.",false);using var output=JsonDocument.Parse(content);
        var classification=output.RootElement.TryGetProperty("classification",out var classificationNode)?JsonSerializer.Deserialize<DocumentClassificationOutput>(classificationNode.GetRawText())!:new(request.PromptCode=="DOCUMENT.CLASSIFICATION"?output.RootElement.GetProperty("documentTypeCode").GetString()??"UNKNOWN":"UNKNOWN",output.RootElement.TryGetProperty("confidence",out var c)?c.GetDecimal():0);
        var fields=output.RootElement.TryGetProperty("fields",out var fieldNode)?JsonSerializer.Deserialize<List<ExtractedDocumentField>>(fieldNode.GetRawText())??[]:[];var warnings=output.RootElement.TryGetProperty("warnings",out var warningNode)?JsonSerializer.Deserialize<List<ExtractedDocumentWarning>>(warningNode.GetRawText())??[]:[];var usage=envelope.RootElement.TryGetProperty("usage",out var usageNode)?usageNode:default;
        return new("AZURE_OPENAI",_options.AzureOpenAiDeployment,request.PromptCode,request.PromptVersion,classification,fields,warnings,content,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("prompt_tokens",out var input)?input.GetInt32():null,usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty("completion_tokens",out var outputTokens)?outputTokens.GetInt32():null,clock.ElapsedMilliseconds);
    }
    private void EnsureConfigured(){if(string.IsNullOrWhiteSpace(_options.AzureOpenAiEndpoint)||string.IsNullOrWhiteSpace(_options.AzureOpenAiDeployment))throw new DocumentAiProviderException("AZURE_OPENAI_NOT_CONFIGURED","DocumentAi:AzureOpenAiEndpoint and AzureOpenAiDeployment are required.",true);}
    private async Task AuthorizeAsync(HttpRequestMessage message,CancellationToken token){if(!string.IsNullOrWhiteSpace(_options.AzureOpenAiApiKey))message.Headers.Add("api-key",_options.AzureOpenAiApiKey);else message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",(await _credential.GetTokenAsync(new TokenRequestContext(Scope),token)).Token);}
}
