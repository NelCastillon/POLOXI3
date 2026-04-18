namespace Ams.Application.Common.Dtos;

public sealed class CommissionPlanVersionDto
{
    public Guid PlanVersionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CommissionPlanId { get; set; }
    public int VersionNumber { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal BaseRatePct { get; set; }
    public DateOnly EffectiveStartDate { get; set; }
    public DateOnly? EffectiveEndDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
