namespace Ams.Application.Features.Iam;

public sealed class UpdateUserPermissionRequest
{
    public Guid      UserPermissionId      { get; set; }
    public bool      IsGranted             { get; set; }
    public DateTime? EffectiveStartDateUtc { get; set; }
    public DateTime? EffectiveEndDateUtc   { get; set; }
    public string?   Reason                { get; set; }
    public Guid?     ApprovedByUserId      { get; set; }
    public Guid?     ModifiedByUserId      { get; set; }
}
