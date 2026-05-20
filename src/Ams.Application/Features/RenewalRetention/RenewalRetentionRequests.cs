using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.RenewalRetention;

public sealed class CreateRenewalRetentionCaseRequest
{
    [Required]
    public Guid TenantId { get; set; }

    public Guid? PolicyId { get; set; }
    public Guid? AccountId { get; set; }

    [Required, StringLength(200)]
    public string AccountName { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string PolicyNumber { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LineOfBusiness { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string Carrier { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string Producer { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string Csr { get; set; } = string.Empty;

    [Required]
    public DateTime ExpirationDate { get; set; } = DateTime.UtcNow.Date.AddDays(60);

    [Range(0, 999999999)]
    public decimal CurrentPremium { get; set; }

    [Range(0, 999999999)]
    public decimal? ProposedPremium { get; set; }

    [Range(0, 100)]
    public int RetentionProbability { get; set; }

    [Range(0, 100)]
    public int RiskScore { get; set; }

    [Required, StringLength(40)]
    public string Stage { get; set; } = "Intake";

    [Required, StringLength(20)]
    public string Priority { get; set; } = "Normal";

    [Required, StringLength(40)]
    public string OutreachStatus { get; set; } = "Not Started";

    [Required, StringLength(40)]
    public string Sentiment { get; set; } = "Neutral";

    [StringLength(1000)]
    public string? RiskDrivers { get; set; }

    [StringLength(500)]
    public string? NextBestAction { get; set; }

    public DateTime? NextActionDueDate { get; set; }
    public Guid? AssignedToUserId { get; set; }

    [StringLength(160)]
    public string? AssignedToName { get; set; }

    public bool IsEscalated { get; set; }
    public bool IsAtRisk { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateRenewalRetentionStageRequest
{
    [Required, StringLength(40)]
    public string Stage { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string OutreachStatus { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Sentiment { get; set; } = string.Empty;

    [StringLength(500)]
    public string? NextBestAction { get; set; }

    public DateTime? NextActionDueDate { get; set; }
    public bool IsEscalated { get; set; }
    public bool IsAtRisk { get; set; }
    public bool IsSaved { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class CreateRenewalRetentionActivityRequest
{
    [Required]
    public Guid RetentionCaseId { get; set; }

    [Required, StringLength(40)]
    public string ActivityType { get; set; } = "Call";

    [Required, StringLength(180)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string Outcome { get; set; } = "Completed";

    [StringLength(2000)]
    public string? Notes { get; set; }

    [Required]
    public DateTime ActivityDateUtc { get; set; } = DateTime.UtcNow;

    [Required, StringLength(160)]
    public string CreatedByName { get; set; } = string.Empty;

    public Guid? CreatedByUserId { get; set; }
}

public sealed class CreateRenewalRetentionOfferRequest
{
    [Required]
    public Guid RetentionCaseId { get; set; }

    [Required, StringLength(160)]
    public string OfferName { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string OfferType { get; set; } = "Coverage Strategy";

    [Range(-999999999, 999999999)]
    public decimal PremiumImpact { get; set; }

    [Range(0, 100)]
    public int RetentionLift { get; set; }

    [Required, StringLength(40)]
    public string Status { get; set; } = "Draft";

    [StringLength(1000)]
    public string? Notes { get; set; }

    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateRenewalRetentionOfferStatusRequest
{
    [Required, StringLength(40)]
    public string Status { get; set; } = string.Empty;

    public Guid? ModifiedByUserId { get; set; }
}
