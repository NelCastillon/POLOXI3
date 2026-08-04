using Ams.Knowledge.Application.Common.Validation;
using Ams.Knowledge.Application.Features.Knowledge;
using Xunit;

namespace Ams.Application.Tests;

public sealed class KnowledgeRequestValidationTests
{
    [Fact]
    public void Relationship_RejectsSelfReferenceAndInvalidEffectiveRange()
    {
        var tenantId = Guid.NewGuid();
        var conceptId = Guid.NewGuid();
        var command = new AddConceptRelationshipCommand(
            tenantId,
            conceptId,
            "BROADER_THAN",
            conceptId,
            1m,
            "Test",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-1),
            "DRAFT",
            tenantId,
            false,
            "Test invalid relationship",
            "test-correlation",
            Guid.NewGuid());

        var error = Assert.Throws<ApplicationValidationException>(() => RequestValidator.Validate(command));

        Assert.Contains("A concept relationship cannot reference itself.", error.Errors);
        Assert.Contains("Effective-to date must be later than effective-from date.", error.Errors);
    }

    [Fact]
    public void ValidationRule_RejectsInvertedCardinalityAndEmptyTenantContext()
    {
        var command = new CreateKnowledgeValidationRuleCommand(
            Guid.Empty,
            Guid.NewGuid(),
            "RULE.TEST",
            "CARDINALITY",
            "policy.coverages",
            "COUNT",
            null,
            2,
            1,
            "ERROR",
            "At least two coverages are required.",
            DateTime.UtcNow,
            null,
            "DRAFT",
            "Test invalid cardinality",
            "test-correlation",
            Guid.NewGuid());

        var error = Assert.Throws<ApplicationValidationException>(() => RequestValidator.Validate(command));

        Assert.Contains("ContextTenantId is required.", error.Errors);
        Assert.Contains("Minimum count cannot exceed maximum count.", error.Errors);
    }

    [Fact]
    public void Mapping_RejectsOutOfRangeConfidenceAndShortStateCode()
    {
        var command = new UpdateExternalMappingCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "CARRIER",
            null,
            "AUTO",
            "Automobile",
            null,
            "BIDIRECTIONAL",
            "EXACT_EXTERNAL_CODE",
            1.1m,
            "C",
            null,
            null,
            DateTime.UtcNow,
            null,
            "Test invalid mapping",
            "test-correlation",
            Guid.NewGuid(),
            new byte[8]);

        var error = Assert.Throws<ApplicationValidationException>(() => RequestValidator.Validate(command));

        Assert.Contains(error.Errors, message => message.Contains("ConfidenceScore", StringComparison.Ordinal));
        Assert.Contains(error.Errors, message => message.Contains("StateCode", StringComparison.Ordinal));
    }
}
