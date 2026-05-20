using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Opportunities;

public sealed class UpdateOpportunityRequest
{
    [Required, StringLength(200)]
    public string OpportunityName { get; set; } = string.Empty;

    [Range(0, 999999999999)]
    public decimal EstimatedAmount { get; set; }

    public DateTime? CloseDate { get; set; }

    [Range(0, 100)]
    public decimal WinProbability { get; set; }

    [Required, StringLength(50)]
    public string ForecastCategoryCode { get; set; } = "Pipeline";

    [Required, StringLength(50)]
    public string StageName { get; set; } = "Qualification";

    [StringLength(2000)]
    public string? Description { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}

public sealed class UpdateOpportunityStageRequest
{
    [Required, StringLength(50)]
    public string Stage { get; set; } = string.Empty;

    public Guid? ModifiedByUserId { get; set; }
}

public sealed class UpsertOpportunityActivityRequest
{
    public Guid? ActivityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OpportunityId { get; set; }

    [Required, StringLength(50)]
    public string ActivityTypeCode { get; set; } = "Call";

    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Notes { get; set; }

    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
}

public sealed class UpsertOpportunitySubmissionRequest
{
    public Guid? SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OpportunityId { get; set; }

    [StringLength(50)]
    public string? SubmissionNumber { get; set; }

    [Required, StringLength(100)]
    public string LineOfBusiness { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Status { get; set; } = "Draft";

    [Range(0, 999999999999)]
    public decimal TargetPremium { get; set; }

    public Guid? UserId { get; set; }
}

public sealed class UpsertOpportunityCompetitorRequest
{
    public Guid? CompetitorId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OpportunityId { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Strength { get; set; } = "Moderate";

    public Guid? UserId { get; set; }
}
