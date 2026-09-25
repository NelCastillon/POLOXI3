using Legal.Application;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Outcome–Dependency Normalization Gate (pre-Core, LEGAL_DECISION / EVALUATE).
// Locks in the deterministic semantic-to-Core boundary that runs BEFORE POLOXI Core scoring:
//   • semantic classification (baseline state / pathway / resolution),
//   • material-legal-identity normalization (MERGE / KEEP_DISTINCT / REQUIRES_REVIEW) — NOT title
//     similarity — with preserved originatingOutcomeNodeIds,
//   • shared-dependency normalization,
//   • explicit-only candidate→dependency edge validation (never manufactured),
//   • a validated LegalDecisionRegistrationPlan + diagnostics.
public sealed class OutcomeDependencyNormalizationGateTests
{
    // ── Harper: premises-liability policy-limits demand. Distinct settlement outcomes must survive;
    //    "demand pending" is a baseline state; "litigation preparation" is a pathway; notice, comparative
    //    fault, injury severity, and coverage are shared dependencies with explicit candidate relations. ──
    private const string HarperJson = """
    {
      "schemaVersion": "v2",
      "decisionIntent": { "decisionTarget": "Harper policy-limits demand", "decisionType": "EVALUATE" },
      "queryUnderstanding": "Evaluate outcomes of the Harper premises-liability policy-limits demand.",
      "outcomeProposalHierarchy": {
        "nodes": [
          { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Complete policy-limits settlement", "description": "Full policy-limits recovery.", "distinguishingProposition": "Full recovery, not a below-limits compromise." },
          { "outcomeNodeId": "O2", "parentOutcomeNodeId": null, "level": 1, "title": "Below-limits settlement", "description": "Negotiated settlement below limits.", "distinguishingProposition": "Below limits distinguishes it from full recovery." },
          { "outcomeNodeId": "O3", "parentOutcomeNodeId": null, "level": 1, "title": "Demand pending / status quo", "description": "The demand is currently unresolved and pending.", "distinguishingProposition": "No resolution yet; current posture." },
          { "outcomeNodeId": "O4", "parentOutcomeNodeId": null, "level": 1, "title": "Litigation preparation pathway", "description": "Proceed to file suit and discovery preparation.", "distinguishingProposition": "A procedural path, not a resolution." }
        ]
      },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "legal_element", "label": "Notice of hazard", "semanticQuestion": "Did the store have notice of the hazard?", "whyOutcomeRelevant": "Notice governs premises liability.", "children": [] },
        { "rootId": "B2", "rootKind": "legal_element", "label": "Comparative fault", "semanticQuestion": "What is the plaintiff's comparative fault?", "whyOutcomeRelevant": "Comparative fault reduces recovery.", "children": [] },
        { "rootId": "B3", "rootKind": "factual", "label": "Injury severity", "semanticQuestion": "How severe are the injuries?", "whyOutcomeRelevant": "Severity drives damages.", "children": [] },
        { "rootId": "B4", "rootKind": "applicability_question", "label": "Available coverage", "semanticQuestion": "Is coverage available?", "whyOutcomeRelevant": "Coverage caps recovery.", "children": [] }
      ],
      "candidates": [
        { "candidateId": "C1", "resolution": "Complete policy-limits settlement", "candidateType": "resolution", "rationaleSummary": "Strong liability.", "score": 0.6, "originatingOutcomeNodeIds": ["O1"] },
        { "candidateId": "C2", "resolution": "Below-limits settlement", "candidateType": "resolution", "rationaleSummary": "Comparative fault risk.", "score": 0.5, "originatingOutcomeNodeIds": ["O2"] },
        { "candidateId": "C3", "resolution": "Demand pending / status quo", "candidateType": "baseline", "rationaleSummary": "Unresolved.", "score": 0.2, "originatingOutcomeNodeIds": ["O3"] },
        { "candidateId": "C4", "resolution": "Litigation preparation pathway", "candidateType": "pathway", "rationaleSummary": "Prepare to file suit.", "score": 0.3, "originatingOutcomeNodeIds": ["O4"] }
      ],
      "candidateBranchRelations": [
        { "candidateId": "C1", "branchId": "B1", "relationType": "required", "rationale": "Notice required." },
        { "candidateId": "C1", "branchId": "B4", "relationType": "required", "rationale": "Coverage required." },
        { "candidateId": "C2", "branchId": "B2", "relationType": "required", "rationale": "Comparative fault reduces." },
        { "candidateId": "C2", "branchId": "B3", "relationType": "supports", "rationale": "Injury severity." }
      ],
      "unresolvedPropositions": [],
      "factProvenance": []
    }
    """;

