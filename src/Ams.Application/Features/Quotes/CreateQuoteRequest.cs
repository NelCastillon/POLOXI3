using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Quotes;

public class CreateQuoteRequest
{
    public Guid TenantId { get; set; }

    [Required, StringLength(50)]
    public string QuoteNumber { get; set; } = string.Empty;

    public Guid? OpportunityId { get; set; }
    public Guid AccountId { get; set; }
    public DateTime? ValidUntilDate { get; set; }

    [Range(0, 999999999999)]
    public decimal TotalAmount { get; set; }

    [Required, StringLength(50)]
    public string StatusCode { get; set; } = "Draft";

    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateQuoteRequest : CreateQuoteRequest
{
    public Guid QuoteId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
