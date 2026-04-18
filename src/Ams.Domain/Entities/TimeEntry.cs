using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class TimeEntry : AuditableEntity
{
    public Guid? EngagementId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public decimal Hours { get; private set; }
    public decimal BillableHours { get; private set; }
    public decimal RateAmount { get; private set; }
    public string? Description { get; private set; }
    public TimeEntryStatus Status { get; private set; } = TimeEntryStatus.Draft;
    public Guid? InvoiceId { get; private set; }

    private TimeEntry() { }

    public TimeEntry(Guid tenantId, Guid accountId, Guid userId, DateOnly entryDate, decimal hours, decimal billableHours, decimal rateAmount, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        UserId = userId;
        EntryDate = entryDate;
        Hours = hours;
        BillableHours = billableHours;
        RateAmount = rateAmount;
        Status = TimeEntryStatus.Draft;
    }
}
