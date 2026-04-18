namespace Ams.Application.Features.Iam;

public sealed class ExtendRoleAssignmentRequest
{
    public Guid      UserRoleId        { get; set; }
    public DateTime  NewEndDateUtc     { get; set; }
    public string?   Reason            { get; set; }
    public Guid?     ExtendedByUserId  { get; set; }
}
