using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class RateCard : AuditableEntity
{
    public string RateCardCode { get; private set; } = string.Empty;
    public string RateCardName { get; private set; } = string.Empty;
    public DateOnly EffectiveStartDate { get; private set; }
    public DateOnly? EffectiveEndDate { get; private set; }
    public RateCardStatus Status { get; private set; } = RateCardStatus.Active;
    public string? Description { get; private set; }

    private RateCard() { }

    public RateCard(Guid tenantId, string rateCardCode, string rateCardName, DateOnly effectiveStartDate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        RateCardCode = rateCardCode;
        RateCardName = rateCardName;
        EffectiveStartDate = effectiveStartDate;
        Status = RateCardStatus.Active;
    }
}
