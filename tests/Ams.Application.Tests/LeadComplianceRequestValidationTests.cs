using System.ComponentModel.DataAnnotations;
using Ams.Application.Features.Leads;
using Xunit;

namespace Ams.Application.Tests;

public sealed class LeadComplianceRequestValidationTests
{
    [Fact]
    public void CreateLeadContact_RejectsWhitespaceNames()
    {
        var request = new CreateLeadContactRequest
        {
            TenantId = Guid.NewGuid(),
            LeadId = Guid.NewGuid(),
            FirstName = "   ",
            LastName = "   "
        };

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.FirstName)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.LastName)));
    }

    [Fact]
    public void CreateLeadContact_RejectsInvalidEmailAndPhone()
    {
        var request = new CreateLeadContactRequest
        {
            TenantId = Guid.NewGuid(),
            LeadId = Guid.NewGuid(),
            FirstName = "Alex",
            LastName = "Morgan",
            Email = "invalid-email",
            Phone = "123"
        };

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.Email)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.Phone)));
    }

    [Fact]
    public void CreatePhoneSuppression_RejectsInvalidPhone()
    {
        var request = new CreatePhoneSuppressionRequest
        {
            TenantId = Guid.NewGuid(),
            LeadId = Guid.NewGuid(),
            PhoneNumber = "123",
            SourceCode = "InternalRequest",
            ReasonCode = "ConsumerRequest",
            ChannelCode = "Call"
        };

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.PhoneNumber)));
    }

    [Fact]
    public void CreatePhoneConsent_RequiresEvidenceReference()
    {
        var request = new CreatePhoneConsentRequest
        {
            TenantId = Guid.NewGuid(),
            LeadId = Guid.NewGuid(),
            PhoneNumber = "(555) 555-0100",
            ConsentTypeCode = "ExpressWritten",
            ChannelCode = "Call",
            PurposeCode = "Marketing",
            LegalBasisCode = "PriorExpressWrittenConsent",
            EvidenceTypeCode = "SignedDocument",
            EvidenceReference = string.Empty
        };

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.EvidenceReference)));
    }

    [Fact]
    public void RecordPhoneScreening_RequiresProviderRegistryAndResult()
    {
        var request = new RecordPhoneScreeningRequest
        {
            TenantId = Guid.NewGuid(),
            PhoneComplianceProfileId = Guid.NewGuid()
        };

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.ProviderCode)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.RegistryCode)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.ResultCode)));
    }

    private static IReadOnlyList<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
