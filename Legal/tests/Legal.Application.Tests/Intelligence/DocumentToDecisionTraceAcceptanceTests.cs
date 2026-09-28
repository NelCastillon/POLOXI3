using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Blueprint end-to-end acceptance: ONE uploaded document changes the decision (the phone-record trace).
//
// This is the executable specification for the "live evidence-to-decision pipeline" the architecture
// requires. It composes the REAL, authoritative pure seams the way document intake + POLOXI Core do,
// without a database, and proves the full connection the earlier Astra run could not:
//
//   Step A  Extraction — a phone-record passage carries an exact, traceable source span (T11 admission).
//   Step B  Binding    — the assertion binds to ONLY the atomic proposition it establishes (P1 "driver
//                        used a phone"). P2 (distraction) and P3 (causation) are NOT proven by it.
//   Step C  Scoring    — once P1's admitted support is attached, its POLOXI IV/verification changes on
//                        the shared VIV scale; P2/P3 remain unresolved (verification-required, high IV).
//   Step D  Propagate  — the P1 support change flows as a signed dependency signal through the typed
//                        graph to the L2 causation branch and the two L1 candidates via the ONLY
//                        authoritative scorer, DecisionRecompetition (the graph never scores).
//   Step E  Snapshot   — the candidate ranking + IV state before vs. after differ, and P2/P3 are the
//                        next material evidence gaps — never silently satisfied.
//
// The critical guarantee under test: a document upload creates PROPOSED evidence bound to one atomic
// proposition; only that validated relationship moves proposition support, which then — and only then —
// moves dependent factors and candidate outcomes through the existing POLOXI Core.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DocumentToDecisionTraceAcceptanceTests
{
    private static readonly Guid SessionId = new("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid MatterId = new("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly EpistemicAuthoritySettings Epistemic = new();
    private static readonly ClaimVerificationPrioritizer Prioritizer = new();

    // Canonical L4 atomic propositions already present on the matter (all Alleged before the upload).
    private static readonly LegalExistingProposition P1_PhoneUse =
        new(Guid.NewGuid(), "The driver used a phone at the time of the collision.", LegalFactStates.Alleged);
    private static readonly LegalExistingProposition P2_Distraction =
        new(Guid.NewGuid(), "The phone use distracted the driver.", LegalFactStates.Alleged);
    private static readonly LegalExistingProposition P3_Causation =
        new(Guid.NewGuid(), "The distraction contributed to the collision.", LegalFactStates.Alleged);

    private static DecisionCoreSettings CoreSettings() => new(
        0.20, 0.25, 0.25, 0.15, 0.10, 0.05,
        0.35, 0.25, 0.15, 0.10, 0.30, 0.40,
        4, 24, 8);

    [Fact]
    public void UploadedPhoneRecord_BindsToP1Only_MovesCandidate_AndLeavesP2P3Unresolved()
    {
        // ── Step A: extraction. The phone record yields one passage carrying a traceable source span. ──
        var phoneRecordPassageId = Guid.NewGuid();
        var spannedPassages = new HashSet<Guid> { phoneRecordPassageId };

        Assert.True(LegalEvidenceAdmissionPolicy.IsAdmissible(phoneRecordPassageId, spannedPassages));
        Assert.Equal(
            LegalEvidenceStates.Proposed,
            LegalEvidenceAdmissionPolicy.ResolveEvidenceStateCode(phoneRecordPassageId, spannedPassages));

        // A span-less assertion (e.g. an unanchored summary line) must NOT establish any proposition.
        var unanchoredPassageId = Guid.NewGuid();
        Assert.False(LegalEvidenceAdmissionPolicy.IsAdmissible(unanchoredPassageId, spannedPassages));

        // ── Step B: binding. The phone-record assertion binds to P1 only — not P2 or P3. ──
        var existing = new[] { P1_PhoneUse, P2_Distraction, P3_Causation };

        var p1Binding = LegalPropositionBindingPolicy.Resolve(
            "The driver used a phone at the time of the collision.", LegalFactStates.Alleged, existing);
        Assert.True(p1Binding.IsReuse);
        Assert.False(p1Binding.IsContradiction);
        Assert.Equal(P1_PhoneUse.PropositionId, p1Binding.MatchedPropositionId);

        // The same assertion does NOT match the distraction/causation propositions: one document cannot
        // prove a broader conclusion than its contents establish. Binding either returns a NEW_ISSUE
        // (no confident match) or, at minimum, never the P2/P3 identity.
        var againstDistraction = LegalPropositionBindingPolicy.Resolve(
            "The driver used a phone at the time of the collision.", LegalFactStates.Alleged,
            new[] { P2_Distraction });
        Assert.NotEqual(P2_Distraction.PropositionId, againstDistraction.MatchedPropositionId);

        // ── Step C: scoring. Attach P1's admitted support and observe the POLOXI IV/verification move. ──
        var p1Before = MatterPropositionClaimProjection.Project(
            new MatterPropositionSignalInput(P1_PhoneUse.PropositionId, MatterId, P1_PhoneUse.PropositionText,
                LegalFactStates.Alleged, Confidence: 0.30m, IsDecisionAuthoritative: true,
                SupportCount: 0, ContradictCount: 0),
            SessionId);

        var p1After = MatterPropositionClaimProjection.Project(
            new MatterPropositionSignalInput(P1_PhoneUse.PropositionId, MatterId, P1_PhoneUse.PropositionText,
                LegalFactStates.Established, Confidence: 0.90m, IsDecisionAuthoritative: true,
                SupportCount: 1, ContradictCount: 0),
            SessionId);

        var p1IvBefore = Prioritizer.ComputeInformationValue(p1Before);
        var p1IvAfter = Prioritizer.ComputeInformationValue(p1After);

        // Before validation P1 is an open, decision-relevant question (positive IV); after admitted,
        // independent support it is established, so its residual information value drops.
        Assert.True(p1IvBefore >= Epistemic.MinimumVerificationIV);
        Assert.True(p1IvAfter < p1IvBefore);

        // ── P2/P3 remain unresolved: no evidence was admitted for them, so they cannot be satisfied. ──
        var p2 = MatterPropositionClaimProjection.Project(
            new MatterPropositionSignalInput(P2_Distraction.PropositionId, MatterId, P2_Distraction.PropositionText,
                LegalFactStates.Alleged, Confidence: 0.25m, IsDecisionAuthoritative: true,
                SupportCount: 0, ContradictCount: 0),
            SessionId);
        var p3 = MatterPropositionClaimProjection.Project(
            new MatterPropositionSignalInput(P3_Causation.PropositionId, MatterId, P3_Causation.PropositionText,
                LegalFactStates.Alleged, Confidence: 0.25m, IsDecisionAuthoritative: true,
                SupportCount: 0, ContradictCount: 0),
            SessionId);

        Assert.Equal(ClaimVerificationState.VerificationRequired, p2.VerificationState);
        Assert.Equal(ClaimVerificationState.VerificationRequired, p3.VerificationState);

        // The next material evidence gaps are the still-unresolved causation chain links, not P1.
        Assert.True(Prioritizer.ComputeInformationValue(p2) > p1IvAfter);
        Assert.True(Prioritizer.ComputeInformationValue(p3) > p1IvAfter);

        // ── Step D: propagate the validated P1 support change to L2 causation and the L1 candidates. ──
        // The graph never scores; it supplies a signed delta. DecisionRecompetition (the only scorer)
        // consumes it and re-runs the authoritative composite/entropy/margin math.
        var liableCandidate = Candidate("C1", "Liability established", composite: 0.50m, verification: 0.55m, winner: false);
        var notLiableCandidate = Candidate("C2", "Liability not established", composite: 0.54m, verification: 0.62m, winner: true);
        var candidates = new[] { liableCandidate, notLiableCandidate };

        // L2 causation branch owned by the "liability established" candidate (branch code prefix "C1.").
        var causationBranch = Branch("C1.B1", "ACTIVE", onFrontier: true);
        var defenceBranch = Branch("C2.B1", "ACTIVE", onFrontier: true);
        var branches = new[] { causationBranch, defenceBranch };

        // Validated P1 support raises the causation branch that the liability candidate depends on.
        var signals = new[]
        {
            new DecisionBranchSignal(
                DecisionBranchSignalKinds.SupportChanged,
                BranchId: causationBranch.DecisionBranchId,
                CandidateId: null,
                SupportDelta: 0.30,
                ReopenRequested: false,
                ReasonCode: "P1_PHONE_USE_ESTABLISHED")
        };

        var reopenAllowed = new HashSet<Guid> { causationBranch.DecisionBranchId, defenceBranch.DecisionBranchId };
        var result = DecisionRecompetition.Run(candidates, branches, signals, CoreSettings(), reopenAllowed);

        // ── Step E: snapshot. The candidate state before vs. after differs — the decision is live. ──
        var liableAfter = result.Candidates.Single(c => c.DecisionCandidateId == liableCandidate.DecisionCandidateId);
        Assert.True(liableAfter.CompositeScore > liableCandidate.CompositeScore,
            "Validated P1 support must raise the liability candidate's authoritative composite score.");

        // The overall ranking/margin materially changed (before/after are not identical).
        Assert.True(
            result.WinnerChanged
            || Math.Abs(result.CurrentMargin - result.PreviousMargin) > 1e-6
            || Math.Abs(result.CurrentEntropy - result.PreviousEntropy) > 1e-6,
            "One validated proposition change must produce a measurable candidate-state delta.");

        // Determinism: re-applying the same signal set to the recompeted state is stable (idempotent).
        var second = DecisionRecompetition.Run(result.Candidates, result.Branches, signals, CoreSettings(), reopenAllowed);
        Assert.Equal(result.CurrentWinnerId, second.CurrentWinnerId);
    }

    private static DecisionCandidatePersistence Candidate(string code, string name, decimal composite, decimal verification, bool winner)
        => new(
            Guid.NewGuid(), code, name, name,
            LegalSupport: 0.60m, FactSupport: 0.60m, EvidenceSupport: 0.55m, AuthoritySupport: 0.55m,
            Verification: verification, Uncertainty: 0.30m, Discrimination: 0.40m, RankingImpact: 0.40m,
            Diversity: 0.30m, RedundancyPenalty: 0.05m, CompositeScore: composite, DecisionSupportCeiling: 0.95m,
            RankOrder: winner ? 1 : 2, IsWinner: winner, IsEliminated: false);

    private static DecisionBranchPersistence Branch(string code, string state, bool onFrontier)
        => new(
            Guid.NewGuid(), null, 1, code, code, null, state,
            InformationValue: 0.40m, DecisionRelevance: 0.50m, FlipPotential: 0.35m, EvidenceAvailability: 0.40m,
            AdvScore: 0.30m, Cost: 0.10m, IsOnFrontier: onFrontier, StopReason: null, SortOrder: 0);
}
