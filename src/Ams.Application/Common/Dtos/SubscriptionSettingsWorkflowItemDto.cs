namespace Ams.Application.Common.Dtos;

public sealed class SubscriptionSettingsWorkflowItemDto
{
    public Guid WorkflowItemId { get; init; }
    public Guid TenantId { get; init; }
    public string PageCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string OwnerName { get; init; } = string.Empty;
    public DateTime? DueDateUtc { get; init; }
    public string RiskCode { get; init; } = string.Empty;
    public string ControlCode { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public DateTime? ModifiedDateUtc { get; init; }
}
