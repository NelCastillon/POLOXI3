namespace Ams.Application.Common.Dtos;

public sealed class RolePermissionDto
{
    public Guid RolePermissionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public string? RoleName { get; set; }
    public Guid PermissionId { get; set; }
    public string? PermissionCode { get; set; }
    public string? PermissionName { get; set; }
    public string? ResourceCode { get; set; }
    public string? ActionCode { get; set; }
    public string? GrantedByFullName { get; set; }
    public DateTime GrantedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
