using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class DeferredRevenueRecognition : AuditableEntity
{
    public Guid DeferredRevenueScheduleId { get; private set; }
    public DateOnly RecognitionDate { get; private set; }
    public decimal Amount { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public string StatusCode { get; private set; } = "Pending";

    private DeferredRevenueRecognition() { }

    public DeferredRevenueRecognition(Guid tenantId, Guid deferredRevenueScheduleId, DateOnly recognitionDate, decimal amount, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        DeferredRevenueScheduleId = deferredRevenueScheduleId;
        RecognitionDate = recognitionDate;
        Amount = amount;
        StatusCode = "Pending";
    }
}
