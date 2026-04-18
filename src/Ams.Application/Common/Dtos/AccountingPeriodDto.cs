namespace Ams.Application.Common.Dtos;

public sealed class AccountingPeriodDto
{
    public Guid AccountingPeriodId { get; set; }
    public Guid TenantId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public int PeriodNumber { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime? ClosedDateUtc { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
