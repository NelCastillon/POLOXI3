using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class DomainPackEvidencePresenceMatcherTests
{
    private static readonly DecisionDomainPackEvidenceTypeDto Surveillance =
        new("SURVEILLANCE_VIDEO", "Surveillance Video", "LIABILITY", "Store, traffic, or dashcam surveillance footage capturing the incident.");

    private static readonly DecisionDomainPackEvidenceTypeDto LifeCarePlan =
        new("LIFE_CARE_PLAN", "Life Care Plan", "DAMAGES_FUTURE", "Certified planner projection of lifetime future care needs and costs.");

    private static readonly HashSet<string> NoDirect = new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Direct_Document_Classification_Admits_Regardless_Of_Text()
    {
        var tokens = DomainPackEvidencePresenceMatcher.NormalizeCorpusTokens("unrelated text").ToHashSet();
        var direct = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LIFE_CARE_PLAN" };

        Assert.True(DomainPackEvidencePresenceMatcher.IsPresent(LifeCarePlan, tokens, direct));
    }

    [Fact]
    public void Matter_Description_Naming_Evidence_Admits_By_Name()
    {
        var tokens = DomainPackEvidencePresenceMatcher.NormalizeCorpusTokens(
            "Description: the store's surveillance camera recorded the fall.").ToHashSet();

        Assert.True(DomainPackEvidencePresenceMatcher.IsPresent(Surveillance, tokens, NoDirect));
    }

    [Fact]
    public void Catalog_Description_Terms_Admit_When_Two_Specific_Terms_Present()
    {
        var tokens = DomainPackEvidencePresenceMatcher.NormalizeCorpusTokens(
            "Dashcam footage from the following vehicle was preserved.").ToHashSet();

        Assert.True(DomainPackEvidencePresenceMatcher.IsPresent(Surveillance, tokens, NoDirect));
    }

    [Fact]
    public void Unrelated_Matter_Does_Not_Admit()
    {
        var tokens = DomainPackEvidencePresenceMatcher.NormalizeCorpusTokens(
            "Rear-end collision; plaintiff treated at urgent care; wage loss claimed.").ToHashSet();

        Assert.False(DomainPackEvidencePresenceMatcher.IsPresent(Surveillance, tokens, NoDirect));
        Assert.False(DomainPackEvidencePresenceMatcher.IsPresent(LifeCarePlan, tokens, NoDirect));
    }

    [Fact]
    public void Empty_Corpus_Admits_Nothing_Without_Direct_Codes()
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);

        Assert.False(DomainPackEvidencePresenceMatcher.IsPresent(Surveillance, tokens, NoDirect));
    }
}
