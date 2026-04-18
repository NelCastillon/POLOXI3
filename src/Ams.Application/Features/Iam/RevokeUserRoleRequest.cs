namespace Ams.Application.Features.Iam;

public sealed class RevokeUserRoleRequest
{
    public Guid    UserRoleId      { get; set; }
    public Guid?   RevokedByUserId { get; set; }
    public string? Reason          { get; set; }
}
