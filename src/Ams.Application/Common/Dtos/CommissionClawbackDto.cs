namespace Ams.Application.Common.Dtos;

public sealed class CommissionClawbackDto
{
    public Guid ClawbackId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PayeeId { get; set; }
    public string PayeeName { get; set; } = string.Empty;
    public string PayeeTypeCode { get; set; } = string.Empty;
    public Guid CommissionResultId { get; set; }
    public Guid OriginalTransactionId { get; set; }
    public string TransactionReference { get; set; } = string.Empty;
    public string SourceEntityName { get; set; } = string.Empty;
    public decimal OriginalCommissionAmount { get; set; }
    public DateOnly ClawbackDate { get; set; }
    public decimal Amount { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
