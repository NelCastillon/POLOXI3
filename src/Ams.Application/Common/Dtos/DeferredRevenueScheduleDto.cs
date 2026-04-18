namespace Ams.Application.Common.Dtos;

public sealed class DeferredRevenueScheduleDto
{
    public Guid DeferredRevenueScheduleId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? AgreementId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RecognizedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string FrequencyCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public Guid? GLAccountId { get; set; }
    public Guid? DeferredGLAccountId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
