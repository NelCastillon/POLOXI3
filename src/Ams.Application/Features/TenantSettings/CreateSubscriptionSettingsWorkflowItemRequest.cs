using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.TenantSettings;

public sealed class CreateSubscriptionSettingsWorkflowItemRequest
{
    public Guid TenantId { get; init; }

    [Required, MaxLength(80)]
    public string PageCode { get; init; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Description { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string Category { get; init; } = string.Empty;

    [Required, MaxLength(80)]
    public string Stage { get; init; } = "Open";

    [Required, MaxLength(80)]
    public string Status { get; init; } = "Open";

    [Required, MaxLength(40)]
    public string Priority { get; init; } = "Medium";

    [Required, MaxLength(200)]
    public string OwnerName { get; init; } = string.Empty;

    public DateTime? DueDateUtc { get; init; }

    [Required, MaxLength(40)]
    public string RiskCode { get; init; } = "Medium";

    [Required, MaxLength(120)]
    public string ControlCode { get; init; } = string.Empty;

    public int SortOrder { get; init; }
    public Guid? CreatedByUserId { get; init; }
}
