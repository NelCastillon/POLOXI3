using Legal.Application;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Composer confidence ceiling invariant: ComposerConfidence <= StructuredDecisionConfidence.
// DescribeConfidence produces the authoritative descriptor injected into the answer artifact so the
// natural-language composer can never sound more confident than the structured decision state. This
// exercises presentation/projection wording only; it does not touch POLOXI scoring or the pipeline.
public sealed class DecisionConfidenceCeilingTests
{
    [Theory]
    [InlineData(0.05, 1.00)]   // Aspen-style dead heat: narrow margin, max entropy.
    [InlineData(0.05, 0.90)]
    [InlineData(0.09, 0.50)]   // Narrow margin even with contained uncertainty stays narrow.
    public void NarrowOrUncertain_NeverClaimsClearOrDecisive(double margin, double entropy)
    {
        var descriptor = LegalDecisionService.DescribeConfidence(margin, entropy);

        Assert.StartsWith("narrow", descriptor);
        Assert.DoesNotContain("clear", descriptor.Split('—')[0]);
    }

    [Fact]
    public void ModerateMargin_AvoidsClearWording()
    {
        var descriptor = LegalDecisionService.DescribeConfidence(margin: 0.12, entropy: 0.70);

        Assert.StartsWith("moderate", descriptor);
    }

    [Fact]
    public void WideMarginContainedUncertainty_MayBeClear()
    {
        var descriptor = LegalDecisionService.DescribeConfidence(margin: 0.20, entropy: 0.40);

        Assert.StartsWith("clear", descriptor);
    }
}
