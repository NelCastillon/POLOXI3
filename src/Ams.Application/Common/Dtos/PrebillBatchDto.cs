namespace Ams.Application.Common.Dtos;

public sealed class PrebillBatchDto
{
    public Guid PrebillBatchId { get; set; }
    public Guid TenantId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
    public DateOnly BillingPeriodStart { get; set; }
    public DateOnly BillingPeriodEnd { get; set; }
    public decimal TotalAmount { get; set; }
    public string StatusCode { get; set; } = "Draft";
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedDateUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
