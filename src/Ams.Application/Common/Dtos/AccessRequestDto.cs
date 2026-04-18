namespace Ams.Application.Common.Dtos;

public sealed class AccessRequestDto
{
    public Guid   AccessRequestId        { get; set; }
    public Guid   TenantId               { get; set; }
    public Guid   RequestedByUserId      { get; set; }
    public string? RequestedByFullName   { get; set; }
    public Guid   RequestedForUserId     { get; set; }
    public string? RequestedForFullName  { get; set; }
    public string? RequestedForEmail     { get; set; }
    public string RequestTypeCode        { get; set; } = string.Empty;
    public Guid?  RoleId                 { get; set; }
    public string? RoleName              { get; set; }
    public Guid?  PermissionId           { get; set; }
    public string? PermissionName        { get; set; }
    public string? ScopeCode             { get; set; }
    public DateTime? StartDateUtc        { get; set; }
    public DateTime? EndDateUtc          { get; set; }
    public string BusinessJustification  { get; set; } = string.Empty;
    public string? TicketReference       { get; set; }
    public string UrgencyCode            { get; set; } = "Normal";
    public string? AttachmentFileName    { get; set; }
    public string StatusCode             { get; set; } = "Pending";
    public string? ApproverComment        { get; set; }
    public DateTime CreatedDateUtc       { get; set; }
    public DateTime? ModifiedDateUtc     { get; set; }
}
