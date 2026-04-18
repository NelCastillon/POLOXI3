namespace Ams.Application.Features.Iam;

public sealed class ApproveRoleAssignmentRequest
{
    public Guid    UserRoleId       { get; set; }
    public Guid?   ApprovedByUserId { get; set; }
    public string? Reason           { get; set; }
}
