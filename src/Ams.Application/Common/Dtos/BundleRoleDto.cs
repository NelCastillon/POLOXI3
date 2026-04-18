namespace Ams.Application.Common.Dtos;

public sealed class BundleRoleDto
{
    public Guid   BundleRoleId  { get; set; }
    public Guid   BundleId      { get; set; }
    public Guid   RoleId        { get; set; }
    public string RoleCode      { get; set; } = string.Empty;
    public string RoleName      { get; set; } = string.Empty;
    public string RoleTypeCode  { get; set; } = string.Empty;
    public bool   IsActive      { get; set; }
    public DateTime AssignedDateUtc { get; set; }
}
