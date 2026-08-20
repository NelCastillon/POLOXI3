using Ams.Application.Abstractions.Intelligence;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ams.Infrastructure.Services;

public sealed class AiProviderRouter(IAiProviderRouteRepository routeRepository,IEnumerable<IAiProvider> providers,ILogger<AiProviderRouter> logger):IAiProviderRouter
{
    public async Task<AiGenerationResult> GenerateAsync(Guid tenantId,string featureCode,string systemPrompt,string userPrompt,string? outputSchemaJson,string correlationId,AiExecutionContext? executionContext=null,CancellationToken cancellationToken=default)
    {
        var executionId=Guid.NewGuid();var started=System.Diagnostics.Stopwatch.StartNew();
        var safety=await routeRepository.GetSafetyPolicyAsync(tenantId,cancellationToken);
        var inputLength=systemPrompt.Length+userPrompt.Length;
        if(safety.MaximumInputCharacters<=0)throw new AiSafetyViolationException("Maximum AI input length is not configured.");
        if(inputLength>safety.MaximumInputCharacters)
        {
            await RecordViolationAsync(tenantId,safety,"MAXIMUM_INPUT_LENGTH",correlationId,"INPUT_LENGTH_EXCEEDED",inputLength,safety.MaximumInputCharacters,Hash(systemPrompt+userPrompt),cancellationToken);
            throw new AiSafetyViolationException("The AI request exceeded the configured maximum input length.");
        }
        AiGenerationResult result;
        try
        {
            result=await ExecuteAsync(tenantId,featureCode,"CHAT",correlationId,(provider,route,context,token)=>provider.GenerateAsync(new(context,featureCode,systemPrompt,userPrompt,outputSchemaJson,route.Temperature,route.MaximumOutputTokens,correlationId),token),cancellationToken);
        }
        catch(Exception ex) when(ex is not OperationCanceledException)
        {
            started.Stop();await routeRepository.RecordExecutionAsync(Execution(executionId,tenantId,featureCode,correlationId,executionContext,"FAILED",started.ElapsedMilliseconds,null,ex.GetType().Name,ex.Message),cancellationToken);throw;
        }
        if(safety.MaximumOutputCharacters<=0)throw new AiSafetyViolationException("Maximum AI output length is not configured.");
        if(result.Content.Length>safety.MaximumOutputCharacters)
        {
            await RecordViolationAsync(tenantId,safety,"MAXIMUM_OUTPUT_LENGTH",correlationId,"OUTPUT_LENGTH_EXCEEDED",result.Content.Length,safety.MaximumOutputCharacters,null,cancellationToken);
            throw new AiSafetyViolationException("The AI response exceeded the configured maximum output length.");
        }
        var structuredControl=safety.Controls.FirstOrDefault(x=>string.Equals(x.ControlCode,"STRUCTURED_OUTPUT_VALIDATION",StringComparison.OrdinalIgnoreCase));
        if(!string.IsNullOrWhiteSpace(outputSchemaJson)&&structuredControl is not null&&!IsJson(result.StructuredOutputJson??result.Content))
        {
            await RecordViolationAsync(tenantId,safety,"STRUCTURED_OUTPUT_VALIDATION",correlationId,"INVALID_STRUCTURED_OUTPUT",result.Content.Length,null,null,cancellationToken);
            throw new AiSafetyViolationException("The AI response did not contain valid structured JSON output.");
        }
        started.Stop();await routeRepository.RecordExecutionAsync(Execution(executionId,tenantId,featureCode,correlationId,executionContext,"COMPLETED",started.ElapsedMilliseconds,result,null,null),cancellationToken);return result;
    }

    public async Task<AiEmbeddingResult> CreateEmbeddingAsync(Guid tenantId,string featureCode,IReadOnlyCollection<string> inputs,string correlationId,CancellationToken cancellationToken=default)
    {
        var safety=await routeRepository.GetSafetyPolicyAsync(tenantId,cancellationToken);
        var inputLength=inputs.Sum(x=>x.Length);
        if(safety.MaximumInputCharacters<=0)throw new AiSafetyViolationException("Maximum AI input length is not configured.");
        if(inputLength>safety.MaximumInputCharacters)
        {
            await RecordViolationAsync(tenantId,safety,"MAXIMUM_INPUT_LENGTH",correlationId,"EMBEDDING_INPUT_LENGTH_EXCEEDED",inputLength,safety.MaximumInputCharacters,Hash(string.Join('\n',inputs)),cancellationToken);
            throw new AiSafetyViolationException("The embedding request exceeded the configured maximum input length.");
        }
        return await ExecuteAsync(tenantId,featureCode,"EMBEDDING",correlationId,(provider,_,context,token)=>provider.CreateEmbeddingAsync(new(context,inputs,correlationId),token),cancellationToken);
    }

