namespace Ams.Application.Common.Dtos;

public sealed class UserRoleDto
{
    public Guid UserRoleId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? UserName { get; set; }
    public Guid RoleId { get; set; }
    public string? RoleName { get; set; }
    public string? RoleCode { get; set; }
    public string? AssignedByFullName { get; set; }
    public DateTime AssignedDateUtc { get; set; }
    public DateTime? EffectiveStartDateUtc { get; set; }
    public DateTime? EffectiveEndDateUtc { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
