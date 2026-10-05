namespace Legal.Infrastructure.Configuration;

public sealed class DocumentIntelligenceOptions
{
    public const string SectionName = "DocumentIntelligence";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "prebuilt-layout";
    public string ModelVersion { get; set; } = "4.0";
    public string StorageRoot { get; set; } = "App_Data/legal-documents";
    public string MediaStorageRoot { get; set; } = "App_Data/legal-media";
    public string BinaryStoreProvider { get; set; } = "AzureBlob";
    public string BlobServiceUri { get; set; } = string.Empty;
    public string BlobConnectionString { get; set; } = string.Empty;
    public string BlobContainerName { get; set; } = "legal-originals";
    public string MediaBlobContainerName { get; set; } = "legal-media-originals";
    public int BlobRetentionDays { get; set; } = 2555;
    public bool ApplyLegalHold { get; set; }
    public string MalwareScanEndpoint { get; set; } = string.Empty;

    // Document Malware scanning is OFF by default. When "Disabled", intake neither executes a scan
    // nor bypasses a configured scanner. Set to "Http" or "DefenderForStorage" to enforce scanning.
    public string MalwareScannerProvider { get; set; } = "Disabled";
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

    // ── Media & Machine Evidence processing capability configuration ──────────────────────────────
    // Image analysis (PHOTO) via an Azure OpenAI vision-capable chat deployment. When the endpoint,
    // key, or deployment is missing the analyzer reports CapabilityUnavailable and the reviewer
    // annotates manually — observations/regions are never fabricated.
    public string MediaVisionEndpoint { get; set; } = string.Empty;
    public string MediaVisionApiKey { get; set; } = string.Empty;
    public string MediaVisionDeployment { get; set; } = string.Empty;
    public string MediaVisionApiVersion { get; set; } = "2024-08-01-preview";

    // Timestamped audio transcription (AUDIO) via an Azure OpenAI Whisper deployment. When missing the
    // transcriber reports CapabilityUnavailable. Plain text extraction is NOT accepted as a substitute.
    public string MediaTranscriptionEndpoint { get; set; } = string.Empty;
    public string MediaTranscriptionApiKey { get; set; } = string.Empty;
    public string MediaTranscriptionDeployment { get; set; } = string.Empty;
    public string MediaTranscriptionApiVersion { get; set; } = "2024-06-01";
}
