namespace Ams.Application.Common.Dtos;

public sealed class SubscriptionDto
{
    public Guid      SubscriptionId  { get; set; }
    public Guid      TenantId        { get; set; }
    public string    TenantName      { get; set; } = string.Empty;
    public Guid      PlanId          { get; set; }
    public string    PlanCode        { get; set; } = string.Empty;
    public string    StatusCode      { get; set; } = string.Empty;
    public string    RenewalType     { get; set; } = string.Empty;
    public string    BillingCycle    { get; set; } = string.Empty;
    public decimal   BaseAmount      { get; set; }
    public DateTime  StartDateUtc    { get; set; }
    public DateTime? EndDateUtc      { get; set; }
    public DateTime  CreatedDateUtc  { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
