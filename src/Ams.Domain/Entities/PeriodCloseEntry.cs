using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class PeriodCloseEntry : AuditableEntity
{
    public Guid AccountingPeriodId { get; private set; }
    public string TaskDescription { get; private set; } = string.Empty;
    public string StatusCode { get; private set; } = "Pending";
    public Guid? CompletedByUserId { get; private set; }
    public DateTime? CompletedDateUtc { get; private set; }
    public string? Notes { get; private set; }

    private PeriodCloseEntry() { }

    public PeriodCloseEntry(Guid tenantId, Guid accountingPeriodId, string taskDescription, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountingPeriodId = accountingPeriodId;
        TaskDescription = taskDescription;
        StatusCode = "Pending";
    }
}
