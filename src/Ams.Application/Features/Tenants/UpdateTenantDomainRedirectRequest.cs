namespace Ams.Application.Features.Tenants;

public sealed class UpdateTenantDomainRedirectRequest
{
    public string? RedirectTarget { get; set; }
    public string? Notes { get; set; }
}
