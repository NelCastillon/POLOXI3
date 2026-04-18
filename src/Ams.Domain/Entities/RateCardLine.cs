using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class RateCardLine : AuditableEntity
{
    public Guid RateCardId { get; private set; }
    public string? RoleCode { get; private set; }
    public string? ServiceCode { get; private set; }
    public string? Description { get; private set; }
    public decimal HourlyRate { get; private set; }
    public decimal? DailyRate { get; private set; }
    public DateOnly EffectiveStartDate { get; private set; }
    public DateOnly? EffectiveEndDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    private RateCardLine() { }

    public RateCardLine(Guid tenantId, Guid rateCardId, decimal hourlyRate, DateOnly effectiveStartDate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        RateCardId = rateCardId;
        HourlyRate = hourlyRate;
        EffectiveStartDate = effectiveStartDate;
        IsActive = true;
    }
}
