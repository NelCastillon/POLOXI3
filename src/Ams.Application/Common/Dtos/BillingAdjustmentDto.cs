namespace Ams.Application.Common.Dtos;

public sealed class BillingAdjustmentDto
{
    public Guid AdjustmentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid AccountId { get; set; }
    public string AdjustmentTypeCode { get; set; } = "Credit";
    public DateOnly AdjustmentDate { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    public string StatusCode { get; set; } = "Pending";
    public DateTime CreatedDateUtc { get; set; }
}
