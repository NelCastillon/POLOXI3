namespace Ams.Application.Common.Dtos;

public sealed record DocumentWorkflowStepTemplateDto
{
    public Guid StepTemplateId { get; init; }
    public Guid TenantId { get; init; }
    public Guid WorkflowTemplateId { get; init; }
    public string StepName { get; init; } = string.Empty;
    public string StepType { get; init; } = string.Empty;
    public int StepOrder { get; init; }
    public string? Description { get; init; }
    public string? AssignedToRoleCode { get; init; }
    public Guid? AssignedToUserId { get; init; }
    public bool AssignToBranchAdmin { get; init; }
    public bool AssignToDocOwner { get; init; }
    public bool IsRequired { get; init; }
    public int? DueDays { get; init; }
    public int? EscalateDays { get; init; }
    public string? EscalateToRoleCode { get; init; }
    public bool RequiresPreviousApproval { get; init; }
    public string? SkipIfCondition { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
