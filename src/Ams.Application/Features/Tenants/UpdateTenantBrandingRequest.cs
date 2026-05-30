namespace Ams.Application.Features.Tenants;

public sealed class UpdateTenantBrandingRequest
{
    public string? WhiteLabelName { get; set; }
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string? CustomDomain { get; set; }
    public string? CustomCssUrl { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }
    public string? FooterText { get; set; }
}