    // ── Mendoza: settlement enforceability. Negotiated, adjudicated, and non-settlement termination
    //    families must survive; a confidential negotiated settlement and an early mediated settlement
    //    overlap (REQUIRES_REVIEW, not silent MERGE); settlement-related dismissal must stay distinct from
    //    an independent dismissal; agreement existence and enforceability are shared dependencies. ──
    private const string MendozaJson = """
    {
      "schemaVersion": "v2",
      "decisionIntent": { "decisionTarget": "Mendoza agreement enforceability", "decisionType": "EVALUATE" },
      "queryUnderstanding": "Evaluate the Mendoza settlement-agreement enforceability outcomes.",
      "outcomeProposalHierarchy": {
        "nodes": [
          { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Confidential negotiated settlement", "description": "A confidential negotiated settlement.", "distinguishingProposition": "Negotiated and confidential." },
          { "outcomeNodeId": "O2", "parentOutcomeNodeId": null, "level": 1, "title": "Early mediated settlement", "description": "An early mediated settlement.", "distinguishingProposition": "Mediated and early." },
          { "outcomeNodeId": "O3", "parentOutcomeNodeId": null, "level": 1, "title": "Adjudicated judgment", "description": "A judgment after trial.", "distinguishingProposition": "Adjudicated, not negotiated." },
          { "outcomeNodeId": "O4", "parentOutcomeNodeId": null, "level": 1, "title": "Settlement-related dismissal", "description": "Dismissal following settlement.", "distinguishingProposition": "Dismissal tied to a settlement." },
          { "outcomeNodeId": "O5", "parentOutcomeNodeId": null, "level": 1, "title": "Independent dismissal", "description": "Dismissal on independent procedural grounds.", "distinguishingProposition": "Dismissal unrelated to any settlement." }
        ]
      },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "legal_element", "label": "Agreement existence", "semanticQuestion": "Does a binding agreement exist?", "whyOutcomeRelevant": "No agreement, nothing to enforce.", "children": [] },
        { "rootId": "B2", "rootKind": "legal_element", "label": "Agreement enforceability", "semanticQuestion": "Is the agreement enforceable?", "whyOutcomeRelevant": "Enforceability governs relief.", "children": [] }
      ],
      "candidates": [
        { "candidateId": "C1", "resolution": "Confidential negotiated settlement", "candidateType": "resolution", "rationaleSummary": "Signed and confidential.", "score": 0.6, "originatingOutcomeNodeIds": ["O1"] },
        { "candidateId": "C2", "resolution": "Early mediated settlement", "candidateType": "resolution", "rationaleSummary": "Mediated early.", "score": 0.55, "originatingOutcomeNodeIds": ["O2"] },
        { "candidateId": "C3", "resolution": "Adjudicated judgment", "candidateType": "resolution", "rationaleSummary": "Trial judgment.", "score": 0.4, "originatingOutcomeNodeIds": ["O3"] },
        { "candidateId": "C4", "resolution": "Settlement-related dismissal", "candidateType": "resolution", "rationaleSummary": "Dismissed after settlement.", "score": 0.3, "originatingOutcomeNodeIds": ["O4"] },
        { "candidateId": "C5", "resolution": "Independent dismissal", "candidateType": "resolution", "rationaleSummary": "Dismissed on procedural grounds.", "score": 0.25, "originatingOutcomeNodeIds": ["O5"] }
      ],
      "candidateBranchRelations": [
        { "candidateId": "C1", "branchId": "B1", "relationType": "required", "rationale": "Agreement must exist." },
        { "candidateId": "C1", "branchId": "B2", "relationType": "required", "rationale": "Must be enforceable." },
        { "candidateId": "C2", "branchId": "B2", "relationType": "required", "rationale": "Must be enforceable." }
      ],
      "unresolvedPropositions": [],
      "factProvenance": []
    }
    """;

