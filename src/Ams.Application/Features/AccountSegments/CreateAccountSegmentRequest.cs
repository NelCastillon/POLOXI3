using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.AccountSegments;

public sealed class CreateAccountSegmentRequest
{
    public Guid? TenantId { get; set; }

    [Required, StringLength(50)]
    public string SegmentCode { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string SegmentName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
