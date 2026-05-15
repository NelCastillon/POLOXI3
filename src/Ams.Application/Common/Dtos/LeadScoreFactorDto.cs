namespace Ams.Application.Common.Dtos;

public sealed class LeadScoreFactorDto
{
    public Guid LeadScoringRuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Points { get; set; }
    public bool Matched { get; set; }
    public string ActualValue { get; set; } = string.Empty;
}
