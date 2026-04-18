namespace Ams.Application.Common.Dtos;

public sealed class WorkflowApprovalHistoryDto
{
    public Guid HistoryId { get; init; }
    public Guid TenantId { get; init; }
    public Guid WorkflowInstanceId { get; init; }
    public Guid? ApprovalStepId { get; init; }
    public Guid? ActorUserId { get; init; }
    public string ActionCode { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public string? PreviousStatusCode { get; init; }
    public string? NewStatusCode { get; init; }
    public bool IsDelegated { get; init; }
    public Guid? DelegatedByUserId { get; init; }
    public DateTime ActionDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