    private async Task<T> ExecuteAsync<T>(Guid tenantId,string featureCode,string capabilityCode,string correlationId,Func<IAiProvider,AiProviderRoute,AiProviderContext,CancellationToken,Task<T>> execute,CancellationToken cancellationToken)
    {
        var routes=await routeRepository.GetRoutesAsync(tenantId,featureCode,capabilityCode,cancellationToken);
        if(routes.Count==0)throw new AiProviderUnavailableException(featureCode,$"No active {capabilityCode} model route is configured for this tenant and feature.");
        var adapters=providers.ToDictionary(x=>x.ProviderTypeCode,StringComparer.OrdinalIgnoreCase);var failures=new List<Exception>();
        foreach(var route in routes)
        {
            if(!adapters.TryGetValue(route.ProviderTypeCode,out var provider)){failures.Add(new InvalidOperationException($"Provider adapter {route.ProviderTypeCode} is not registered."));continue;}
            var context=new AiProviderContext(route.TenantId,route.ProviderCode,route.ProviderTypeCode,route.ModelCode,route.DeploymentName,route.EndpointReference,route.CredentialReference,route.ApiVersion,route.TimeoutSeconds);
            try
            {
                var health=await provider.CheckHealthAsync(context,cancellationToken);
                if(!string.Equals(health.StatusCode,"HEALTHY",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(health.Message);
                return await execute(provider,route,context,cancellationToken);
            }
            catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){throw;}
            catch(Exception ex){failures.Add(ex);logger.LogWarning(ex,"AI route {ProviderCode}/{ModelCode} failed for feature {FeatureCode}, correlation {CorrelationId}; trying the next configured route.",route.ProviderCode,route.ModelCode,featureCode,correlationId);}
        }
        throw new AiProviderUnavailableException(featureCode,"All configured AI provider routes were unavailable.",new AggregateException(failures));
    }

    private async Task RecordViolationAsync(Guid tenantId,AiSafetyPolicy safety,string controlCode,string correlationId,string eventTypeCode,int actual,int? configured,string? inputHash,CancellationToken cancellationToken)
    {
        var control=safety.Controls.FirstOrDefault(x=>string.Equals(x.ControlCode,controlCode,StringComparison.OrdinalIgnoreCase));
        if(control is null)throw new AiSafetyViolationException($"Required safety control {controlCode} is not active.");
        var details=JsonSerializer.Serialize(new{correlationId,actual,configured});
        await routeRepository.RecordSafetyEventAsync(new(tenantId,control.SafetyControlId,eventTypeCode,control.EnforcementStageCode,control.ActionCode,"HIGH",inputHash,details,control.RequiresHumanReview,control.RequiresHumanReview?"PENDING":null,$"{controlCode}:{correlationId}"),cancellationToken);
    }

    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool IsJson(string value){try{using var _=JsonDocument.Parse(value);return true;}catch(JsonException){return false;}}
    private static string Module(string featureCode)=>featureCode.Contains('.',StringComparison.Ordinal)?featureCode[..featureCode.IndexOf('.',StringComparison.Ordinal)].ToUpperInvariant():"INTELLIGENCE";
    private static AiExecutionRecord Execution(Guid executionId,Guid tenantId,string featureCode,string correlationId,AiExecutionContext? context,string status,long duration,AiGenerationResult? result,string? errorCode,string? errorMessage)=>new(executionId,tenantId,featureCode,context?.ModuleCode??Module(featureCode),context?.EntityTypeCode,context?.EntityId,status,correlationId,result?.ProviderCode,result?.ModelCode,duration,result?.InputTokenCount,result?.OutputTokenCount,result?.Confidence,context?.InputReference,context?.GroundingSourceTypeCode,context?.GroundingSourceEntityId,context?.GroundingSourceReference,context?.GroundingTitle,errorCode,errorMessage);
}

public sealed class AiSafetyViolationException(string message):Exception(message);
