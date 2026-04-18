using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class Opportunity : AuditableEntity
{
    public string OpportunityNumber { get; private set; } = string.Empty;
    public Guid AccountId { get; private set; }
    public string OpportunityName { get; private set; } = string.Empty;
    public decimal EstimatedAmount { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public OpportunityStatus Status { get; private set; }

    private Opportunity() { }

    public Opportunity(Guid tenantId, string opportunityNumber, Guid accountId, string opportunityName, decimal estimatedAmount, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        OpportunityNumber = opportunityNumber;
        AccountId = accountId;
        OpportunityName = opportunityName;
        EstimatedAmount = estimatedAmount;
        Status = OpportunityStatus.Open;
    }
}
