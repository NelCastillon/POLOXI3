namespace Ams.Application.Common.Dtos;

public sealed class RecurringBillingScheduleDto
{
    public Guid ScheduleId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? AgreementId { get; set; }
    public string ScheduleName { get; set; } = string.Empty;
    public string FrequencyCode { get; set; } = "Monthly";
    public decimal BillingAmount { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly NextBillingDate { get; set; }
    public DateOnly? LastBillingDate { get; set; }
    public string StatusCode { get; set; } = "Active";
    public string? Description { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
