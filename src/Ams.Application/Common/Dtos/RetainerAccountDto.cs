namespace Ams.Application.Common.Dtos;

public sealed class RetainerAccountDto
{
    public Guid RetainerAccountId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? AgreementId { get; set; }
    public string RetainerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal UsedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public string StatusCode { get; set; } = "Active";
    public DateTime CreatedDateUtc { get; set; }
}
