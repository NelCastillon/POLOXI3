using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal — EVIDENCE VERIFIER PROPOSITION-SUPPORT REGRESSION.
//
// Invariant under test:  VERIFIED  ⇒  the source passage actually supports the proposition.
//
// The prior verifier used raw Jaccard token overlap with no stopword removal against a 0.10 support
// floor. An unrelated title ("NULLIFYING AN EXECUTIVE ORDER ... STRIKING EMPLOYEES") shared only the
// function words "that"/"for" plus the incidental token "employees" with an uncompensated-overtime
// objective and squeaked over the floor → a false VERIFIED that would gain decision authority.
//
// These deterministic tests pin the fix: incidental function-word / single-content-token overlap must
// NOT establish support, while a genuine wage-and-hour passage must clear the floor.
// ─────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionEvidenceVerifierTests
{
    private const string OvertimeObjective =
        "whether there is credible evidence that employees worked overtime hours for which they were not compensated";

    // ── NEGATIVE: the exact Delgado false-positive. Unrelated executive-order title must be UNSUPPORTED. ──
    [Fact]
    public void VerifyRetrievedSource_UnrelatedTitleOnly_IsNotVerified_AndGrantsNoPositiveAuthority()
    {
        var source = new DecisionRetrievedSource(
            "https://congress.example/hr-1234",
            "Nullifying an executive order that prohibits federal contracts with companies that hire permanent replacements for striking employees",
            // Title-only provenance: the snippet is just the title, as in the failing run.
            "NULLIFYING AN EXECUTIVE ORDER THAT PROHIBITS FEDERAL CONTRACTS WITH COMPANIES THAT HIRE PERMANENT REPLACEMENTS FOR STRIKING EMPLOYEES");

        var evidence = LegalDecisionService.VerifyRetrievedSource(source, OvertimeObjective, branchId: null);

        Assert.NotEqual(DecisionVerificationStates.Verified, evidence.VerificationStatus);
        // No positive authority: the supporting passage must not be recorded for a non-verified source.
        Assert.Null(evidence.SupportingPassage);
    }

    // ── The measure itself: incidental overlap yields zero support (below the shared-content-token floor). ──
    [Fact]
    public void PropositionSupport_IncidentalFunctionWordOverlap_IsZero()
    {
        var support = DecisionCoreMath.PropositionSupport(
            OvertimeObjective,
            "NULLIFYING AN EXECUTIVE ORDER THAT PROHIBITS FEDERAL CONTRACTS WITH COMPANIES THAT HIRE PERMANENT REPLACEMENTS FOR STRIKING EMPLOYEES");

        Assert.Equal(0d, support);
    }

    // ── POSITIVE: a genuine wage-and-hour passage discussing uncompensated overtime establishes support. ──
    [Fact]
    public void VerifyRetrievedSource_GenuineWageAndHourPassage_IsVerified()
    {
        var source = new DecisionRetrievedSource(
            "https://reporter.example/wage-hour-42",
            "Acme Logistics v. Delgado (Wage and Hour)",
            "The plaintiff presented credible evidence that employees worked overtime hours for which they were not compensated, "
            + "including time records showing unpaid work beyond forty hours in multiple weeks.");

        var evidence = LegalDecisionService.VerifyRetrievedSource(source, OvertimeObjective, branchId: null);

        Assert.Equal(DecisionVerificationStates.Verified, evidence.VerificationStatus);
        Assert.False(string.IsNullOrWhiteSpace(evidence.SupportingPassage));
    }

    // ── The measure: a genuine passage clears the shared-content-token floor with real overlap. ──
    [Fact]
    public void PropositionSupport_GenuineOverlap_IsPositive()
    {
        var support = DecisionCoreMath.PropositionSupport(
            OvertimeObjective,
            "employees worked overtime hours for which they were not compensated");

        Assert.True(support > 0d);
    }

    // ── NEGATIVE: the exact returned false positive. A title-echo snippet is IDENTITY, not support. ──
    // "Proposed Rule on Overtime Pay" shares the topical anchors (overtime, pay) with the misclassification
    // objective and its snippet is just its own title. Topical relevance MUST NOT establish support: the
    // source must never be VERIFIED and must grant zero positive authority.
    private const string MisclassificationObjective =
        "employees were misclassified as exempt and should be entitled to overtime pay";

    [Fact]
    public void VerifyRetrievedSource_TopicalTitleEcho_ProposedRuleOnOvertimePay_IsNotVerified()
    {
        var source = new DecisionRetrievedSource(
            "https://regulations.example/overtime-proposed-rule",
            "Proposed Rule on Overtime Pay",
            // Title-only provenance: the snippet merely echoes the title, as in the failing run.
            "Proposed Rule on Overtime Pay");

        var evidence = LegalDecisionService.VerifyRetrievedSource(source, MisclassificationObjective, branchId: null);

        Assert.NotEqual(DecisionVerificationStates.Verified, evidence.VerificationStatus);
        Assert.Null(evidence.SupportingPassage);
        // Topical anchor overlap alone must not manufacture positive authority.
        Assert.Equal(0m, evidence.VerificationValue);
    }

    // ── The guard itself: a snippet that adds no content tokens beyond the title is a title echo. ──
    [Fact]
    public void PassageEchoesTitle_SnippetEqualsTitle_IsEcho()
    {
        Assert.True(DecisionCoreMath.PassageEchoesTitle(
            "Proposed Rule on Overtime Pay", "Proposed Rule on Overtime Pay"));
    }

    [Fact]
    public void PassageEchoesTitle_SnippetAddsSubstance_IsNotEcho()
    {
        Assert.False(DecisionCoreMath.PassageEchoesTitle(
            "Proposed Rule on Overtime Pay",
            "The plaintiff presented time records showing unpaid work beyond forty hours in multiple weeks."));
    }
}
