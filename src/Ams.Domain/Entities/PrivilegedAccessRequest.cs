using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class PrivilegedAccessRequest : AuditableEntity
{
    public Guid RequestedByUserId { get; private set; }
    public Guid TargetRoleId { get; private set; }
    public string JustificationText { get; private set; } = string.Empty;
    public DateTime RequestedStartDateUtc { get; private set; }
    public DateTime RequestedEndDateUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovalDateUtc { get; private set; }
    public DateTime? GrantedStartDateUtc { get; private set; }
    public DateTime? GrantedEndDateUtc { get; private set; }
    public PrivilegedAccessStatus Status { get; private set; } = PrivilegedAccessStatus.Pending;
    public Guid? RevokedByUserId { get; private set; }
    public DateTime? RevokedDateUtc { get; private set; }
    public string? RevokedReason { get; private set; }

    private PrivilegedAccessRequest() { }

    public PrivilegedAccessRequest(Guid tenantId, Guid requestedByUserId, Guid targetRoleId, string justificationText, DateTime requestedStartDateUtc, DateTime requestedEndDateUtc)
        : base(tenantId, requestedByUserId)
    {
        RequestedByUserId = requestedByUserId;
        TargetRoleId = targetRoleId;
        JustificationText = justificationText;
        RequestedStartDateUtc = requestedStartDateUtc;
        RequestedEndDateUtc = requestedEndDateUtc;
    }
}
