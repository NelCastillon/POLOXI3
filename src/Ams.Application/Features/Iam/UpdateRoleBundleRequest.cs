namespace Ams.Application.Features.Iam;

public sealed class UpdateRoleBundleRequest
{
    public Guid    BundleId         { get; set; }
    public string  BundleName       { get; set; } = string.Empty;
    public string? Description      { get; set; }
    public int     SortOrder        { get; set; }
    public Guid?   ModifiedByUserId { get; set; }
}
