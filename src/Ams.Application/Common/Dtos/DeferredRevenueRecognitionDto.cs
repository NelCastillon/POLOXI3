namespace Ams.Application.Common.Dtos;

public sealed class DeferredRevenueRecognitionDto
{
    public Guid RecognitionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid DeferredRevenueScheduleId { get; set; }
    public DateOnly RecognitionDate { get; set; }
    public decimal Amount { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
