namespace Ams.Application.Common.Dtos;

public sealed class PermissionScopePreviewDto
{
    public Guid    PermissionId   { get; set; }
    public string? PermissionName { get; set; }
    public string? PermissionCode { get; set; }
    public string? ResourceCode   { get; set; }
    public string? ActionCode     { get; set; }
    public bool    IsGranted      { get; set; }
    public string  Source         { get; set; } = string.Empty;  // "Role" | "Override"
    public string? RoleName       { get; set; }
    public Guid?   OverrideId     { get; set; }
    public string? ScopeTypeCode  { get; set; }
    public string? ScopeValue     { get; set; }
}
