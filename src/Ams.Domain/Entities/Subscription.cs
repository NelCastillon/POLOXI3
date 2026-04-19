namespace Ams.Domain.Entities;

public sealed class Subscription
{
    public Guid      SubscriptionId  { get; private set; } = Guid.NewGuid();
    public Guid      TenantId        { get; private set; }
    public Guid      PlanId          { get; private set; }
    public string    StatusCode      { get; private set; } = "Active";
    public string    RenewalType     { get; private set; } = "Auto";
    public string    BillingCycle    { get; private set; } = "Monthly";
    public decimal   BaseAmount      { get; private set; }
    public DateTime  StartDateUtc    { get; private set; } = DateTime.UtcNow;
    public DateTime? EndDateUtc      { get; private set; }
    public DateTime  CreatedDateUtc  { get; private set; } = DateTime.UtcNow;
    public DateTime? ModifiedDateUtc { get; private set; }
    public Guid?     CreatedByUserId { get; private set; }
    public bool      IsDeleted       { get; private set; }

    private Subscription() { }

    public Subscription(Guid tenantId, Guid planId, string renewalType, string billingCycle,
                        decimal baseAmount, DateTime startDateUtc, DateTime? endDateUtc,
                        Guid? createdByUserId)
    {
        TenantId        = tenantId;
        PlanId          = planId;
        RenewalType     = renewalType;
        BillingCycle    = billingCycle;
        BaseAmount      = baseAmount;
        StartDateUtc    = startDateUtc;
        EndDateUtc      = endDateUtc;
        CreatedByUserId = createdByUserId;
    }
}
