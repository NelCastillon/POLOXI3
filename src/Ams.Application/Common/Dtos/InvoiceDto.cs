namespace Ams.Application.Common.Dtos;

public sealed class InvoiceDto
{
    public Guid InvoiceId { get; set; }
    public Guid TenantId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public Guid? AgreementId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public int StatusCode { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
