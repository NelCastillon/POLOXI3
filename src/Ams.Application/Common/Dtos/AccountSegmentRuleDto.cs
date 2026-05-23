namespace Ams.Application.Common.Dtos;

public sealed class AccountSegmentRuleDto
{
    public Guid RuleId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? SegmentId { get; set; }
    public string SegmentCode { get; set; } = string.Empty;
    public string SegmentName { get; set; } = string.Empty;
    public string RuleCode { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CriteriaJson { get; set; } = "[]";
    public string LogicConnector { get; set; } = "AND";
    public int Priority { get; set; }
    public bool RunOnSchedule { get; set; }
    public int AccountsMatched { get; set; }
    public decimal AccuracyPercent { get; set; }
    public DateTime? LastRunDateUtc { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
