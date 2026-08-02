namespace Ams.Application.Common.Dtos;

public sealed class ClientAcceptanceDto
{
    public Guid ClientAcceptanceId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid ProposalId { get; set; }
    public int ProposalVersionNumber { get; set; }
    public Guid QuoteId { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public string QuoteFingerprint { get; set; } = string.Empty;
    public string DecisionCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string? DecisionNotes { get; set; }
    public string AuthorizationMethodCode { get; set; } = string.Empty;
    public string? AuthorizationReference { get; set; }
    public Guid? AuthorizationDocumentId { get; set; }
    public Guid? ESignRequestId { get; set; }
    public string AuthorizedByName { get; set; } = string.Empty;
    public string AuthorizedByTitle { get; set; } = string.Empty;
    public string AuthorityBasisCode { get; set; } = string.Empty;
    public DateTime AuthorizedDateUtc { get; set; }
    public string? SignerEmail { get; set; }
    public string? SignerIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Guid? CustomerAuthorizationId { get; set; }
    public Guid? PolicyBindTransactionId { get; set; }
    public string? IdempotencyKey { get; set; }
    public long VersionNumber { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public IReadOnlyList<ClientAcceptanceCoverageElectionDto> CoverageElections { get; set; } = [];
    public IReadOnlyList<ClientAcceptanceConsentDto> Consents { get; set; } = [];
    public IReadOnlyList<ClientAcceptanceAuditEventDto> AuditEvents { get; set; } = [];
}

public sealed class ClientAcceptanceCoverageElectionDto
{
    public Guid ClientAcceptanceCoverageElectionId { get; set; }
    public Guid ClientAcceptanceId { get; set; }
    public Guid QuoteLineId { get; set; }
    public Guid SubmissionLineId { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public string ElectionCode { get; set; } = string.Empty;
    public decimal QuotedPremium { get; set; }
    public decimal? Limit { get; set; }
    public decimal? Deductible { get; set; }
    public string? CoverageForms { get; set; }
    public string? Subjectivities { get; set; }
    public string? Exclusions { get; set; }
    public string? PaymentTerms { get; set; }
    public bool? TriaIncluded { get; set; }
    public string? ElectionNotes { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ClientAcceptanceConsentDto
{
    public Guid ClientAcceptanceConsentId { get; set; }
    public Guid ClientAcceptanceId { get; set; }
    public string ConsentCode { get; set; } = string.Empty;
    public string ConsentVersion { get; set; } = string.Empty;
    public bool IsAccepted { get; set; }
    public DateTime AttestedDateUtc { get; set; }
    public Guid? EvidenceDocumentId { get; set; }
}

public sealed class ClientAcceptanceAuditEventDto
{
    public Guid ClientAcceptanceAuditEventId { get; set; }
    public Guid ClientAcceptanceId { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public string? EventDetail { get; set; }
    public string? DataJson { get; set; }
    public DateTime EventDateUtc { get; set; }
    public Guid? ActorUserId { get; set; }
}

public sealed class ClientAcceptanceReadinessDto
{
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid ProposalId { get; set; }
    public int ProposalVersionNumber { get; set; }
    public Guid? SelectedQuoteId { get; set; }
    public string? QuoteFingerprint { get; set; }
    public bool IsProposalDelivered { get; set; }
    public bool IsProposalCurrent { get; set; }
    public bool IsQuoteInProposal { get; set; }
    public bool IsQuoteActive { get; set; }
    public bool IsQuoteUnexpired { get; set; }
    public bool IsQuoteBindable { get; set; }
    public bool HasCoverageLines { get; set; }
    public bool HasCompleteBindableCoverage { get; set; }
    public bool CanAccept { get; set; }
    public IReadOnlyList<string> BlockingReasons { get; set; } = [];
    public IReadOnlyList<ProposalQuoteDto> Quotes { get; set; } = [];
    public IReadOnlyList<SubmissionQuoteLineDto> QuoteLines { get; set; } = [];
}
