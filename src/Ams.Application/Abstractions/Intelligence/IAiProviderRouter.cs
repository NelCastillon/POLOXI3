namespace Ams.Application.Abstractions.Intelligence;

public interface IAiProviderRouter
{
    Task<AiGenerationResult> GenerateAsync(Guid tenantId,string featureCode,string systemPrompt,string userPrompt,string? outputSchemaJson,string correlationId,CancellationToken cancellationToken=default);
    Task<AiEmbeddingResult> CreateEmbeddingAsync(Guid tenantId,string featureCode,IReadOnlyCollection<string> inputs,string correlationId,CancellationToken cancellationToken=default);
}

public interface IAiProviderRouteRepository
{
    Task<IReadOnlyCollection<AiProviderRoute>> GetRoutesAsync(Guid tenantId,string featureCode,string capabilityCode,CancellationToken cancellationToken=default);
}

public sealed record AiProviderRoute(Guid TenantId,string FeatureCode,string ProviderCode,string ProviderTypeCode,string ModelCode,string DeploymentName,string? EndpointReference,string? CredentialReference,string? ApiVersion,int TimeoutSeconds,decimal Temperature,int MaximumOutputTokens,int Priority,bool IsFallback);
