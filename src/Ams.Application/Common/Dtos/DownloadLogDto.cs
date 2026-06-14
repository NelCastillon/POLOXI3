namespace Ams.Application.Common.Dtos;

public sealed class DownloadLogDto
{
    public Guid DownloadLogId { get; set; }
    public Guid CarrierDownloadBatchId => DownloadLogId;
    public Guid TenantId { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string FeedType { get; set; } = string.Empty;
    public string SourceType => FeedType;
    public string Status { get; set; } = string.Empty;
    public int RecordsReceived { get; set; }
    public int TotalRecords => RecordsReceived;
    public int RecordsProcessed { get; set; }
    public int ProcessedRecords => RecordsProcessed;
    public int RecordsFailed { get; set; }
    public int FailedRecords => RecordsFailed;
    public DateTime StartedUtc { get; set; }
    public DateTime ReceivedDateUtc => StartedUtc;
    public DateTime? CompletedUtc { get; set; }
    public DateTime? CompletedDateUtc => CompletedUtc;
    public string? FileName { get; set; }
    public string? RawStorageUri { get; set; }
    public string? ErrorMessage { get; set; }
}
