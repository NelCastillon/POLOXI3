namespace Ams.Application.Features.Sod;

public sealed class UpdateSodRuleRequest
{
    public string  RuleCode            { get; set; } = string.Empty;
    public string  RuleName            { get; set; } = string.Empty;
    public string? Description         { get; set; }
    public Guid    RoleAId             { get; set; }
    public Guid    RoleBId             { get; set; }
    public Guid?   PermissionAId       { get; set; }
    public Guid?   PermissionBId       { get; set; }
    public string  SeverityCode        { get; set; } = string.Empty;
    public string? Reason              { get; set; }
    public string? ExceptionPolicyCode { get; set; }
    public Guid?   ModifiedByUserId    { get; set; }
}
