using System.ComponentModel.DataAnnotations;
using Ams.Application.Features.Accounts;
using Xunit;

namespace Ams.Application.Tests;

public sealed class AccountRequestValidationTests
{
    [Theory]
    [InlineData("(555) 555-0100")]
    [InlineData("+1 555 555 0100")]
    [InlineData("555-555-0100 ext 25")]
    public void CreateAccount_AcceptsSupportedPhoneFormats(string phone)
    {
        var request = ValidRequest();
        request.MainPhone = phone;

        Assert.Empty(Validate(request));
    }

    [Fact]
    public void CreateAccount_DoesNotExposeClientAssignedAccountNumber()
    {
        Assert.DoesNotContain(typeof(CreateAccountRequest).GetProperties(), property => property.Name == "AccountNumber");
    }

    [Theory]
    [InlineData("_form.MainPhone")]
    [InlineData("555-CALL-NOW")]
    [InlineData("123")]
    public void CreateAccount_RejectsInvalidPhoneFormats(string phone)
    {
        var request = ValidRequest();
        request.MainPhone = phone;

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(CreateAccountRequest.MainPhone)));
    }

    [Fact]
    public void CreateAccount_AcceptsDatabaseAlignedLengths()
    {
        var request = ValidRequest();
        request.AccountName = new string('A', 300);
        request.MainEmail = $"{new string('a', 287)}@example.com";
        request.Industry = new string('I', 100);

        Assert.Empty(Validate(request));
    }

    private static CreateAccountRequest ValidRequest() => new()
    {
        TenantId = Guid.NewGuid(),
        AccountName = "Contoso",
        AccountTypeCode = "Commercial",
        StatusCode = "Active"
    };

    private static IReadOnlyList<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
