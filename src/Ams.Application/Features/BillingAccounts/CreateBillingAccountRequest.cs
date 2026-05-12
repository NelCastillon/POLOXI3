using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.BillingAccounts;

public sealed class CreateBillingAccountRequest
{
    public Guid TenantId { get; set; }
    [Required]
    public Guid AccountId { get; set; }
    [Required, StringLength(50)]
    public string BillingModeCode { get; set; } = "Direct Bill";
    [Required, StringLength(50)]
    public string PaymentTermsCode { get; set; } = "Net 30";
    [Required, StringLength(50)]
    public string DefaultPaymentMethodCode { get; set; } = "ACH";
    [Range(0, 999999999999)]
    public decimal CreditLimit { get; set; } = 10000m;
    public bool AutopayEnrolled { get; set; }
    [Required, StringLength(50)]
    public string StatusCode { get; set; } = "Active";
    public Guid? CreatedByUserId { get; set; }
}
