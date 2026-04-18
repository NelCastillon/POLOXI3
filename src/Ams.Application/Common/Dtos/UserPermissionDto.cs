namespace Ams.Application.Common.Dtos;

public sealed class UserPermissionDto
{
    public Guid      UserPermissionId      { get; set; }
    public Guid      TenantId              { get; set; }
    public Guid      UserId                { get; set; }
    public string?   UserFullName          { get; set; }
    public Guid      PermissionId          { get; set; }
    public string?   PermissionCode        { get; set; }
    public string?   PermissionName        { get; set; }
    public string?   ResourceCode          { get; set; }
    public string?   ActionCode            { get; set; }
    public bool      IsGranted             { get; set; }
    public string?   GrantedByFullName     { get; set; }
    public string?   ApprovedByFullName    { get; set; }
    public string?   Reason                { get; set; }
    public DateTime  GrantedDateUtc        { get; set; }
    public DateTime? EffectiveStartDateUtc { get; set; }
    public DateTime? ExpiresDateUtc        { get; set; }
    public DateTime  CreatedDateUtc        { get; set; }
}
