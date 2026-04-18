namespace Ams.Application.Common.Dtos;

public sealed class DelinquencyFlagDto
{
    public Guid DelinquencyFlagId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? InvoiceId { get; set; }
    public DateOnly FlagDate { get; set; }
    public int DaysOverdue { get; set; }
    public decimal OverdueAmount { get; set; }
    public string SeverityCode { get; set; } = "Low";
    public string StatusCode { get; set; } = "Open";
    public DateOnly? ResolvedDate { get; set; }
    public string? Notes { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
