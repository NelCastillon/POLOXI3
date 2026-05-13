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
}

public sealed class UpdateClaimStatusRequest
{
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = string.Empty;
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class UpdateClaimFollowUpRequest
{
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
}
