namespace Ams.Application.Features.TenantSettings;

public sealed class AdvanceSubscriptionSettingsWorkflowRequest
{
    public string? Stage { get; init; }
    public string? Status { get; init; }
    public Guid? ModifiedByUserId { get; init; }
}
