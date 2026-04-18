namespace Ams.Domain.Entities;

public sealed class TenantBranding
{
    public Guid BrandingId { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public string? WhiteLabelName { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? FaviconUrl { get; private set; }
    public string PrimaryColor { get; private set; } = "#0d6efd";
    public string SecondaryColor { get; private set; } = "#6c757d";
    public string AccentColor { get; private set; } = "#198754";
    public string? CustomDomain { get; private set; }
    public string? CustomCssUrl { get; private set; }
    public string? SupportEmail { get; private set; }
    public string? SupportPhone { get; private set; }
    public string? FooterText { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ModifiedDateUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public bool IsDeleted { get; private set; }

    private TenantBranding() { }

    public TenantBranding(Guid tenantId, string? whiteLabelName, string primaryColor, string secondaryColor)
    {
        TenantId = tenantId;
        WhiteLabelName = whiteLabelName;
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
    }
}
