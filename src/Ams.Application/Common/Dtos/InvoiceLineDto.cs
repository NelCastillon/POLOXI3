namespace Ams.Application.Common.Dtos;

public sealed class InvoiceLineDto
{
    public Guid InvoiceLineId { get; set; }
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public int LineOrder { get; set; }
    public string? ItemCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal LineTotal { get; set; }
    public string? SourceEntityName { get; set; }
    public Guid? SourceEntityId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
