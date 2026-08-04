using Ams.Knowledge.Domain.Common;
using Ams.Knowledge.Domain.Concepts;
using Xunit;

namespace Ams.Application.Tests;

public sealed class KnowledgeDomainTests
{
    [Fact]
    public void EnsureCanAddParent_RejectsTransitiveCycle()
    {
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();
        var grandchild = Guid.NewGuid();
        var edges = new[] { (parent, child), (child, grandchild) };

        var exception = Assert.Throws<KnowledgeDomainException>(() =>
            ConceptHierarchy.EnsureCanAddParent(parent, grandchild, edges));

        Assert.Contains("cycle", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureCanAddParent_AllowsUnrelatedParent()
    {
        var existingParent = Guid.NewGuid();
        var child = Guid.NewGuid();
        var proposedParent = Guid.NewGuid();

        ConceptHierarchy.EnsureCanAddParent(child, proposedParent, [(existingParent, child)]);
    }

    [Fact]
    public void PublishedConcept_RejectsRevision()
    {
        var concept = CreateConcept("PUBLISHED");

        var exception = Assert.Throws<KnowledgeDomainException>(() => concept.ReviseDraft(
            "COVERAGE", "Changed", null, null, false, true, DateTime.UtcNow, null,
            Guid.NewGuid(), Guid.NewGuid(), null, "ISO", null, Guid.NewGuid(), DateTime.UtcNow));

        Assert.Contains("immutable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddLabel_RejectsDuplicateNormalizedLanguage()
    {
        var tenantId = Guid.NewGuid();
        var concept = CreateConcept("DRAFT", tenantId);
        concept.AddLabel(CreateLabel(concept.Id, tenantId, "Commercial   Auto"));

        var exception = Assert.Throws<KnowledgeDomainException>(() =>
            concept.AddLabel(CreateLabel(concept.Id, tenantId, " commercial auto ")));

        Assert.Contains("same normalized value", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static KnowledgeConcept CreateConcept(string status, Guid? tenantId = null)
    {
        var userId = Guid.NewGuid();
        return new KnowledgeConcept(Guid.NewGuid(), Guid.NewGuid(), "COMMERCIAL_AUTO", "PRODUCT",
            "Commercial Auto", "Commercial automobile insurance", null, false, true, status,
            DateTime.UtcNow, null, 1, null, tenantId, tenantId is null, userId, userId, null,
            "Internal governance", null, userId, DateTime.UtcNow);
    }

    private static ConceptLabel CreateLabel(Guid conceptId, Guid tenantId, string value)
        => new(Guid.NewGuid(), conceptId, value, "ALTERNATIVE", "en", "Test", true, false,
            tenantId, false, Guid.NewGuid(), DateTime.UtcNow);
}
