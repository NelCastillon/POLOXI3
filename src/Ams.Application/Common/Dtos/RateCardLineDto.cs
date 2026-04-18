namespace Ams.Application.Common.Dtos;

public sealed class RateCardLineDto
{
    public Guid RateCardLineId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RateCardId { get; set; }
    public string? RoleCode { get; set; }
    public string? ServiceCode { get; set; }
    public string? Description { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal? DailyRate { get; set; }
    public DateOnly EffectiveStartDate { get; set; }
    public DateOnly? EffectiveEndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
