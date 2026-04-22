namespace Ams.Application.Common.Dtos;

public sealed class DownloadExceptionDto
{
    public Guid DownloadExceptionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid DownloadLogId { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RawPayload { get; set; }
    public string ResolutionStatus { get; set; } = string.Empty;
    public string? ResolvedByUserId { get; set; }
    public DateTime OccurredUtc { get; set; }
    public DateTime? ResolvedUtc { get; set; }
}
