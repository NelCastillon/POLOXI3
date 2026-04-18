namespace Ams.Application.Common.Dtos;

public sealed class SegregationOfDutyRuleDto
{
    public Guid    SodRuleId           { get; set; }
    public Guid?   TenantId            { get; set; }
    public string  RuleCode            { get; set; } = string.Empty;
    public string  RuleName            { get; set; } = string.Empty;
    public string? Description         { get; set; }
    // Role pair
    public Guid    RoleAId             { get; set; }
    public string  RoleAName           { get; set; } = string.Empty;
    public Guid    RoleBId             { get; set; }
    public string  RoleBName           { get; set; } = string.Empty;
    // Permission pair (optional)
    public Guid?   PermissionAId       { get; set; }
    public string? PermissionAName     { get; set; }
    public Guid?   PermissionBId       { get; set; }
    public string? PermissionBName     { get; set; }
    // Risk / policy
    public string  SeverityCode        { get; set; } = string.Empty;
    public string? Reason              { get; set; }
    public string? ExceptionPolicyCode { get; set; }
    // Metadata
    public bool    IsActive            { get; set; }
    public bool    IsSystemDefined     { get; set; }
    public DateTime  CreatedDateUtc    { get; set; }
    public DateTime? ModifiedDateUtc   { get; set; }
}
