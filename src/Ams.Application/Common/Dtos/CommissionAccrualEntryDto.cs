namespace Ams.Application.Common.Dtos;

public sealed class CommissionAccrualEntryDto
{
    public Guid AccrualEntryId { get; set; }
    public Guid TenantId { get; set; }
    public Guid TransactionId { get; set; }
    public Guid? GLAccountId { get; set; }
    public DateOnly AccrualDate { get; set; }
    public decimal AccruedAmount { get; set; }
    public DateOnly? ReversalDate { get; set; }
    public decimal? ReversedAmount { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
