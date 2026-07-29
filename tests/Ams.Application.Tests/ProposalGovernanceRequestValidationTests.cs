using System.ComponentModel.DataAnnotations;
using Ams.Application.Features.Submissions;
using Xunit;

namespace Ams.Application.Tests;

public sealed class ProposalGovernanceRequestValidationTests
{
    [Fact]
    public void ReviewDecision_RejectsUnsupportedState()
    {
        var request = new DecideProposalReviewRequest(Guid.NewGuid(), "Delivered", "Invalid governance transition.");

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.DecisionCode)));
    }

    [Fact]
    public void SignerRecipient_MustBeMarkedAsSigner()
    {
        var request = new UpsertProposalRecipientRequest(Guid.NewGuid(), null, null, "Signer", "Alex Morgan", "alex@example.com", 1, true, false);

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.IsSigner)));
    }

    [Fact]
    public void Callback_RequiresProviderEventPayloadAndSignature()
    {
        var request = new ProposalProviderCallbackRequest(Guid.NewGuid(), "", "", null, "", "", "", "");

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.ProviderCode)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.ProviderEventId)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.PayloadJson)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(request.SignatureHeader)));
    }

    [Fact]
    public void SlaEscalation_CannotPrecedeDueTime()
    {
        var request = new UpsertProposalSlaPolicyRequest(Guid.NewGuid(), "ReviewPending", 120, 60, "High", "PROPOSAL_REVIEW", true);

        Assert.Contains(Validate(request), result => result.MemberNames.Contains(nameof(request.EscalateAfterMinutes)));
    }

    private static IReadOnlyList<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}