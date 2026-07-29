using System.ComponentModel.DataAnnotations;
using Ams.Application.Features.Submissions;
using Xunit;

namespace Ams.Application.Tests;

public sealed class ClientAcceptanceRequestValidationTests
{
    [Fact]
    public void AcceptedDecision_RequiresElectionsConsentsAndEvidence()
    {
        var request = CreateValidRequest("Accepted");

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.CoverageElections)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.Consents)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.AuthorizationReference)));
    }

    [Fact]
    public void AcceptedDecision_RejectsDuplicateQuoteLineElectionsAndUnacceptedConsent()
    {
        var quoteLineId = Guid.NewGuid();
        var request = CreateValidRequest("Accepted");
        request.AuthorizationReference = "Client portal confirmation";
        request.CoverageElections =
        [
            new() { QuoteLineId = quoteLineId, ElectionCode = "Accepted" },
            new() { QuoteLineId = quoteLineId, ElectionCode = "Rejected" }
        ];
        request.Consents = [new() { ConsentCode = "TermsReviewed", ConsentVersion = "1.0", IsAccepted = false }];

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.CoverageElections)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.Consents)));
    }

    [Fact]
    public void NonAcceptedDecision_RequiresNotesButNotAcceptanceEvidence()
    {
        var request = CreateValidRequest("Declined");

        var results = Validate(request);

        Assert.Single(results, result => result.MemberNames.Contains(nameof(request.DecisionNotes)));
        request.DecisionNotes = "Client declined the proposed terms.";
        Assert.Empty(Validate(request));
    }

    [Fact]
    public void AcceptedDecision_WithCompleteEvidence_IsValid()
    {
        var request = CreateValidRequest("Accepted");
        request.AuthorizationReference = "Signed email received";
        request.CoverageElections = [new() { QuoteLineId = Guid.NewGuid(), ElectionCode = "Accepted" }];
        request.Consents = [new() { ConsentCode = "TermsReviewed", ConsentVersion = "1.0", IsAccepted = true }];

        Assert.Empty(Validate(request));
    }

    private static RecordClientAcceptanceRequest CreateValidRequest(string decisionCode) => new()
    {
        TenantId = Guid.NewGuid(),
        ProposalId = Guid.NewGuid(),
        ProposalVersionNumber = 1,
        QuoteId = Guid.NewGuid(),
        QuoteFingerprint = new string('a', 64),
        DecisionCode = decisionCode,
        AuthorizationMethodCode = "Email",
        AuthorizedByName = "Alex Morgan",
        AuthorizedByTitle = "Owner",
        AuthorityBasisCode = "NamedInsured",
        AuthorizedDateUtc = DateTime.UtcNow,
        IdempotencyKey = Guid.NewGuid().ToString("N")
    };

    private static IReadOnlyList<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
