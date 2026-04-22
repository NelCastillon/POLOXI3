namespace Ams.Application.Common.Dtos;

public sealed class IntegrationCatalogDto
{
    public Guid IntegrationId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
