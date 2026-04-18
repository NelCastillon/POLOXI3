using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class RecurringBillingSchedule : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public Guid? AgreementId { get; private set; }
    public string ScheduleName { get; private set; } = string.Empty;
    public RecurringBillingFrequency Frequency { get; private set; } = RecurringBillingFrequency.Monthly;
    public decimal BillingAmount { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public DateOnly NextBillingDate { get; private set; }
    public DateOnly? LastBillingDate { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    private RecurringBillingSchedule() { }

    public RecurringBillingSchedule(Guid tenantId, Guid accountId, string scheduleName, decimal billingAmount, DateOnly startDate, DateOnly nextBillingDate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        ScheduleName = scheduleName;
        BillingAmount = billingAmount;
        StartDate = startDate;
        NextBillingDate = nextBillingDate;
        Frequency = RecurringBillingFrequency.Monthly;
        IsActive = true;
    }
}
