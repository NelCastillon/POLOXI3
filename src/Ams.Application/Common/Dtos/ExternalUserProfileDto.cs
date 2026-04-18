namespace Ams.Application.Common.Dtos;

public sealed class ExternalUserProfileDto
{
    public Guid ExternalProfileId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string ExternalUserTypeCode { get; set; } = string.Empty;
    public string? OrganizationName { get; set; }
    public string? LicenseNumber { get; set; }
    public string? LicenseState { get; set; }
    public DateOnly? LicenseExpiryDate { get; set; }
    public string? NpnNumber { get; set; }
    public string? TaxId { get; set; }
    public bool PortalAccessEnabled { get; set; }
    public DateTime? PortalLastLoginDateUtc { get; set; }
    public string? SsoProvider { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
