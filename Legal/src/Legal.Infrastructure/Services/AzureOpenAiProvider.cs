using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Legal.Application.Abstractions.Intelligence;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Services;

public sealed class AzureOpenAiProvider(HttpClient httpClient,ILogger<AzureOpenAiProvider> logger):IAiProvider
{
    // Process-wide monotonic call counter: makes it obvious in the console whether repeated
    // identical-URL requests are distinct pipeline stages progressing or the same stage retrying.
    private static int _callSequence;
    private static readonly string[] Scope=["https://cognitiveservices.azure.com/.default"];
    // Managed identity is only attempted when the host exposes an identity endpoint; otherwise IMDS probes (169.254.169.254) time out locally and abort the request.
    // Locally the Azure CLI session is used. Token is cached until shortly before expiry to avoid re-invoking az per request.
    private static readonly bool UseManagedIdentity=!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT"))||!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSI_ENDPOINT"))||!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_CLIENT_ID"));
    private readonly TokenCredential _managedIdentity=CreateCredential();
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
        // Payload shaping for mechanical stages on reasoning models: migration 0167 raised the shared
        // Wide feature-policy output budget to 16000 tokens so the ANSWER stage never truncates, but the
        // mechanical strict-JSON stages (intent, hierarchy, information value, enumeration) emit small
        // structured objects and NEVER need that headroom. On a reasoning model, max_completion_tokens is
        // also the hidden-reasoning ceiling for the call, so an oversized budget directly inflates
        // wall-clock latency (observed: multi-minute stalls on INTELLIGENCE_WIDE_INFORMATION_VALUE).
        // Mechanical stages are therefore capped at 4000 completion tokens; the existing truncation
        // retry (doubled budget on finish_reason=length) remains the fail-soft safety net, and
        // answer/explanation stages keep the full configured budget.
        var outputBudget=request.MaximumOutputTokens;
        if(IsReasoningModel(request.Context.ModelCode)&&ResolveReasoningEffort(request.FeatureCode)=="minimal")outputBudget=Math.Min(outputBudget,4000);
        if(useCompletionTokens)body["max_completion_tokens"]=outputBudget;else body["max_tokens"]=outputBudget;
        // Reasoning models spend most of their latency on hidden reasoning tokens. Mechanical extraction
        // stages (intent, hierarchy, information value, enumeration) are strict-JSON structured tasks that
        // gain little from deep reasoning, so they run at low effort; judgment stages (final answer,
        // challenge, explanation) keep medium effort. This keeps every stage on the selected reasoning
        // model while cutting per-call latency substantially. Deployments that reject the parameter fall
        // back through the existing unsupported_parameter negotiation below.
        if(IsReasoningModel(request.Context.ModelCode)&&!profile.DropReasoningEffort){var effort=ResolveReasoningEffort(request.FeatureCode);body["reasoning_effort"]=profile.MinimalEffortRejected&&effort=="minimal"?"low":effort;}
        if(responseFormat is not null)body["response_format"]=JsonSerializer.SerializeToNode(responseFormat);
        string json;System.Net.HttpStatusCode statusCode;var requestId=string.Empty;var reasoningBudgetRaised=false;
        for(var attempt=0;;attempt++)
        {
            var callNumber=Interlocked.Increment(ref _callSequence);
            // Payload diagnostics: for non-streaming chat/completions, response HEADERS only arrive after the
            // model finishes generating, so a "hanging" call is usually the model reasoning against a large
            // max_completion_tokens budget. Logging the exact effective payload shape (prompt sizes, output
            // budget, reasoning_effort actually sent or dropped) makes that visible per call.
            logger.LogInformation("AI call #{CallNumber} payload: feature {FeatureCode}, systemChars={SystemChars}, userChars={UserChars}, outputBudget={OutputBudget}, reasoningEffort={ReasoningEffort}, strictJson={StrictJson}, timeoutSeconds={TimeoutSeconds}",callNumber,request.FeatureCode,request.SystemPrompt?.Length??0,request.UserPrompt?.Length??0,(body["max_completion_tokens"]??body["max_tokens"])?.GetValue<int>(),body["reasoning_effort"]?.GetValue<string>()??"(not sent)",responseFormat is not null,Math.Clamp(request.Context.TimeoutSeconds,1,900)*(IsReasoningModel(request.Context.ModelCode)?3:1));
            logger.LogInformation("AI call #{CallNumber} start: feature {FeatureCode}, model {ModelCode}, attempt {Attempt}, correlation {CorrelationId}",callNumber,request.FeatureCode,request.Context.ModelCode,attempt,request.CorrelationId);
            using var message=new HttpRequestMessage(HttpMethod.Post,BuildUri(request.Context,"chat/completions")){Content=JsonContent.Create(body)};await AuthorizeAsync(message,request.Context,timeout.Token);using var response=await httpClient.SendAsync(message,timeout.Token);json=await response.Content.ReadAsStringAsync(timeout.Token);statusCode=response.StatusCode;requestId=response.Headers.TryGetValues("x-request-id",out var values)?values.FirstOrDefault()??string.Empty:string.Empty;
            if(response.IsSuccessStatusCode)
            {
                // Reasoning-family models (gpt-5*, o-series) burn hidden reasoning tokens against max_completion_tokens,
                // so a budget sized for standard models (e.g. gpt-4.1-mini, which is unaffected here) can truncate the
                // visible answer. Retry ONCE for those specific models only, with doubled completion headroom.
                if(!reasoningBudgetRaised&&IsReasoningModel(request.Context.ModelCode)&&body.ContainsKey("max_completion_tokens")&&IsTruncated(json))
                {
                    logger.LogWarning("AI call #{CallNumber} truncated: feature {FeatureCode} retrying once with doubled completion budget.",callNumber,request.FeatureCode);
                    // Double the EFFECTIVE budget (not the raw configured one) so a capped mechanical stage
                    // retries at 8000 rather than jumping straight to 32000 reasoning-token headroom.
                    reasoningBudgetRaised=true;body["max_completion_tokens"]=outputBudget*2;continue;
                }
                logger.LogInformation("AI call #{CallNumber} completed: feature {FeatureCode}, model {ModelCode}, {ElapsedMs}ms elapsed.",callNumber,request.FeatureCode,request.Context.ModelCode,timer.ElapsedMilliseconds);
                break;
            }
            if((int)statusCode==400&&attempt<3&&TryGetUnsupportedParameter(json,out var unsupported))
            {
                if(unsupported=="max_tokens"&&body.ContainsKey("max_tokens")){body.Remove("max_tokens");body["max_completion_tokens"]=outputBudget;LearnedModelProfiles.AddOrUpdate(modelKey,new LearnedModelProfile(true,false,false,false),(_,existing)=>existing with{UseCompletionTokens=true});continue;}
                // reasoning_effort degrades gracefully: a deployment that rejects "minimal" retries at
                // "low" (still far cheaper than the model's medium default) before the parameter is
                // dropped entirely. Dropping on first rejection silently reverted mechanical stages to
                // default effort, which is the slowest possible configuration.
                if(unsupported=="reasoning_effort"&&body["reasoning_effort"]?.GetValue<string>()=="minimal"){logger.LogWarning("AI call feature {FeatureCode}: model {ModelCode} rejected reasoning_effort=minimal; retrying with low.",request.FeatureCode,request.Context.ModelCode);body["reasoning_effort"]="low";LearnedModelProfiles.AddOrUpdate(modelKey,new LearnedModelProfile(false,false,false,true),(_,existing)=>existing with{MinimalEffortRejected=true});continue;}
                if(unsupported is "temperature" or "max_completion_tokens" or "reasoning_effort"&&body.Remove(unsupported)){if(unsupported=="temperature")LearnedModelProfiles.AddOrUpdate(modelKey,new LearnedModelProfile(false,true,false,false),(_,existing)=>existing with{DropTemperature=true});if(unsupported=="reasoning_effort")LearnedModelProfiles.AddOrUpdate(modelKey,new LearnedModelProfile(false,false,true,false),(_,existing)=>existing with{DropReasoningEffort=true});continue;}
            }
            throw new HttpRequestException($"Azure OpenAI generation failed with HTTP {(int)statusCode}: {json}",null,statusCode);
        }
        using var envelope=JsonDocument.Parse(json);var choice=envelope.RootElement.GetProperty("choices")[0];if(choice.TryGetProperty("finish_reason",out var finishReasonNode)&&finishReasonNode.GetString()=="length")throw new InvalidOperationException($"Azure OpenAI output was truncated because the completion hit the configured maximum output tokens ({request.MaximumOutputTokens}); increase MaximumOutputTokens for feature '{request.FeatureCode}' in AI.Legal_FeaturePolicy.");var content=choice.GetProperty("message").GetProperty("content").GetString()??throw new InvalidOperationException("Azure OpenAI returned no content.");var usage=envelope.RootElement.TryGetProperty("usage",out var usageNode)?usageNode:default;decimal? confidence=null;if(content.Length>0&&content[0]=='{'){using var output=JsonDocument.Parse(content);if(output.RootElement.TryGetProperty("confidence",out var confidenceNode)&&confidenceNode.TryGetDecimal(out var parsed))confidence=Math.Clamp(parsed>1m?parsed/100m:parsed,0m,1m);}
        return new(content,string.IsNullOrWhiteSpace(request.OutputSchemaJson)?null:content,Token(usage,"prompt_tokens"),Token(usage,"completion_tokens"),confidence,requestId,timer.Elapsed,request.Context.ProviderCode,request.Context.ModelCode??string.Empty);
    }

    private static TokenCredential CreateCredential()
    {
        if(!UseManagedIdentity)return new AzureCliCredential();
        var clientId=Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        var identityId=string.IsNullOrWhiteSpace(clientId)
            ?ManagedIdentityId.SystemAssigned
            :ManagedIdentityId.FromUserAssignedClientId(clientId);
        return new ManagedIdentityCredential(identityId);
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
    private static bool IsReasoningModel(string? modelCode)=>modelCode is not null&&(modelCode.StartsWith("gpt-5",StringComparison.OrdinalIgnoreCase)||modelCode.StartsWith("gpt-6",StringComparison.OrdinalIgnoreCase)||modelCode.StartsWith("o1",StringComparison.OrdinalIgnoreCase)||modelCode.StartsWith("o3",StringComparison.OrdinalIgnoreCase)||modelCode.StartsWith("o4",StringComparison.OrdinalIgnoreCase));

    // Learned per-model parameter adjustments (process lifetime). A model that rejects max_tokens or a
    // non-default temperature pays the negotiation 400 ONCE; every later call builds the body correctly
    // up front. Known reasoning families never pay it at all (seeded by IsReasoningModel).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,LearnedModelProfile> LearnedModelProfiles=new(StringComparer.OrdinalIgnoreCase);
    private readonly record struct LearnedModelProfile(bool UseCompletionTokens,bool DropTemperature,bool DropReasoningEffort,bool MinimalEffortRejected);

    // Stage-tuned reasoning effort: judgment stages (final answer/challenge/explanation) keep medium;
    // every mechanical structured-extraction stage runs at minimal effort for maximum speed on the same
    // model. Measured basis (AI.Legal_Execution): at "low" the hierarchy step still spent 68-155s per
    // call at ~35-40 output tok/s (hidden reasoning dominating a strict-JSON extraction task); "minimal"
    // suppresses nearly all hidden reasoning for these stages. Deployments that reject the value fall
    // back through the existing unsupported_value negotiation and drop the parameter.
    private static string ResolveReasoningEffort(string featureCode)=>
        featureCode.Contains("ANSWER",StringComparison.OrdinalIgnoreCase)||featureCode.Contains("EXPLANATION",StringComparison.OrdinalIgnoreCase)?"medium":"minimal";

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
