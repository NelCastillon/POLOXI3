namespace Ams.Application.Common.Dtos;

public sealed class AccountOwnerHistoryDto
{
    public Guid HistoryId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public Guid? PreviousOwnerUserId { get; set; }
    public string PreviousOwnerName { get; set; } = string.Empty;
    public Guid? NewOwnerUserId { get; set; }
    public string NewOwnerName { get; set; } = string.Empty;
    public DateTime ChangedDateUtc { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? Notes { get; set; }
}
