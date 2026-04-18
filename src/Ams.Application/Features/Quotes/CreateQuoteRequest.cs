namespace Ams.Application.Features.Quotes;

public sealed class CreateQuoteRequest
{
    public Guid TenantId { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public Guid? OpportunityId { get; set; }
    public Guid AccountId { get; set; }
    public DateTime? ValidUntilDate { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
