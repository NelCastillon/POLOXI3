namespace Ams.Domain.Entities;

public sealed class Plan
{
    public Guid   PlanId                { get; private set; } = Guid.NewGuid();
    public string PlanCode              { get; private set; } = string.Empty;
    public string PlanName              { get; private set; } = string.Empty;
    public string BillingFrequency      { get; private set; } = "Monthly";
    public decimal BasePrice            { get; private set; }
    public int    IncludedUsers         { get; private set; }
    public decimal IncludedStorageGb    { get; private set; }
    public int    IncludedApiCallsPerDay { get; private set; }
    public bool   IsActive              { get; private set; } = true;
    public DateTime CreatedDateUtc      { get; private set; } = DateTime.UtcNow;
    public DateTime? ModifiedDateUtc    { get; private set; }
    public Guid?  CreatedByUserId       { get; private set; }
    public bool   IsDeleted             { get; private set; }

    private Plan() { }

    public Plan(string planCode, string planName, string billingFrequency, decimal basePrice,
                int includedUsers, decimal includedStorageGb, int includedApiCallsPerDay,
                Guid? createdByUserId)
    {
        PlanCode               = planCode;
        PlanName               = planName;
        BillingFrequency       = billingFrequency;
        BasePrice              = basePrice;
        IncludedUsers          = includedUsers;
        IncludedStorageGb      = includedStorageGb;
        IncludedApiCallsPerDay = includedApiCallsPerDay;
        CreatedByUserId        = createdByUserId;
    }
}
