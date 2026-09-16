using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// POLOXI Legal terminal-state invariants (§32-34). The core rule under test:
//   RESEARCH_EXHAUSTED ⇒ no executable, sufficiently valuable research action remains.
// High candidate entropy alone must NEVER collapse the run to RESEARCH_EXHAUSTED while a
// high-value frontier (ADV above the configured floor) is still open.
public sealed class DecisionTerminalStateInvariantTests
{
    // ThresholdResearchExhaustionAdv is the 10th parameter (0.10 here, matching seeded config).
    private static DecisionCoreSettings Settings() => new(
        0.20, 0.25, 0.25, 0.15, 0.10, 0.05,
        0.35, 0.25, 0.15, 0.10, 0.30, 0.40,
        4, 24, 8);

    [Fact]
    public void HighEntropy_WithOpenHighValueFrontier_IsProvisional_NotExhausted()
    {
        // Arrange: nearly maximum entropy but the frontier is open with ADV well above the floor.
        var (statusCode, terminalState, reason) = LegalDecisionService.ResolveTerminalState(
            Settings(), margin: 0.10, entropy: 0.998, frontierOpen: true, maxAvailableAdv: 0.54);

        // Assert: research is NOT exhausted; a provisional (leading-outcome) decision is emitted.
        Assert.Equal(DecisionStatusCodes.ProvisionalDecision, statusCode);
        Assert.Equal(DecisionStatusCodes.ProvisionalDecision, terminalState);
        Assert.Equal("LEADING_OUTCOME_WITH_OPEN_HIGH_VALUE_FRONTIER", reason);
    }

    [Fact]
    public void ResearchExhausted_OnlyWhenMaxAdvBelowThreshold()
    {
        // Arrange: frontier is open but nothing on it clears the ADV value floor.
        var (statusCode, _, reason) = LegalDecisionService.ResolveTerminalState(
            Settings(), margin: 0.10, entropy: 0.998, frontierOpen: true, maxAvailableAdv: 0.05);

        // Assert: this is the ONLY legitimate path to RESEARCH_EXHAUSTED.
        Assert.Equal(DecisionStatusCodes.ResearchExhausted, statusCode);
        Assert.Equal("MAX_AVAILABLE_ADV_BELOW_THRESHOLD", reason);
    }

    [Fact]
    public void ClearMargin_ContainedUncertainty_NoFrontier_IsDecisionReady()
    {
        var (statusCode, terminalState, reason) = LegalDecisionService.ResolveTerminalState(
            Settings(), margin: 0.30, entropy: 0.40, frontierOpen: false, maxAvailableAdv: 0.0);

        Assert.Equal(DecisionStatusCodes.DecisionReady, statusCode);
        Assert.Equal(DecisionStatusCodes.DecisionReady, terminalState);
        Assert.Equal("NO_CRITICAL_FRONTIER_AND_CLEAR_MARGIN", reason);
    }

    [Fact]
    public void ResearchExhausted_ImpliesNoExecutableHighValueResearch()
    {
        // Invariant: whenever the classifier returns RESEARCH_EXHAUSTED, maxAvailableAdv must be
        // below the exhaustion floor (i.e. no worthwhile research action can remain).
        var settings = Settings();
        double[] margins = { 0.0, 0.05, 0.10, 0.24 };
        double[] entropies = { 0.10, 0.60, 0.85, 0.998 };
        bool[] frontiers = { true, false };
        double[] advs = { 0.0, 0.05, 0.09, 0.10, 0.30, 0.54 };

        foreach (var margin in margins)
        foreach (var entropy in entropies)
        foreach (var frontierOpen in frontiers)
        foreach (var adv in advs)
        {
            var (statusCode, _, _) = LegalDecisionService.ResolveTerminalState(
                settings, margin, entropy, frontierOpen, adv);

            if (statusCode == DecisionStatusCodes.ResearchExhausted)
                Assert.True(adv < settings.ThresholdResearchExhaustionAdv,
                    $"RESEARCH_EXHAUSTED emitted while executable research remains (adv={adv}).");
        }
    }
}
