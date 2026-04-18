namespace Ams.Application.Common.Dtos;

public sealed class RateCardDto
{
    public Guid RateCardId { get; set; }
    public Guid TenantId { get; set; }
    public string RateCardCode { get; set; } = string.Empty;
    public string RateCardName { get; set; } = string.Empty;
    public DateOnly EffectiveStartDate { get; set; }
    public DateOnly? EffectiveEndDate { get; set; }
    public string StatusCode { get; set; } = "Active";
    public string? Description { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
