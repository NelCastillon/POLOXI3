using System.Reflection;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────
// Deterministic Candidate-Landscape narrative grounding — unit coverage.
//
// The deterministic fallback that guarantees every Overview candidate card gets a
// short + long narrative must CONNECT each outcome to the real matter instead of
// emitting a bare score recap. These tests prove the pure static grounding helpers
// that supply that connection:
//   • GroundingTokens builds a normalized token set from a candidate name plus its
//     strongest branch labels.
//   • MatchMatterFields pulls only the saved matter facts that are topically
//     related to the outcome (token overlap), ranked by overlap.
//   • MatchAdmittedEvidence pulls only the admitted documents related to the
//     outcome (token overlap OR matched-branch overlap), ranked by relevance.
//   • TopInterpretiveItems surfaces the proposition-level findings for the
//     outcome's scored branches, ranked by score.
// All members are private static on IntelligenceWide2Service, reached by reflection
// (pure logic — no service instance or dependencies required).
// ─────────────────────────────────────────────────────────────────────────────
public sealed class LegalDecisionNarrativeGroundingTests
{
    private static readonly Type ServiceType = typeof(Legal.Application.IntelligenceWide2Service);

    private static MethodInfo Method(string name) =>
        ServiceType.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(ServiceType.FullName, name);

    private static HashSet<string> InvokeGroundingTokens(string candidate, IEnumerable<string> branches) =>
        (HashSet<string>)Method("GroundingTokens").Invoke(null, [candidate, branches])!;

    private static IReadOnlyList<MatterContextField> InvokeMatchMatterFields(
        IReadOnlyList<MatterContextField> fields, HashSet<string> tokens, int max) =>
        (IReadOnlyList<MatterContextField>)Method("MatchMatterFields").Invoke(null, [fields, tokens, max])!;

    private static IReadOnlyList<PoloxiEvidenceDto> InvokeMatchAdmittedEvidence(
        IReadOnlyList<PoloxiEvidenceDto> evidence, HashSet<string> tokens, IEnumerable<string> branches, int max) =>
        (IReadOnlyList<PoloxiEvidenceDto>)Method("MatchAdmittedEvidence").Invoke(null, [evidence, tokens, branches, max])!;

    private static IReadOnlyList<WideInterpretiveResultItemDto> InvokeTopInterpretiveItems(
        IReadOnlyCollection<WideInterpretiveResultDto> results, IEnumerable<string> branches, int max) =>
        (IReadOnlyList<WideInterpretiveResultItemDto>)Method("TopInterpretiveItems").Invoke(null, [results, branches, max])!;

    private static MatterContextField Field(string label, string? value, MatterFieldProvenance provenance = MatterFieldProvenance.Supplied) =>
        new(label, value, provenance);

    private static PoloxiEvidenceDto Evidence(string title, string? excerpt, decimal relevance, IReadOnlyCollection<string>? matchedBranches = null) =>
        new(
            HierarchyBranchId: Guid.NewGuid(),
            SearchDocumentId: Guid.NewGuid(),
            EntityTypeCode: "DOC",
            EntityId: Guid.NewGuid(),
            ModuleCode: "DMS",
            Title: title,
            Excerpt: excerpt,
            NavigationRoute: null,
            RelevanceScore: relevance,
            RankNumber: 1,
            MatchedBranches: matchedBranches ?? []);

    private static WideInterpretiveResultDto Interp(string branch, params WideInterpretiveResultItemDto[] items) =>
        new(branch, $"{branch} interpretation", 0.9m, items);

    // ── GroundingTokens ──────────────────────────────────────────────────────

    [Fact]
    public void GroundingTokens_DropsShortNoiseWords_AndIncludesBranchTerms()
    {
        var tokens = InvokeGroundingTokens("Defense-favorable judgment", ["Liability", "Damages"]);

        Assert.Contains("liability", tokens);
        // The normalizer stems words, so "Damages" becomes "damag"; the branch term is present.
        Assert.Contains(tokens, t => t.StartsWith("damag", StringComparison.Ordinal));
        Assert.Contains("judgment", tokens);
        // 3-char-or-shorter tokens are excluded as noise.
        Assert.DoesNotContain(tokens, t => t.Length <= 3);
    }

    // ── MatchMatterFields ────────────────────────────────────────────────────

