namespace Ams.Application.Features.Iam;

public sealed class RemoveRoleAssignmentRequest
{
    public Guid    UserRoleId      { get; set; }
    public Guid?   RemovedByUserId { get; set; }
    public string? Reason          { get; set; }
}
