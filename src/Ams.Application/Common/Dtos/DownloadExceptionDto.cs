namespace Ams.Application.Common.Dtos;

public sealed class DownloadExceptionDto
{
    public Guid DownloadExceptionId { get; set; }
    public Guid CarrierDownloadExceptionId => DownloadExceptionId;
    public Guid TenantId { get; set; }
    public Guid DownloadLogId { get; set; }
    public Guid CarrierDownloadBatchId => DownloadLogId;
    public Guid CarrierDownloadItemId { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string? CarrierPolicyNumber { get; set; }
    public string? NamedInsured { get; set; }
    public string? TransactionType { get; set; }
    public string? LineOfBusiness { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public decimal? Premium { get; set; }
    public string ExceptionType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RawPayload { get; set; }
    public string ResolutionStatus { get; set; } = string.Empty;
    public string Status => ResolutionStatus;
    public Guid? AssignedToUserId { get; set; }
    public string? ResolvedByUserId { get; set; }
    public DateTime OccurredUtc { get; set; }
    public DateTime? ResolvedUtc { get; set; }
    public string? ResolutionNotes { get; set; }
}
