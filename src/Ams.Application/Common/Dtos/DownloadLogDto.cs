namespace Ams.Application.Common.Dtos;

public sealed class DownloadLogDto
{
    public Guid DownloadLogId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string FeedType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RecordsReceived { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsFailed { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? ErrorMessage { get; set; }
}
