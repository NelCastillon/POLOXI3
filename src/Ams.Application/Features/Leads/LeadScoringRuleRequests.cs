using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Leads;

public sealed class CreateLeadScoringRuleRequest
{
    public Guid TenantId { get; set; }

    [Required, StringLength(200)]
    public string RuleName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Field { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Operator { get; set; } = string.Empty;

    [StringLength(500)]
    public string Value { get; set; } = string.Empty;

    [Range(-1000, 1000)]
    public int Points { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, 100000)]
    public int SortOrder { get; set; }
}

public sealed class UpdateLeadScoringRuleRequest
{
    public Guid ScoringRuleId { get; set; }
    public Guid TenantId { get; set; }

    [Required, StringLength(200)]
    public string RuleName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Field { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Operator { get; set; } = string.Empty;

    [StringLength(500)]
    public string Value { get; set; } = string.Empty;

    [Range(-1000, 1000)]
    public int Points { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, 100000)]
    public int SortOrder { get; set; }
}
