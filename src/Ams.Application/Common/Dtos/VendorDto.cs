namespace Ams.Application.Common.Dtos;

public sealed class VendorDto
{
    public Guid VendorId { get; set; }
    public Guid TenantId { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string PaymentTermsCode { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string VendorTypeCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
