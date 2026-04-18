namespace Ams.Application.Common.Dtos;

public sealed class CommissionPayeeDto
{
    public Guid PayeeId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid CommissionPlanId { get; set; }
    public string PayeeTypeCode { get; set; } = string.Empty;
    public decimal SplitPercentage { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
