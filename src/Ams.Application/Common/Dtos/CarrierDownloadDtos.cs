namespace Ams.Application.Common.Dtos;

public sealed class CarrierDownloadItemDto
{
    public Guid CarrierDownloadItemId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CarrierDownloadBatchId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public string? CarrierPolicyNumber { get; set; }
    public string? NamedInsured { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? LineOfBusiness { get; set; }
    public decimal? Premium { get; set; }
    public decimal? Commission { get; set; }
    public string? RawPayload { get; set; }
    public string? NormalizedPayload { get; set; }
    public string MatchStatus { get; set; } = string.Empty;
    public string ProcessingStatus { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class CarrierDownloadMatchDto
{
    public Guid CarrierDownloadMatchId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CarrierDownloadItemId { get; set; }
    public Guid? MatchedAccountId { get; set; }
    public Guid? MatchedPolicyId { get; set; }
    public Guid? MatchedContactId { get; set; }
    public decimal MatchScore { get; set; }
    public string MatchMethod { get; set; } = string.Empty;
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedDateUtc { get; set; }
}

public sealed class CarrierDownloadDashboardDto
{
    public int TotalBatches { get; set; }
    public int ReceivedBatches { get; set; }
    public int ProcessingBatches { get; set; }
    public int CompletedBatches { get; set; }
    public int CompletedWithErrorsBatches { get; set; }
    public int FailedBatches { get; set; }
    public int TotalItems { get; set; }
    public int AutoMatchedItems { get; set; }
    public int ExceptionItems { get; set; }
    public int OpenExceptions { get; set; }
    public int HighSeverityExceptions { get; set; }
}

public sealed class CarrierPolicyDownloadDto
{
    public string CarrierCode { get; set; } = string.Empty;
    public string? PolicyNumber { get; set; }
    public string? NamedInsured { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public decimal? Premium { get; set; }
    public decimal? Commission { get; set; }
    public string? LineOfBusiness { get; set; }
    public string? CoveragesJson { get; set; }
    public string? VehiclesJson { get; set; }
    public string? DriversJson { get; set; }
    public string? LocationsJson { get; set; }
    public string? DocumentsJson { get; set; }
}
