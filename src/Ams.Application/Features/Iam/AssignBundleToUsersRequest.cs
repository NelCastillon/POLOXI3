namespace Ams.Application.Features.Iam;

public sealed class AssignBundleToUsersRequest
{
    public Guid                TenantId          { get; set; }
    public Guid                BundleId          { get; set; }
    public IReadOnlyList<Guid> UserIds           { get; set; } = [];
    public Guid?               AssignedByUserId  { get; set; }
}
