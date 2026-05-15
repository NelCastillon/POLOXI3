using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Leads;

public sealed class CreateLeadEngagementFactorRequest
{
    public Guid TenantId { get; set; }

    [Required, StringLength(200)]
    public string FactorName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Metric { get; set; } = string.Empty;

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

public sealed class UpdateLeadEngagementFactorRequest
{
    public Guid EngagementFactorId { get; set; }
    public Guid TenantId { get; set; }

    [Required, StringLength(200)]
    public string FactorName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Metric { get; set; } = string.Empty;

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
