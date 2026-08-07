using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.SearchMatching;

public sealed class EntityMatchRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required, StringLength(120)]
    public string ProfileCode { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string EntityTypeCode { get; set; } = string.Empty;

    public Guid? SourceEntityId { get; set; }

    [Required, StringLength(200)]
    public string CorrelationId { get; set; } = string.Empty;

    public Guid? RequestedByUserId { get; set; }

    [Required, MinLength(1)]
    public IReadOnlyDictionary<string, string?> Fields { get; set; } = new Dictionary<string, string?>();
}

public static class MatchProfileCodes
{
    public const string GlobalEnterpriseSearch = "GLOBAL_ENTERPRISE_SEARCH";
    public const string LeadDuplicate = "LEAD_DUPLICATE";
    public const string AccountDuplicate = "ACCOUNT_DUPLICATE";
    public const string ContactDuplicate = "CONTACT_DUPLICATE";
    public const string SubmissionEntity = "SUBMISSION_ENTITY";
    public const string PolicyReconciliation = "POLICY_RECONCILIATION";
    public const string ClaimReconciliation = "CLAIM_RECONCILIATION";
    public const string DocumentRouting = "DOCUMENT_ROUTING";
    public const string AccountingReconciliation = "ACCOUNTING_RECONCILIATION";
    public const string CertificateParty = "CERTIFICATE_PARTY";
    public const string CarrierNormalization = "CARRIER_NORMALIZATION";
    public const string LocationMatch = "LOCATION_MATCH";
    public const string VehicleMatch = "VEHICLE_MATCH";
    public const string ClaimPartyMatch = "CLAIM_PARTY_MATCH";
    public const string CommissionLineReconciliation = "COMMISSION_LINE_RECONCILIATION";
}

public sealed class ModuleMatchRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required, StringLength(120)]
    public string ProfileCode { get; set; } = string.Empty;

    public Guid? SourceEntityId { get; set; }

    [Required, StringLength(200)]
    public string CorrelationId { get; set; } = string.Empty;

    public Guid? RequestedByUserId { get; set; }

    [Required, MinLength(1)]
    public IReadOnlyDictionary<string, string?> Fields { get; set; } = new Dictionary<string, string?>();
}

public sealed class EnterpriseFuzzySearchRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required, StringLength(500, MinimumLength = 2)]
    public string Query { get; set; } = string.Empty;

    public IReadOnlyCollection<string> EntityTypeCodes { get; set; } = [];

    public IReadOnlyCollection<string> GrantedPermissions { get; set; } = [];

    [Range(1, 100)]
    public int MaximumResults { get; set; } = 25;

    public Guid? RequestedByUserId { get; set; }

    [StringLength(200)]
    public string? CorrelationId { get; set; }
}

public sealed record MatchReason(
    string FieldCode,
    string AlgorithmCode,
    decimal SimilarityScore,
    decimal WeightedScore,
    string ReasonCode,
    string Explanation,
    bool IsExactMatch,
    bool IsDiscrepancy);

public sealed record MatchCandidate(
    Guid EntityId,
    string DisplayName,
    string? SecondaryText,
    string? NavigationRoute,
    decimal OverallScore,
    string ConfidenceBandCode,
    IReadOnlyList<MatchReason> Reasons,
    bool IsExactMatch,
    bool RequiresReview);

public sealed record EntityMatchResult(
    Guid MatchExecutionId,
    string ProfileCode,
    decimal ExactThreshold,
    decimal StrongThreshold,
    decimal PossibleThreshold,
    IReadOnlyList<MatchCandidate> Candidates);

public sealed record SearchMatchResult(
    Guid EntityId,
    string EntityTypeCode,
    string DisplayName,
    string? SecondaryText,
    string? NavigationRoute,
    decimal Score,
    IReadOnlyList<MatchReason> Reasons);

public static class MatchReviewDecisionCodes
{
    public const string UseExisting = "USE_EXISTING";
    public const string CreateNew = "CREATE_NEW";
    public const string Compare = "COMPARE";
    public const string MergeRequest = "MERGE_REQUEST";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { UseExisting, CreateNew, Compare, MergeRequest };
}