    // ── Harper ─────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Harper_PreservesDistinctSettlementOutcomes()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16);
        // Complete policy-limits (C1) and below-limits (C2) are materially opposed ⇒ never merged.
        var complete = gate.Plan.Candidates.Single(c => c.RepresentativeSemanticId == "C1");
        var below = gate.Plan.Candidates.Single(c => c.RepresentativeSemanticId == "C2");
        Assert.NotEqual(complete.Identity.Signature, below.Identity.Signature);
        Assert.Empty(complete.MergedSemanticIds);
        Assert.Empty(below.MergedSemanticIds);
    }

    [Fact]
    public void Harper_ClassifiesDemandPending_AsBaselineState() =>
        Assert.Equal(
            LegalDecisionService.CandidateSemanticRole.BaselineState,
            LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16)
                .Plan.Candidates.Single(c => c.RepresentativeSemanticId == "C3").Role);

    [Fact]
    public void Harper_ClassifiesLitigationPreparation_AsPathway() =>
        Assert.Equal(
            LegalDecisionService.CandidateSemanticRole.ProceduralPathway,
            LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16)
                .Plan.Candidates.Single(c => c.RepresentativeSemanticId == "C4").Role);

    [Fact]
    public void Harper_MovesNoticeFaultSeverityCoverage_IntoSharedDependencies()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16);
        var ids = gate.Plan.Dependencies.Select(d => d.DependencyId).ToHashSet();
        Assert.Contains("B1", ids); // notice
        Assert.Contains("B2", ids); // comparative fault
        Assert.Contains("B3", ids); // injury severity
        Assert.Contains("B4", ids); // coverage
    }

    [Fact]
    public void Harper_RegistersExplicitCandidateToDependencyRelations()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16);
        Assert.Equal(4, gate.Plan.Edges.Count);
        Assert.Contains(gate.Plan.Edges, e => e.CandidateSemanticId == "C1" && e.DependencyId == "B1");
        Assert.Contains(gate.Plan.Edges, e => e.CandidateSemanticId == "C2" && e.DependencyId == "B2");
        // No manufactured edges: C3/C4 had no explicit relations, so none exist.
        Assert.DoesNotContain(gate.Plan.Edges, e => e.CandidateSemanticId == "C3");
        Assert.DoesNotContain(gate.Plan.Edges, e => e.CandidateSemanticId == "C4");
    }

    [Fact]
    public void Harper_ProducesValidRegistrationPlan_AndDiagnostics()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16);
        Assert.True(gate.Plan.IsValid);
        Assert.Equal(4, gate.Diagnostics.RawProposalCount);
        Assert.Equal(4, gate.Diagnostics.NormalizedCandidates);
        Assert.Equal(0, gate.Diagnostics.DuplicateMappings);
        Assert.Equal(4, gate.Diagnostics.SharedDependencies);
        Assert.Equal(4, gate.Diagnostics.ValidatedRelationCount);
    }

    // ── Mendoza ────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Mendoza_PreservesNegotiatedAdjudicatedAndNonSettlementFamilies()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(MendozaJson, 16);
        var negotiated = gate.Plan.Candidates.Single(c => c.RepresentativeSemanticId == "C1");
        var adjudicated = gate.Plan.Candidates.Single(c => c.RepresentativeSemanticId == "C3");
        var termination = gate.Plan.Candidates.Single(c => c.RepresentativeSemanticId == "C5");
        Assert.Equal("negotiated_settlement", negotiated.Identity.Family);
        Assert.Equal("adjudicated_resolution", adjudicated.Identity.Family);
        Assert.Equal("non_settlement_termination", termination.Identity.Family);
    }

    [Fact]
    public void Mendoza_DetectsOverlap_ConfidentialNegotiated_And_EarlyMediated_AsRequiresReview()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(MendozaJson, 16);
        // Same family (negotiated_settlement), no antonym conflict, different distinctive qualifiers ⇒
        // overlapping but not provably identical ⇒ REQUIRES_REVIEW (never silently merged).
        var c2 = gate.Plan.Candidates.Single(c => c.RepresentativeSemanticId == "C2");
        Assert.Equal(LegalDecisionService.NormalizationDecision.RequiresReview, c2.Decision);
        Assert.Empty(c2.MergedSemanticIds);
        Assert.Equal(0, gate.Diagnostics.DuplicateMappings);
    }

    [Fact]
    public void Mendoza_DistinguishesSettlementRelatedDismissal_FromIndependentDismissal()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(MendozaJson, 16);
        var related = gate.Plan.Candidates.Single(c => c.RepresentativeSemanticId == "C4");
        var independent = gate.Plan.Candidates.Single(c => c.RepresentativeSemanticId == "C5");
        // Antonym conflict (related vs independent) ⇒ kept distinct.
        Assert.Equal(LegalDecisionService.NormalizationDecision.KeepDistinct, related.Decision);
        Assert.NotEqual(related.Identity.Signature, independent.Identity.Signature);
        Assert.Empty(related.MergedSemanticIds);
        Assert.Empty(independent.MergedSemanticIds);
    }

    [Fact]
    public void Mendoza_IdentifiesAgreementExistenceAndEnforceability_AsSharedDependencies()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(MendozaJson, 16);
        var ids = gate.Plan.Dependencies.Select(d => d.DependencyId).ToHashSet();
        Assert.Contains("B1", ids); // agreement existence
        Assert.Contains("B2", ids); // enforceability
        Assert.All(gate.Plan.Dependencies, d =>
            Assert.Equal(LegalDecisionService.DependencyCategory.Legal, d.Category));
    }

    [Fact]
    public void Mendoza_PreservesAllFiveDistinctOutcomeFamilies_NoSilentCollapse()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(MendozaJson, 16);
        Assert.Equal(5, gate.Plan.Candidates.Count);
        Assert.Equal(0, gate.Diagnostics.DuplicateMappings);
        Assert.True(gate.Plan.IsValid);
    }

    // ── Competition eligibility (§9): only substantive/conditional resolutions with settled identity
    //    reach Core; baseline/procedural/dependency roles and REQUIRES_REVIEW identities are registered
    //    but withheld from authoritative competition. Identity is still preserved in Plan.Candidates. ──
    [Fact]
    public void Harper_WithholdsBaselineAndPathway_FromCompetition_ButKeepsThemRegistered()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16);
        Assert.Equal(4, gate.Plan.Candidates.Count);
        Assert.Equal(new[] { "C1", "C2" }, gate.Plan.EligibleCandidateSemanticIds);
        Assert.Contains("C3", gate.Plan.DeferredCandidateSemanticIds);
        Assert.Contains("C4", gate.Plan.DeferredCandidateSemanticIds);
        Assert.Equal(2, gate.Diagnostics.EligibleCandidateCount);
        Assert.Equal(2, gate.Diagnostics.DeferredCandidateCount);
    }

    [Fact]
    public void Harper_NormalizedProposal_CarriesOnlyEligibleCandidates_IntoCore()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16);
        var proposedIds = gate.NormalizedProposal.Select(c => c.SemanticCandidateId).ToArray();
        Assert.Equal(new[] { "C1", "C2" }, proposedIds);
    }

    [Fact]
    public void Mendoza_RequiresReviewCandidate_IsWithheld_FromCompetition()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(MendozaJson, 16);
        Assert.Contains("C2", gate.Plan.DeferredCandidateSemanticIds);
        Assert.DoesNotContain("C2", gate.Plan.EligibleCandidateSemanticIds);
        Assert.DoesNotContain(gate.NormalizedProposal, c => c.SemanticCandidateId == "C2");
    }
}
