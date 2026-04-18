using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class Agreement : AuditableEntity
{
    public string AgreementNumber { get; private set; } = string.Empty;
    public Guid AccountId { get; private set; }
    public Guid? OpportunityId { get; private set; }
    public AgreementStatus Status { get; private set; }
    public DateOnly EffectiveStartDate { get; private set; }
    public DateOnly? EffectiveEndDate { get; private set; }
    public decimal? TotalContractValue { get; private set; }

    private Agreement() { }

    public Agreement(Guid tenantId, string agreementNumber, Guid accountId, DateOnly startDate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AgreementNumber = agreementNumber;
        AccountId = accountId;
        EffectiveStartDate = startDate;
        Status = AgreementStatus.Draft;
    }
}
