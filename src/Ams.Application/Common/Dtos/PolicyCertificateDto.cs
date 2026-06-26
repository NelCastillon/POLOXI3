namespace Ams.Application.Common.Dtos;

public sealed class PolicyCertificateDto
{
    public Guid CertificateId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? PolicyId { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string HolderName { get; set; } = string.Empty;
    public string HolderAddress { get; set; } = string.Empty;
    public string CertificateType { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool AdditionalInsured { get; set; }
    public bool WaiverSubrogation { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? LastDeliveredDateUtc { get; set; }
    public DateTime? RevokedDateUtc { get; set; }
    public Guid? RevokedByUserId { get; set; }
    public string? RevokeReason { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
