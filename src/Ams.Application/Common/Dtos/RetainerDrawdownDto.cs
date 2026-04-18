namespace Ams.Application.Common.Dtos;

public sealed class RetainerDrawdownDto
{
    public Guid DrawdownId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RetainerAccountId { get; set; }
    public Guid? InvoiceId { get; set; }
    public DateOnly DrawdownDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
