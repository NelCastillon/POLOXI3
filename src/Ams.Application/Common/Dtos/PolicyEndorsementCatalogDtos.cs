namespace Ams.Application.Common.Dtos;

public sealed class PolicyEndorsementCatalogDto
{
    public IReadOnlyList<PolicyEndorsementTypeCatalogDto> Types { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementOptionDto> Options { get; set; } = [];
}

public sealed class PolicyEndorsementTypeCatalogDto
{
    public Guid EndorsementTypeId { get; set; }
    public Guid TenantId { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public PolicyEndorsementTypeProfileDto? Profile { get; set; }
    public IReadOnlyList<PolicyEndorsementTypeLineOfBusinessDto> LinesOfBusiness { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementTypeDocumentRequirementDto> DocumentRequirements { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementTypeWorkflowRuleDto> WorkflowRules { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementTypeCarrierMethodDto> CarrierMethods { get; set; } = [];
}

public sealed class PolicyEndorsementTypeProfileDto
{
    public Guid EndorsementTypeProfileId { get; set; }
    public Guid TenantId { get; set; }
    public Guid EndorsementTypeId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string DefaultOperationCode { get; set; } = string.Empty;
    public string PremiumImpactCode { get; set; } = string.Empty;
    public string BillingImpactCode { get; set; } = string.Empty;
    public string CommissionImpactCode { get; set; } = string.Empty;
    public string AuthorityCode { get; set; } = string.Empty;
    public string ApprovalLevelCode { get; set; } = string.Empty;
    public string CarrierMethodCode { get; set; } = string.Empty;
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
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class PolicyEndorsementTypeLineOfBusinessDto
{
    public Guid EndorsementTypeLineOfBusinessId { get; set; }
    public Guid TenantId { get; set; }
    public Guid EndorsementTypeId { get; set; }
    public string LineOfBusinessCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public sealed class PolicyEndorsementTypeDocumentRequirementDto
{
    public Guid EndorsementTypeDocumentRequirementId { get; set; }
    public Guid TenantId { get; set; }
    public Guid EndorsementTypeId { get; set; }
    public string RequirementCode { get; set; } = string.Empty;
    public string? DocumentGroupCode { get; set; }
    public string? DocumentKindCode { get; set; }
    public string? AcordFormNumber { get; set; }
    public bool IsRequired { get; set; }
    public string? AppliesWhenJson { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public sealed class PolicyEndorsementTypeWorkflowRuleDto
{
    public Guid EndorsementTypeWorkflowRuleId { get; set; }
    public Guid TenantId { get; set; }
    public Guid EndorsementTypeId { get; set; }
    public string FromStatusCode { get; set; } = string.Empty;
    public string ToStatusCode { get; set; } = string.Empty;
    public string? RequiredPermissionCode { get; set; }
    public bool RequiresApproval { get; set; }
    public bool RequiresCarrierDispatch { get; set; }
    public bool RequiresAccountingWork { get; set; }
    public bool RequiresCommissionWork { get; set; }
    public bool RequiresDocumentWork { get; set; }
    public bool RequiresCertificateReview { get; set; }
    public bool RequiresPolicyVersion { get; set; }
    public string? RuleJson { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public sealed class PolicyEndorsementTypeCarrierMethodDto
{
    public Guid EndorsementTypeCarrierMethodId { get; set; }
    public Guid TenantId { get; set; }
    public Guid EndorsementTypeId { get; set; }
    public Guid? CarrierId { get; set; }
    public string? LineOfBusinessCode { get; set; }
    public string CarrierMethodCode { get; set; } = string.Empty;
    public Guid? CarrierConfigurationId { get; set; }
    public string? PortalInstructions { get; set; }
    public string? EmailTemplateCode { get; set; }
    public string? PayloadTemplateCode { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
