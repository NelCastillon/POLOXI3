namespace Legal.Infrastructure.Configuration;

public sealed class DocumentIntelligenceOptions
{
    public const string SectionName = "DocumentIntelligence";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "prebuilt-layout";
    public string ModelVersion { get; set; } = "4.0";
    public string StorageRoot { get; set; } = "App_Data/legal-documents";
    public string BinaryStoreProvider { get; set; } = "AzureBlob";
    public string BlobServiceUri { get; set; } = string.Empty;
    public string BlobConnectionString { get; set; } = string.Empty;
    public string BlobContainerName { get; set; } = "legal-originals";
    public int BlobRetentionDays { get; set; } = 2555;
    public bool ApplyLegalHold { get; set; }
    public string MalwareScanEndpoint { get; set; } = string.Empty;
    public string MalwareScannerProvider { get; set; } = "DefenderForStorage";
    public string DefenderScanContainerName { get; set; } = "legal-quarantine";
    public int DefenderScanTimeoutSeconds { get; set; } = 300;
    public int DefenderScanPollSeconds { get; set; } = 5;
    public bool SearchProjectionEnabled { get; set; }
    public string SearchEndpoint { get; set; } = string.Empty;
    public string SearchApiKey { get; set; } = string.Empty;
    public string SearchIndexName { get; set; } = "legal-matter-corpus";
    public long MaximumFileSizeBytes { get; set; } = 100 * 1024 * 1024;
    public int NativeTextMinimumCharactersPerPage { get; set; } = 80;
    public decimal NativeTextMinimumReadableCharacterRatio { get; set; } = 0.85m;
}
