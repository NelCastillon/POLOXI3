using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Validation;

namespace Ams.Application.Features.SubmissionIntake;

/// <summary>
/// Captures a direct submission that arrived outside the CRM lead path
/// (email, portal, API, producer upload, carrier request, walk-in).
/// The record is staged first, then normalized into Account -> Opportunity -> Submission.
/// </summary>
public sealed class CreateSubmissionIntakeRequest
{
    public Guid TenantId { get; set; }

    [Required, StringLength(50)]
    public string Source { get; set; } = "Email";

    [StringLength(200)]
    public string? ApplicantName { get; set; }

    [Required, StringLength(200)]
    public string BusinessName { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Fein { get; set; }

    [AmsEmailAddress, StringLength(200)]
    public string? Email { get; set; }

    [AmsPhone, StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(250)]
    public string? AddressLine { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(50)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(50)]
    public string? ExistingPolicyNumber { get; set; }

    [StringLength(50)]
    public string? ProducerCode { get; set; }

    [Required, StringLength(100)]
    public string LineOfBusiness { get; set; } = "Commercial Property";

    public DateTime? RequestedEffectiveDate { get; set; }

    [Range(0, 999999999999)]
    public decimal? EstimatedPremium { get; set; }

    [StringLength(4000)]
    public string? Attachments { get; set; }

    public string? RawPayload { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateSubmissionIntakeRequest
{
    public Guid TenantId { get; set; }

    [Required, StringLength(50)]
    public string Source { get; set; } = "Email";

    [StringLength(200)]
    public string? ApplicantName { get; set; }

    [Required, StringLength(200)]
    public string BusinessName { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Fein { get; set; }

    [AmsEmailAddress, StringLength(200)]
    public string? Email { get; set; }

    [AmsPhone, StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(250)]
    public string? AddressLine { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(50)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(50)]
    public string? ExistingPolicyNumber { get; set; }

    [StringLength(50)]
    public string? ProducerCode { get; set; }

    [Required, StringLength(100)]
    public string LineOfBusiness { get; set; } = "Commercial Property";

    public DateTime? RequestedEffectiveDate { get; set; }

    [Range(0, 999999999999)]
    public decimal? EstimatedPremium { get; set; }

    [StringLength(4000)]
    public string? Attachments { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}

/// <summary>
/// Promotes a staged intake into the enterprise model. The system matches or creates an
/// Account (LifecycleStage = Prospect when new), creates an Opportunity, then a Submission.
/// </summary>
public sealed class PromoteSubmissionIntakeRequest
{
    public Guid TenantId { get; set; }

    /// <summary>When supplied, attach to this existing account instead of creating a new one.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>When true, force-create a new Prospect account even if matches exist.</summary>
    public bool CreateNewAccount { get; set; }

    public Guid? ProcessedByUserId { get; set; }
}

public sealed class UpdateSubmissionIntakeStatusRequest
{
    public Guid TenantId { get; set; }

    [Required, StringLength(50)]
    public string IntakeStatus { get; set; } = "Pending";

    [StringLength(1000)]
    public string? Notes { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}

public sealed class PromoteSubmissionIntakeResult
{
    public Guid IntakeId { get; set; }
    public Guid AccountId { get; set; }
    public bool AccountCreated { get; set; }
    public Guid OpportunityId { get; set; }
    public Guid SubmissionId { get; set; }
    public int MatchScore { get; set; }
    public string Message { get; set; } = string.Empty;
}
