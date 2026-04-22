namespace Ams.Application.Common.Dtos;

public sealed class QuoteComparisonDto
{
    public Guid QuoteId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string QuoteNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal AnnualPremium { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Limit { get; set; }
    public string? CoverageNotes { get; set; }
    public DateTime QuotedDateUtc { get; set; }
    public DateTime ExpiresDateUtc { get; set; }
}
