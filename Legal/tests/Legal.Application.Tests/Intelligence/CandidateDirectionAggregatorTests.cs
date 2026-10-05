using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Gap 3 — per-candidate NET direction aggregation. A single retrieved proposition can reach the SAME
// owning candidate from several placements; the old first-wins dedup silently discarded conflicting
// directions and was order-dependent. These tests pin the deterministic aggregation:
//   • agreeing contributions keep their shared direction,
//   • opposing contributions are CONTESTED → neutral RequiresEvaluation (reopen, zero ranking bias),
//   • any neutral qualifier forces the candidate neutral,
//   • severity escalates to Material if ANY contributor is material,
//   • the result is ORDER-INDEPENDENT.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class CandidateDirectionAggregatorTests
{
    private static CandidateDirectionAggregator New() => new("CAND_A", "Candidate A");

    [Fact]
    public void SingleStrengthen_ResolvesStrengthened()
    {
        var agg = New().Add(CandidateDirectionTokens.Strengthened, isMaterial: false, "n1:Supports");

        Assert.False(agg.IsContested);
        Assert.Equal(CandidateDirectionTokens.Strengthened, agg.ResolveNetDirection());
        Assert.False(agg.IsMaterial);
    }

    [Fact]
    public void MultipleAgreeingStrengthen_StaysStrengthened()
    {
        var agg = New()
            .Add(CandidateDirectionTokens.Strengthened, false, "n1:Supports")
            .Add(CandidateDirectionTokens.Strengthened, false, "n2:Supports");

        Assert.False(agg.IsContested);
        Assert.Equal(CandidateDirectionTokens.Strengthened, agg.ResolveNetDirection());
    }

    [Fact]
    public void MultipleAgreeingWeaken_StaysWeakened()
    {
        var agg = New()
            .Add(CandidateDirectionTokens.Weakened, false, "n1:Contradicts")
            .Add(CandidateDirectionTokens.Weakened, false, "n2:Contradicts");

        Assert.False(agg.IsContested);
        Assert.Equal(CandidateDirectionTokens.Weakened, agg.ResolveNetDirection());
    }

    [Fact]
    public void OpposingContributions_AreContested_ResolveRequiresEvaluation()
    {
        var agg = New()
            .Add(CandidateDirectionTokens.Strengthened, false, "n1:Supports")
            .Add(CandidateDirectionTokens.Weakened, false, "n2:Contradicts");

        Assert.True(agg.IsContested);
        Assert.Equal(CandidateDirectionTokens.RequiresEvaluation, agg.ResolveNetDirection());
    }

    [Fact]
    public void OpposingContributions_AreOrderIndependent()
    {
        var forward = New()
            .Add(CandidateDirectionTokens.Strengthened, false, "n1:Supports")
            .Add(CandidateDirectionTokens.Weakened, false, "n2:Contradicts");
        var reverse = New()
            .Add(CandidateDirectionTokens.Weakened, false, "n2:Contradicts")
            .Add(CandidateDirectionTokens.Strengthened, false, "n1:Supports");

        Assert.Equal(forward.ResolveNetDirection(), reverse.ResolveNetDirection());
        Assert.Equal(CandidateDirectionTokens.RequiresEvaluation, forward.ResolveNetDirection());
    }

    [Fact]
    public void NeutralQualifier_MixedWithDirectional_StaysNeutral()
    {
        var agg = New()
            .Add(CandidateDirectionTokens.Strengthened, false, "n1:Supports")
            .Add(CandidateDirectionTokens.RequiresEvaluation, false, "n2:Qualifies");

        Assert.False(agg.IsContested);
        Assert.Equal(CandidateDirectionTokens.RequiresEvaluation, agg.ResolveNetDirection());
    }

    [Fact]
    public void Severity_EscalatesToMaterial_WhenAnyContributorMaterial()
    {
        var agg = New()
            .Add(CandidateDirectionTokens.Strengthened, isMaterial: false, "n1:Supports")
            .Add(CandidateDirectionTokens.Weakened, isMaterial: true, "n2:Contradicts");

        Assert.True(agg.IsMaterial);
    }

    [Fact]
    public void ContestedRationale_ExplainsNeutralReopen()
    {
        var agg = New()
            .Add(CandidateDirectionTokens.Strengthened, false, "n1:Supports")
            .Add(CandidateDirectionTokens.Weakened, false, "n2:Contradicts");

        var rationale = agg.BuildRationale(agg.ResolveNetDirection());

        Assert.Contains("CONTESTED", rationale);
        Assert.Contains("without biasing the ranking", rationale);
    }
}
