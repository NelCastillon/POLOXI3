namespace Ams.Application.Common.Dtos;

public sealed class ApInvoiceLineDto
{
    public Guid ApInvoiceLineId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ApInvoiceId { get; set; }
    public int LineOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? GLAccountId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
