using Legal.Application;
using Legal.Application.Features.Intelligence;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Locks the deterministic reset-phrase contract shared by the /legal/search UI and the Wide service.
// A follow-up that explicitly abandons the thread (e.g. "start over", "new topic") must clear the
// parent anchor and begin a fresh root; ordinary narrowing follow-ups must NOT trigger a reset.
public sealed class WideFollowUpHeuristicsTests
{
    [Theory]
    [InlineData("start over")]
    [InlineData("Start Over with something else")]
    [InlineData("scratch that, new topic")]
    [InlineData("disregard previous results")]
    [InlineData("ignore the previous question")]
    [InlineData("forget previous")]
    [InlineData("let's do a new search")]
    [InlineData("new question please")]
    [InlineData("reset")]
    [InlineData("  RESET  ")]
    public void IsReset_ExplicitAbandonPhrases_ReturnsTrue(string followUp)
    {
        Assert.True(WideFollowUpHeuristics.IsReset(followUp));
    }

    [Theory]
    [InlineData("which is the cheapest?")]
    [InlineData("best for a family")]
    [InlineData("show me the worst school")]
    [InlineData("include dates and location")]
    [InlineData("narrow to California only")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsReset_OrdinaryFollowUps_ReturnsFalse(string? followUp)
    {
        Assert.False(WideFollowUpHeuristics.IsReset(followUp));
    }

    // The PIVOT binding guard: when the model names a winner but leaves the list in the prior ordering
    // (e.g. headline "Torrance" while the items still rank the cheapest city #1), the winner-named item
    // must be promoted to rank 1 and the rest renumbered so the headline and the list cannot contradict.
    [Fact]
    public void ReconcileWinner_WinnerNotRankOne_PromotesWinnerAndRenumbers()
    {
        var results = new[]
        {
            new WideInterpretiveResultDto("Best for raising a family", "Family suitability", 0.9m,
            [
                new WideInterpretiveResultItemDto(1, "Hawthorne", "Cheapest", 0.9m),
                new WideInterpretiveResultItemDto(2, "Torrance", "Family-friendly", 0.85m),
                new WideInterpretiveResultItemDto(3, "Gardena", "Mixed", 0.8m),
            ])
        };

        var reconciled = WideFollowUpHeuristics.ReconcileWinner(results, "Torrance");

        var items = reconciled[0].Items.ToArray();
        Assert.Equal("Torrance", items[0].Name);
        Assert.Equal(1, items[0].RankNumber);
        Assert.Equal("Hawthorne", items[1].Name);
        Assert.Equal(2, items[1].RankNumber);
        Assert.Equal("Gardena", items[2].Name);
        Assert.Equal(3, items[2].RankNumber);
    }

    [Fact]
    public void ReconcileWinner_WinnerAlreadyLeads_LeavesOrderUnchanged()
    {
        var results = new[]
        {
            new WideInterpretiveResultDto("Affordability", "Housing cost", 0.9m,
            [
                new WideInterpretiveResultItemDto(1, "Hawthorne", "Cheapest", 0.9m),
                new WideInterpretiveResultItemDto(2, "Torrance", "Moderate", 0.85m),
            ])
        };

        var reconciled = WideFollowUpHeuristics.ReconcileWinner(results, "Hawthorne");

        Assert.Equal("Hawthorne", reconciled[0].Items.First().Name);
        Assert.Equal(2, reconciled[0].Items.Count);
    }

    [Fact]
    public void ReconcileWinner_WinnerAbsentOrBlank_ReturnsResultsUnchanged()
    {
        var results = new[]
        {
            new WideInterpretiveResultDto("Affordability", "Housing cost", 0.9m,
            [
                new WideInterpretiveResultItemDto(1, "Hawthorne", "Cheapest", 0.9m),
                new WideInterpretiveResultItemDto(2, "Torrance", "Moderate", 0.85m),
            ])
        };

        Assert.Same(results, WideFollowUpHeuristics.ReconcileWinner(results, "Compton"));
        Assert.Same(results, WideFollowUpHeuristics.ReconcileWinner(results, null));
        Assert.Same(results, WideFollowUpHeuristics.ReconcileWinner(results, "   "));
    }
}
