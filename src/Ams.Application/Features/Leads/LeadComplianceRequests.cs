using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Validation;

namespace Ams.Application.Features.Leads;

public sealed class CreatePhoneSuppressionRequest
{
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? LeadContactId { get; set; }

    [Required, StringLength(50), AmsPhone]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string SourceCode { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string ReasonCode { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string ChannelCode { get; set; } = "Call";

    [StringLength(50)]
    public string? PurposeCode { get; set; }

    [StringLength(20)]
    public string? JurisdictionCode { get; set; }

    public DateTime EffectiveDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpirationDateUtc { get; set; }
    public DateTime? RequestedDateUtc { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(500)]
    public string? EvidenceReference { get; set; }

    public Guid? CreatedByUserId { get; set; }
}

public sealed class RevokePhoneSuppressionRequest
{
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid PhoneSuppressionId { get; set; }

    [Required, StringLength(500)]
    public string RevocationReason { get; set; } = string.Empty;

    public Guid? RevokedByUserId { get; set; }
}

public sealed class CreatePhoneConsentRequest
{
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? LeadContactId { get; set; }

    [Required, StringLength(50), AmsPhone]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string ConsentTypeCode { get; set; } = "Express";

    [Required, StringLength(50)]
    public string ChannelCode { get; set; } = "Call";

    [Required, StringLength(50)]
    public string PurposeCode { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LegalBasisCode { get; set; } = string.Empty;

    public DateTime CapturedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime EffectiveDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpirationDateUtc { get; set; }

    [Required, StringLength(50)]
    public string EvidenceTypeCode { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string EvidenceReference { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? ConsentText { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class RevokePhoneConsentRequest
{
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid PhoneConsentId { get; set; }

    [Required, StringLength(500)]
    public string RevocationReason { get; set; } = string.Empty;

    public Guid? RevokedByUserId { get; set; }
}

public sealed class EvaluatePhoneContactRequest
{
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? LeadContactId { get; set; }

    [Required, StringLength(50), AmsPhone]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string ChannelCode { get; set; } = "Call";

    [Required, StringLength(50)]
    public string PurposeCode { get; set; } = "Marketing";

    [StringLength(100)]
    public string? CorrelationId { get; set; }

    public Guid? EvaluatedByUserId { get; set; }
}

public sealed class RecordPhoneScreeningRequest
{
    public Guid TenantId { get; set; }
    public Guid PhoneComplianceProfileId { get; set; }
    public Guid? PhoneScreeningBatchId { get; set; }

    [Required, StringLength(50)]
    public string ProviderCode { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string RegistryCode { get; set; } = string.Empty;

    [StringLength(20)]
    public string? JurisdictionCode { get; set; }

    [Required, StringLength(50)]
    public string ResultCode { get; set; } = string.Empty;

    public DateTime ScreenedDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ValidThroughDateUtc { get; set; }

    [StringLength(200)]
    public string? ProviderReference { get; set; }

    [StringLength(128)]
    public string? RawResponseHash { get; set; }

    [StringLength(1000)]
    public string? ErrorDetails { get; set; }

    public Guid? CreatedByUserId { get; set; }
}
