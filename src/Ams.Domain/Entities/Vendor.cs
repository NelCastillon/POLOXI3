using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class Vendor : AuditableEntity
{
    public string VendorCode { get; private set; } = string.Empty;
    public string VendorName { get; private set; } = string.Empty;
    public string? ContactName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string PaymentTermsCode { get; private set; } = "Net30";
    public string CurrencyCode { get; private set; } = "USD";
    public string? TaxId { get; private set; }
    public string VendorTypeCode { get; private set; } = "Supplier";
    public VendorStatus Status { get; private set; } = VendorStatus.Active;

    private Vendor() { }

    public Vendor(Guid tenantId, string vendorCode, string vendorName, string paymentTermsCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        VendorCode = vendorCode;
        VendorName = vendorName;
        PaymentTermsCode = paymentTermsCode;
        Status = VendorStatus.Active;
    }
}
