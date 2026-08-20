using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Claims;

public sealed class CreateClaimRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(50)]
    public string PolicyNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string AccountName { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string Lob { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string Carrier { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string LossType { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string PrimaryClaimant { get; set; } = string.Empty;

    [Required]
    public DateTime DateOfLoss { get; set; }

    [Required]
    public DateTime DateReported { get; set; }

    [Required]
    [StringLength(2000)]
    public string LossDescription { get; set; } = string.Empty;

    [StringLength(400)]
    public string? LossLocation { get; set; }

    [StringLength(20)]
    public string? StateOfLoss { get; set; }

    [StringLength(120)]
    public string? CauseOfLoss { get; set; }

    [StringLength(120)]
    public string AssignedHandler { get; set; } = "Unassigned";

    [StringLength(40)]
    public string Priority { get; set; } = "Standard";

    [StringLength(50)]
    public string Status { get; set; } = "Open";

    [StringLength(80)]
    public string? CatCode { get; set; }

    public decimal TotalIncurred { get; set; }
    public decimal TotalReserves { get; set; }
    public decimal TotalPaid { get; set; }
    public bool IsLitigation { get; set; }
    public bool HasSubrogation { get; set; }
    public bool IsCatastrophe { get; set; }
    public bool IsDisputed { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? CarrierId { get; set; }
    [StringLength(80)] public string? CarrierClaimNumber { get; set; }
    [StringLength(120)] public string? ReportedBy { get; set; }
    [EmailAddress, StringLength(254)] public string? ClaimantEmail { get; set; }
    [Phone, StringLength(50)] public string? ClaimantPhone { get; set; }
}

public sealed record AssignClaimAdjusterRequest([Required] Guid TenantId,[Required] Guid ClaimId,[Required,StringLength(50)] string AdjusterTypeCode,[Required,StringLength(200)] string AdjusterName,[StringLength(200)] string? CompanyName,[EmailAddress,StringLength(254)] string? EmailAddress,[Phone,StringLength(50)] string? PhoneNumber,[StringLength(80)] string? LicenseNumber,bool IsPrimary,Guid? UserId);
public sealed record UpsertClaimPartyRequest([Required] Guid TenantId,[Required] Guid ClaimId,Guid? ClaimPartyId,Guid? ContactId,[Required,StringLength(50)] string PartyTypeCode,[Required,StringLength(200)] string DisplayName,[StringLength(200)] string? OrganizationName,[EmailAddress,StringLength(254)] string? EmailAddress,[Phone,StringLength(50)] string? PhoneNumber,string? AddressJson,[StringLength(50)] string? PreferredContactMethodCode,bool IsPrimary,bool IsActive,Guid? UserId);
public sealed record CreateClaimFinancialTransactionRequest([Required] Guid TenantId,[Required] Guid ClaimId,[Required,StringLength(50)] string TransactionTypeCode,DateOnly TransactionDate,[Range(typeof(decimal),"0.01","9999999999999999")] decimal Amount,[Required,StringLength(3)] string CurrencyCode,[StringLength(80)] string? CoverageCode,Guid? PayeeClaimPartyId,[StringLength(100)] string? ReferenceNumber,[StringLength(1000)] string? Description,Guid? UserId);
public sealed record ReverseClaimFinancialTransactionRequest([Required] Guid TenantId,[Required] Guid ClaimFinancialTransactionId,[Required,StringLength(1000)] string Reason,Guid? UserId);
public sealed record CreateClaimNoteRequest([Required] Guid TenantId,[Required] Guid ClaimId,[Required,StringLength(50)] string NoteTypeCode,[Required,StringLength(200)] string Subject,[Required] string NoteText,bool IsPinned,bool IsConfidential,Guid? UserId,string? UserName);
public sealed record CreateClaimTaskRequest([Required] Guid TenantId,[Required] Guid ClaimId,[Required,StringLength(50)] string TaskTypeCode,[Required,StringLength(200)] string Title,[StringLength(2000)] string? Description,[Required,StringLength(50)] string PriorityCode,Guid? AssignedToUserId,[StringLength(200)] string? AssignedToName,DateOnly? DueDate,Guid? UserId);
public sealed record CompleteClaimTaskRequest([Required] Guid TenantId,[Required] Guid ClaimTaskId,[StringLength(1000)] string? CompletionNotes,Guid? UserId);
public sealed record LinkClaimDocumentRequest([Required] Guid TenantId,[Required] Guid ClaimId,[Required] Guid DocumentId,[Required,StringLength(80)] string DocumentRoleCode,[StringLength(500)] string? Description,Guid? UserId);
public sealed record ImportLossRunRequest([Required] Guid TenantId,[Required] Guid AccountId,Guid? PolicyId,Guid? CarrierId,DateOnly AsOfDate,DateOnly? PeriodStartDate,DateOnly? PeriodEndDate,Guid? SourceDocumentId,[Required,StringLength(260)] string SourceFileName,[Required] string CsvContent,Guid? UserId);

public sealed class UpdateClaimStatusRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = string.Empty;
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class UpdateClaimFollowUpRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [StringLength(120)]
    public string? FollowUpReason { get; set; }
    [StringLength(40)]
    public string? Priority { get; set; }
    public DateTime? FollowUpDueDate { get; set; }
    public bool IsSnoozed { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class CreateClaimActivityRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required]
    public Guid ClaimId { get; set; }
    [Required]
    [StringLength(50)]
    public string ActivityType { get; set; } = string.Empty;
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    [StringLength(80)]
    public string Category { get; set; } = string.Empty;
    [StringLength(120)]
    public string Party { get; set; } = string.Empty;
    [StringLength(2000)]
    public string Notes { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public decimal? PriorAmount { get; set; }
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;
    [StringLength(120)]
    public string CreatedBy { get; set; } = "Current User";
    public bool IsPinned { get; set; }
}

public sealed class CreateCatEventRequest
{
    [Required]
    public Guid TenantId { get; set; }
    [Required]
    [StringLength(160)]
    public string Name { get; set; } = string.Empty;
    [Required]
    [StringLength(80)]
    public string CatCode { get; set; } = string.Empty;
    [Required]
    [StringLength(80)]
    public string EventType { get; set; } = string.Empty;
    [Required]
    [StringLength(40)]
    public string Severity { get; set; } = "High";
    [StringLength(120)]
    public string AffectedStates { get; set; } = string.Empty;
    [Required]
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime? EndDate { get; set; }
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;
}

public sealed class CatBlastRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid CatEventId { get; set; }

    [Required]
    [StringLength(50)]
    public string Channel { get; set; } = "Email";

    [StringLength(160)]
    public string Template { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Body { get; set; } = string.Empty;

    [StringLength(40)]
    public string Status { get; set; } = "Delivered";

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string? SentBy { get; set; }
}

public sealed class FastCatFnolRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid CatEventId { get; set; }

    [Required]
    [StringLength(50)]
    public string PolicyNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string InsuredName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(80)]
    public string LossType { get; set; } = "Other";

    [StringLength(50)]
    public string EstimatedRange { get; set; } = "Unknown";

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
}
