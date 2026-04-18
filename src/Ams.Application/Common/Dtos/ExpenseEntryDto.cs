namespace Ams.Application.Common.Dtos;

public sealed class ExpenseEntryDto
{
    public Guid ExpenseId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? EngagementId { get; set; }
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public bool IsBillable { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public Guid? InvoiceId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
