using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Common.Dtos;

public sealed class PriceClassDto
{
    public Guid PriceClassId { get; set; }
    public Guid TenantId { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string LobCode { get; set; } = string.Empty;
    public string? RiskTierCode { get; set; }
    public string? Description { get; set; }
    public decimal BaseRate { get; set; }
    public decimal? MinPremium { get; set; }
    public decimal? MaxPremium { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class MarketAppetiteDto
{
    public Guid MarketAppetiteId { get; set; }
    public Guid TenantId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string? CarrierNaic { get; set; }
    public string LobCode { get; set; } = string.Empty;
    public string AppetiteLevelCode { get; set; } = "Acceptable";
    public decimal? MinPremium { get; set; }
    public decimal? MaxPremium { get; set; }
    public string? StateCode { get; set; }
    public string? Notes { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class CarrierMappingDto
{
    public Guid CarrierMappingId { get; set; }
    public Guid TenantId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string? CarrierNaic { get; set; }
    public string? InternalCode { get; set; }
    public string? ExternalCode { get; set; }
    public string? LobCode { get; set; }
    public string DownloadFormatCode { get; set; } = "IVANS";
    public string? IntegrationKey { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastTestedDateUtc { get; set; }
    public string? LastTestStatusCode { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class UpsertPriceClassRequest
{
    public Guid TenantId { get; set; }
    [Required, StringLength(50)] public string ClassCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string ClassName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string LobCode { get; set; } = string.Empty;
    [StringLength(50)] public string? RiskTierCode { get; set; }
    [StringLength(500)] public string? Description { get; set; }
    [Range(0, 1)] public decimal BaseRate { get; set; }
    [Range(0, 999999)] public decimal? MinPremium { get; set; }
    [Range(0, 999999999)] public decimal? MaxPremium { get; set; }
    [Range(1, 999)] public int Priority { get; set; } = 10;
    public bool IsActive { get; set; } = true;
    public Guid? UserId { get; set; }
}

public sealed class UpsertMarketAppetiteRequest
{
    public Guid TenantId { get; set; }
    [Required, StringLength(200)] public string CarrierName { get; set; } = string.Empty;
    [StringLength(20)] public string? CarrierNaic { get; set; }
    [Required, StringLength(50)] public string LobCode { get; set; } = string.Empty;
    [Required, StringLength(50)] public string AppetiteLevelCode { get; set; } = "Acceptable";
    [Range(0, 999999)] public decimal? MinPremium { get; set; }
    [Range(0, 999999999)] public decimal? MaxPremium { get; set; }
    [StringLength(10)] public string? StateCode { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    [Range(1, 999)] public int Priority { get; set; } = 10;
    public bool IsActive { get; set; } = true;
    public Guid? UserId { get; set; }
}

public sealed class UpsertCarrierMappingRequest
{
    public Guid TenantId { get; set; }
    [Required, StringLength(200)] public string CarrierName { get; set; } = string.Empty;
    [StringLength(20)] public string? CarrierNaic { get; set; }
    [StringLength(50)] public string? InternalCode { get; set; }
    [StringLength(100)] public string? ExternalCode { get; set; }
    [StringLength(50)] public string? LobCode { get; set; }
    [Required, StringLength(50)] public string DownloadFormatCode { get; set; } = "IVANS";
    [StringLength(100)] public string? IntegrationKey { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? UserId { get; set; }
}
