namespace Ams.Domain.Entities;

public sealed class AccountOwnerHistory
{
    public Guid HistoryId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? PreviousOwnerUserId { get; set; }
    public Guid? NewOwnerUserId { get; set; }
    public DateTime ChangedDateUtc { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
}
