namespace Ams.Application.Common.Dtos;

public sealed class AccountSegmentDto
{
    public Guid SegmentId { get; set; }
    public Guid? TenantId { get; set; }
    public string SegmentCode { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
