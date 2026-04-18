namespace Ams.Application.Features.Iam;

public sealed class SetBundleRolesRequest
{
    public Guid                  BundleId         { get; set; }
    public IReadOnlyList<Guid>   RoleIds          { get; set; } = [];
    public Guid?                 ModifiedByUserId { get; set; }
}
