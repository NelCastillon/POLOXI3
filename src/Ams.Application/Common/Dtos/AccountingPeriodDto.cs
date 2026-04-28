namespace Ams.Application.Common.Dtos;

public sealed class AccountingPeriodDto
{
    public Guid AccountingPeriodId { get; set; }
    public Guid TenantId { get; set; }
    public string PeriodCode { get; set; } = string.Empty;
    public string PeriodName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
