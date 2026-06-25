using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.PolicyCoverages;

public sealed class CreatePolicyCoverageDetailRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid PolicyId { get; set; }

    [Required, StringLength(80)]
    public string PolicyNumber { get; set; } = string.Empty;

    public Guid? CoverageTypeId { get; set; }

    [Required, StringLength(50)]
    public string CoverageCode { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string CoverageName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LineOfBusinessCode { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string CoverageCategoryCode { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string CoverageFormCode { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string CoverageTriggerCode { get; set; } = string.Empty;

    [StringLength(50)]
    public string ValuationBasisCode { get; set; } = string.Empty;

    [StringLength(50)]
    public string TerritoryCode { get; set; } = string.Empty;

    [Range(0, 999999999)]
    public decimal? OccurrenceLimit { get; set; }

    [Range(0, 999999999)]
    public decimal? AggregateLimit { get; set; }

    [Range(0, 999999999)]
    public decimal? Sublimit { get; set; }

    [Range(0, 999999999)]
    public decimal? Deductible { get; set; }

    [Range(0, 999999999)]
    public decimal? Retention { get; set; }

    [Range(-999999999, 999999999)]
    public decimal Premium { get; set; }

    [Range(0, 999999999)]
    public decimal? Rate { get; set; }

    [Range(0, 999999999)]
    public decimal? ExposureBase { get; set; }

    [StringLength(50)]
    public string ExposureBasisCode { get; set; } = string.Empty;

    [Required]
    public DateTime EffectiveDate { get; set; }

    [Required]
    public DateTime ExpirationDate { get; set; }

    [Required, StringLength(120)]
    public string CarrierName { get; set; } = string.Empty;

    [StringLength(120)]
    public string WritingCompanyName { get; set; } = string.Empty;

    [StringLength(120)]
    public string UnderwriterName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string StatusCode { get; set; } = string.Empty;

    public bool IsIncluded { get; set; }
    public bool IsAuditable { get; set; }
    public bool IsClaimsMade { get; set; }
    public bool RequiresSchedule { get; set; }
    public bool RequiresCertificateReview { get; set; }

    [StringLength(1000)]
    public string FormsAndEndorsements { get; set; } = string.Empty;

    [StringLength(1000)]
    public string CoinsuranceClause { get; set; } = string.Empty;

    [StringLength(50)]
    public string BlanketOrSpecificCode { get; set; } = string.Empty;

    [StringLength(2000)]
    public string CoveredOperations { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Exclusions { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Conditions { get; set; } = string.Empty;

    [StringLength(2000)]
    public string RatingNotes { get; set; } = string.Empty;

    [StringLength(2000)]
    public string ServiceInstructions { get; set; } = string.Empty;

    [StringLength(2000)]
    public string AuditInstructions { get; set; } = string.Empty;

    [StringLength(2000)]
    public string CertificateInstructions { get; set; } = string.Empty;

    public Guid? CreatedByUserId { get; set; }
    public List<CreatePolicyCoverageFieldRequest> Fields { get; set; } = [];
}

public sealed class UpdatePolicyCoverageDetailRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid CoverageDetailId { get; set; }

    [Required, StringLength(80)]
    public string CoverageName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string CoverageCategoryCode { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string CoverageFormCode { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string CoverageTriggerCode { get; set; } = string.Empty;

    [StringLength(50)]
    public string ValuationBasisCode { get; set; } = string.Empty;

    [StringLength(50)]
    public string TerritoryCode { get; set; } = string.Empty;

    [Range(0, 999999999)]
    public decimal? OccurrenceLimit { get; set; }

    [Range(0, 999999999)]
    public decimal? AggregateLimit { get; set; }

    [Range(0, 999999999)]
    public decimal? Sublimit { get; set; }

    [Range(0, 999999999)]
    public decimal? Deductible { get; set; }

    [Range(0, 999999999)]
    public decimal? Retention { get; set; }

    [Range(-999999999, 999999999)]
    public decimal Premium { get; set; }

    [Range(0, 999999999)]
    public decimal? Rate { get; set; }

    [Range(0, 999999999)]
    public decimal? ExposureBase { get; set; }

    [StringLength(50)]
    public string ExposureBasisCode { get; set; } = string.Empty;

    [Required]
    public DateTime EffectiveDate { get; set; }

    [Required]
    public DateTime ExpirationDate { get; set; }

    [Required, StringLength(120)]
    public string CarrierName { get; set; } = string.Empty;

    [StringLength(120)]
    public string WritingCompanyName { get; set; } = string.Empty;

    [StringLength(120)]
    public string UnderwriterName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string StatusCode { get; set; } = string.Empty;

    public bool IsIncluded { get; set; }
    public bool IsAuditable { get; set; }
    public bool IsClaimsMade { get; set; }
    public bool RequiresSchedule { get; set; }
    public bool RequiresCertificateReview { get; set; }

    [StringLength(1000)]
    public string FormsAndEndorsements { get; set; } = string.Empty;

    [StringLength(1000)]
    public string CoinsuranceClause { get; set; } = string.Empty;

    [StringLength(50)]
    public string BlanketOrSpecificCode { get; set; } = string.Empty;

    [StringLength(2000)]
    public string CoveredOperations { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Exclusions { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Conditions { get; set; } = string.Empty;

    [StringLength(2000)]
    public string RatingNotes { get; set; } = string.Empty;

    [StringLength(2000)]
    public string ServiceInstructions { get; set; } = string.Empty;

    [StringLength(2000)]
    public string AuditInstructions { get; set; } = string.Empty;

    [StringLength(2000)]
    public string CertificateInstructions { get; set; } = string.Empty;

    public Guid? ModifiedByUserId { get; set; }
}

public sealed class DeletePolicyCoverageDetailRequest
{
    [Required]
    public Guid TenantId { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}

public sealed class CreatePolicyCoverageFieldRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid CoverageDetailId { get; set; }

    [Required, StringLength(50)]
    public string FieldGroupCode { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string FieldCode { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string FieldLabel { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string FieldValue { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string FieldValueTypeCode { get; set; } = "Text";

    [StringLength(40)]
    public string UnitOfMeasureCode { get; set; } = string.Empty;

    public bool IsRequired { get; set; }
    public bool IsRatingField { get; set; }
    public bool IsScheduleField { get; set; }

    [Range(0, 10000)]
    public int SortOrder { get; set; }

    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdatePolicyCoverageFieldRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid FieldId { get; set; }

    [Required, StringLength(50)]
    public string FieldGroupCode { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string FieldCode { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string FieldLabel { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string FieldValue { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string FieldValueTypeCode { get; set; } = "Text";

    [StringLength(40)]
    public string UnitOfMeasureCode { get; set; } = string.Empty;

    public bool IsRequired { get; set; }
    public bool IsRatingField { get; set; }
    public bool IsScheduleField { get; set; }

    [Range(0, 10000)]
    public int SortOrder { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
