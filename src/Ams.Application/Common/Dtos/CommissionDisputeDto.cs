namespace Ams.Application.Common.Dtos;

public sealed class CommissionDisputeDto
{
    public Guid DisputeId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PayeeId { get; set; }
    public Guid? TransactionId { get; set; }
    public DateOnly DisputeDate { get; set; }
    public string DisputeReason { get; set; } = string.Empty;
    public decimal DisputedAmount { get; set; }
    public string? Resolution { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedDateUtc { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
