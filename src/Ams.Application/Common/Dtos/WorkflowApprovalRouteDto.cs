namespace Ams.Application.Common.Dtos;

public sealed class WorkflowApprovalRouteDto
{
    public Guid RouteId { get; init; }
    public Guid TenantId { get; init; }
    public Guid WorkflowDefinitionId { get; init; }
    public int StepOrder { get; init; }
    public string StepName { get; init; } = string.Empty;
    public Guid? ApproverUserId { get; init; }
    public string? ApproverRoleCode { get; init; }
    public decimal? ThresholdMinAmount { get; init; }
    public decimal? ThresholdMaxAmount { get; init; }
    public bool RequireAllApprovers { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
