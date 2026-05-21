namespace Ams.Application.Common.Dtos;

public sealed record DocumentApprovalDto
{
    public Guid ApprovalId { get; init; }
    public Guid TenantId { get; init; }
    public Guid WorkflowInstanceId { get; init; }
    public Guid DocumentId { get; init; }
    public Guid? StepTemplateId { get; init; }
    public string ApprovalName { get; init; } = string.Empty;
    public string ApprovalType { get; init; } = string.Empty;
    public int StepOrder { get; init; }
    public Guid AssignedToUserId { get; init; }
    public string? AssignedToName { get; init; }
    public string? AssignedToRoleCode { get; init; }
    public DateTime AssignedDateUtc { get; init; }
    public string ApprovalStatus { get; init; } = string.Empty;
    public DateTime? ResponseDateUtc { get; init; }
    public Guid? ResponseByUserId { get; init; }
    public string? ResponseByName { get; init; }
    public string? Comments { get; init; }
    public DateTime? DueDateUtc { get; init; }
    public DateTime? EscalatedDateUtc { get; init; }
    public Guid? EscalatedToUserId { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
