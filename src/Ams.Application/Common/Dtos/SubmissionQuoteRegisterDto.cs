namespace Ams.Application.Common.Dtos;

public sealed class SubmissionQuoteRegisterDto
{
    public Guid QuoteId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid? SubmissionMarketId { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string QuoteNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal AnnualPremium { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Limit { get; set; }
    public string? CoverageNotes { get; set; }
    public DateTime? QuoteReceivedDateUtc { get; set; }
    public DateTime QuotedDateUtc { get; set; }
    public DateTime ExpiresDateUtc { get; set; }
    public string SubmissionNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public string? AssignedToUserName { get; set; }
    public IReadOnlyList<SubmissionQuoteLineDto> Lines { get; set; } = [];
}
