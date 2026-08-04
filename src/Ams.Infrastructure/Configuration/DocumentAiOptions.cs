namespace Ams.Infrastructure.Configuration;

public sealed class DocumentAiOptions
{
    public string DocumentIntelligenceEndpoint { get; set; } = string.Empty;
    public string DocumentIntelligenceApiKey { get; set; } = string.Empty;
    public string DocumentIntelligenceModelId { get; set; } = "prebuilt-layout";
    public string DocumentIntelligenceApiVersion { get; set; } = "2024-11-30";
    public string AzureOpenAiEndpoint { get; set; } = string.Empty;
    public string AzureOpenAiApiKey { get; set; } = string.Empty;
    public string AzureOpenAiDeployment { get; set; } = string.Empty;
    public string AzureOpenAiApiVersion { get; set; } = "2025-04-01-preview";
    public int RequestTimeoutSeconds { get; set; } = 180;
}
