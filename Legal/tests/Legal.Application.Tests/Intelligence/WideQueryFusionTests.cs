using Legal.Application;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Guards the continuation-query fusion contract that a free-text follow-up depends on.
// Regression: the Blazor follow-up composer must send the follow-up text as ClarificationAnswer so
// the Wide service fuses it onto the parent's ORIGINAL question (keeping "legal AI conferences in
// California" in scope) instead of reasoning about the bare follow-up ("include dates and location")
// in isolation. These tests lock the deterministic fusion the pipeline relies on.
public sealed class WideQueryFusionTests
{
    [Fact]
    public void Compose_FreeTextFollowUp_FusesOntoOriginalQuery()
    {
        var fused = WideQueryFusion.Compose(
            "list all incoming legal AI conferences across California",
            clarificationTarget: null,
            clarificationAnswer: "can you include the dates and exact location as well");

        Assert.Equal(
            "list all incoming legal AI conferences across California (can you include the dates and exact location as well)",
            fused);
    }

    [Fact]
    public void Compose_WithTarget_PrefixesConstraintWithTarget()
    {
        var fused = WideQueryFusion.Compose(
            "compare general liability options",
            clarificationTarget: "coverage",
            clarificationAnswer: "for a small construction contractor");

        Assert.Equal(
            "compare general liability options (coverage: for a small construction contractor)",
            fused);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Compose_NoClarification_ReturnsOriginalUnchanged(string? clarification)
    {
        var fused = WideQueryFusion.Compose("statute of limitations in Texas", null, clarification);

        Assert.Equal("statute of limitations in Texas", fused);
    }

    [Fact]
    public void Compose_CollapsesWhitespaceInBothParts()
    {
        var fused = WideQueryFusion.Compose(
            "  find   recent   rulings ",
            clarificationTarget: null,
            clarificationAnswer: "in   the   ninth   circuit  ");

        Assert.Equal("find recent rulings (in the ninth circuit)", fused);
    }
}
