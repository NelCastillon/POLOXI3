namespace Ams.Application.Features.Iam;

public sealed class AddUserGroupMemberRequest
{
    public Guid TenantId { get; set; }
    public Guid UserGroupId { get; set; }
    public Guid UserId { get; set; }
    public Guid? AddedByUserId { get; set; }
}
