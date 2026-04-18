namespace Ams.Application.Common.Dtos;

public sealed class CommissionPayoutBatchDto
{
    public Guid PayoutBatchId { get; set; }
    public Guid TenantId { get; set; }
    public string BatchReference { get; set; } = string.Empty;
    public DateOnly PayPeriodStart { get; set; }
    public DateOnly PayPeriodEnd { get; set; }
    public decimal TotalAmount { get; set; }
    public int PayoutCount { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public Guid? ProcessedByUserId { get; set; }
    public DateTime? ProcessedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
