using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class SsoConfiguration : AuditableEntity
{
    public SsoProviderType ProviderType { get; private set; } = SsoProviderType.AzureAD;
    public string ProviderName { get; private set; } = string.Empty;
    public string? MetadataUrl { get; private set; }
    public string? ClientId { get; private set; }
    public string? ClientSecretHash { get; private set; }
    public string? TenantDomain { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool RequireSso { get; private set; }
    public bool AllowLocalLogin { get; private set; } = true;
    public string? SsoAttributeMap { get; private set; }

    private SsoConfiguration() { }

    public SsoConfiguration(Guid tenantId, SsoProviderType providerType, string providerName, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        ProviderType = providerType;
        ProviderName = providerName;
    }
}
