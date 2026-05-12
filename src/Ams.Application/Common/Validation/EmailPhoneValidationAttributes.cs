using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Ams.Application.Common.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed partial class AmsEmailAddressAttribute : ValidationAttribute
{
    private static readonly EmailAddressAttribute EmailAddress = new();

    public AmsEmailAddressAttribute()
    {
        ErrorMessage = "Enter a valid email address.";
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        var email = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            return true;
        }

        return email.Length <= 320
            && !email.Any(char.IsWhiteSpace)
            && EmailAddress.IsValid(email)
            && EmailDomainRegex().IsMatch(email);
    }

    [GeneratedRegex(@"^[^@]+@[^@\.]+(\.[^@\.]+)+$", RegexOptions.CultureInvariant, 250)]
    private static partial Regex EmailDomainRegex();
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed partial class AmsPhoneAttribute : ValidationAttribute
{
    public AmsPhoneAttribute()
    {
        ErrorMessage = "Enter a valid phone number.";
    }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        var phone = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(phone))
        {
            return true;
        }

        if (phone.Length > 50 || !PhoneRegex().IsMatch(phone))
        {
            return false;
        }

        var digits = phone.Count(char.IsDigit);
        return digits is >= 7 and <= 15;
    }

    [GeneratedRegex(@"^\+?[0-9\s().\-]*(?:\s*(?:x|ext\.?|extension)\s*\d{1,6})?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 250)]
    private static partial Regex PhoneRegex();
}
