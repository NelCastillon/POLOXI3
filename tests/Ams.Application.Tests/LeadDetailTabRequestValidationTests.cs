using System.ComponentModel.DataAnnotations;
using Ams.Application.Features.LeadActivities;
using Ams.Application.Features.Leads;
using Xunit;

namespace Ams.Application.Tests;

public sealed class LeadDetailTabRequestValidationTests
{
    [Fact]
    public void Contact_ValidatesOnlyContactFields()
    {
        var request = new CreateLeadContactRequest
        {
            FirstName = "Alex",
            LastName = "Morgan",
            Email = "alex@example.com",
            Phone = "(555) 555-0100"
        };

        Assert.Empty(Validate(request));
    }

    [Theory]
    [MemberData(nameof(InvalidPopupRequests))]
    public void PopupRequest_RejectsItsOwnMissingRequiredFields(object request)
    {
        Assert.NotEmpty(Validate(request));
    }

    public static TheoryData<object> InvalidPopupRequests => new()
    {
        new CreateLeadContactRequest(),
        new CreateLeadInterestLineRequest { LineOfBusiness = string.Empty, Priority = string.Empty },
        new CreateLeadActivityRequest { ActivityTypeCode = string.Empty, Subject = string.Empty },
        new CreateLeadCommunicationRequest
        {
            Channel = string.Empty,
            Subject = string.Empty,
            Preview = string.Empty,
            Direction = string.Empty,
            DeliveryStatus = string.Empty,
            EngagementStatus = string.Empty
        },
        new CreateLeadCampaignEnrollmentRequest
        {
            CampaignName = string.Empty,
            CampaignType = string.Empty,
            Segment = string.Empty,
            Status = string.Empty
        },
        new CreateLeadDocumentRequest
        {
            FileName = string.Empty,
            Extension = string.Empty,
            Category = string.Empty
        }
    };

    private static IReadOnlyList<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
