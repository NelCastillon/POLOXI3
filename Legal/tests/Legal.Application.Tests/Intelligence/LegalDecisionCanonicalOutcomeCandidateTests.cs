using System.Reflection;
using System.Runtime.CompilerServices;
using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// Canonical PI outcome-candidate regression — DB-backed pool admission (migrations 0396 + 0397).
//
// Proves that once the canonical PERSONAL_INJURY outcome candidates are loaded from the domain pack
// (_canonicalOutcomeCandidates), their exact noun-form Names are admitted as competing Decision
// Outcome candidates on a legal EVALUATE run — including the C6–C9 extended pool added by 0397
// (Arbitration or ADR award, Procedural or jurisdictional bar, Voluntary dismissal or withdrawal,
// Default judgment). The shared determining FACTORS (comparative fault, causation, …) must still be
// rejected so the candidate/branch separation is preserved.
//
// Mirrors LegalDecisionCandidateSeparationTests: the heavy 10-dependency constructor is bypassed with
// GetUninitializedObject because IsDecisionOutcomeCandidate is pure deterministic string logic that
// reads only _matterContext, _decisionIntent and the injected _canonicalOutcomeCandidates field.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LegalDecisionCanonicalOutcomeCandidateTests
{
    private static readonly string[] CanonicalOutcomeNames =
    [
        "Confidential negotiated settlement",          // C1
        "Continued negotiation or mediation",          // C2
        "Plaintiff-favorable adjudication or verdict",  // C3
        "Defense-favorable judgment or dismissal",      // C4
        "Recorded settlement disbursement",             // C5 (ASSERTED_HISTORICAL / verify-first)
        "Arbitration or ADR award",                     // C6 (0397)
        "Procedural or jurisdictional bar",             // C7 (0397)
        "Voluntary dismissal or withdrawal",            // C8 (0397)
        "Default judgment",                             // C9 (0397)
    ];

    private static DecisionDomainPackOutcomeCandidateDto Outcome(string code, string name, string role, bool requiresVerification, int sort,
        bool requiresFactualPredicate = false, string? factualPredicateKeywords = null)
        => new(code, name, null, role, requiresVerification, "PERSONAL_INJURY", sort, requiresFactualPredicate, factualPredicateKeywords);

    // C9 Default judgment opts into the factual-predicate gate (migration 0398): it is only eligible when
    // the matter facts reference a default / failure to appear or defend.
    private const string DefaultJudgmentPredicateKeywords =
        "default judgment|default|failed to appear|failure to appear|failed to defend|failure to defend|did not appear|did not respond|no appearance|no response|non-appearance|defaulted";

    private static IReadOnlyList<DecisionDomainPackOutcomeCandidateDto> CanonicalPool() =>
    [
        Outcome("C1", "Confidential negotiated settlement", "PATHWAY", false, 10),
        Outcome("C2", "Continued negotiation or mediation", "PATHWAY", false, 20),
        Outcome("C3", "Plaintiff-favorable adjudication or verdict", "PATHWAY", false, 30),
        Outcome("C4", "Defense-favorable judgment or dismissal", "PATHWAY", false, 40),
        Outcome("C5", "Recorded settlement disbursement", "ASSERTED_HISTORICAL", true, 50),
        Outcome("C6", "Arbitration or ADR award", "PATHWAY", false, 60),
        Outcome("C7", "Procedural or jurisdictional bar", "PATHWAY", false, 70),
        Outcome("C8", "Voluntary dismissal or withdrawal", "PATHWAY", false, 80),
        Outcome("C9", "Default judgment", "PATHWAY", false, 90, requiresFactualPredicate: true, factualPredicateKeywords: DefaultJudgmentPredicateKeywords),
    ];

    private static IntelligenceWide2Service NewServiceWithCanonicalPool(
        MatterContextSnapshot? matter,
        DecisionIntentResolver.Resolution intent,
        IReadOnlyList<DecisionDomainPackOutcomeCandidateDto> pool)
    {
        var service = (IntelligenceWide2Service)RuntimeHelpers.GetUninitializedObject(typeof(IntelligenceWide2Service));
        typeof(IntelligenceWide2Service).GetField("_matterContext", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(service, matter);
        typeof(IntelligenceWide2Service).GetField("_decisionIntent", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(service, intent);
        typeof(IntelligenceWide2Service).GetField("_canonicalOutcomeCandidates", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(service, pool);
        return service;
    }

    private static bool InvokeIsDecisionOutcomeCandidate(IntelligenceWide2Service service, string name)
        => (bool)typeof(IntelligenceWide2Service)
            .GetMethod("IsDecisionOutcomeCandidate", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(service, [name])!;

    private static MatterContextSnapshot MatterWithContext() => MatterContextSnapshot.Empty with
    {
        MatterId = Guid.NewGuid(),
        Decision = [new MatterContextField("Requested Disposition", "Resolve the personal-injury claim", MatterFieldProvenance.Supplied)],
    };

    // A matter whose material facts reference a defendant default, satisfying the C9 predicate.
    private static MatterContextSnapshot MatterWithDefaultContext() => MatterContextSnapshot.Empty with
    {
        MatterId = Guid.NewGuid(),
        Decision = [new MatterContextField("Requested Disposition", "Resolve the personal-injury claim", MatterFieldProvenance.Supplied)],
        Facts = [new MatterContextField("Procedural Status", "The defendant failed to appear and a default judgment was entered.", MatterFieldProvenance.Supplied)],
    };

    [Theory]
    [InlineData("Confidential negotiated settlement")]       // C1
    [InlineData("Continued negotiation or mediation")]       // C2
    [InlineData("Plaintiff-favorable adjudication or verdict")] // C3
    [InlineData("Defense-favorable judgment or dismissal")]  // C4
    [InlineData("Recorded settlement disbursement")]         // C5
    [InlineData("Arbitration or ADR award")]                 // C6
    [InlineData("Procedural or jurisdictional bar")]         // C7
    [InlineData("Voluntary dismissal or withdrawal")]        // C8
    public void CanonicalOutcomeName_OnLegalEvaluateRun_IsAdmittedAsCandidate(string canonicalName)
    {
        // C1-C8 declare no factual predicate, so they are always admitted on a legal EVALUATE run.
        var service = NewServiceWithCanonicalPool(MatterWithContext(), new(DecisionIntent.Evaluate, "evaluate", false), CanonicalPool());

        Assert.True(InvokeIsDecisionOutcomeCandidate(service, canonicalName), $"'{canonicalName}' must be admitted as a Decision Outcome candidate.");
    }

    [Fact]
    public void PredicateOutcome_IsNotAdmitted_WhenMatterFactsDoNotSatisfyPredicate()
    {
        // C9 Default judgment requires a factual predicate (migration 0398). A matter with no default /
        // failure-to-appear fact must NOT admit it as a candidate, even though it is seeded and active.
        var service = NewServiceWithCanonicalPool(MatterWithContext(), new(DecisionIntent.Evaluate, "evaluate", false), CanonicalPool());

        Assert.False(InvokeIsDecisionOutcomeCandidate(service, "Default judgment"),
            "'Default judgment' must not be admitted when the matter facts do not establish a default.");
    }

    [Fact]
    public void PredicateOutcome_IsAdmitted_WhenMatterFactsSatisfyPredicate()
    {
        // When the matter facts reference a default / failure to appear, the C9 predicate is satisfied and
        // Default judgment becomes an eligible competing candidate.
        var service = NewServiceWithCanonicalPool(MatterWithDefaultContext(), new(DecisionIntent.Evaluate, "evaluate", false), CanonicalPool());

        Assert.True(InvokeIsDecisionOutcomeCandidate(service, "Default judgment"),
            "'Default judgment' must be admitted when the matter facts establish a default.");
    }

    [Theory]
    [InlineData("C6")]
    [InlineData("C7")]
    [InlineData("C8")]
    [InlineData("C9")]
    public void ExtendedPool_From0397_IsPresent(string outcomeCode)
    {
        Assert.Contains(CanonicalPool(), outcome => outcome.OutcomeCode == outcomeCode);
    }

    [Fact]
    public void CanonicalNameMatch_IsCaseInsensitive()
    {
        var service = NewServiceWithCanonicalPool(MatterWithDefaultContext(), new(DecisionIntent.Evaluate, "evaluate", false), CanonicalPool());

        Assert.True(InvokeIsDecisionOutcomeCandidate(service, "arbitration or adr award"));
        // C9 is predicate-gated; MatterWithDefaultContext satisfies it, so case-insensitive match still holds.
        Assert.True(InvokeIsDecisionOutcomeCandidate(service, "DEFAULT JUDGMENT"));
    }

    [Theory]
    [InlineData("Comparative fault apportionment")]
    [InlineData("Causation of the claimed harm")]
    [InlineData("Statutory applicability analysis")]
    public void SharedDeterminingFactor_IsNotAdmittedAsCandidate(string factor)
    {
        // The determining FACTORS live in the shared L1->Ln evaluation hierarchy UNDER each candidate.
        // They are not seeded into the canonical pool and must not be classified as outcome candidates.
        var service = NewServiceWithCanonicalPool(MatterWithContext(), new(DecisionIntent.Evaluate, "evaluate", false), CanonicalPool());

        Assert.False(InvokeIsDecisionOutcomeCandidate(service, factor), $"'{factor}' is a shared factor, not an outcome candidate.");
    }

    [Fact]
    public void CanonicalPool_DeliversAtLeastTwoCandidates_SoCompetitionIsNotStarved()
    {
        // The original defect delivered < 2 candidates => REGISTERED_SCORING_BLOCKED => Provisional.
        // The DB-backed canonical pool guarantees a non-starved floor; assert every seeded name is admitted.
        var service = NewServiceWithCanonicalPool(MatterWithContext(), new(DecisionIntent.Evaluate, "evaluate", false), CanonicalPool());

        var admitted = CanonicalOutcomeNames.Count(name => InvokeIsDecisionOutcomeCandidate(service, name));

        Assert.True(admitted >= 2, "Canonical pool must deliver at least two competing candidates.");
        // C9 is predicate-gated and this matter does not satisfy it, so every name EXCEPT C9 is admitted.
        Assert.Equal(CanonicalOutcomeNames.Length - 1, admitted);
    }

    [Fact]
    public void C5_RemainsVerifyFirstAssertedHistorical()
    {
        // C5 is admitted as a competing candidate but carries the verify-first contract: RoleCode
        // ASSERTED_HISTORICAL with RequiresVerification = 1. It must never be auto-promoted silently.
        var c5 = CanonicalPool().Single(outcome => outcome.OutcomeCode == "C5");

        Assert.Equal("ASSERTED_HISTORICAL", c5.RoleCode);
        Assert.True(c5.RequiresVerification);
    }

    [Fact]
    public void NoMatterContext_DisablesCanonicalCandidateClassification()
    {
        var service = NewServiceWithCanonicalPool(null, new(DecisionIntent.Evaluate, "no matter", false), CanonicalPool());

        Assert.False(InvokeIsDecisionOutcomeCandidate(service, "Arbitration or ADR award"));
    }
}
