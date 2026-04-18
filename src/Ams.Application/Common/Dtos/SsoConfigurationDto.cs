namespace Ams.Application.Common.Dtos;

public sealed class SsoConfigurationDto
{
    public Guid SsoConfigId { get; set; }
    public Guid TenantId { get; set; }
    public string ProviderTypeCode { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string? MetadataUrl { get; set; }
    public string? ClientId { get; set; }
    public string? TenantDomain { get; set; }
    public bool IsEnabled { get; set; }
    public bool RequireSso { get; set; }
    public bool AllowLocalLogin { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
