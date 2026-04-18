namespace Ams.Application.Features.Audit;

public sealed class LogApprovalHistoryRequest
{
    public Guid TenantId { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public Guid? ApprovalStepId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? PreviousStatusCode { get; set; }
    public string? NewStatusCode { get; set; }
    public bool IsDelegated { get; set; }
    public Guid? DelegatedByUserId { get; set; }
}
