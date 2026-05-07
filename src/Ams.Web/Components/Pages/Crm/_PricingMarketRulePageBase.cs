using System.ComponentModel.DataAnnotations;

namespace Ams.Web.Components.Pages.Crm;

internal static class PricingMarketRulePageConstants
{
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly string[] LobCodes = ["Commercial", "Commercial Property", "Commercial Liability", "Workers Comp", "Commercial Auto", "Personal", "Homeowners", "Personal Auto", "Specialty", "Professional Liab"];
    public static readonly string[] RiskTiers = ["Preferred", "Standard", "NonStandard", "Legacy"];
    public static readonly string[] AppetiteLevels = ["Preferred", "Acceptable", "Avoid", "Declined"];
    public static readonly string[] DownloadFormats = ["IVANS", "AL3", "Custom"];

    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed class PriceClassFormModel
{
    [Required(ErrorMessage = "Class Code is required.")]
    [StringLength(50, ErrorMessage = "Class Code cannot exceed 50 characters.")]
    public string ClassCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Class Name is required.")]
    [StringLength(200, ErrorMessage = "Class Name cannot exceed 200 characters.")]
    public string ClassName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Line of Business is required.")]
    [StringLength(50, ErrorMessage = "Line of Business cannot exceed 50 characters.")]
    public string LobCode { get; set; } = "Commercial";

    [StringLength(50, ErrorMessage = "Risk Tier cannot exceed 50 characters.")]
    public string? RiskTierCode { get; set; }

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [Range(0, 1, ErrorMessage = "Base Rate must be between 0 and 1.")]
    public decimal BaseRate { get; set; }

    [Range(0, 999999, ErrorMessage = "Minimum Premium must be 0 or greater.")]
    public decimal? MinPremium { get; set; }

    [Range(0, 999999999, ErrorMessage = "Maximum Premium must be 0 or greater.")]
    public decimal? MaxPremium { get; set; }

    [Range(1, 999, ErrorMessage = "Priority must be between 1 and 999.")]
    public int Priority { get; set; } = 10;

    public bool IsActive { get; set; } = true;
}

internal sealed class MarketAppetiteFormModel
{
    [Required(ErrorMessage = "Carrier Name is required.")]
    [StringLength(200, ErrorMessage = "Carrier Name cannot exceed 200 characters.")]
    public string CarrierName { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "NAIC cannot exceed 20 characters.")]
    public string? CarrierNaic { get; set; }

    [Required(ErrorMessage = "Line of Business is required.")]
    [StringLength(50, ErrorMessage = "Line of Business cannot exceed 50 characters.")]
    public string LobCode { get; set; } = "Commercial";

    [Required(ErrorMessage = "Appetite Level is required.")]
    [StringLength(50, ErrorMessage = "Appetite Level cannot exceed 50 characters.")]
    public string AppetiteLevelCode { get; set; } = "Acceptable";

    [Range(0, 999999, ErrorMessage = "Minimum Premium must be 0 or greater.")]
    public decimal? MinPremium { get; set; }

    [Range(0, 999999999, ErrorMessage = "Maximum Premium must be 0 or greater.")]
    public decimal? MaxPremium { get; set; }

    [StringLength(10, ErrorMessage = "State Code cannot exceed 10 characters.")]
    public string? StateCode { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }

    [Range(1, 999, ErrorMessage = "Priority must be between 1 and 999.")]
    public int Priority { get; set; } = 10;

    public bool IsActive { get; set; } = true;
}

internal sealed class CarrierMappingFormModel
{
    [Required(ErrorMessage = "Carrier Name is required.")]
    [StringLength(200, ErrorMessage = "Carrier Name cannot exceed 200 characters.")]
    public string CarrierName { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "NAIC cannot exceed 20 characters.")]
    public string? CarrierNaic { get; set; }

    [StringLength(50, ErrorMessage = "Internal Code cannot exceed 50 characters.")]
    public string? InternalCode { get; set; }

    [StringLength(100, ErrorMessage = "External Code cannot exceed 100 characters.")]
    public string? ExternalCode { get; set; }

    [StringLength(50, ErrorMessage = "Line of Business cannot exceed 50 characters.")]
    public string? LobCode { get; set; }

    [Required(ErrorMessage = "Download Format is required.")]
    [StringLength(50, ErrorMessage = "Download Format cannot exceed 50 characters.")]
    public string DownloadFormatCode { get; set; } = "IVANS";

    [StringLength(100, ErrorMessage = "Integration Key cannot exceed 100 characters.")]
    public string? IntegrationKey { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
