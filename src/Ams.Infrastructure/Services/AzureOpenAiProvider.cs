using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ams.Application.Abstractions.Intelligence;
using Azure.Core;
using Azure.Identity;

namespace Ams.Infrastructure.Services;

public sealed class AzureOpenAiProvider(HttpClient httpClient):IAiProvider
{
    private static readonly string[] Scope=["https://cognitiveservices.azure.com/.default"];
    private readonly TokenCredential _managedIdentity=new DefaultAzureCredential();
    public string ProviderTypeCode=>"AZURE_OPENAI";

    public async Task<AiProviderHealth> CheckHealthAsync(AiProviderContext context,CancellationToken cancellationToken=default)
    {
        var timer=Stopwatch.StartNew();
        try
        {
            EnsureConfigured(context);using var timeout=CreateTimeout(context,cancellationToken);using var request=new HttpRequestMessage(HttpMethod.Get,BuildUri(context,""));await AuthorizeAsync(request,context,timeout.Token);using var response=await httpClient.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);return response.IsSuccessStatusCode?new("HEALTHY","Provider deployment is available.",timer.Elapsed):new("UNHEALTHY",$"Provider health probe returned HTTP {(int)response.StatusCode}.",timer.Elapsed);
        }
        catch(OperationCanceledException)when(!cancellationToken.IsCancellationRequested){return new("UNHEALTHY","Provider health probe timed out.",timer.Elapsed);}
        catch(Exception ex){return new("UNHEALTHY",ex.Message,timer.Elapsed);}
    }

    public async Task<AiGenerationResult> GenerateAsync(AiGenerationRequest request,CancellationToken cancellationToken=default)
    {
        EnsureConfigured(request.Context);var timer=Stopwatch.StartNew();using var timeout=CreateTimeout(request.Context,cancellationToken);object? responseFormat=null;
        if(!string.IsNullOrWhiteSpace(request.OutputSchemaJson)){using var schema=JsonDocument.Parse(request.OutputSchemaJson);responseFormat=new{type="json_schema",json_schema=new{name=NormalizeName(request.FeatureCode),strict=true,schema=schema.RootElement.Clone()}};}
        var body=new{messages=new[]{new{role="system",content=request.SystemPrompt},new{role="user",content=request.UserPrompt}},temperature=request.Temperature,max_tokens=request.MaximumOutputTokens,response_format=responseFormat};
        using var message=new HttpRequestMessage(HttpMethod.Post,BuildUri(request.Context,"chat/completions")){Content=JsonContent.Create(body)};await AuthorizeAsync(message,request.Context,timeout.Token);using var response=await httpClient.SendAsync(message,timeout.Token);var json=await response.Content.ReadAsStringAsync(timeout.Token);if(!response.IsSuccessStatusCode)throw new HttpRequestException($"Azure OpenAI generation failed with HTTP {(int)response.StatusCode}: {json}",null,response.StatusCode);
        using var envelope=JsonDocument.Parse(json);var content=envelope.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()??throw new InvalidOperationException("Azure OpenAI returned no content.");var usage=envelope.RootElement.TryGetProperty("usage",out var usageNode)?usageNode:default;decimal? confidence=null;if(content.Length>0&&content[0]=='{'){using var output=JsonDocument.Parse(content);if(output.RootElement.TryGetProperty("confidence",out var confidenceNode)&&confidenceNode.TryGetDecimal(out var parsed))confidence=parsed;}
        return new(content,string.IsNullOrWhiteSpace(request.OutputSchemaJson)?null:content,Token(usage,"prompt_tokens"),Token(usage,"completion_tokens"),confidence,response.Headers.TryGetValues("x-request-id",out var values)?values.FirstOrDefault()??string.Empty:string.Empty,timer.Elapsed);
    }

    public async Task<AiEmbeddingResult> CreateEmbeddingAsync(AiEmbeddingRequest request,CancellationToken cancellationToken=default)
    {
        EnsureConfigured(request.Context);var timer=Stopwatch.StartNew();using var timeout=CreateTimeout(request.Context,cancellationToken);using var message=new HttpRequestMessage(HttpMethod.Post,BuildUri(request.Context,"embeddings")){Content=JsonContent.Create(new{input=request.Inputs})};await AuthorizeAsync(message,request.Context,timeout.Token);using var response=await httpClient.SendAsync(message,timeout.Token);var json=await response.Content.ReadAsStringAsync(timeout.Token);if(!response.IsSuccessStatusCode)throw new HttpRequestException($"Azure OpenAI embedding failed with HTTP {(int)response.StatusCode}: {json}",null,response.StatusCode);
        using var envelope=JsonDocument.Parse(json);var embeddings=envelope.RootElement.GetProperty("data").EnumerateArray().OrderBy(x=>x.GetProperty("index").GetInt32()).Select(x=>(ReadOnlyMemory<float>)x.GetProperty("embedding").EnumerateArray().Select(n=>n.GetSingle()).ToArray()).ToArray();var usage=envelope.RootElement.TryGetProperty("usage",out var usageNode)?usageNode:default;return new(embeddings,Token(usage,"prompt_tokens"),response.Headers.TryGetValues("x-request-id",out var values)?values.FirstOrDefault()??string.Empty:string.Empty,timer.Elapsed);
    }

    private static Uri BuildUri(AiProviderContext context,string operation){var endpoint=context.EndpointReference!.TrimEnd('/');var path=string.IsNullOrEmpty(operation)?$"openai/deployments/{Uri.EscapeDataString(context.DeploymentName)}":$"openai/deployments/{Uri.EscapeDataString(context.DeploymentName)}/{operation}";return new($"{endpoint}/{path}?api-version={Uri.EscapeDataString(context.ApiVersion!)}");}
    private static void EnsureConfigured(AiProviderContext context){if(!Uri.TryCreate(context.EndpointReference,UriKind.Absolute,out _))throw new InvalidOperationException("The database-backed Azure OpenAI endpoint is missing or invalid.");if(string.IsNullOrWhiteSpace(context.DeploymentName))throw new InvalidOperationException("The database-backed Azure OpenAI deployment is missing.");if(string.IsNullOrWhiteSpace(context.ApiVersion))throw new InvalidOperationException("The database-backed Azure OpenAI API version is missing.");}
    private async Task AuthorizeAsync(HttpRequestMessage request,AiProviderContext context,CancellationToken cancellationToken)
    {
        if(!string.IsNullOrWhiteSpace(context.CredentialReference))
        {
            const string environmentPrefix="env://";
            if(!context.CredentialReference.StartsWith(environmentPrefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Azure OpenAI credentials must use an env://VARIABLE_NAME reference or managed identity; plaintext database credentials are not accepted.");
            var variable=context.CredentialReference[environmentPrefix.Length..].Trim();
            var credential=string.IsNullOrWhiteSpace(variable)?null:Environment.GetEnvironmentVariable(variable);
            if(string.IsNullOrWhiteSpace(credential))throw new InvalidOperationException($"Azure OpenAI credential environment variable '{variable}' is not configured.");
            request.Headers.Add("api-key",credential);
        }
        else request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",(await _managedIdentity.GetTokenAsync(new TokenRequestContext(Scope),cancellationToken)).Token);
    }
    private static CancellationTokenSource CreateTimeout(AiProviderContext context,CancellationToken cancellationToken){var source=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);source.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(context.TimeoutSeconds,1,900)));return source;}
    private static int Token(JsonElement usage,string property)=>usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty(property,out var node)?node.GetInt32():0;
    private static string NormalizeName(string value)=>new(value.Select(c=>char.IsLetterOrDigit(c)?char.ToLowerInvariant(c):'_').ToArray());
}
