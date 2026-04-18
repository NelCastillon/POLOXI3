namespace Ams.Application.Common.Dtos;

/// <summary>
/// Flattened effective permission for a user — union of role permissions
/// minus explicit user denies, plus explicit user grants.
/// </summary>
public sealed class EffectivePermissionDto
{
    public Guid UserId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public string PermissionName { get; set; } = string.Empty;
    public string ResourceCode { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string GrantSource { get; set; } = string.Empty;  // "Role" | "Direct"
    public string? RoleName { get; set; }
    public DateTime? ExpiresDateUtc { get; set; }
}
