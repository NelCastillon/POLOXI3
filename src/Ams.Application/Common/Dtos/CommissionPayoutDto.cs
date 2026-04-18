namespace Ams.Application.Common.Dtos;

public sealed class CommissionPayoutDto
{
    public Guid PayoutId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PayeeId { get; set; }
    public DateOnly PayoutDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime? ProcessedDateUtc { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
