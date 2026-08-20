using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.CrmConfig;

public sealed record CreateLeadSourceRequest(
    Guid TenantId,
    [Required, StringLength(80)] string SourceCode,
    [Required, StringLength(200)] string SourceName);

public sealed record UpdateLeadSourceRequest(
    [Required, StringLength(80)] string SourceCode,
    [Required, StringLength(200)] string SourceName,
    bool IsActive);

public sealed record CreateLeadStatusRequest(
    Guid TenantId,
    [Required, StringLength(80)] string StatusCode,
    [Required, StringLength(200)] string StatusName,
    [StringLength(50)] string? StatusType,
    [StringLength(500)] string? Description,
    [StringLength(20)] string? ColorHex,
    bool IsDefault,
    [Range(0, int.MaxValue)] int SortOrder);

public sealed record UpdateLeadStatusRequest(
    [Required, StringLength(80)] string StatusCode,
    [Required, StringLength(200)] string StatusName,
    [StringLength(50)] string? StatusType,
    [StringLength(500)] string? Description,
    [StringLength(20)] string? ColorHex,
    bool IsDefault,
    bool IsActive,
    [Range(0, int.MaxValue)] int SortOrder);

public sealed record CreateOpportunityStageRequest(
    Guid TenantId,
    [Required, StringLength(80)] string StageCode,
    [Required, StringLength(200)] string StageName,
    [Range(0, int.MaxValue)] int SortOrder,
    [Range(0, 100)] int ProbabilityPercent,
    bool IsClosedStage,
    bool IsWonStage);

public sealed record UpdateOpportunityStageRequest(
    [Required, StringLength(80)] string StageCode,
    [Required, StringLength(200)] string StageName,
    [Range(0, int.MaxValue)] int SortOrder,
    [Range(0, 100)] int ProbabilityPercent,
    bool IsClosedStage,
    bool IsWonStage,
    bool IsActive);

public sealed record CreateOpportunityForecastCategoryRequest(
    Guid TenantId,
    [Required, StringLength(80)] string CategoryCode,
    [Required, StringLength(100)] string CategoryName,
    [Range(0, int.MaxValue)] int SortOrder,
    [Range(0, 100)] decimal? DefaultProbabilityPercent,
    bool IsClosedCategory,
    bool IsDefault);

public sealed record UpdateOpportunityForecastCategoryRequest(
    [Required, StringLength(80)] string CategoryCode,
    [Required, StringLength(100)] string CategoryName,
    [Range(0, int.MaxValue)] int SortOrder,
    [Range(0, 100)] decimal? DefaultProbabilityPercent,
    bool IsClosedCategory,
    bool IsDefault,
    bool IsActive);

public sealed record UpdatePipelineSettingRequest(
    [Required] string SettingValue);

public sealed record CreateDuplicateRuleRequest(
    Guid TenantId,
    [Required, StringLength(200)] string RuleName,
    [Required, StringLength(80)] string EntityType,
    [StringLength(500)] string? MatchFields,
    [Range(0, 100)] int MatchThreshold,
    [StringLength(80)] string? ActionOnMatch,
    [StringLength(500)] string? Description);

public sealed record UpdateDuplicateRuleRequest(
    [Required, StringLength(200)] string RuleName,
    [Required, StringLength(80)] string EntityType,
    [StringLength(500)] string? MatchFields,
    [Range(0, 100)] int MatchThreshold,
    [StringLength(80)] string? ActionOnMatch,
    [StringLength(500)] string? Description,
    bool IsActive);

public sealed record CreateAssignmentRuleRequest(
    Guid TenantId,
    [Required, StringLength(200)] string RuleName,
    [Required, StringLength(80)] string EntityType,
    [StringLength(80)] string? AssignmentMethod,
    [StringLength(1000)] string? Criteria,
    Guid? AssignToUserId,
    [StringLength(200)] string? AssignToTeam,
    [Range(0, int.MaxValue)] int Priority,
    [StringLength(500)] string? Description);

public sealed record UpdateAssignmentRuleRequest(
    [Required, StringLength(200)] string RuleName,
    [Required, StringLength(80)] string EntityType,
    [StringLength(80)] string? AssignmentMethod,
    [StringLength(1000)] string? Criteria,
    Guid? AssignToUserId,
    [StringLength(200)] string? AssignToTeam,
    [Range(0, int.MaxValue)] int Priority,
    [StringLength(500)] string? Description,
    bool IsActive);

public sealed record CreateLeadActivityOutcomeRequest(
    Guid TenantId,
    [Required, StringLength(50)] string ActivityTypeCode,
    [Required, StringLength(50)] string OutcomeCode,
    [Required, StringLength(100)] string OutcomeName,
    [StringLength(500)] string? Description,
    [Range(0, int.MaxValue)] int SortOrder,
    Guid? CreatedByUserId = null);

public sealed record UpdateLeadActivityOutcomeRequest(
    [Required, StringLength(50)] string ActivityTypeCode,
    [Required, StringLength(50)] string OutcomeCode,
    [Required, StringLength(100)] string OutcomeName,
    [StringLength(500)] string? Description,
    [Range(0, int.MaxValue)] int SortOrder,
    bool IsActive,
    Guid? ModifiedByUserId = null);

public sealed record CreateLeadActivityTypeRequest(
    Guid TenantId,
    [Required, StringLength(50)] string ActivityTypeCode,
    [Required, StringLength(100)] string ActivityTypeName,
    [StringLength(100)] string? IconCssClass,
    [StringLength(500)] string? Description,
    [Range(0, int.MaxValue)] int SortOrder,
    Guid? CreatedByUserId = null);

public sealed record UpdateLeadActivityTypeRequest(
    [Required, StringLength(50)] string ActivityTypeCode,
    [Required, StringLength(100)] string ActivityTypeName,
    [StringLength(100)] string? IconCssClass,
    [StringLength(500)] string? Description,
    [Range(0, int.MaxValue)] int SortOrder,
    bool IsActive,
    Guid? ModifiedByUserId = null);

public sealed record CreateCrmCustomFieldRequest(
    Guid TenantId,
    [Required, StringLength(80)] string FieldCode,
    [Required, StringLength(200)] string FieldName,
    [Required, StringLength(80)] string EntityType,
    [Required, StringLength(80)] string FieldType,
    [StringLength(500)] string? DefaultValue,
    string? DropdownOptions,
    bool IsRequired,
    bool IsSearchable,
    [Range(0, int.MaxValue)] int SortOrder);

public sealed record UpdateCrmCustomFieldRequest(
    [Required, StringLength(80)] string FieldCode,
    [Required, StringLength(200)] string FieldName,
    [Required, StringLength(80)] string EntityType,
    [Required, StringLength(80)] string FieldType,
    [StringLength(500)] string? DefaultValue,
    string? DropdownOptions,
    bool IsRequired,
    bool IsSearchable,
    bool IsActive,
    [Range(0, int.MaxValue)] int SortOrder);
