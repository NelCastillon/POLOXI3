namespace Ams.Application.Common.Dtos;

public sealed class BillingAccountDto
{
    public Guid AccountId { get; set; }
    public Guid TenantId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string BillingModeCode { get; set; } = "Direct Bill";
    public decimal BalanceAmount { get; set; }
    public decimal CreditLimit { get; set; }
    public string PaymentTermsCode { get; set; } = "Net 30";
    public string DefaultPaymentMethodCode { get; set; } = "ACH";
    public bool AutopayEnrolled { get; set; }
    public string StatusCode { get; set; } = "Active";
    public int PolicyCount { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public string? MainEmail { get; set; }
    public string? MainPhone { get; set; }
}
