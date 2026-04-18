using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class ExternalUserProfile : AuditableEntity
{
    public Guid UserId { get; private set; }
    public ExternalUserType ExternalUserType { get; private set; } = ExternalUserType.Client;
    public string? OrganizationName { get; private set; }
    public string? LicenseNumber { get; private set; }
    public string? LicenseState { get; private set; }
    public DateOnly? LicenseExpiryDate { get; private set; }
    public string? NpnNumber { get; private set; }
    public string? TaxId { get; private set; }
    public bool PortalAccessEnabled { get; private set; }
    public DateTime? PortalLastLoginDateUtc { get; private set; }
    public string? SsoSubjectId { get; private set; }
    public string? SsoProvider { get; private set; }

    private ExternalUserProfile() { }

    public ExternalUserProfile(Guid tenantId, Guid userId, ExternalUserType externalUserType, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        UserId = userId;
        ExternalUserType = externalUserType;
    }
}
