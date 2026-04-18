using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class DelinquencyFlag : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public DateOnly FlagDate { get; private set; }
    public int DaysOverdue { get; private set; }
    public decimal OverdueAmount { get; private set; }
    public DelinquencySeverity Severity { get; private set; } = DelinquencySeverity.Low;
    public string StatusCode { get; private set; } = "Open";
    public DateOnly? ResolvedDate { get; private set; }
    public string? Notes { get; private set; }
    public Guid? AssignedToUserId { get; private set; }

    private DelinquencyFlag() { }

    public DelinquencyFlag(Guid tenantId, Guid accountId, DateOnly flagDate, int daysOverdue, decimal overdueAmount, DelinquencySeverity severity, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        FlagDate = flagDate;
        DaysOverdue = daysOverdue;
        OverdueAmount = overdueAmount;
        Severity = severity;
        StatusCode = "Open";
    }
}
