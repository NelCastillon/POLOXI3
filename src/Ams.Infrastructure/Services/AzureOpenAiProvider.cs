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
        // Newer model families (gpt-5*, o-series reasoning models) reject the legacy max_tokens parameter and non-default
        // temperature with HTTP 400 unsupported_parameter. Known reasoning models get the correct parameters UP FRONT
        // (no wasted 400 round trip); unknown models that reject a parameter are remembered per model code for the
        // process lifetime, so the negotiation 400 happens at most once per model instead of on every call.
        var modelKey=request.Context.ModelCode??string.Empty;
        var profile=LearnedModelProfiles.GetValueOrDefault(modelKey);
        var useCompletionTokens=profile.UseCompletionTokens||IsReasoningModel(request.Context.ModelCode);
        var dropTemperature=profile.DropTemperature||IsReasoningModel(request.Context.ModelCode);
        var body=new JsonObject{["messages"]=new JsonArray(new JsonObject{["role"]="system",["content"]=request.SystemPrompt},new JsonObject{["role"]="user",["content"]=request.UserPrompt})};
        if(!dropTemperature)body["temperature"]=request.Temperature;
        if(useCompletionTokens)body["max_completion_tokens"]=request.MaximumOutputTokens;else body["max_tokens"]=request.MaximumOutputTokens;
        if(responseFormat is not null)body["response_format"]=JsonSerializer.SerializeToNode(responseFormat);
        string json;System.Net.HttpStatusCode statusCode;var requestId=string.Empty;var reasoningBudgetRaised=false;
        for(var attempt=0;;attempt++)
        {
            using var message=new HttpRequestMessage(HttpMethod.Post,BuildUri(request.Context,"chat/completions")){Content=JsonContent.Create(body)};await AuthorizeAsync(message,request.Context,timeout.Token);using var response=await httpClient.SendAsync(message,timeout.Token);json=await response.Content.ReadAsStringAsync(timeout.Token);statusCode=response.StatusCode;requestId=response.Headers.TryGetValues("x-request-id",out var values)?values.FirstOrDefault()??string.Empty:string.Empty;
            if(response.IsSuccessStatusCode)
            {
                // Reasoning-family models (gpt-5*, o-series) burn hidden reasoning tokens against max_completion_tokens,
                // so a budget sized for standard models (e.g. gpt-4.1-mini, which is unaffected here) can truncate the
                // visible answer. Retry ONCE for those specific models only, with doubled completion headroom.
                if(!reasoningBudgetRaised&&IsReasoningModel(request.Context.ModelCode)&&body.ContainsKey("max_completion_tokens")&&IsTruncated(json))
                {
                    reasoningBudgetRaised=true;body["max_completion_tokens"]=request.MaximumOutputTokens*2;continue;
                }
                break;
            }
            if((int)statusCode==400&&attempt<3&&TryGetUnsupportedParameter(json,out var unsupported))
            {
                if(unsupported=="max_tokens"&&body.ContainsKey("max_tokens")){body.Remove("max_tokens");body["max_completion_tokens"]=request.MaximumOutputTokens;LearnedModelProfiles.AddOrUpdate(modelKey,(true,false),(_,existing)=>(true,existing.DropTemperature));continue;}
                if(unsupported is "temperature" or "max_completion_tokens"&&body.Remove(unsupported)){if(unsupported=="temperature")LearnedModelProfiles.AddOrUpdate(modelKey,(false,true),(_,existing)=>(existing.UseCompletionTokens,true));continue;}
            }
            throw new HttpRequestException($"Azure OpenAI generation failed with HTTP {(int)statusCode}: {json}",null,statusCode);
        }
        using var envelope=JsonDocument.Parse(json);var choice=envelope.RootElement.GetProperty("choices")[0];if(choice.TryGetProperty("finish_reason",out var finishReasonNode)&&finishReasonNode.GetString()=="length")throw new InvalidOperationException($"Azure OpenAI output was truncated because the completion hit the configured maximum output tokens ({request.MaximumOutputTokens}); increase MaximumOutputTokens for feature '{request.FeatureCode}' in AI.FeaturePolicy.");var content=choice.GetProperty("message").GetProperty("content").GetString()??throw new InvalidOperationException("Azure OpenAI returned no content.");var usage=envelope.RootElement.TryGetProperty("usage",out var usageNode)?usageNode:default;decimal? confidence=null;if(content.Length>0&&content[0]=='{'){using var output=JsonDocument.Parse(content);if(output.RootElement.TryGetProperty("confidence",out var confidenceNode)&&confidenceNode.TryGetDecimal(out var parsed))confidence=Math.Clamp(parsed>1m?parsed/100m:parsed,0m,1m);}
        return new(content,string.IsNullOrWhiteSpace(request.OutputSchemaJson)?null:content,Token(usage,"prompt_tokens"),Token(usage,"completion_tokens"),confidence,requestId,timer.Elapsed,request.Context.ProviderCode,request.Context.ModelCode);
    }

    // Extracts the offending parameter name from an Azure OpenAI 400 unsupported_parameter or
    // unsupported_value error payload (e.g. gpt-5* models only accept the default temperature).
    private static bool TryGetUnsupportedParameter(string json,out string parameter)
    {
        parameter=string.Empty;
        try
        {
            using var document=JsonDocument.Parse(json);
            if(document.RootElement.TryGetProperty("error",out var error)&&error.TryGetProperty("code",out var code)&&code.GetString() is "unsupported_parameter" or "unsupported_value"&&error.TryGetProperty("param",out var param)&&param.GetString() is { Length:>0 } name){parameter=name;return true;}
        }
        catch(JsonException){}
        return false;
    }

    // Scoped to reasoning-family model codes only (gpt-5*, o1*, o3*, o4*); standard models like
    // gpt-4.1-mini keep their configured budget untouched because they don't spend hidden reasoning tokens.
    private static bool IsReasoningModel(string? modelCode)=>modelCode is not null&&(modelCode.StartsWith("gpt-5",StringComparison.OrdinalIgnoreCase)||modelCode.StartsWith("o1",StringComparison.OrdinalIgnoreCase)||modelCode.StartsWith("o3",StringComparison.OrdinalIgnoreCase)||modelCode.StartsWith("o4",StringComparison.OrdinalIgnoreCase));

    // Learned per-model parameter adjustments (process lifetime). A model that rejects max_tokens or a
    // non-default temperature pays the negotiation 400 ONCE; every later call builds the body correctly
    // up front. Known reasoning families never pay it at all (seeded by IsReasoningModel).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,(bool UseCompletionTokens,bool DropTemperature)> LearnedModelProfiles=new(StringComparer.OrdinalIgnoreCase);

    // True when the successful completion envelope reports finish_reason=length (output truncated by token budget).
    private static bool IsTruncated(string json)
    {
        try
        {
            using var document=JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("choices",out var choices)&&choices.GetArrayLength()>0&&choices[0].TryGetProperty("finish_reason",out var reason)&&reason.GetString()=="length";
        }
        catch(JsonException){return false;}
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
    // Reasoning-family models (gpt-5*, o-series) spend extended time on hidden reasoning tokens before emitting
    // output, so the route timeout sized for standard models (e.g. gpt-4.1-mini) is tripled for those models only,
    // still capped at the 900s hard ceiling. Standard models keep the configured TimeoutSeconds untouched.
    private static CancellationTokenSource CreateTimeout(AiProviderContext context,CancellationToken cancellationToken){var seconds=Math.Clamp(context.TimeoutSeconds,1,900);if(IsReasoningModel(context.ModelCode))seconds=Math.Min(seconds*3,900);var source=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);source.CancelAfter(TimeSpan.FromSeconds(seconds));return source;}
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
