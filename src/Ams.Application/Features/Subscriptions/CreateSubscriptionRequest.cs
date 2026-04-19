namespace Ams.Application.Features.Subscriptions;

public sealed class CreateSubscriptionRequest
{
    public Guid      TenantId        { get; set; }
    public Guid      PlanId          { get; set; }
    public string    RenewalType     { get; set; } = "Auto";
    public string    BillingCycle    { get; set; } = "Monthly";
    public decimal   BaseAmount      { get; set; }
    public DateTime  StartDateUtc    { get; set; } = DateTime.UtcNow;
    public DateTime? EndDateUtc      { get; set; }
    public Guid?     CreatedByUserId { get; set; }
}
