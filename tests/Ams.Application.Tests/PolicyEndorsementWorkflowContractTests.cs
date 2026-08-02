using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyEndorsements;
using Xunit;

namespace Ams.Application.Tests;

public sealed class PolicyEndorsementWorkflowContractTests
{
    [Fact]
    public void Change_RequiresExactlyOneTypedValue()
    {
        var change = new PolicyEndorsementChangeInput
        {
            CategoryCode = "Vehicle",
            OperationCode = "Update",
            Vehicle = new PolicyEndorsementVehicleChangeDto(),
            Driver = new PolicyEndorsementDriverChangeDto()
        };

        var results = Validate(change);

        Assert.Contains(results, result => result.ErrorMessage == "Exactly one typed endorsement change is required.");
    }

    [Fact]
    public void Change_RequiresCategoryToMatchTypedValue()
    {
        var change = new PolicyEndorsementChangeInput
        {
            CategoryCode = "Driver",
            OperationCode = "Update",
            Vehicle = new PolicyEndorsementVehicleChangeDto()
        };

        var results = Validate(change);

        Assert.Contains(results, result => result.ErrorMessage == "CategoryCode must be 'Vehicle' for the supplied typed change.");
    }

    [Theory]
    [MemberData(nameof(ConcurrencyRequests))]
    public void WorkflowMutations_RequireConcurrencyTokens(object request)
    {
        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Any(name => name.Contains("RowVersion", StringComparison.Ordinal)));
    }

    public static TheoryData<object> ConcurrencyRequests => new()
    {
        new SavePolicyEndorsementDraftRequest(),
        new TransitionPolicyEndorsementRequest(),
        new DecidePolicyEndorsementApprovalRequest(),
        new ReversePolicyEndorsementRequest()
    };

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
