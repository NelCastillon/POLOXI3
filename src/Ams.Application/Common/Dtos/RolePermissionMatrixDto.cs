namespace Ams.Application.Common.Dtos;

public sealed class RolePermissionMatrixDto
{
    public IEnumerable<RoleDto>       Roles       { get; set; } = [];
    public IEnumerable<PermissionDto> Permissions { get; set; } = [];
    public IEnumerable<MatrixGrantDto> Grants     { get; set; } = [];
}
