namespace Legal.Application.Abstractions.Intelligence;

public interface IAiProviderRouter
{
    Task<AiGenerationResult> GenerateAsync(Guid tenantId,string featureCode,string systemPrompt,string userPrompt,string? outputSchemaJson,string correlationId,AiExecutionContext? executionContext=null,string? modelCodeOverride=null,CancellationToken cancellationToken=default);
    Task<AiEmbeddingResult> CreateEmbeddingAsync(Guid tenantId,string featureCode,IReadOnlyCollection<string> inputs,string correlationId,CancellationToken cancellationToken=default);
}

public sealed class AiProviderUnavailableException:Exception
{
    public AiProviderUnavailableException(string featureCode,string message,Exception? innerException=null):base(message,innerException)=>FeatureCode=featureCode;
    public string FeatureCode{get;}
    /// <summary>True when every route failed because the provider throttled the request (HTTP 429) - the call never reached a model and should not count as a consumed model call.</summary>
    public bool IsThrottled=>InnerException is AiProviderThrottledException||(InnerException is AggregateException aggregate&&aggregate.InnerExceptions.Count>0&&aggregate.InnerExceptions.All(x=>x is AiProviderThrottledException));
}

/// <summary>Raised by a provider adapter when the upstream service rate-limited the request after the adapter's bounded Retry-After waits were exhausted.</summary>
public sealed class AiProviderThrottledException(string message,TimeSpan? retryAfter,Exception? innerException=null):Exception(message,innerException)
{
    public TimeSpan? RetryAfter{get;}=retryAfter;
}

public interface IAiProviderRouteRepository
{
    Task<IReadOnlyCollection<AiProviderRoute>> GetRoutesAsync(Guid tenantId,string featureCode,string capabilityCode,string? modelCodeOverride=null,CancellationToken cancellationToken=default);
    Task<AiSafetyPolicy> GetSafetyPolicyAsync(Guid tenantId,CancellationToken cancellationToken=default);
    Task RecordSafetyEventAsync(AiSafetyEventRecord safetyEvent,CancellationToken cancellationToken=default);
    Task RecordExecutionAsync(AiExecutionRecord execution,CancellationToken cancellationToken=default);
}

public sealed record AiProviderRoute(Guid TenantId,string FeatureCode,string ProviderCode,string ProviderTypeCode,string ModelCode,string DeploymentName,string? EndpointReference,string? CredentialReference,string? ApiVersion,int TimeoutSeconds,decimal Temperature,int MaximumOutputTokens,int Priority,bool IsFallback);
public sealed record AiSafetyControl(Guid SafetyControlId,string ControlCode,string EnforcementStageCode,string ActionCode,bool RequiresHumanReview);
public sealed record AiSafetyPolicy(int MaximumInputCharacters,int MaximumOutputCharacters,IReadOnlyCollection<AiSafetyControl> Controls);
public sealed record AiSafetyEventRecord(Guid TenantId,Guid SafetyControlId,string EventTypeCode,string EnforcementStageCode,string ActionCode,string SeverityCode,string? InputHash,string DetailsJson,bool RequiresHumanReview,string? ReviewStatusCode,string IdempotencyKey);
public sealed record AiExecutionContext(string ModuleCode,string? EntityTypeCode,Guid? EntityId,string? InputReference,string? GroundingSourceTypeCode,Guid? GroundingSourceEntityId,string? GroundingSourceReference,string? GroundingTitle);
public sealed record AiExecutionRecord(Guid ExecutionId,Guid TenantId,string FeatureCode,string ModuleCode,string? EntityTypeCode,Guid? EntityId,string StatusCode,string CorrelationId,string? ProviderCode,string? ModelCode,long DurationMilliseconds,int? InputTokenCount,int? OutputTokenCount,decimal? Confidence,string? InputReference,string? GroundingSourceTypeCode,Guid? GroundingSourceEntityId,string? GroundingSourceReference,string? GroundingTitle,string? ErrorCode,string? ErrorMessage);
