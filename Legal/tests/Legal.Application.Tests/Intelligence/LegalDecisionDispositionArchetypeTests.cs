using System.Reflection;
using Legal.Application.Features.Intelligence;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────
// Disposition-archetype gate — unit coverage for the DB-backed LPI scoring model.
//
// Proves the two pure static helpers that drive the Wide2 legal-disposition
// composite (migration 0450 wiring):
//   • TryParseArchetypeCode maps the DB ArchetypeCode strings to the private
//     DispositionArchetype enum (case/whitespace insensitive) and fails soft to
//     NEUTRAL=false for unknown/empty codes, so a mis-seeded row never throws.
//   • ComputeGatedDispositionComposite gates the element-satisfaction vector by
//     archetype so the SAME evidence vector produces DIFFERENT, legally-correct
//     scores: a weak element caps PLAINTIFF_MERITS (conjunctive), lifts
//     DEFENSE_MERITS (disjunctive over failures), peaks SETTLEMENT on mixed
//     elements, and NEUTRAL stays a plain membership-weighted average.
// Both members are private static, reached by reflection (no service instance
// or dependencies required — the logic is pure).
// ─────────────────────────────────────────────────────────────────────────────
public sealed class LegalDecisionDispositionArchetypeTests
{
    private static readonly Type ServiceType =
        typeof(Legal.Application.IntelligenceWide2Service);

    private static readonly Type ArchetypeEnumType =
        ServiceType.GetNestedType("DispositionArchetype", BindingFlags.NonPublic)!;

    private static readonly MethodInfo TryParse =
        ServiceType.GetMethod("TryParseArchetypeCode", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo ComputeGated =
        ServiceType.GetMethod("ComputeGatedDispositionComposite", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static (bool Ok, string Archetype) InvokeTryParse(string? code)
    {
        var args = new object?[] { code, null };
        var ok = (bool)TryParse.Invoke(null, args)!;
        return (ok, args[1]!.ToString()!);
    }

    private static decimal InvokeGatedComposite(string archetypeName, IReadOnlyList<(WideBranchRecord Branch, decimal Score)> scored)
    {
        var archetype = Enum.Parse(ArchetypeEnumType, archetypeName);
        var weights = scored.ToDictionary(s => s.Branch.WideBranchId, _ => 1m);
        var result = ComputeGated.Invoke(null, [archetype, scored, weights])!;
        // result is a ValueTuple<decimal,string,string>; Item1 is the composite.
        return (decimal)result.GetType().GetField("Item1")!.GetValue(result)!;
    }

    private static WideBranchRecord Element(int level = 1) => new(
        WideBranchId: Guid.NewGuid(),
        WideExecutionId: Guid.NewGuid(),
        ParentWideBranchId: null,
        TenantId: Guid.NewGuid(),
        LevelNumber: level,
        BranchCode: "B",
        DisplayName: "element",
        Interpretation: "",
        CapabilityCode: null,
        SearchText: null,
        GroundingStatusCode: "GROUNDED",
        EvidenceCount: 0,
        Confidence: 0m,
        ContinueNarrowing: false,
        StopReason: null,
        IsEliminated: false,
        EliminationReason: null,
        SortOrder: 0);

    private static IReadOnlyList<(WideBranchRecord, decimal)> Vector(params decimal[] scores)
        => scores.Select(s => (Element(), s)).ToArray();

    [Theory]
    [InlineData("PLAINTIFF_MERITS", "PlaintiffMerits")]
    [InlineData("DEFENSE_MERITS", "DefenseMerits")]
    [InlineData("SETTLEMENT", "Settlement")]
    [InlineData("NEUTRAL", "Neutral")]
    [InlineData("  plaintiff_merits  ", "PlaintiffMerits")] // trimmed + case-insensitive
    public void TryParseArchetypeCode_KnownCodes_ParseToEnum(string code, string expected)
    {
        var (ok, archetype) = InvokeTryParse(code);

        Assert.True(ok);
        Assert.Equal(expected, archetype);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NOT_A_REAL_CODE")]
    public void TryParseArchetypeCode_UnknownCodes_FailSoftToNeutral(string? code)
    {
        var (ok, archetype) = InvokeTryParse(code);

        Assert.False(ok);
        Assert.Equal("Neutral", archetype);
    }

    [Fact]
    public void PlaintiffMerits_IsConjunctive_WeakElementCapsComposite()
    {
        // Three strong elements but one weak (0.20) element. Conjunctive gate must
        // pull the composite DOWN toward the weakest, not average it away.
        var composite = InvokeGatedComposite("PlaintiffMerits", Vector(0.90m, 0.85m, 0.20m));

        Assert.True(composite < 0.50m, $"weak element must cap plaintiff merits; was {composite}");
    }

    [Fact]
    public void DefenseMerits_IsComplementOfPlaintiff_SameWeakElementLiftsComposite()
    {
        var vector = Vector(0.90m, 0.85m, 0.20m);
        var plaintiff = InvokeGatedComposite("PlaintiffMerits", vector);
        var defense = InvokeGatedComposite("DefenseMerits", vector);

        // Defense is the complement of the plaintiff conjunction on the same vector.
        Assert.True(defense > 0.50m, $"a failed element must favor defense; was {defense}");
        Assert.Equal(1m, Math.Round(plaintiff + defense, 2));
    }

    [Fact]
    public void Settlement_PeaksOnMixedElements_LowOnDecisiveElements()
    {
        var mixed = InvokeGatedComposite("Settlement", Vector(0.50m, 0.50m, 0.50m));
        var decisive = InvokeGatedComposite("Settlement", Vector(0.95m, 0.05m, 0.95m));

        Assert.True(mixed > decisive, $"settlement tension must peak on balanced elements; mixed={mixed} decisive={decisive}");
        Assert.True(mixed > 0.90m, $"fully-balanced elements should near-maximize settlement tension; was {mixed}");
    }

    [Fact]
    public void Neutral_IsMembershipWeightedAverage()
    {
        // Equal weights => plain mean of the element scores.
        var composite = InvokeGatedComposite("Neutral", Vector(0.40m, 0.60m, 0.80m));

        Assert.Equal(0.60m, Math.Round(composite, 2));
    }

    [Fact]
    public void EmptyVector_ReturnsZeroComposite()
    {
        var composite = InvokeGatedComposite("PlaintiffMerits", []);

        Assert.Equal(0m, composite);
    }
}
