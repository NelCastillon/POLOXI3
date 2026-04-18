namespace Ams.Application.Common.Dtos;

public sealed class UserGroupMemberDto
{
    public Guid MemberId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserGroupId { get; set; }
    public string? GroupName { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public DateTime JoinedDateUtc { get; set; }
    public DateTime? RemovedDateUtc { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
