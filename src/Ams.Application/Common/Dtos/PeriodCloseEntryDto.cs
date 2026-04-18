namespace Ams.Application.Common.Dtos;

public sealed class PeriodCloseEntryDto
{
    public Guid PeriodCloseEntryId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountingPeriodId { get; set; }
    public string TaskDescription { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public Guid? CompletedByUserId { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
