using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class OperationalActivityLog : AuditableEntity
{
    public Guid? AccountId { get; private set; }
    public Guid? EngagementId { get; private set; }
    public Guid? AgreementId { get; private set; }
    public DateOnly ActivityDate { get; private set; }
    public string ActivityTypeCode { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid? PerformedByUserId { get; private set; }

    private OperationalActivityLog() { }

    public OperationalActivityLog(Guid tenantId, DateOnly activityDate, string activityTypeCode, string subject, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        ActivityDate = activityDate;
        ActivityTypeCode = activityTypeCode;
        Subject = subject;
    }
}
