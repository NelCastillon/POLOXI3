namespace Ams.Application.Common.Dtos;

public sealed class LeadSourceDto
{
    public Guid     LeadSourceId    { get; set; }
    public Guid     TenantId        { get; set; }
    public string   SourceCode      { get; set; } = string.Empty;
    public string   SourceName      { get; set; } = string.Empty;
    public bool     IsActive        { get; set; }
    public DateTime CreatedDateUtc  { get; set; }
}

public sealed class LeadStatusDto
{
    public Guid     LeadStatusId    { get; set; }
    public Guid     TenantId        { get; set; }
    public string   StatusCode      { get; set; } = string.Empty;
    public string   StatusName      { get; set; } = string.Empty;
    public string?  StatusType      { get; set; }
    public string?  Description     { get; set; }
    public string?  ColorHex        { get; set; }
    public bool     IsDefault       { get; set; }
    public bool     IsActive        { get; set; }
    public int      SortOrder       { get; set; }
    public DateTime CreatedDateUtc  { get; set; }
}

public sealed class OpportunityStageDto
{
    public Guid     OpportunityStageId  { get; set; }
    public Guid     TenantId            { get; set; }
    public string   StageCode           { get; set; } = string.Empty;
    public string   StageName           { get; set; } = string.Empty;
    public int      SortOrder           { get; set; }
    public int      ProbabilityPercent  { get; set; }
    public bool     IsClosedStage       { get; set; }
    public bool     IsWonStage          { get; set; }
    public bool     IsActive            { get; set; }
}

public sealed class PipelineSettingDto
{
    public Guid     PipelineSettingId { get; set; }
    public Guid     TenantId          { get; set; }
    public string   SettingKey        { get; set; } = string.Empty;
    public string?  SettingValue      { get; set; }
    public string?  SettingType       { get; set; }
    public string?  Category          { get; set; }
    public string?  Description       { get; set; }
    public DateTime CreatedDateUtc    { get; set; }
}

public sealed class DuplicateRuleDto
{
    public Guid     DuplicateRuleId { get; set; }
    public Guid     TenantId        { get; set; }
    public string   RuleName        { get; set; } = string.Empty;
    public string   EntityType      { get; set; } = string.Empty;
    public string?  MatchFields     { get; set; }
    public int      MatchThreshold  { get; set; }
    public string?  ActionOnMatch   { get; set; }
    public string?  Description     { get; set; }
    public bool     IsActive        { get; set; }
    public DateTime CreatedDateUtc  { get; set; }
}

public sealed class AssignmentRuleDto
{
    public Guid     AssignmentRuleId  { get; set; }
    public Guid     TenantId          { get; set; }
    public string   RuleName          { get; set; } = string.Empty;
    public string   EntityType        { get; set; } = string.Empty;
    public string?  AssignmentMethod  { get; set; }
    public string?  Criteria          { get; set; }
    public Guid?    AssignToUserId    { get; set; }
    public string?  AssignToTeam      { get; set; }
    public int      Priority          { get; set; }
    public string?  Description       { get; set; }
    public bool     IsActive          { get; set; }
    public DateTime CreatedDateUtc    { get; set; }
}

public sealed class LeadActivityTypeDto
{
    public Guid     ActivityTypeId    { get; set; }
    public Guid     TenantId          { get; set; }
    public string   ActivityTypeCode  { get; set; } = string.Empty;
    public string   ActivityTypeName  { get; set; } = string.Empty;
    public string?  IconCssClass      { get; set; }
    public string?  Description       { get; set; }
    public int      SortOrder         { get; set; }
    public bool     IsActive          { get; set; }
    public DateTime CreatedDateUtc    { get; set; }
}

public sealed class CrmCustomFieldDto
{
    public Guid     CustomFieldId   { get; set; }
    public Guid     TenantId        { get; set; }
    public string   FieldCode       { get; set; } = string.Empty;
    public string   FieldName       { get; set; } = string.Empty;
    public string   EntityType      { get; set; } = string.Empty;
    public string   FieldType       { get; set; } = string.Empty;
    public string?  DefaultValue    { get; set; }
    public string?  DropdownOptions { get; set; }
    public bool     IsRequired      { get; set; }
    public bool     IsSearchable    { get; set; }
    public bool     IsActive        { get; set; }
    public int      SortOrder       { get; set; }
    public DateTime CreatedDateUtc  { get; set; }
}
