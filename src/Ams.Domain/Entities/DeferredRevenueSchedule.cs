using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class DeferredRevenueSchedule : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public Guid? AgreementId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal RecognizedAmount { get; private set; }
    public decimal RemainingAmount { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string FrequencyCode { get; private set; } = "Monthly";
    public DeferredRevenueStatus Status { get; private set; } = DeferredRevenueStatus.Active;
    public Guid? GLAccountId { get; private set; }
    public Guid? DeferredGLAccountId { get; private set; }

    private DeferredRevenueSchedule() { }

    public DeferredRevenueSchedule(Guid tenantId, Guid accountId, decimal totalAmount, DateOnly startDate, string frequencyCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        TotalAmount = totalAmount;
        RemainingAmount = totalAmount;
        StartDate = startDate;
        FrequencyCode = frequencyCode;
        Status = DeferredRevenueStatus.Active;
    }
}
