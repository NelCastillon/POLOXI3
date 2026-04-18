namespace Ams.Domain.Entities;

public sealed class SupportedLocale
{
    public Guid LocaleId { get; private set; } = Guid.NewGuid();
    public string LocaleCode { get; private set; } = string.Empty;
    public string LocaleName { get; private set; } = string.Empty;
    public string? NativeName { get; private set; }
    public string CurrencyCode { get; private set; } = "USD";
    public string? CurrencySymbol { get; private set; }
    public string? DateFormat { get; private set; }
    public string? TimeFormat { get; private set; }
    public string? NumberFormat { get; private set; }
    public bool IsRtl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public bool IsDeleted { get; private set; }

    private SupportedLocale() { }

    public SupportedLocale(string localeCode, string localeName, string currencyCode)
    {
        LocaleCode = localeCode;
        LocaleName = localeName;
        CurrencyCode = currencyCode;
    }
}
