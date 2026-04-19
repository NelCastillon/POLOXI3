namespace Ams.Application.Common.Dtos;

public sealed class TenantDto
{
    public Guid TenantId { get; set; }
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = "Active";
    public string PlanCode { get; set; } = string.Empty;
    public string RegionCode { get; set; } = string.Empty;
    public string IsolationMode { get; set; } = string.Empty;
    public string? PrimaryDomain { get; set; }
    public int ActiveUsers { get; set; }
    public bool IsActive { get; set; }
    public string Locale { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? GoLiveDateUtc { get; set; }
}
