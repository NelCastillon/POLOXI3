namespace Ams.Application.Common.Dtos;

public sealed class RoleBundleDto
{
    public Guid    BundleId       { get; set; }
    public Guid    TenantId       { get; set; }
    public string  BundleCode     { get; set; } = string.Empty;
    public string  BundleName     { get; set; } = string.Empty;
    public string? Description    { get; set; }
    public bool    IsSystemBundle { get; set; }
    public bool    IsActive       { get; set; }
    public int     SortOrder      { get; set; }
    public int     RoleCount      { get; set; }
    public int     UserCount      { get; set; }
    public DateTime  CreatedDateUtc  { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
