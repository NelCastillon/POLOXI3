using Ams.Application.Abstractions.Intelligence;
using Microsoft.Extensions.Logging;

namespace Ams.Infrastructure.Services;

public sealed class AiProviderRouter(IAiProviderRouteRepository routeRepository,IEnumerable<IAiProvider> providers,ILogger<AiProviderRouter> logger):IAiProviderRouter
{
    public Task<AiGenerationResult> GenerateAsync(Guid tenantId,string featureCode,string systemPrompt,string userPrompt,string? outputSchemaJson,string correlationId,CancellationToken cancellationToken=default)
        => ExecuteAsync(tenantId,featureCode,"CHAT",correlationId,(provider,route,context,token)=>provider.GenerateAsync(new(context,featureCode,systemPrompt,userPrompt,outputSchemaJson,route.Temperature,route.MaximumOutputTokens,correlationId),token),cancellationToken);

    public Task<AiEmbeddingResult> CreateEmbeddingAsync(Guid tenantId,string featureCode,IReadOnlyCollection<string> inputs,string correlationId,CancellationToken cancellationToken=default)
        => ExecuteAsync(tenantId,featureCode,"EMBEDDING",correlationId,(provider,_,context,token)=>provider.CreateEmbeddingAsync(new(context,inputs,correlationId),token),cancellationToken);

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
}

public sealed class AiProviderUnavailableException:Exception
{
    public AiProviderUnavailableException(string featureCode,string message,Exception? innerException=null):base(message,innerException)=>FeatureCode=featureCode;
    public string FeatureCode{get;}
}
