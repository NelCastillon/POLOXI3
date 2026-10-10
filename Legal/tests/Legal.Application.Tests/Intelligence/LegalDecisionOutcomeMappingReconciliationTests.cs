using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Advisory outcome→canonical mapping reconciliation (DECISION_DISCOVERY_DOMAIN_PACK_V2 / 0408).
//
// The LLM tags each proposed outcome with mappedOutcomeCodes[]. ReconcileAdvisoryOutcomeMapping is
// the deterministic, pure sanitizer POLOXI runs over those tags: it keeps ONLY codes present in the
// resolved pack (dropping invented/unknown codes), records unmapped outcomes + dropped codes for
// telemetry, and preserves EVERY candidate (and all its scores/branches) unchanged apart from the
// sanitized advisory mapping. The mapping must never eliminate, re-order, score, or gate a candidate.
// ─────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LegalDecisionOutcomeMappingReconciliationTests
{
    private static DecisionDomainPackOutcomeCandidateDto Outcome(string code, string name, string role, bool requiresVerification, int sort,
        bool requiresFactualPredicate = false, string? factualPredicateKeywords = null)
        => new(code, name, null, role, requiresVerification, "PERSONAL_INJURY", sort, requiresFactualPredicate, factualPredicateKeywords);

    private static IReadOnlyList<DecisionDomainPackOutcomeCandidateDto> CanonicalPool() =>
    [
        Outcome("C1", "Confidential negotiated settlement", "PATHWAY", false, 10),
        Outcome("C4", "Defense-favorable judgment or dismissal", "PATHWAY", false, 40),
        Outcome("C5", "Recorded settlement disbursement", "ASSERTED_HISTORICAL", true, 50),
        Outcome("C7", "Procedural or jurisdictional bar", "PATHWAY", false, 70),
        Outcome("C8", "Voluntary dismissal or withdrawal", "PATHWAY", false, 80),
        Outcome("C9", "Default judgment", "PATHWAY", false, 90, requiresFactualPredicate: true, factualPredicateKeywords: "default"),
    ];

    private static LegalDecisionService.ProposedCandidate Candidate(string name, params string[] mappedCodes)
        => new(name, name, 1, 1, 1, 1, 1, 1, 1, [])
        {
            MappedOutcomeCodes = mappedCodes,
        };

    [Fact]
    public void KnownCanonicalCodes_ArePreserved()
    {
        var proposal = new[] { Candidate("Case settles", "C1") };

        var result = LegalDecisionService.ReconcileAdvisoryOutcomeMapping(proposal, CanonicalPool());

        Assert.Equal(["C1"], result.Proposal[0].MappedOutcomeCodes);
        Assert.Empty(result.UnmappedOutcomes);
        Assert.Empty(result.DroppedInvalidCodes);
    }

    [Fact]
    public void InventedCodes_AreDropped_ButCandidateIsPreserved()
    {
        var proposal = new[] { Candidate("Novel disposition", "C1", "C99", "ZZ") };

        var result = LegalDecisionService.ReconcileAdvisoryOutcomeMapping(proposal, CanonicalPool());

        // Only the real pack code survives; invented codes are dropped, candidate stays.
        Assert.Single(result.Proposal);
        Assert.Equal(["C1"], result.Proposal[0].MappedOutcomeCodes);
        Assert.Contains("C99", result.DroppedInvalidCodes);
        Assert.Contains("ZZ", result.DroppedInvalidCodes);
    }

    [Fact]
    public void FullyUnmappedOutcome_IsPreservedFirstClass_AndReportedUnmapped()
    {
        var proposal = new[] { Candidate("Outcome with no canonical fit", "C99") };

        var result = LegalDecisionService.ReconcileAdvisoryOutcomeMapping(proposal, CanonicalPool());

        // Candidate is never dropped; its sanitized mapping is empty; it is reported unmapped.
        Assert.Single(result.Proposal);
        Assert.Empty(result.Proposal[0].MappedOutcomeCodes);
        Assert.Equal(["Outcome with no canonical fit"], result.UnmappedOutcomes);
    }

    [Fact]
    public void MappingIsCaseInsensitive_AgainstPackVocabulary()
    {
        var proposal = new[] { Candidate("Dismissed on the merits", "c4") };

        var result = LegalDecisionService.ReconcileAdvisoryOutcomeMapping(proposal, CanonicalPool());

        Assert.Equal(["c4"], result.Proposal[0].MappedOutcomeCodes);
        Assert.Empty(result.UnmappedOutcomes);
    }

    [Fact]
    public void DismissalCategories_C4_C7_C8_AreDistinctCanonicalCodes()
    {
        // The three legally-distinct "dismissal" outcomes map to three different canonical codes; the
        // reconciliation preserves whichever the LLM chose without collapsing them together.
        var proposal = new[]
        {
            Candidate("Merits defense dismissal", "C4"),
            Candidate("Jurisdictional bar", "C7"),
            Candidate("Plaintiff voluntary dismissal", "C8"),
        };

        var result = LegalDecisionService.ReconcileAdvisoryOutcomeMapping(proposal, CanonicalPool());

        Assert.Equal(["C4"], result.Proposal[0].MappedOutcomeCodes);
        Assert.Equal(["C7"], result.Proposal[1].MappedOutcomeCodes);
        Assert.Equal(["C8"], result.Proposal[2].MappedOutcomeCodes);
    }

    [Fact]
    public void Reconciliation_PreservesCandidateOrderCountAndScores()
    {
        var proposal = new[]
        {
            Candidate("First", "C1"),
            Candidate("Second", "C99"),
            Candidate("Third", "C4", "C7"),
        };

        var result = LegalDecisionService.ReconcileAdvisoryOutcomeMapping(proposal, CanonicalPool());

        // Advisory-only: count, order, display names, and scores are all unchanged.
        Assert.Equal(3, result.Proposal.Count);
        Assert.Equal(["First", "Second", "Third"], result.Proposal.Select(c => c.DisplayName));
        Assert.All(result.Proposal, c => Assert.Equal(1, c.LegalSupport));
    }

    [Fact]
    public void C5AndC9_RemainAdvisoryTargets_ReconciliationNeverAppliesVerificationGates()
    {
        // C5 (ASSERTED_HISTORICAL/verify-first) and C9 (factual-predicate) are valid mapping targets.
        // Reconciliation keeps the codes but applies NO eligibility/verification gate — those remain
        // owned by POLOXI Core's IsCanonicalOutcomeEligible downstream.
        var proposal = new[] { Candidate("Historical disbursement", "C5", "C9") };

        var result = LegalDecisionService.ReconcileAdvisoryOutcomeMapping(proposal, CanonicalPool());

        Assert.Equal(["C5", "C9"], result.Proposal[0].MappedOutcomeCodes);
        Assert.Empty(result.DroppedInvalidCodes);
    }
}
