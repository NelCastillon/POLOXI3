namespace Ams.Application.Common.Dtos;

public sealed class ProposalDto
{
    public Guid ProposalId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string? PdfUrl { get; set; }
    public string? HtmlContent { get; set; }
    public string? CustomIntroduction { get; set; }
    public string? DeliveryMethod { get; set; }
    public string? Recipient { get; set; }
    public DateTime? SentDateUtc { get; set; }
    public DateTime? PresentedDateUtc { get; set; }
    public string? ClientDecision { get; set; }
    public string? DecisionNotes { get; set; }
    public DateTime? DecisionDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? GeneratedDateUtc { get; set; }
    public IReadOnlyList<ProposalQuoteDto> Quotes { get; set; } = [];
    public IReadOnlyList<ProposalLifecycleEventDto> Events { get; set; } = [];
}

public sealed class ProposalQuoteDto
{
    public Guid QuoteId { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public decimal AnnualPremium { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Limit { get; set; }
    public string? CoverageNotes { get; set; }
    public bool IsSelected { get; set; }
    public int SortOrder { get; set; }
}

public sealed class ProposalLifecycleEventDto
{
    public Guid ProposalLifecycleEventId { get; set; }
    public string EventCode { get; set; } = string.Empty;
    public string? EventDetail { get; set; }
    public DateTime EventDateUtc { get; set; }
}

public sealed class ProposalWorkflowOptionDto
{
    public Guid ProposalWorkflowOptionId { get; set; }
    public string OptionGroupCode { get; set; } = string.Empty;
    public string OptionCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}
