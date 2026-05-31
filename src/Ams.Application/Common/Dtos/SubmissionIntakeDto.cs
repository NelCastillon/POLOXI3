namespace Ams.Application.Common.Dtos;

public sealed class SubmissionIntakeDto
{
    public Guid IntakeId { get; set; }
    public Guid TenantId { get; set; }
    public string IntakeNumber { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public string? ApplicantName { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string? Fein { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? ExistingPolicyNumber { get; set; }
    public string? ProducerCode { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public DateTime? RequestedEffectiveDate { get; set; }
    public decimal? EstimatedPremium { get; set; }
    public string? Attachments { get; set; }
    public string? Notes { get; set; }
    public string IntakeStatus { get; set; } = "Pending";
    public int MatchScore { get; set; }
    public Guid? MatchedAccountId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? SubmissionId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public DateTime? ProcessedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
