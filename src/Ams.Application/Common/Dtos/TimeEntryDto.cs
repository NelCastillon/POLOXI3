namespace Ams.Application.Common.Dtos;

public sealed class TimeEntryDto
{
    public Guid TimeEntryId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? EngagementId { get; set; }
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly EntryDate { get; set; }
    public decimal Hours { get; set; }
    public decimal BillableHours { get; set; }
    public decimal RateAmount { get; set; }
    public string? Description { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public Guid? InvoiceId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