public sealed class MatchReviewDecisionRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required]
    public Guid MatchExecutionId { get; set; }
    public Guid? CandidateEntityId { get; set; }
    [Required, StringLength(40)]
    public string DecisionCode { get; set; } = string.Empty;
    [StringLength(2000)]
    public string? Notes { get; set; }
    public Guid RequestedByUserId { get; set; }
    [Required, StringLength(200)]
    public string CorrelationId { get; set; } = string.Empty;
}

public sealed record MatchReviewDecision(
    Guid MatchReviewDecisionId,
    Guid MatchExecutionId,
    Guid? CandidateEntityId,
    string DecisionCode,
    string? Notes,
    Guid RequestedByUserId,
    string CorrelationId,
    DateTime CreatedDateUtc);

public sealed record MatchPolicy(
    Guid MatchProfileId,
    string ProfileCode,
    string EntityTypeCode,
    decimal ExactThreshold,
    decimal StrongThreshold,
    decimal PossibleThreshold,
    int MaximumCandidates,
    int SemanticMaximumConcepts,
    bool RequiresReview,
    IReadOnlyList<MatchFieldPolicy> Fields,
    IReadOnlyList<NormalizationTermPolicy> NormalizationTerms);

public sealed record MatchFieldPolicy(
    Guid MatchFieldRuleId,
    string FieldCode,
    string DisplayName,
    string AlgorithmCode,
    decimal Weight,
    decimal MinimumSimilarity,
    bool IsRequired,
    bool IsCriticalIdentifier,
    bool ExactMatchOnly,
    bool IsSensitive);

public sealed record NormalizationTermPolicy(
    string EntityTypeCode,
    string FieldCode,
    string SourceValue,
    string NormalizedValue,
    string TermKindCode);

public sealed record MatchProjection(
    Guid EntityProjectionId,
    Guid EntityId,
    string EntityTypeCode,
    string DisplayName,
    string? SecondaryText,
    string? NavigationRoute,
    string PermissionCode,
    IReadOnlyDictionary<string, string?> Fields);

public sealed record SearchMatchingAdministration(
    IReadOnlyList<MatchProfileSetting> Profiles,
    IReadOnlyList<MatchAlgorithmSetting> Algorithms,
    IReadOnlyList<NormalizationTermSetting> NormalizationTerms,
    IReadOnlyList<SearchCapabilitySetting> Capabilities,
    SearchMatchingOperationalTelemetry Telemetry);

public sealed record MatchProfileSetting(Guid MatchProfileId, bool IsInherited, string ProfileCode, string EntityTypeCode, string DisplayName, string? Description, decimal ExactThreshold, decimal StrongThreshold, decimal PossibleThreshold, int MaximumCandidates, int SemanticMaximumConcepts, bool RequiresReview, bool IsActive, byte[] RowVersion, IReadOnlyList<MatchFieldRuleSetting> FieldRules);
public sealed record MatchFieldRuleSetting(Guid MatchFieldRuleId, bool IsInherited, Guid MatchProfileId, string FieldCode, string DisplayName, Guid MatchAlgorithmId, string AlgorithmCode, decimal Weight, decimal MinimumSimilarity, bool IsRequired, bool IsCriticalIdentifier, bool ExactMatchOnly, bool IsSensitive, int SortOrder, bool IsActive, byte[] RowVersion);
public sealed record MatchAlgorithmSetting(Guid MatchAlgorithmId, bool IsInherited, string AlgorithmCode, string DisplayName, string AlgorithmKindCode, string? Description, string ConfigurationJson, bool IsActive, byte[] RowVersion);
public sealed record NormalizationTermSetting(Guid NormalizationTermId, bool IsInherited, string EntityTypeCode, string FieldCode, string SourceValue, string NormalizedValue, string TermKindCode, string? CultureCode, int SortOrder, bool IsActive, byte[] RowVersion);
public sealed record SearchCapabilitySetting(string CapabilityCode, string DisplayName, bool IsAvailable, bool IsEnabled, string ConfigurationJson, DateTime? LastVerifiedDateUtc, string? LastError);
public sealed record SemanticPreprocessingSettings(int MaximumTokens, int MaximumPhraseLength, int MaximumPhrases);
public sealed record SearchMatchingOperationalTelemetry(long ExecutionCount, long CompletedExecutionCount, long FailedExecutionCount, long SemanticEvidenceCount, long ReviewDecisionCount, long OpenDuplicateGroupCount, DateTime? LastExecutionDateUtc, DateTime? LastSemanticEvidenceDateUtc, DateTime? LastReviewDecisionDateUtc);

