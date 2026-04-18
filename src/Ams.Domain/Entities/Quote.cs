namespace Ams.Domain.Entities;

public sealed class Quote
{
    public Guid QuoteId { get; set; }
    public Guid TenantId { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public Guid? OpportunityId { get; set; }
    public Guid AccountId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateOnly? ValidUntilDate { get; set; }
    public string StatusCode { get; set; } = "Draft";
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}
