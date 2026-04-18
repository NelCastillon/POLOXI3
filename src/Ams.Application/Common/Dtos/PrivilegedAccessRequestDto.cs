namespace Ams.Application.Common.Dtos;

public sealed class PrivilegedAccessRequestDto
{
    public Guid RequestId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string? RequestedByFullName { get; set; }
    public Guid TargetRoleId { get; set; }
    public string? TargetRoleName { get; set; }
    public string JustificationText { get; set; } = string.Empty;
    public DateTime RequestedStartDateUtc { get; set; }
    public DateTime RequestedEndDateUtc { get; set; }
    public string? ApprovedByFullName { get; set; }
    public DateTime? ApprovalDateUtc { get; set; }
    public DateTime? GrantedStartDateUtc { get; set; }
    public DateTime? GrantedEndDateUtc { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? RevokedReason { get; set; }
    public DateTime? RevokedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
