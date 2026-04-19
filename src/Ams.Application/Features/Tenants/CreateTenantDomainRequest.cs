namespace Ams.Application.Features.Tenants;

public sealed class CreateTenantDomainRequest
{
    public Guid TenantId { get; set; }
    public string DomainName { get; set; } = "";
    public bool IsPrimary { get; set; }
    public string? RedirectTarget { get; set; }
    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
