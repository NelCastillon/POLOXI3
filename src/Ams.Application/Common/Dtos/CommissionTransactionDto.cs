namespace Ams.Application.Common.Dtos;

public sealed class CommissionTransactionDto
{
    public Guid TransactionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PayeeId { get; set; }
    public Guid CommissionPlanId { get; set; }
    public string SourceEntityName { get; set; } = string.Empty;
    public Guid SourceEntityId { get; set; }
    public DateOnly TransactionDate { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public Guid? PayoutId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
