using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ams.Application.Abstractions.Intelligence;
using Azure.Core;
using Azure.Identity;

namespace Ams.Infrastructure.Services;

public sealed class AzureOpenAiProvider(HttpClient httpClient):IAiProvider
{
    private static readonly string[] Scope=["https://cognitiveservices.azure.com/.default"];
    // Managed identity is only attempted when the host exposes an identity endpoint; otherwise IMDS probes (169.254.169.254) time out locally and abort the request.
    // Locally the Azure CLI session is used. Token is cached until shortly before expiry to avoid re-invoking az per request.
    private static readonly bool UseManagedIdentity=!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT"))||!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSI_ENDPOINT"))||!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_CLIENT_ID"));
    private readonly TokenCredential _managedIdentity=UseManagedIdentity?new ManagedIdentityCredential():new AzureCliCredential();
    private AccessToken _cachedToken;
    private readonly SemaphoreSlim _tokenLock=new(1,1);
    public string ProviderTypeCode=>"AZURE_OPENAI";

    public Task<AiProviderHealth> CheckHealthAsync(AiProviderContext context,CancellationToken cancellationToken=default)
    {
        var timer=Stopwatch.StartNew();
        try
        {
            EnsureConfigured(context);return Task.FromResult(new AiProviderHealth("HEALTHY","Provider route configuration is valid; operation-level authentication determines availability.",timer.Elapsed));
        }
        catch(Exception ex){return Task.FromResult(new AiProviderHealth("UNHEALTHY",ex.Message,timer.Elapsed));}
    }

    public async Task<AiGenerationResult> GenerateAsync(AiGenerationRequest request,CancellationToken cancellationToken=default)
    {
        EnsureConfigured(request.Context);var timer=Stopwatch.StartNew();using var timeout=CreateTimeout(request.Context,cancellationToken);object? responseFormat=null;
        if(!string.IsNullOrWhiteSpace(request.OutputSchemaJson)){var schema=JsonNode.Parse(request.OutputSchemaJson)!;NormalizeStrictSchema(schema);responseFormat=new{type="json_schema",json_schema=new{name=NormalizeName(request.FeatureCode),strict=true,schema}};}
        var body=new{messages=new[]{new{role="system",content=request.SystemPrompt},new{role="user",content=request.UserPrompt}},temperature=request.Temperature,max_tokens=request.MaximumOutputTokens,response_format=responseFormat};
        using var message=new HttpRequestMessage(HttpMethod.Post,BuildUri(request.Context,"chat/completions")){Content=JsonContent.Create(body)};await AuthorizeAsync(message,request.Context,timeout.Token);using var response=await httpClient.SendAsync(message,timeout.Token);var json=await response.Content.ReadAsStringAsync(timeout.Token);if(!response.IsSuccessStatusCode)throw new HttpRequestException($"Azure OpenAI generation failed with HTTP {(int)response.StatusCode}: {json}",null,response.StatusCode);
        using var envelope=JsonDocument.Parse(json);var choice=envelope.RootElement.GetProperty("choices")[0];if(choice.TryGetProperty("finish_reason",out var finishReasonNode)&&finishReasonNode.GetString()=="length")throw new InvalidOperationException($"Azure OpenAI output was truncated because the completion hit the configured maximum output tokens ({request.MaximumOutputTokens}); increase MaximumOutputTokens for feature '{request.FeatureCode}' in AI.FeaturePolicy.");var content=choice.GetProperty("message").GetProperty("content").GetString()??throw new InvalidOperationException("Azure OpenAI returned no content.");var usage=envelope.RootElement.TryGetProperty("usage",out var usageNode)?usageNode:default;decimal? confidence=null;if(content.Length>0&&content[0]=='{'){using var output=JsonDocument.Parse(content);if(output.RootElement.TryGetProperty("confidence",out var confidenceNode)&&confidenceNode.TryGetDecimal(out var parsed))confidence=parsed;}
        return new(content,string.IsNullOrWhiteSpace(request.OutputSchemaJson)?null:content,Token(usage,"prompt_tokens"),Token(usage,"completion_tokens"),confidence,response.Headers.TryGetValues("x-request-id",out var values)?values.FirstOrDefault()??string.Empty:string.Empty,timer.Elapsed,request.Context.ProviderCode,request.Context.ModelCode);
    }

    public async Task<AiEmbeddingResult> CreateEmbeddingAsync(AiEmbeddingRequest request,CancellationToken cancellationToken=default)
    {
        EnsureConfigured(request.Context);var timer=Stopwatch.StartNew();using var timeout=CreateTimeout(request.Context,cancellationToken);using var message=new HttpRequestMessage(HttpMethod.Post,BuildUri(request.Context,"embeddings")){Content=JsonContent.Create(new{input=request.Inputs})};await AuthorizeAsync(message,request.Context,timeout.Token);using var response=await httpClient.SendAsync(message,timeout.Token);var json=await response.Content.ReadAsStringAsync(timeout.Token);if(!response.IsSuccessStatusCode)throw new HttpRequestException($"Azure OpenAI embedding failed with HTTP {(int)response.StatusCode}: {json}",null,response.StatusCode);
        using var envelope=JsonDocument.Parse(json);var embeddings=envelope.RootElement.GetProperty("data").EnumerateArray().OrderBy(x=>x.GetProperty("index").GetInt32()).Select(x=>(ReadOnlyMemory<float>)x.GetProperty("embedding").EnumerateArray().Select(n=>n.GetSingle()).ToArray()).ToArray();var usage=envelope.RootElement.TryGetProperty("usage",out var usageNode)?usageNode:default;return new(embeddings,Token(usage,"prompt_tokens"),response.Headers.TryGetValues("x-request-id",out var values)?values.FirstOrDefault()??string.Empty:string.Empty,timer.Elapsed);
    }

    private static Uri BuildUri(AiProviderContext context,string operation){var endpoint=ResolveEndpoint(context.EndpointReference!).TrimEnd('/');var path=string.IsNullOrEmpty(operation)?$"openai/deployments/{Uri.EscapeDataString(context.DeploymentName)}":$"openai/deployments/{Uri.EscapeDataString(context.DeploymentName)}/{operation}";return new($"{endpoint}/{path}?api-version={Uri.EscapeDataString(context.ApiVersion!)}");}
    private static string ResolveEndpoint(string endpoint){if(!endpoint.StartsWith("env://",StringComparison.OrdinalIgnoreCase))return endpoint;var variable=endpoint["env://".Length..].Trim();var value=string.IsNullOrWhiteSpace(variable)?null:Environment.GetEnvironmentVariable(variable);if(string.IsNullOrWhiteSpace(value))throw new InvalidOperationException($"Azure OpenAI endpoint environment variable '{variable}' is not configured.");return value;}
    private static void EnsureConfigured(AiProviderContext context){if(string.IsNullOrWhiteSpace(context.EndpointReference)||!Uri.TryCreate(ResolveEndpoint(context.EndpointReference),UriKind.Absolute,out _))throw new InvalidOperationException("The database-backed Azure OpenAI endpoint is missing or invalid.");if(string.IsNullOrWhiteSpace(context.DeploymentName))throw new InvalidOperationException("The database-backed Azure OpenAI deployment is missing.");if(string.IsNullOrWhiteSpace(context.ApiVersion))throw new InvalidOperationException("The database-backed Azure OpenAI API version is missing.");}
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
        else request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",await GetBearerTokenAsync(cancellationToken));
    }
    private async Task<string> GetBearerTokenAsync(CancellationToken cancellationToken)
    {
        if(_cachedToken.ExpiresOn>DateTimeOffset.UtcNow.AddMinutes(5))return _cachedToken.Token;
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if(_cachedToken.ExpiresOn<=DateTimeOffset.UtcNow.AddMinutes(5))_cachedToken=await _managedIdentity.GetTokenAsync(new TokenRequestContext(Scope),cancellationToken);
            return _cachedToken.Token;
        }
        finally{_tokenLock.Release();}
    }
    private static CancellationTokenSource CreateTimeout(AiProviderContext context,CancellationToken cancellationToken){var source=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);source.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(context.TimeoutSeconds,1,900)));return source;}
    private static int Token(JsonElement usage,string property)=>usage.ValueKind==JsonValueKind.Object&&usage.TryGetProperty(property,out var node)?node.GetInt32():0;
    private static string NormalizeName(string value)=>new(value.Select(c=>char.IsLetterOrDigit(c)?char.ToLowerInvariant(c):'_').ToArray());
    private static void NormalizeStrictSchema(JsonNode? node)
    {
        if(node is JsonArray array){foreach(var item in array)NormalizeStrictSchema(item);return;}
        if(node is not JsonObject obj)return;
        if(obj["type"] is JsonValue typeValue&&typeValue.TryGetValue<string>(out var type)&&type=="object")
        {
            obj["additionalProperties"]=false;
            if(obj["properties"] is JsonObject properties)obj["required"]=new JsonArray([..properties.Select(p=>(JsonNode)p.Key)]);
        }
        foreach(var key in new[]{"properties","items","anyOf","allOf","oneOf","$defs","definitions"})
            if(obj[key] is JsonObject childObject)
            {
                if(key=="properties")foreach(var property in childObject)NormalizeStrictSchema(property.Value);
                else NormalizeStrictSchema(childObject);
            }
            else if(obj[key] is JsonArray childArray)NormalizeStrictSchema(childArray);
    }
}
