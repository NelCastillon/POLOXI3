namespace Ams.Application.Common.Dtos;

public sealed class BadDebtEntryDto
{
    public Guid BadDebtEntryId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? InvoiceId { get; set; }
    public DateOnly WriteOffDate { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? GLAccountId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
