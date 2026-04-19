namespace Ams.Application.Common.Dtos;

public sealed class PlanDto
{
    public Guid    PlanId                 { get; set; }
    public string  PlanCode               { get; set; } = string.Empty;
    public string  PlanName               { get; set; } = string.Empty;
    public string  BillingFrequency       { get; set; } = string.Empty;
    public decimal BasePrice              { get; set; }
    public int     IncludedUsers          { get; set; }
    public decimal IncludedStorageGb      { get; set; }
    public int     IncludedApiCallsPerDay { get; set; }
    public bool    IsEnterprise           { get; set; }
    public bool    IsActive               { get; set; }
    public DateTime CreatedDateUtc        { get; set; }
    public DateTime? ModifiedDateUtc      { get; set; }
}
