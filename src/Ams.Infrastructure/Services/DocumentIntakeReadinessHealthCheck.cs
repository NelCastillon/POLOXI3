using Ams.Application.Abstractions.Persistence;
using Ams.Infrastructure.Configuration;
using Azure.Identity;
using Azure.Storage.Blobs;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Ams.Infrastructure.Services;

public sealed class DocumentIntakeReadinessHealthCheck(ISqlConnectionFactory connectionFactory,IOptions<DocumentAiOptions> aiOptions,IOptions<DocumentStorageOptions> storageOptions,IConfiguration configuration,IDocumentIntakeOperationsRepository operations):IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,CancellationToken cancellationToken=default)
    {
        var failures=new List<string>();
        try
        {
            using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var promptCount=await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(1) FROM DMS.AiPromptDefinition WHERE StatusCode=N'APPROVED' AND EffectiveFromUtc<=SYSUTCDATETIME() AND(EffectiveToUtc IS NULL OR EffectiveToUtc>SYSUTCDATETIME());",cancellationToken:cancellationToken));
            if(promptCount==0)failures.Add("No effective approved intake prompt is available.");
        }
        catch(Exception ex){failures.Add($"SQL queue unavailable: {ex.Message}");}

        try
        {
            var storage=storageOptions.Value;
            BlobContainerClient container=!string.IsNullOrWhiteSpace(storage.ConnectionString)?new(storage.ConnectionString,storage.ContainerName):new BlobServiceClient(new Uri(storage.AccountUri),new DefaultAzureCredential()).GetBlobContainerClient(storage.ContainerName);
            await container.GetPropertiesAsync(cancellationToken:cancellationToken);
        }
        catch(Exception ex){failures.Add($"Blob storage unavailable: {ex.Message}");}

        var ai=aiOptions.Value;
        if(!Uri.TryCreate(ai.DocumentIntelligenceEndpoint,UriKind.Absolute,out _))failures.Add("Document Intelligence endpoint is not configured.");
        if(!Uri.TryCreate(ai.AzureOpenAiEndpoint,UriKind.Absolute,out _)||string.IsNullOrWhiteSpace(ai.AzureOpenAiDeployment))failures.Add("Azure OpenAI endpoint or deployment is not configured.");
        if(!Uri.TryCreate(configuration["DocumentSearch:Endpoint"],UriKind.Absolute,out _)||string.IsNullOrWhiteSpace(configuration["DocumentSearch:IndexName"]))failures.Add("Azure AI Search endpoint or index is not configured.");
        try
        {
            var settings=await operations.GetSettingsAsync(null,cancellationToken);
            if(settings.MalwareEnabled&&string.IsNullOrWhiteSpace(settings.MalwareProviderCode))failures.Add("Malware enforcement is enabled without a provider.");
        }
        catch(Exception ex){failures.Add($"Operational settings unavailable: {ex.Message}");}

        return failures.Count==0?HealthCheckResult.Healthy("Document Intake dependencies are ready."):HealthCheckResult.Unhealthy(string.Join(" ",failures));
    }
}
