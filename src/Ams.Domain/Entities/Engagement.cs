using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class Engagement : AuditableEntity
{
    public string EngagementNumber { get; private set; } = string.Empty;
    public Guid AccountId { get; private set; }
    public Guid? AgreementId { get; private set; }
    public string EngagementName { get; private set; } = string.Empty;
    public string EngagementTypeCode { get; private set; } = "Project";
    public Guid? OwnerUserId { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public EngagementStatus Status { get; private set; } = EngagementStatus.Active;

    private Engagement() { }

    public Engagement(Guid tenantId, string engagementNumber, Guid accountId, string engagementName, string engagementTypeCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        EngagementNumber = engagementNumber;
        AccountId = accountId;
        EngagementName = engagementName;
        EngagementTypeCode = engagementTypeCode;
        Status = EngagementStatus.Active;
    }
}
