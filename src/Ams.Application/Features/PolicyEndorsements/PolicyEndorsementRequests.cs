using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.PolicyEndorsements;

public sealed class CreatePolicyEndorsementRequest
{
    [Required]
    public Guid TenantId { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? AccountId { get; set; }
    [Required, StringLength(50)]
    public string PolicyNumber { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string AccountName { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string LineOfBusiness { get; set; } = string.Empty;
    [Required, StringLength(160)]
    public string Carrier { get; set; } = string.Empty;
    [Required, StringLength(120)]
    public string EndorsementType { get; set; } = string.Empty;
    [Required, StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    [Required]
    public DateTime EffectiveDate { get; set; }
    [Range(-10000000, 10000000)]
    public decimal PremiumDelta { get; set; }
    [Required, StringLength(40)]
    public string Priority { get; set; } = "Normal";
    [Required, StringLength(160)]
    public string RequestedByName { get; set; } = string.Empty;
    [Required, StringLength(160)]
    public string AssignedToName { get; set; } = string.Empty;
    [StringLength(160)]
    public string? UnderwriterName { get; set; }
    [StringLength(1000)]
    public string? Reason { get; set; }
    [StringLength(1000)]
    public string? RequiredDocuments { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsUrgent { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdatePolicyEndorsementRequest
{
    [Required, StringLength(120)]
    public string EndorsementType { get; set; } = string.Empty;
    [Required, StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    [Required]
    public DateTime EffectiveDate { get; set; }
    [Range(-10000000, 10000000)]
    public decimal PremiumDelta { get; set; }
    [Required, StringLength(40)]
    public string Priority { get; set; } = "Normal";
    [Required, StringLength(160)]
    public string AssignedToName { get; set; } = string.Empty;
    [StringLength(160)]
    public string? UnderwriterName { get; set; }
    [StringLength(1000)]
    public string? Reason { get; set; }
    [StringLength(1000)]
    public string? RequiredDocuments { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsUrgent { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class UpdatePolicyEndorsementStatusRequest
{
    [Required, StringLength(40)]
    public string Status { get; set; } = string.Empty;
    [StringLength(1000)]
    public string? Notes { get; set; }
    [Required, StringLength(160)]
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class AddPolicyEndorsementActivityRequest
{
    [Required]
    public Guid EndorsementId { get; set; }
    [Required, StringLength(60)]
    public string ActivityType { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;
    [StringLength(1000)]
    public string? Notes { get; set; }
    [Required, StringLength(160)]
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpsertPolicyEndorsementDeltaRequest
{
    [Required]
    public Guid EndorsementId { get; set; }
    [Required, StringLength(120)]
    public string FieldName { get; set; } = string.Empty;
    [StringLength(500)]
    public string BeforeValue { get; set; } = string.Empty;
    [StringLength(500)]
    public string AfterValue { get; set; } = string.Empty;
    [Range(-10000000, 10000000)]
    public decimal NumericDelta { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