    [Fact]
    public void MatchMatterFields_ReturnsOnlyTopicallyRelatedFields_RankedByOverlap()
    {
        var fields = new List<MatterContextField>
        {
            Field("Liability theory", "Negligence and breach of duty by the defendant"),
            Field("Damages claimed", "Past medical and future damages"),
            Field("Favorite color", "Blue"),
        };
        var tokens = InvokeGroundingTokens("Plaintiff-favorable verdict", ["Liability", "Damages"]);

        var matched = InvokeMatchMatterFields(fields, tokens, 4);

        Assert.NotEmpty(matched);
        Assert.Contains(matched, f => f.Label == "Liability theory");
        Assert.Contains(matched, f => f.Label == "Damages claimed");
        Assert.DoesNotContain(matched, f => f.Label == "Favorite color");
    }

    [Fact]
    public void MatchMatterFields_ReturnsEmpty_WhenNoTokensOrNoFields()
    {
        var fields = new List<MatterContextField> { Field("Liability", "Negligence") };
        Assert.Empty(InvokeMatchMatterFields(fields, [], 4));
        Assert.Empty(InvokeMatchMatterFields([], InvokeGroundingTokens("Liability", []), 4));
    }

    // ── MatchAdmittedEvidence ────────────────────────────────────────────────

    [Fact]
    public void MatchAdmittedEvidence_MatchesOnBranchName_EvenWithoutTitleOverlap()
    {
        var evidence = new List<PoloxiEvidenceDto>
        {
            Evidence("Itemized medical records", null, 0.80m, matchedBranches: ["Damages"]),
            Evidence("Unrelated memo", "nothing relevant", 0.99m),
        };
        // Tokens deliberately do NOT include "medical"/"records"; linkage comes from MatchedBranches.
        var tokens = InvokeGroundingTokens("Settlement", []);

        var matched = InvokeMatchAdmittedEvidence(evidence, tokens, ["Damages", "Liability"], 4);

        Assert.Single(matched);
        Assert.Equal("Itemized medical records", matched[0].Title);
    }

    [Fact]
    public void MatchAdmittedEvidence_RanksByHitsThenRelevance()
    {
        var evidence = new List<PoloxiEvidenceDto>
        {
            Evidence("Liability expert report on negligence", "negligence and breach", 0.50m, matchedBranches: ["Liability"]),
            Evidence("Liability note", "negligence", 0.95m),
        };
        var tokens = InvokeGroundingTokens("Plaintiff verdict", ["Liability"]);

        var matched = InvokeMatchAdmittedEvidence(evidence, tokens, ["Liability"], 4);

        Assert.Equal(2, matched.Count);
        // The report scores more token + branch hits, so it outranks the higher-relevance note.
        Assert.Equal("Liability expert report on negligence", matched[0].Title);
    }

    // ── TopInterpretiveItems ─────────────────────────────────────────────────

    [Fact]
    public void TopInterpretiveItems_FiltersToScoredBranches_AndOrdersByScore()
    {
        var results = new List<WideInterpretiveResultDto>
        {
            Interp("Liability",
                new WideInterpretiveResultItemDto(2, "Breach of duty", "clear breach", 0.70m),
                new WideInterpretiveResultItemDto(1, "Negligence liability", "strong", 0.92m)),
            Interp("Procedure",
                new WideInterpretiveResultItemDto(1, "Jurisdiction", "irrelevant here", 0.99m)),
        };

        var items = InvokeTopInterpretiveItems(results, ["Liability"], 4);

        Assert.Equal(2, items.Count);
        Assert.Equal("Negligence liability", items[0].Name); // highest score first
        Assert.DoesNotContain(items, it => it.Name == "Jurisdiction"); // excluded branch
    }

    [Fact]
    public void TopInterpretiveItems_RespectsMaxCount()
    {
        var results = new List<WideInterpretiveResultDto>
        {
            Interp("Damages",
                new WideInterpretiveResultItemDto(1, "A", "", 0.9m),
                new WideInterpretiveResultItemDto(2, "B", "", 0.8m),
                new WideInterpretiveResultItemDto(3, "C", "", 0.7m)),
        };

        var items = InvokeTopInterpretiveItems(results, ["Damages"], 2);

        Assert.Equal(2, items.Count);
    }
}
