namespace Ams.Application.Common.Dtos;

public sealed class SupportedLocaleDto
{
    public Guid LocaleId { get; set; }
    public string LocaleCode { get; set; } = string.Empty;
    public string LocaleName { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string? CurrencySymbol { get; set; }
    public string? DateFormat { get; set; }
    public string? TimeFormat { get; set; }
    public string? NumberFormat { get; set; }
    public bool IsRtl { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
