using Legal.Application;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Guards the outcome-restatement filter used by the dynamic Personal Injury (Wide2) pipeline when it
// projects surviving decision branches into shared scoring dependencies. The filter must remove true
// outcome restatements WITHOUT swallowing genuine sub-issue branches that merely share vocabulary with a
// competing disposition. Before this guard, a short outcome ("Below-limits settlement") collapsed every
// richer branch that happened to contain its words, emptying the dependency forest on disposition-only
// matters and producing "0% support" with dashed overview metrics.
public sealed class CompetingOutcomeMatchGuardTests
{
    private static readonly string[] Outcomes =
    {
        "Complete policy-limits settlement",
        "Below-limits settlement",
        "Demand pending",
    };

    [Fact]
    public void ExactRestatement_IsTreatedAsOutcome()
    {
        Assert.True(IntelligenceWide2Service.MatchesCompetingOutcomeForTest(
            "Complete policy-limits settlement", Outcomes));
    }

    [Fact]
    public void BranchSubsetOfOutcome_IsTreatedAsOutcome()
    {
        // "policy-limits settlement" carries no term the outcome lacks — it names the outcome.
        Assert.True(IntelligenceWide2Service.MatchesCompetingOutcomeForTest(
            "policy-limits settlement", Outcomes));
    }

    [Fact]
    public void GenuineSubIssueBranch_SharingVocabulary_Survives()
    {
        // Shares "settlement" (and partially "limits") with outcomes but introduces independent
        // significant terms ("adequacy", "comparative", "fault") — a real scoring axis, not a restatement.
        Assert.False(IntelligenceWide2Service.MatchesCompetingOutcomeForTest(
            "Settlement amount adequacy given comparative fault", Outcomes));
    }

    [Fact]
    public void SingleSharedTerm_IsNotTreatedAsOutcome()
    {
        // Only "settlement" overlaps; a single coincidental term must not wipe the branch.
        Assert.False(IntelligenceWide2Service.MatchesCompetingOutcomeForTest(
            "Settlement authority of the adjuster", Outcomes));
    }

    [Fact]
    public void UnrelatedBranch_Survives()
    {
        Assert.False(IntelligenceWide2Service.MatchesCompetingOutcomeForTest(
            "Notice of hazardous condition", Outcomes));
    }
}
