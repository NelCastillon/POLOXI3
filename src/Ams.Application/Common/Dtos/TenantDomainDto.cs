namespace Ams.Application.Common.Dtos;

public sealed class TenantDomainDto
{
    public Guid TenantDomainId { get; set; }
    public Guid TenantId { get; set; }
    public string DomainName { get; set; } = "";
    public bool IsPrimary { get; set; }
    public string SslStatusCode { get; set; } = "None";
    public string VerificationStatusCode { get; set; } = "Pending";
    public string? VerificationToken { get; set; }
    public DateTime? VerifiedDateUtc { get; set; }
    public string? RedirectTarget { get; set; }
    public DateTime? SslExpiresDateUtc { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? Notes { get; set; }
    public string? TenantName { get; set; }
}