public sealed class SaveMatchProfileSettingRequest : IValidatableObject
{
    public Guid? MatchProfileId { get; set; }
    [Required, StringLength(120)] public string ProfileCode { get; set; } = string.Empty;
    [Required, StringLength(80)] public string EntityTypeCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string DisplayName { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Range(0, 100)] public decimal ExactThreshold { get; set; }
    [Range(0, 100)] public decimal StrongThreshold { get; set; }
    [Range(0, 100)] public decimal PossibleThreshold { get; set; }
    [Range(1, 500)] public int MaximumCandidates { get; set; }
    [Range(1, 50)] public int SemanticMaximumConcepts { get; set; } = 12;
    public bool AllowAutomaticLink => false;
    public bool RequiresReview { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StrongThreshold > ExactThreshold) yield return new("Strong threshold cannot exceed exact threshold.", [nameof(StrongThreshold)]);
        if (PossibleThreshold > StrongThreshold) yield return new("Possible threshold cannot exceed strong threshold.", [nameof(PossibleThreshold)]);
    }
}

public sealed class SaveMatchFieldRuleSettingRequest
{
    public Guid? MatchFieldRuleId { get; set; }
    [Required] public Guid MatchProfileId { get; set; }
    [Required, StringLength(100)] public string FieldCode { get; set; } = string.Empty;
    [Required, StringLength(160)] public string DisplayName { get; set; } = string.Empty;
    [Required] public Guid MatchAlgorithmId { get; set; }
    [Range(typeof(decimal), "0.0001", "100")] public decimal Weight { get; set; }
    [Range(0, 100)] public decimal MinimumSimilarity { get; set; }
    public bool IsRequired { get; set; }
    public bool IsCriticalIdentifier { get; set; }
    public bool ExactMatchOnly { get; set; }
    public bool IsSensitive { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }
}

public sealed class SaveMatchAlgorithmSettingRequest
{
    public Guid? MatchAlgorithmId { get; set; }
    [Required, RegularExpression("EXACT|NORMALIZED_EXACT|SOUNDEX|DAMERAU_LEVENSHTEIN|TOKEN_JACCARD|SEMANTIC_ADVISORY")] public string AlgorithmCode { get; set; } = string.Empty;
    [Required, StringLength(160)] public string DisplayName { get; set; } = string.Empty;
    [Required, RegularExpression("EXACT|NORMALIZED|PHONETIC|EDIT_DISTANCE|FUZZY|SEMANTIC")] public string AlgorithmKindCode { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Required] public string ConfigurationJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }
}

public sealed class SaveNormalizationTermSettingRequest
{
    public Guid? NormalizationTermId { get; set; }
    [Required, StringLength(80)] public string EntityTypeCode { get; set; } = string.Empty;
    [Required, StringLength(100)] public string FieldCode { get; set; } = string.Empty;
    [Required, StringLength(300)] public string SourceValue { get; set; } = string.Empty;
    [StringLength(300)] public string NormalizedValue { get; set; } = string.Empty;
    [Required, RegularExpression("STOP_WORD|REPLACEMENT|ABBREVIATION|SYNONYM")] public string TermKindCode { get; set; } = "SYNONYM";
    [StringLength(20)] public string? CultureCode { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[]? RowVersion { get; set; }
}
