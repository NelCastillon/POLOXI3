using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// R3 regression tests — decision ownership.
//
// Prove the deterministic DecisionIntentResolver guard so a supplied CurrentOutcome/RequestedDisposition
// is never silently promoted to a fixed conclusion:
//   • an explicit "which disposition" question forces EVALUATE (outcome is NOT a hard constraint),
//   • an explicit "draft/implement" question forces IMPLEMENT_DRAFT (disposition IS the premise),
//   • an ambiguous question defaults to EVALUATE (safe: never auto-adopts a supplied outcome),
//   • explicit user intent overrides a conflicting Stage 0 proposal.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionIntentResolverTests
{
    [Fact]
    public void EvaluateQuestion_ForcesEvaluate_AndCurrentOutcomeIsNotHardConstraint()
    {
        var result = DecisionIntentResolver.Resolve(
            proposedIntent: DecisionIntent.ImplementDraft, // conflicting proposal must lose
            originalQuestion: "Which disposition should the court reach on the comparative-fault defense?");

        Assert.Equal(DecisionIntent.Evaluate, result.Intent);
        Assert.False(result.CurrentOutcomeIsHardConstraint);
    }

    [Fact]
    public void ImplementDraftQuestion_ForcesImplementDraft_AndOutcomeIsHardConstraint()
    {
        var result = DecisionIntentResolver.Resolve(
            proposedIntent: DecisionIntent.Evaluate, // conflicting proposal must lose
            originalQuestion: "Draft the order implementing the specified disposition.");

        Assert.Equal(DecisionIntent.ImplementDraft, result.Intent);
        Assert.True(result.CurrentOutcomeIsHardConstraint);
    }

    [Fact]
    public void AmbiguousQuestion_WithNoProposal_DefaultsToEvaluate_NeverAutoAdoptsOutcome()
    {
        var result = DecisionIntentResolver.Resolve(
            proposedIntent: null,
            originalQuestion: "Thompson v. Acme premises liability matter.");

        Assert.Equal(DecisionIntent.Evaluate, result.Intent);
        Assert.False(result.CurrentOutcomeIsHardConstraint);
    }

    [Fact]
    public void AmbiguousQuestion_HonorsStage0ImplementDraftProposal()
    {
        var result = DecisionIntentResolver.Resolve(
            proposedIntent: DecisionIntent.ImplementDraft,
            originalQuestion: "Thompson v. Acme premises liability matter.");

        Assert.Equal(DecisionIntent.ImplementDraft, result.Intent);
        Assert.True(result.CurrentOutcomeIsHardConstraint);
    }

    [Fact]
    public void EmptyQuestion_DefaultsToEvaluate()
    {
        var result = DecisionIntentResolver.Resolve(proposedIntent: null, originalQuestion: null);

        Assert.Equal(DecisionIntent.Evaluate, result.Intent);
        Assert.False(result.CurrentOutcomeIsHardConstraint);
    }
}
