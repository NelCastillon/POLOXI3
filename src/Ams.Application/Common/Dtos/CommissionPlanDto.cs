namespace Ams.Application.Common.Dtos;

public sealed class CommissionPlanDto
{
    public Guid CommissionPlanId { get; set; }
    public Guid TenantId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
