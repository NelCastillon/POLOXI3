namespace Ams.Application.Common.Dtos;

public sealed class MilestoneBillingLinkDto
{
    public Guid LinkId { get; set; }
    public Guid TenantId { get; set; }
    public Guid MilestoneId { get; set; }
    public Guid? InvoiceId { get; set; }
    public decimal BillingAmount { get; set; }
    public DateTime? TriggeredDateUtc { get; set; }
    public string StatusCode { get; set; } = "Pending";
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
