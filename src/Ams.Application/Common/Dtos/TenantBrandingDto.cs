namespace Ams.Application.Common.Dtos;

public sealed class TenantBrandingDto
{
    public Guid BrandingId { get; set; }
    public Guid TenantId { get; set; }
    public string? WhiteLabelName { get; set; }
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string PrimaryColor { get; set; } = "#0d6efd";
    public string SecondaryColor { get; set; } = "#6c757d";
    public string AccentColor { get; set; } = "#198754";
    public string? CustomDomain { get; set; }
    public string? CustomCssUrl { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }
    public string? FooterText { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
