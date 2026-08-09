using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.PolicyEndorsements;

public sealed class UpdatePolicyEndorsementTypeProfileRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required, StringLength(50)]
    public string CategoryCode { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string DefaultOperationCode { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string PremiumImpactCode { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string BillingImpactCode { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string CommissionImpactCode { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string AuthorityCode { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string ApprovalLevelCode { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string CarrierMethodCode { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string DocumentDeliveryCode { get; set; } = string.Empty;
    public bool RequiresCarrierApproval { get; set; }
    public bool RequiresUnderwritingReview { get; set; }
    public bool RequiresSignedRequest { get; set; }
    public bool RequiresClientAuthorization { get; set; }
    public bool RequiresCertificateReview { get; set; }
    public bool RequiresBrokerOfRecord { get; set; }
    public bool RequiresAccountingWork { get; set; }
    public bool RequiresCommissionWork { get; set; }
    public bool RequiresDocumentWork { get; set; }
    public bool RequiresPolicyVersion { get; set; }
    public bool SupportsBackdate { get; set; }
    public bool SupportsReversal { get; set; }
    public bool IsHighRisk { get; set; }
    public bool IsPremiumBearing { get; set; }
    public bool IsCertificateRelated { get; set; }
    public bool IsActive { get; set; } = true;
    [Range(0, 100000)]
    public int SortOrder { get; set; }
    [Required, MinLength(1)]
    public byte[] RowVersion { get; set; } = [];
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class ReplacePolicyEndorsementTypeConfigurationRequest
{
    [Required]
    public Guid TenantId { get; set; }
    public List<PolicyEndorsementTypeLineOfBusinessInput> LinesOfBusiness { get; set; } = [];
    public List<PolicyEndorsementTypeDocumentRequirementInput> DocumentRequirements { get; set; } = [];
    public List<PolicyEndorsementTypeWorkflowRuleInput> WorkflowRules { get; set; } = [];
    public List<PolicyEndorsementTypeCarrierMethodInput> CarrierMethods { get; set; } = [];
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class PolicyEndorsementTypeLineOfBusinessInput
{
    [Required, StringLength(100)]
    public string LineOfBusinessCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    [Range(0, 100000)]
    public int SortOrder { get; set; }
}

public sealed class PolicyEndorsementTypeDocumentRequirementInput
{
    [Required, StringLength(80)]
    public string RequirementCode { get; set; } = string.Empty;
    [StringLength(80)]
    public string? DocumentGroupCode { get; set; }
    [StringLength(80)]
    public string? DocumentKindCode { get; set; }
    [StringLength(50)]
    public string? AcordFormNumber { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? AppliesWhenJson { get; set; }
    public bool IsActive { get; set; } = true;
    [Range(0, 100000)]
    public int SortOrder { get; set; }
}

public sealed class PolicyEndorsementTypeWorkflowRuleInput
{
    [Required, StringLength(80)]
    public string FromStatusCode { get; set; } = string.Empty;
    [Required, StringLength(80)]
    public string ToStatusCode { get; set; } = string.Empty;
    [StringLength(100)]
    public string? RequiredPermissionCode { get; set; }
    public bool RequiresApproval { get; set; }
    public bool RequiresCarrierDispatch { get; set; }
    public bool RequiresAccountingWork { get; set; }
    public bool RequiresCommissionWork { get; set; }
    public bool RequiresDocumentWork { get; set; }
    public bool RequiresCertificateReview { get; set; }
    public bool RequiresPolicyVersion { get; set; }
    public string? RuleJson { get; set; }
    public bool IsActive { get; set; } = true;
    [Range(0, 100000)]
    public int SortOrder { get; set; }
}

public sealed class PolicyEndorsementTypeCarrierMethodInput
{
    public Guid? CarrierId { get; set; }
    [StringLength(100)]
    public string? LineOfBusinessCode { get; set; }
    [Required, StringLength(50)]
    public string CarrierMethodCode { get; set; } = string.Empty;
    public Guid? CarrierConfigurationId { get; set; }
    [StringLength(2000)]
    public string? PortalInstructions { get; set; }
    [StringLength(100)]
    public string? EmailTemplateCode { get; set; }
    [StringLength(100)]
    public string? PayloadTemplateCode { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    [Range(0, 100000)]
    public int SortOrder { get; set; }
}
