namespace Ams.Application.Abstractions.Intelligence;

public interface IAiProviderRouter
{
    Task<AiGenerationResult> GenerateAsync(Guid tenantId,string featureCode,string systemPrompt,string userPrompt,string? outputSchemaJson,string correlationId,AiExecutionContext? executionContext=null,CancellationToken cancellationToken=default);
    Task<AiEmbeddingResult> CreateEmbeddingAsync(Guid tenantId,string featureCode,IReadOnlyCollection<string> inputs,string correlationId,CancellationToken cancellationToken=default);
}

public interface IAiProviderRouteRepository
{
    Task<IReadOnlyCollection<AiProviderRoute>> GetRoutesAsync(Guid tenantId,string featureCode,string capabilityCode,CancellationToken cancellationToken=default);
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
