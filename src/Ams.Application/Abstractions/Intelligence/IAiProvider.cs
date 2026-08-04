namespace Ams.Application.Abstractions.Intelligence;

public interface IAiProvider
{
    string ProviderTypeCode { get; }
    Task<AiProviderHealth> CheckHealthAsync(AiProviderContext context,CancellationToken cancellationToken=default);
    Task<AiGenerationResult> GenerateAsync(AiGenerationRequest request,CancellationToken cancellationToken=default);
    Task<AiEmbeddingResult> CreateEmbeddingAsync(AiEmbeddingRequest request,CancellationToken cancellationToken=default);
}

public sealed record AiProviderContext(Guid TenantId,string ProviderCode,string ProviderTypeCode,string ModelCode,string DeploymentName,string? EndpointReference,string? CredentialReference,string? ApiVersion,int TimeoutSeconds);
public sealed record AiGenerationRequest(AiProviderContext Context,string FeatureCode,string SystemPrompt,string UserPrompt,string? OutputSchemaJson,decimal Temperature,int MaximumOutputTokens,string CorrelationId);
public sealed record AiGenerationResult(string Content,string? StructuredOutputJson,int InputTokenCount,int OutputTokenCount,decimal? Confidence,string ProviderRequestId,TimeSpan Duration);
public sealed record AiEmbeddingRequest(AiProviderContext Context,IReadOnlyCollection<string> Inputs,string CorrelationId);
public sealed record AiEmbeddingResult(IReadOnlyCollection<ReadOnlyMemory<float>> Embeddings,int InputTokenCount,string ProviderRequestId,TimeSpan Duration);
public sealed record AiProviderHealth(string StatusCode,string Message,TimeSpan Duration);
