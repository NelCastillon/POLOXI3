using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class AccountingPeriod : AuditableEntity
{
    public string PeriodName { get; private set; } = string.Empty;
    public int FiscalYear { get; private set; }
    public int PeriodNumber { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public AccountingPeriodStatus Status { get; private set; } = AccountingPeriodStatus.Open;
    public DateTime? ClosedDateUtc { get; private set; }
    public Guid? ClosedByUserId { get; private set; }

    private AccountingPeriod() { }

    public AccountingPeriod(Guid tenantId, string periodName, int fiscalYear, int periodNumber, DateOnly startDate, DateOnly endDate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        PeriodName = periodName;
        FiscalYear = fiscalYear;
        PeriodNumber = periodNumber;
        StartDate = startDate;
        EndDate = endDate;
        Status = AccountingPeriodStatus.Open;
    }
}
