namespace Ams.Application.Common.Dtos;

public sealed class WorkflowReworkRequestDto
{
    public Guid ReworkRequestId { get; init; }
    public Guid TenantId { get; init; }
    public Guid WorkflowInstanceId { get; init; }
    public Guid? ApprovalStepId { get; init; }
    public Guid? RequestedByUserId { get; init; }
    public string RejectionReason { get; init; } = string.Empty;
    public string? ReworkInstructions { get; init; }
    public int? ReturnToStepOrder { get; init; }
    public string StatusCode { get; init; } = string.Empty;
    public Guid? ResubmittedByUserId { get; init; }
    public DateTime? ResubmittedDateUtc { get; init; }
    public DateTime? ResolvedDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
