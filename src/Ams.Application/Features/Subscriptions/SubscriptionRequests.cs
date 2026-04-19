namespace Ams.Application.Features.Subscriptions;

public sealed class UpgradeSubscriptionRequest
{
    public Guid PlanId { get; set; }
}

public sealed class DowngradeSubscriptionRequest
{
    public Guid PlanId { get; set; }
}

public sealed class RenewSubscriptionRequest
{
    public DateTime NewEndDateUtc { get; set; }
}

public sealed class AddSubscriptionAddonRequest
{
    public string AddonCode        { get; set; } = string.Empty;
    public string AddonDescription { get; set; } = string.Empty;
}
