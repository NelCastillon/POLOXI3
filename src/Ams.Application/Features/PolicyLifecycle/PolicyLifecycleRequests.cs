using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.PolicyLifecycle;

public sealed class CreatePolicyLifecycleTransactionRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid PolicyId { get; set; }

    public Guid? PolicyTermId { get; set; }
    public Guid? ParentPolicyTransactionId { get; set; }
    public Guid? SupersedesPolicyTransactionId { get; set; }

    [Required]
    [StringLength(80)]
    public string TransactionTypeCode { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string TransactionStatusCode { get; set; } = "Draft";

    [Required]
    public DateOnly EffectiveDate { get; set; }

    public DateOnly? ExpirationDate { get; set; }

    [Range(typeof(decimal), "-999999999999.99", "999999999999.99")]
    public decimal? PriorWrittenPremium { get; set; }

    [Range(typeof(decimal), "-999999999999.99", "999999999999.99")]
    public decimal? PremiumChange { get; set; }

    [Range(typeof(decimal), "-999999999999.99", "999999999999.99")]
    public decimal? NewWrittenPremium { get; set; }

    [Range(typeof(decimal), "-999999999999.99", "999999999999.99")]
    public decimal? TaxesChange { get; set; }

    [Range(typeof(decimal), "-999999999999.99", "999999999999.99")]
    public decimal? FeesChange { get; set; }

    [Range(typeof(decimal), "-999999999999.99", "999999999999.99")]
    public decimal? SurchargesChange { get; set; }

    [Range(typeof(decimal), "-999999999999.99", "999999999999.99")]
    public decimal? TotalCostChange { get; set; }

    [StringLength(80)]
    public string? ReasonCode { get; set; }

    [StringLength(80)]
    public string? SourceCode { get; set; }

    [StringLength(160)]
    public string? ExternalReference { get; set; }

    [StringLength(160)]
    public string? CarrierTransactionNumber { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public Guid? RequestedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public Guid? IssuedByUserId { get; set; }
    public List<PolicyLifecycleLineChangeRequest> LineChanges { get; set; } = [];
    public List<PolicyLifecycleDocumentLinkRequest> Documents { get; set; } = [];
}

public sealed class PolicyLifecycleLineChangeRequest
{
    public Guid? PolicyLineId { get; set; }
    public Guid? LineOfBusinessId { get; set; }

    [Required]
    [StringLength(80)]
    public string LineOfBusinessCode { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string LineOfBusinessName { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string ChangeTypeCode { get; set; } = "UpdateLine";

    [Range(typeof(decimal), "-999999999999.99", "999999999999.99")]
    public decimal? PriorPremium { get; set; }

    [Range(typeof(decimal), "-999999999999.99", "999999999999.99")]
    public decimal? PremiumChange { get; set; }

    [Range(typeof(decimal), "-999999999999.99", "999999999999.99")]
    public decimal? NewPremium { get; set; }

    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}

public sealed class PolicyLifecycleDocumentLinkRequest
{
    public Guid? DocumentId { get; set; }

    [Required]
    [StringLength(80)]
    public string DocumentRoleCode { get; set; } = string.Empty;

    [Required]
    [StringLength(240)]
    public string DocumentTitle { get; set; } = string.Empty;

    [StringLength(120)]
    public string? DocumentNumber { get; set; }

    [StringLength(260)]
    public string? FileName { get; set; }

    [StringLength(1000)]
    public string? StorageUri { get; set; }
}

public sealed class TransitionPolicyLifecycleTransactionRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(80)]
    public string ToStatusCode { get; set; } = string.Empty;

    [StringLength(80)]
    public string? ReasonCode { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public Guid? ChangedByUserId { get; set; }
}

public sealed class CreatePolicyServicingActivityRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid PolicyId { get; set; }

    public Guid? PolicyTransactionId { get; set; }

    [Required, StringLength(80)]
    public string ActivityTypeCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Notes { get; set; }

    [StringLength(80)]
    public string? ChannelCode { get; set; }

    [StringLength(80)]
    public string? OutcomeCode { get; set; }

    public DateTime? ActivityDateUtc { get; set; }
    public Guid? PerformedByUserId { get; set; }
}

public sealed class SendPolicyCommunicationRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid PolicyId { get; set; }

    public Guid? PolicyTransactionId { get; set; }

    [Required, StringLength(80)]
    public string ChannelCode { get; set; } = string.Empty;

    [Required, StringLength(254)]
    public string Recipient { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(8000)]
    public string Body { get; set; } = string.Empty;

    public Guid? SentByUserId { get; set; }
}
