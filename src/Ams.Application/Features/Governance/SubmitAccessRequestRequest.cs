namespace Ams.Application.Features.Governance;

public sealed class SubmitAccessRequestRequest
{
    public Guid   TenantId               { get; set; }
    public Guid   RequestedByUserId      { get; set; }
    public Guid   RequestedForUserId     { get; set; }
    public string RequestTypeCode        { get; set; } = string.Empty;
    public Guid?  RoleId                 { get; set; }
    public Guid?  PermissionId           { get; set; }
    public string? ScopeCode             { get; set; }
    public DateTime? StartDateUtc        { get; set; }
    public DateTime? EndDateUtc          { get; set; }
    public string BusinessJustification  { get; set; } = string.Empty;
    public string? TicketReference       { get; set; }
    public string UrgencyCode            { get; set; } = "Normal";
    public string? AttachmentFileName    { get; set; }
}
