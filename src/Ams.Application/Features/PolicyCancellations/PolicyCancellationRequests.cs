using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.PolicyCancellations;

public sealed class CreatePolicyCancellationRequest
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
    [Required, StringLength(100)]
    public string CancellationReason { get; set; } = string.Empty;
    [Required, StringLength(40)]
    public string CancellationType { get; set; } = "Pro-Rata";
    [Required, StringLength(40)]
    public string RequestType { get; set; } = "Cancellation";
    [Required]
    public DateTime EffectiveDate { get; set; }
    [Range(-10000000, 10000000)]
    public decimal ReturnPremium { get; set; }
    [Range(0, 10000000)]
    public decimal PremiumDue { get; set; }
    [Required, StringLength(40)]
    public string Priority { get; set; } = "Normal";
    [Required, StringLength(160)]
    public string RequestedByName { get; set; } = string.Empty;
    [Required, StringLength(160)]
    public string AssignedToName { get; set; } = string.Empty;
    [StringLength(1000)]
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsUrgent { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdatePolicyCancellationRequest
{
    [Required, StringLength(100)]
    public string CancellationReason { get; set; } = string.Empty;
    [Required, StringLength(40)]
    public string CancellationType { get; set; } = "Pro-Rata";
    [Required]
    public DateTime EffectiveDate { get; set; }
    [Range(-10000000, 10000000)]
    public decimal ReturnPremium { get; set; }
    [Range(0, 10000000)]
    public decimal PremiumDue { get; set; }
    [Required, StringLength(40)]
    public string Priority { get; set; } = "Normal";
    [Required, StringLength(160)]
    public string AssignedToName { get; set; } = string.Empty;
    [StringLength(1000)]
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsUrgent { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class UpdatePolicyCancellationStatusRequest
{
    [Required, StringLength(40)]
    public string Status { get; set; } = string.Empty;
    [StringLength(1000)]
    public string? Notes { get; set; }
    [Required, StringLength(160)]
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class AddPolicyCancellationActivityRequest
{
    [Required]
    public Guid CancellationId { get; set; }
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
