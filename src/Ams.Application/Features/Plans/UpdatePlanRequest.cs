namespace Ams.Application.Features.Plans;

public sealed class UpdatePlanRequest
{
    public string  PlanName               { get; set; } = string.Empty;
    public string  BillingFrequency       { get; set; } = "Monthly";
    public decimal BasePrice              { get; set; }
    public int     IncludedUsers          { get; set; }
    public decimal IncludedStorageGb      { get; set; }
    public int     IncludedApiCallsPerDay { get; set; }
    public bool    IsEnterprise           { get; set; }
}
