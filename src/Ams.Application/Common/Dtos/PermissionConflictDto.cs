namespace Ams.Application.Common.Dtos;

public sealed class PermissionConflictDto
{
    public Guid    UserId          { get; set; }
    public string? UserFullName    { get; set; }
    public Guid    PermissionId    { get; set; }
    public string? PermissionName  { get; set; }
    public string? PermissionCode  { get; set; }
    public string  ConflictType    { get; set; } = string.Empty;
    public Guid    AllowOverrideId { get; set; }
    public Guid    DenyOverrideId  { get; set; }
}
