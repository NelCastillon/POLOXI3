namespace Ams.Application.Features.Iam;

public sealed class SubmitPrivilegedAccessRequest
{
    public Guid TenantId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid TargetRoleId { get; set; }
    public string JustificationText { get; set; } = string.Empty;
    public DateTime RequestedStartDateUtc { get; set; }
    public DateTime RequestedEndDateUtc { get; set; }
}

public sealed class ReviewAccessDecisionRequest
{
    public Guid RequestId { get; set; }
    public bool IsApproved { get; set; }
    public Guid ReviewerUserId { get; set; }
    public string? Notes { get; set; }
}
