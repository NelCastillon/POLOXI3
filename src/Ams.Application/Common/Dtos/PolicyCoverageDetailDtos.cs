namespace Ams.Application.Common.Dtos;

public sealed class PolicyCoverageDetailDto
{
    public Guid CoverageDetailId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public Guid? CoverageTypeId { get; set; }
    public string CoverageCode { get; set; } = string.Empty;
    public string CoverageName { get; set; } = string.Empty;
    public string LineOfBusinessCode { get; set; } = string.Empty;
    public string CoverageCategoryCode { get; set; } = string.Empty;
    public string CoverageFormCode { get; set; } = string.Empty;
    public string CoverageTriggerCode { get; set; } = string.Empty;
    public string ValuationBasisCode { get; set; } = string.Empty;
    public string TerritoryCode { get; set; } = string.Empty;
    public decimal? OccurrenceLimit { get; set; }
    public decimal? AggregateLimit { get; set; }
    public decimal? Sublimit { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Retention { get; set; }
    public decimal Premium { get; set; }
    public decimal? Rate { get; set; }
    public decimal? ExposureBase { get; set; }
    public string ExposureBasisCode { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string WritingCompanyName { get; set; } = string.Empty;
    public string UnderwriterName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public bool IsIncluded { get; set; }
    public bool IsAuditable { get; set; }
    public bool IsClaimsMade { get; set; }
    public bool RequiresSchedule { get; set; }
    public bool RequiresCertificateReview { get; set; }
    public string FormsAndEndorsements { get; set; } = string.Empty;
    public string CoinsuranceClause { get; set; } = string.Empty;
    public string BlanketOrSpecificCode { get; set; } = string.Empty;
    public string CoveredOperations { get; set; } = string.Empty;
    public string Exclusions { get; set; } = string.Empty;
    public string Conditions { get; set; } = string.Empty;
    public string RatingNotes { get; set; } = string.Empty;
    public string ServiceInstructions { get; set; } = string.Empty;
    public string AuditInstructions { get; set; } = string.Empty;
    public string CertificateInstructions { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public List<PolicyCoverageDetailFieldDto> Fields { get; set; } = [];
}

public sealed class PolicyCoverageDetailTemplateDto
{
    public Guid TemplateId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CoverageTypeId { get; set; }
    public string CoverageCode { get; set; } = string.Empty;
    public string CoverageName { get; set; } = string.Empty;
    public string LineOfBusinessCode { get; set; } = string.Empty;
    public string CoverageCategoryCode { get; set; } = string.Empty;
    public string CoverageFormCode { get; set; } = string.Empty;
    public string CoverageTriggerCode { get; set; } = string.Empty;
    public string ValuationBasisCode { get; set; } = string.Empty;
    public string TerritoryCode { get; set; } = string.Empty;
    public decimal? DefaultOccurrenceLimit { get; set; }
    public decimal? DefaultAggregateLimit { get; set; }
    public decimal? DefaultSublimit { get; set; }
    public decimal? DefaultDeductible { get; set; }
    public decimal? DefaultRetention { get; set; }
    public decimal DefaultPremium { get; set; }
    public decimal? DefaultRate { get; set; }
    public decimal? DefaultExposureBase { get; set; }
    public string ExposureBasisCode { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public string WritingCompanyName { get; set; } = string.Empty;
    public string UnderwriterName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public bool IsIncluded { get; set; }
    public bool IsAuditable { get; set; }
    public bool IsClaimsMade { get; set; }
    public bool RequiresSchedule { get; set; }
    public bool RequiresCertificateReview { get; set; }
    public string FormsAndEndorsements { get; set; } = string.Empty;
    public string CoinsuranceClause { get; set; } = string.Empty;
    public string BlanketOrSpecificCode { get; set; } = string.Empty;
    public string CoveredOperations { get; set; } = string.Empty;
    public string Exclusions { get; set; } = string.Empty;
    public string Conditions { get; set; } = string.Empty;
    public string RatingNotes { get; set; } = string.Empty;
    public string ServiceInstructions { get; set; } = string.Empty;
    public string AuditInstructions { get; set; } = string.Empty;
    public string CertificateInstructions { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<PolicyCoverageDetailTemplateFieldDto> Fields { get; set; } = [];
}

public sealed class PolicyCoverageDetailFieldDto
{
    public Guid FieldId { get; set; }
    public Guid CoverageDetailId { get; set; }
    public string FieldGroupCode { get; set; } = string.Empty;
    public string FieldCode { get; set; } = string.Empty;
    public string FieldLabel { get; set; } = string.Empty;
    public string FieldValue { get; set; } = string.Empty;
    public string FieldValueTypeCode { get; set; } = string.Empty;
    public string UnitOfMeasureCode { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsRatingField { get; set; }
    public bool IsScheduleField { get; set; }
    public int SortOrder { get; set; }
}

public sealed class PolicyCoverageDetailTemplateFieldDto
{
    public Guid TemplateFieldId { get; set; }
    public Guid TemplateId { get; set; }
    public string FieldGroupCode { get; set; } = string.Empty;
    public string FieldCode { get; set; } = string.Empty;
    public string FieldLabel { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;
    public string FieldValueTypeCode { get; set; } = string.Empty;
    public string UnitOfMeasureCode { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsRatingField { get; set; }
    public bool IsScheduleField { get; set; }
    public int SortOrder { get; set; }
}
