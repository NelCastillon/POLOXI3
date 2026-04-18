namespace Ams.Application.Features.Iam;

public sealed class CreateRoleBundleRequest
{
    public Guid    TenantId       { get; set; }
    public string  BundleCode     { get; set; } = string.Empty;
    public string  BundleName     { get; set; } = string.Empty;
    public string? Description    { get; set; }
    public bool    IsSystemBundle { get; set; }
    public int     SortOrder      { get; set; }
    public Guid?   CreatedByUserId { get; set; }
}
