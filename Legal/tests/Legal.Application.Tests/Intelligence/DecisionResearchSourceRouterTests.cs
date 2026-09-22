using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class DecisionResearchSourceRouterTests
{
    private readonly DecisionResearchSourceRouter _router = new();

    [Fact]
    public void MatterEvidence_RoutesToMatterCorpus_WithDocumentFilter()
    {
        var need = Need(DecisionResearchNeedTypes.MatterEvidence, DecisionResearchSourceClasses.MatterDocument) with
        {
            RequiredEvidenceKind = "MEDICAL_RECORD,MEDICAL_BILL"
        };

        var route = _router.Route(need, "California");

        Assert.Equal(DecisionResearchRouteCodes.MatterCorpus, route.RouteCode);
        Assert.True(route.RetrievalRequired);
        Assert.Equal(["MEDICAL_RECORD", "MEDICAL_BILL"], route.DocumentTypeCodes);
        Assert.Empty(route.AuthorityKinds);
    }

    [Fact]
    public void LegalRule_RoutesToLegalAuthority_WithAuthorityFilters()
    {
        var need = Need(DecisionResearchNeedTypes.LegalRule, DecisionResearchSourceClasses.LegalAuthority) with
        {
            AuthorityKindsJson = "[\"CASE_LAW\",\"STATUTE\"]"
        };
        var cutoff = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var route = _router.Route(need, "California", cutoff);

        Assert.Equal(DecisionResearchRouteCodes.LegalAuthority, route.RouteCode);
        Assert.True(route.RetrievalRequired);
        Assert.Equal("California", route.Jurisdiction);
        Assert.Equal(cutoff, route.AuthorityCutoffDate);
        Assert.Equal(["CASE_LAW", "STATUTE"], route.AuthorityKinds);
    }

    [Theory]
    [InlineData(DecisionResearchNeedTypes.Application)]
    [InlineData(DecisionResearchNeedTypes.Derived)]
    public void DerivedNeeds_DoNotRetrieve(string needType)
    {
        var need = Need(needType, DecisionResearchSourceClasses.None) with
        {
            IsResearchable = false,
            ApplicationDeferred = true
        };

        var route = _router.Route(need, null);

        Assert.Equal(DecisionResearchRouteCodes.NoneDerived, route.RouteCode);
        Assert.False(route.RetrievalRequired);
    }

    private static DecisionResearchNeedPersistence Need(string type, string sourceClass) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), null,
        "Issue", "Proposition", null, null, "Why", 0.5m, 0.5m, 0.5m, null, "OPEN")
    {
        ResearchNeedTypeCode = type,
        SourceClassCode = sourceClass,
        IsResearchable = true,
        SearchQuery = "query"
    };
}
