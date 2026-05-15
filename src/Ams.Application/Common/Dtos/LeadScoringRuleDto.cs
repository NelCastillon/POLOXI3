namespace Ams.Application.Common.Dtos;

public sealed class LeadScoringRuleDto
{
    public Guid LeadScoringRuleId { get; set; }
    public Guid ScoringRuleId
    {
        get => LeadScoringRuleId;
        set => LeadScoringRuleId = value;
    }
    public Guid TenantId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string? RuleDescription { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int PointValue { get; set; }
    public int Points
    {
        get => PointValue;
        set => PointValue = value;
    }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
