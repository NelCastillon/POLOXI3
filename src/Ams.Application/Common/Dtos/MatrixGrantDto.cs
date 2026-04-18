namespace Ams.Application.Common.Dtos;

public sealed class MatrixGrantDto
{
    public Guid RolePermissionId { get; set; }
    public Guid RoleId           { get; set; }
    public Guid PermissionId     { get; set; }
}
