namespace Ams.Application.Common.Dtos;

public sealed class WorkflowMakerCheckerRuleDto
{
    public Guid MakerCheckerRuleId { get; init; }
    public Guid TenantId { get; init; }
    public string EntityName { get; init; } = string.Empty;
    public string OperationCode { get; init; } = string.Empty;
    public bool RequiresDifferentUser { get; init; }
    public string? MakerRoleCode { get; init; }
    public string? CheckerRoleCode { get; init; }
    public Guid? WorkflowDefinitionId { get; init; }
    public bool IsActive { get; init; }
    public bool IsSystemDefined { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
