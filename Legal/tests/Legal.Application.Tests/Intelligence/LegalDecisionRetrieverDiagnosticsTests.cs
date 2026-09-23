using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Infrastructure.Intelligence;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class LegalDecisionRetrieverDiagnosticsTests
{
    [Fact]
    public void RequestedStatute_RetainsClassifiedStatuteAndRejectsCaseLaw()
    {
        var statute = Snippet("STATUTE", "https://delcode.delaware.gov/title10/c081/sc01/index.html#8132");
        var caseLaw = Snippet("CASE_LAW", "https://www.courtlistener.com/opinion/1/example/");
        var request = new DecisionRetrievalRequest("LEGAL", "10 Del. C. § 8132", 5)
        {
            AuthorityKinds = ["STATUTE"],
        };

        var result = LegalDecisionRetriever.ApplyFilters(request, Retrieval(statute, caseLaw));

        var source = Assert.Single(result.Sources);
        Assert.Equal(EvidenceSourceType.Statute, source.SourceType);
        Assert.Equal(statute.Url, source.SourceRef);
        Assert.Equal(2, result.RawResultCount);
        Assert.Equal(1, result.AuthorityKindFilteredCount);
        Assert.Equal(0, result.AuthorityDateFilteredCount);
    }

    [Fact]
    public void MissingAuthorityClassification_RemainsRejectedByStrictFilter()
    {
        var unclassified = Snippet(null, "https://authority.example/unclassified");
        var request = new DecisionRetrievalRequest("LEGAL", "Delaware comparative negligence", 5)
        {
            AuthorityKinds = ["STATUTE"],
        };

        var result = LegalDecisionRetriever.ApplyFilters(request, Retrieval(unclassified));

        Assert.Empty(result.Sources);
        Assert.Equal(1, result.RawResultCount);
        Assert.Equal(1, result.AuthorityKindFilteredCount);
    }

    [Fact]
    public void AuthorityCutoff_RejectsOnlySourcesRetrievedAfterCutoff()
    {
        var before = Snippet("CASE_LAW", "https://authority.example/before") with { RetrievedDateUtc = new DateTime(2024, 1, 1) };
        var after = Snippet("CASE_LAW", "https://authority.example/after") with { RetrievedDateUtc = new DateTime(2026, 1, 1) };
        var request = new DecisionRetrievalRequest("LEGAL", "Delaware comparative negligence", 5)
        {
            AuthorityKinds = ["CASE_LAW"],
            AuthorityCutoffDate = new DateTime(2025, 1, 1),
        };

        var result = LegalDecisionRetriever.ApplyFilters(request, Retrieval(before, after));

        Assert.Single(result.Sources);
        Assert.Equal(0, result.AuthorityKindFilteredCount);
        Assert.Equal(1, result.AuthorityDateFilteredCount);
    }

    private static LegalRetrievalResult Retrieval(params WideExternalKnowledgeSnippet[] snippets) =>
        new(snippets, [new("TEST", true, "SUCCEEDED", snippets.Length, snippets.Length)]);

    private static WideExternalKnowledgeSnippet Snippet(string? authorityKind, string url) =>
        new("query", "Authority", url, "Delaware comparative negligence authority", 1m, DateTime.UtcNow)
        {
            AuthorityKind = authorityKind,
            SourceProvider = "TEST",
            SourceVersion = "TEST_V1",
            ProviderIdentityVerified = true,
        };
}
