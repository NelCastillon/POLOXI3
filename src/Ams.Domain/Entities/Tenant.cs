namespace Ams.Domain.Entities;

public sealed class Tenant
{
    public Guid TenantId { get; private set; } = Guid.NewGuid();
    public string TenantCode { get; private set; } = string.Empty;
    public string TenantName { get; private set; } = string.Empty;
    public string PlanCode { get; private set; } = "Standard";
    public bool IsActive { get; private set; } = true;
    public string Locale { get; private set; } = "en-US";
    public string CurrencyCode { get; private set; } = "USD";
    public string TimeZoneId { get; private set; } = "UTC";
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public bool IsDeleted { get; private set; }

    private Tenant() { }

    public Tenant(string tenantCode, string tenantName, string planCode)
    {
        TenantCode = tenantCode;
        TenantName = tenantName;
        PlanCode = planCode;
    }
}
