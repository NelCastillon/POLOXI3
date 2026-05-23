using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.AccountSegments;

public sealed class CreateAccountSegmentRuleRequest
{
    public Guid TenantId { get; set; }
    public Guid? SegmentId { get; set; }

    [Required, StringLength(80)]
    public string SegmentCode { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string RuleCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string RuleName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required, StringLength(4000)]
    public string CriteriaJson { get; set; } = "[]";

    [Required, StringLength(10)]
    public string LogicConnector { get; set; } = "AND";

    [Range(0, int.MaxValue)]
    public int Priority { get; set; } = 10;

    public bool RunOnSchedule { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateAccountSegmentRuleRequest
{
    public Guid RuleId { get; set; }
    public Guid? SegmentId { get; set; }

    [Required, StringLength(80)]
    public string SegmentCode { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string RuleCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string RuleName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required, StringLength(4000)]
    public string CriteriaJson { get; set; } = "[]";

    [Required, StringLength(10)]
    public string LogicConnector { get; set; } = "AND";

    [Range(0, int.MaxValue)]
    public int Priority { get; set; } = 10;

    public bool RunOnSchedule { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? ModifiedByUserId { get; set; }
}
