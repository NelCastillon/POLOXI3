using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Submissions;

public sealed class RecordClientAcceptanceRequest : IValidatableObject
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid ProposalId { get; set; }

    [Range(1, int.MaxValue)]
    public int ProposalVersionNumber { get; set; }

    [Required]
    public Guid QuoteId { get; set; }

    [Required, StringLength(64, MinimumLength = 64)]
    public string QuoteFingerprint { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string DecisionCode { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? DecisionNotes { get; set; }

    [Required, StringLength(50)]
    public string AuthorizationMethodCode { get; set; } = string.Empty;

    [StringLength(500)]
    public string? AuthorizationReference { get; set; }

    public Guid? AuthorizationDocumentId { get; set; }
    public Guid? ESignRequestId { get; set; }

    [Required, StringLength(200)]
    public string AuthorizedByName { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string AuthorizedByTitle { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string AuthorityBasisCode { get; set; } = string.Empty;

    public DateTime AuthorizedDateUtc { get; set; }

    [EmailAddress, StringLength(320)]
    public string? SignerEmail { get; set; }

    [StringLength(64)]
    public string? SignerIpAddress { get; set; }

    [StringLength(1000)]
    public string? UserAgent { get; set; }

    [Required, StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Range(0, long.MaxValue)]
    public long ExpectedVersionNumber { get; set; }

    public Guid? RecordedByUserId { get; set; }
    public List<ClientAcceptanceCoverageElectionRequest> CoverageElections { get; set; } = [];
    public List<ClientAcceptanceConsentRequest> Consents { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var supportedDecisions = new[] { "Accepted", "Declined", "ChangesRequested", "Deferred" };
        if (!supportedDecisions.Contains(DecisionCode, StringComparer.OrdinalIgnoreCase))
            yield return new ValidationResult("Decision must be Accepted, Declined, ChangesRequested, or Deferred.", [nameof(DecisionCode)]);

        if (AuthorizedDateUtc == default || AuthorizedDateUtc > DateTime.UtcNow.AddMinutes(5))
            yield return new ValidationResult("Authorization date is required and cannot be in the future.", [nameof(AuthorizedDateUtc)]);

        if (DecisionCode.Equals("Accepted", StringComparison.OrdinalIgnoreCase))
        {
            if (CoverageElections.Count == 0)
                yield return new ValidationResult("Acceptance requires at least one coverage election.", [nameof(CoverageElections)]);
            if (CoverageElections.GroupBy(x => x.QuoteLineId).Any(g => g.Count() > 1))
                yield return new ValidationResult("Each quote line may be elected only once.", [nameof(CoverageElections)]);
            if (Consents.Count == 0 || Consents.Any(x => !x.IsAccepted))
                yield return new ValidationResult("All required consent attestations must be accepted.", [nameof(Consents)]);
            if (string.IsNullOrWhiteSpace(AuthorizationReference) && !AuthorizationDocumentId.HasValue && !ESignRequestId.HasValue)
                yield return new ValidationResult("Acceptance requires an authorization reference, evidence document, or e-signature request.", [nameof(AuthorizationReference)]);
        }
        else if (string.IsNullOrWhiteSpace(DecisionNotes))
        {
            yield return new ValidationResult("Decision notes are required when the client does not accept.", [nameof(DecisionNotes)]);
        }
    }
}

public sealed class ClientAcceptanceCoverageElectionRequest
{
    [Required]
    public Guid QuoteLineId { get; set; }

    [Required, StringLength(50)]
    public string ElectionCode { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? ElectionNotes { get; set; }
}

public sealed class ClientAcceptanceConsentRequest
{
    [Required, StringLength(100)]
    public string ConsentCode { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string ConsentVersion { get; set; } = string.Empty;

    public bool IsAccepted { get; set; }
    public Guid? EvidenceDocumentId { get; set; }
}

public sealed record WithdrawClientAcceptanceRequest(
    Guid TenantId,
    long ExpectedVersionNumber,
    [property: Required, StringLength(1000)] string Reason,
    Guid? WithdrawnByUserId = null);
