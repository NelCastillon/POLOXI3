using System.Reflection;
using System.Runtime.CompilerServices;
using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// R2 Ramirez regression — candidate/branch separation on a legal EVALUATE run.
//
// Proves the deterministic boundary that keeps legal outcome DISPOSITIONS (candidate objects) apart
// from the shared decision-dependency forest:
//   • on a matter-backed EVALUATE run a disposition-headed phrase ("Grant summary judgment",
//     "Deny the motion") is classified as a competing outcome candidate,
//   • a procedural / factual sub-issue ("Comparative fault apportionment") is NOT a disposition,
//   • the whole path is a strict no-op when there is no matter context, and
//   • a supplied CurrentOutcome that is a HardConstraint (ImplementDraft) disables the candidate
//     path so the supplied outcome never becomes both the candidate and the conclusion.
// The service's heavy 10-dependency constructor is bypassed with GetUninitializedObject because the
// classifier under test is pure deterministic string logic that touches none of those dependencies.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LegalDecisionCandidateSeparationTests
{
    private static IntelligenceWide2Service NewService(MatterContextSnapshot? matter, DecisionIntentResolver.Resolution intent)
    {
        var service = (IntelligenceWide2Service)RuntimeHelpers.GetUninitializedObject(typeof(IntelligenceWide2Service));
        typeof(IntelligenceWide2Service).GetField("_matterContext", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(service, matter);
        typeof(IntelligenceWide2Service).GetField("_decisionIntent", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(service, intent);
        return service;
    }

    private static bool InvokeIsDecisionOutcomeCandidate(IntelligenceWide2Service service, string name)
        => (bool)typeof(IntelligenceWide2Service)
            .GetMethod("IsDecisionOutcomeCandidate", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(service, [name])!;

    private static MatterContextSnapshot MatterWithContext() => MatterContextSnapshot.Empty with
    {
        MatterId = Guid.NewGuid(),
        Decision = [new MatterContextField("Requested Disposition", "Grant summary judgment for plaintiff", MatterFieldProvenance.Supplied)],
    };

    [Theory]
    [InlineData("Grant summary judgment for plaintiff")]
    [InlineData("Deny the motion")]
    [InlineData("Dismiss the complaint")]
    [InlineData("Remand for further proceedings")]
    public void DispositionPhrase_OnLegalEvaluateRun_IsOutcomeCandidate(string disposition)
    {
        var service = NewService(MatterWithContext(), new(DecisionIntent.Evaluate, "evaluate", false));

        Assert.True(InvokeIsDecisionOutcomeCandidate(service, disposition));
    }

    [Theory]
    [InlineData("Comparative fault apportionment")]
    [InlineData("Admissibility of the expert report")]
    [InlineData("Negligence per se elements")]
    public void DependencySubIssue_OnLegalEvaluateRun_IsNotOutcomeCandidate(string subIssue)
    {
        var service = NewService(MatterWithContext(), new(DecisionIntent.Evaluate, "evaluate", false));

        Assert.False(InvokeIsDecisionOutcomeCandidate(service, subIssue));
    }

    [Fact]
    public void NoMatterContext_DisablesOutcomeCandidateClassification()
    {
        var service = NewService(null, new(DecisionIntent.Evaluate, "no matter", false));

        Assert.False(InvokeIsDecisionOutcomeCandidate(service, "Grant summary judgment for plaintiff"));
    }

    [Fact]
    public void HardConstraintImplementDraft_DisablesOutcomeCandidateClassification()
    {
        // On an ImplementDraft run the supplied disposition is the governing premise; it must never be
        // re-admitted as a competing candidate that could become both candidate and conclusion.
        var service = NewService(MatterWithContext(), new(DecisionIntent.ImplementDraft, "draft", true));

        Assert.False(InvokeIsDecisionOutcomeCandidate(service, "Grant summary judgment for plaintiff"));
    }

    [Fact]
    public void RamirezDispositionPool_AllFourLabelsAreAdmissibleOutcomeCandidates()
    {
        // Regression for the "5 proposed → 0 accepted" defect: the named-entity admission gate rejected
        // every disposition on a zero-evidence run because none was a corpus-attested entity, so the
        // Candidate × Branch competition never executed. The admission fix keys off exactly this
        // predicate (IsLegalDecisionEvaluationRun && IsDecisionOutcomeCandidate) — assert all four
        // Ramirez L1 labels satisfy it so they enter the competition instead of being ruled out.
        var service = NewService(MatterWithContext(), new(DecisionIntent.Evaluate, "evaluate", false));

        foreach (var disposition in new[]
        {
            "Grant summary judgment for plaintiff",
            "Deny summary judgment",
            "Grant summary judgment for defendant",
            "Grant conditional liability-only relief",
        })
        {
            Assert.True(InvokeIsDecisionOutcomeCandidate(service, disposition), $"'{disposition}' must be admissible.");
        }
    }
}
