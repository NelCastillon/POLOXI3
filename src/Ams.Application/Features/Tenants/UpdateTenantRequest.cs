namespace Ams.Application.Features.Tenants;

public sealed class UpdateTenantRequest
{
    public string TenantName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string RegionCode { get; set; } = string.Empty;
    public string IsolationMode { get; set; } = "Shared";
    public string? PrimaryDomain { get; set; }
    public string Locale { get; set; } = "en-US";
    public string CurrencyCode { get; set; } = "USD";
    public string TimeZoneId { get; set; } = "UTC";
    public DateTime? GoLiveDateUtc { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
