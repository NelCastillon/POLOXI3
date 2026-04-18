namespace Ams.Application.Common.Dtos;

public sealed class WorkflowSlaRuleDto
{
    public Guid SlaRuleId { get; init; }
    public Guid TenantId { get; init; }
    public Guid WorkflowDefinitionId { get; init; }
    public int? StepOrder { get; init; }
    public int SlaHours { get; init; }
    public Guid? EscalationUserId { get; init; }
    public string? EscalationRoleCode { get; init; }
    public string? EscalationMessage { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
