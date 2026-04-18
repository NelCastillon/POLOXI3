namespace Ams.Domain.Entities;

public sealed class AccountSegment
{
    public Guid SegmentId { get; set; }
    public Guid? TenantId { get; set; }
    public string SegmentCode { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDateUtc { get; set; }
    public bool IsDeleted { get; set; }
}
