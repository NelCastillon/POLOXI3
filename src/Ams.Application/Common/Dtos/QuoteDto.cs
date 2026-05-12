namespace Ams.Application.Common.Dtos;

public sealed class QuoteDto
{
    public Guid QuoteId { get; set; }
    public Guid TenantId { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public Guid? OpportunityId { get; set; }
    public string? OpportunityName { get; set; }
    public Guid AccountId { get; set; }
    public string? AccountName { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? ValidUntilDate { get; set; }
    public string StatusCode { get; set; } = "Draft";
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
