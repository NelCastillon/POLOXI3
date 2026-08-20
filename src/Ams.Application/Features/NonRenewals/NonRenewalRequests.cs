using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.NonRenewals;

public sealed class CreateNonRenewalRequest
{
    [Required]
    public Guid TenantId { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? AccountId { get; set; }
    [Required, StringLength(120)]
    public string PolicyNumber { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string AccountName { get; set; } = string.Empty;
    [StringLength(200)]
    public string? CarrierName { get; set; }
    [StringLength(120)]
    public string? LineOfBusiness { get; set; }
    [StringLength(2)]
    public string? StateCode { get; set; }
    public DateTime? PolicyExpirationDate { get; set; }
    [StringLength(50)]
    public string? ReasonCode { get; set; }
    [Required, StringLength(30)]
    public string InitiatedByCode { get; set; } = "Carrier";
    public DateTime? CarrierNoticeDate { get; set; }
    [StringLength(40)]
    public string? CarrierNoticeMethodCode { get; set; }
    [StringLength(120)]
    public string? CarrierNoticeReference { get; set; }
    [StringLength(1000)]
    public string? CarrierNoticeSummary { get; set; }
    public Guid? AssignedToUserId { get; set; }
    [StringLength(200)]
    public string? AssignedToName { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    public bool IsUrgent { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateNonRenewalRequest
{
    [StringLength(50)]
    public string? ReasonCode { get; set; }
    [Required, StringLength(30)]
    public string InitiatedByCode { get; set; } = "Carrier";
    public DateTime? CarrierNoticeDate { get; set; }
    [StringLength(40)]
    public string? CarrierNoticeMethodCode { get; set; }
    [StringLength(120)]
    public string? CarrierNoticeReference { get; set; }
    [StringLength(1000)]
    public string? CarrierNoticeSummary { get; set; }
    public bool RemarketRecommended { get; set; }
    public Guid? RemarketSubmissionId { get; set; }
    [StringLength(1000)]
    public string? ResolutionSummary { get; set; }
    public Guid? AssignedToUserId { get; set; }
    [StringLength(200)]
    public string? AssignedToName { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    public bool IsUrgent { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class UpdateNonRenewalStatusRequest
{
    [Required, StringLength(50)]
    public string StatusCode { get; set; } = string.Empty;
    [StringLength(1000)]
    public string? Notes { get; set; }
    [Required, StringLength(200)]
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class RecordInsuredNotificationRequest
{
    [Required]
    public DateTime InsuredNotifiedDate { get; set; }
    [Required, StringLength(40)]
    public string InsuredNotificationMethodCode { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string InsuredNotificationProofReference { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string InsuredNotificationSentByName { get; set; } = string.Empty;
    [StringLength(1000)]
    public string? Notes { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class AddNonRenewalActivityRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required]
    public Guid NonRenewalId { get; set; }
    [Required, StringLength(50)]
    public string ActivityType { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;
    [StringLength(2000)]
    public string? Notes { get; set; }
    [Required, StringLength(200)]
    public string CreatedByName { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
}
