namespace Ams.Application.Common.Dtos;

public sealed class LeadScoringRuleDto
{
    public Guid LeadScoringRuleId { get; set; }
    public Guid TenantId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string? RuleDescription { get; set; }
    public int PointValue { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
