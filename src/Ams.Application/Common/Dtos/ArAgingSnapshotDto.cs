namespace Ams.Application.Common.Dtos;

public sealed class ArAgingSnapshotDto
{
    public Guid SnapshotId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public decimal CurrentAmount { get; set; }
    public decimal Days30Amount { get; set; }
    public decimal Days60Amount { get; set; }
    public decimal Days90Amount { get; set; }
    public decimal Days90PlusAmount { get; set; }
    public decimal TotalOutstanding { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
