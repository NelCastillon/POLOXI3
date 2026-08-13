using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.PolicyChecks;

public sealed class CreatePolicyCheckRequest
{
    [Required]
    public Guid TenantId { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? QuoteId { get; set; }
    [Required, StringLength(120)]
    public string PolicyNumber { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string AccountName { get; set; } = string.Empty;
    [StringLength(200)]
    public string? CarrierName { get; set; }
    [StringLength(120)]
    public string? LineOfBusiness { get; set; }
    public DateTime? PolicyEffectiveDate { get; set; }
    public DateTime? PolicyExpirationDate { get; set; }
    [Required, StringLength(40)]
    public string CheckTypeCode { get; set; } = "NewBusiness";
    [Required, StringLength(30)]
    public string PriorityCode { get; set; } = "Normal";
    public Guid? AssignedToUserId { get; set; }
    [StringLength(200)]
    public string? AssignedToName { get; set; }
    public DateTime? DueDate { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    public bool IsUrgent { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdatePolicyCheckRequest
{
    [Required, StringLength(30)]
    public string PriorityCode { get; set; } = "Normal";
    [Required, StringLength(40)]
    public string CheckTypeCode { get; set; } = "NewBusiness";
    public Guid? AssignedToUserId { get; set; }
    [StringLength(200)]
    public string? AssignedToName { get; set; }
    public DateTime? DueDate { get; set; }
    [StringLength(1000)]
    public string? ResultSummary { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    public bool IsUrgent { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class UpdatePolicyCheckStatusRequest
{
    [Required, StringLength(50)]
    public string StatusCode { get; set; } = string.Empty;
    [StringLength(1000)]
    public string? Notes { get; set; }
    [Required, StringLength(200)]
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class UpdatePolicyCheckItemRequest
{
    [StringLength(500)]
    public string? ExpectedValue { get; set; }
    [StringLength(500)]
    public string? ActualValue { get; set; }
    [Required, StringLength(30)]
    public string MatchStatusCode { get; set; } = "Unchecked";
    [StringLength(1000)]
    public string? Notes { get; set; }
    [Required, StringLength(200)]
    public string CheckedByName { get; set; } = string.Empty;
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class AddPolicyCheckDiscrepancyRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required]
    public Guid PolicyCheckId { get; set; }
    public Guid? PolicyCheckItemId { get; set; }
    [Required, StringLength(50)]
    public string TypeCode { get; set; } = string.Empty;
    [Required, StringLength(120)]
    public string TypeName { get; set; } = string.Empty;
    [Required, StringLength(30)]
    public string SeverityCode { get; set; } = "Major";
    [Required, StringLength(1000)]
    public string Description { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
}

public sealed class ResolvePolicyCheckDiscrepancyRequest
{
    [Required, StringLength(40)]
    public string StatusCode { get; set; } = "Resolved";
    public bool CarrierNotified { get; set; }
    [StringLength(100)]
    public string? CarrierReferenceNumber { get; set; }
    [StringLength(1000)]
    public string? ResolutionNotes { get; set; }
    [Required, StringLength(200)]
    public string ResolvedByName { get; set; } = string.Empty;
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class AddPolicyCheckActivityRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required]
    public Guid PolicyCheckId { get; set; }
    [Required, StringLength(50)]
    public string ActivityType { get; set; } = "Note";
    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;
    [StringLength(2000)]
    public string? Notes { get; set; }
    [Required, StringLength(200)]
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
}
