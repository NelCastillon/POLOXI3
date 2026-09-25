using Legal.Application;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ── Universal Legal Factor Inventory projection ─────────────────────────────────────────────────
// Proves the Factor Inventory is projected from the SAME validated normalization plan the inspector
// uses (global factor identities = shared dependencies; Candidate×Factor relationships = edges) and
// that matter data supplies actual values with explicit MISSING/UNVERIFIED state when absent. No LLM,
// no network, no DB: the projector is instance-free and reads only the deterministic plan + snapshot.
public sealed class FactorInventoryProjectionTests
{
    // Reuse the committed Harper fixture (premises-liability policy-limits demand).
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

    private static MatterContextSnapshot BuildMatterContext()
        => new(
            MatterId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            OriginalQuestion: "Evaluate the Harper policy-limits demand.",
            DomainPackCode: "PI_PREMISES",
            PracticeAreaCode: "PI",
            Decision: [],
            LegalScope: [],
            PersonalInjuryProfile: [],
            // Matter data supplies an actual value for the "Available coverage" factor.
            Facts: [new MatterContextField("Available coverage", "$1,000,000 policy limits confirmed", MatterFieldProvenance.Supplied)],
            Evidence: []);

    [Fact]
    public void Factors_AreGlobalIdentities_NotDuplicatedPerCandidate()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16);

        var inventory = IntelligenceWide2Service.ProjectFactorInventory(
            gate.Plan, BuildMatterContext(), domainPackResolved: true, domainPackCode: "PI_PREMISES", anyCandidateDelivered: true);

        // One factor row per shared dependency — never one per candidate edge.
        Assert.Equal(gate.Plan.Dependencies.Count, inventory.Factors.Count);
        Assert.Equal(
            inventory.Factors.Select(f => f.FactorId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            inventory.Factors.Count);

        // Every factor exposes the full eight-field contract with no empty required strings.
        Assert.All(inventory.Factors, f =>
        {
            Assert.False(string.IsNullOrWhiteSpace(f.FactorName));
            Assert.False(string.IsNullOrWhiteSpace(f.Source));
            Assert.False(string.IsNullOrWhiteSpace(f.Availability));
            Assert.False(string.IsNullOrWhiteSpace(f.VerificationStatus));
            Assert.False(string.IsNullOrWhiteSpace(f.Requirement));
        });
    }

    [Fact]
    public void Factors_WithoutMatterValue_StayMissingAndUnverified_NotFabricated()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16);

        var inventory = IntelligenceWide2Service.ProjectFactorInventory(
            gate.Plan, BuildMatterContext(), domainPackResolved: true, domainPackCode: "PI_PREMISES", anyCandidateDelivered: true);

        // Coverage factor (B4) has a supplied matter value → AVAILABLE + SUPPLIED with a real value.
        var coverage = inventory.Factors.Single(f => f.FactorId == "B4");
        Assert.Equal("AVAILABLE", coverage.Availability);
        Assert.Equal("SUPPLIED", coverage.VerificationStatus);
        Assert.False(string.IsNullOrWhiteSpace(coverage.ActualValue));

        // Comparative fault (B2) has no matter value → explicit MISSING/UNVERIFIED, value never invented.
        var fault = inventory.Factors.Single(f => f.FactorId == "B2");
        Assert.Equal("MISSING", fault.Availability);
        Assert.Equal("UNVERIFIED", fault.VerificationStatus);
        Assert.Null(fault.ActualValue);
        Assert.False(string.IsNullOrWhiteSpace(fault.MissingInformation));

        Assert.True(inventory.MissingFactorCount >= 1);
    }

    [Fact]
    public void Relationships_ConnectFactorsToCandidates_WithRequiredDistinction()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16);

        var inventory = IntelligenceWide2Service.ProjectFactorInventory(
            gate.Plan, BuildMatterContext(), domainPackResolved: true, domainPackCode: "PI_PREMISES", anyCandidateDelivered: true);

        // Notice (B1) is REQUIRED for the full-recovery candidate C1.
        var noticeRel = inventory.Relationships.Single(r => r.FactorId == "B1" && r.CandidateId == "C1");
        Assert.Equal("REQUIRED", noticeRel.RelationType, ignoreCase: true);
        Assert.True(noticeRel.IsRequired);

        // Injury severity (B3) SUPPORTS candidate C2 — evaluative, not a required-to-establish factor.
        var severityRel = inventory.Relationships.Single(r => r.FactorId == "B3" && r.CandidateId == "C2");
        Assert.False(severityRel.IsRequired);

        Assert.Equal(gate.Plan.Candidates.Count, inventory.CandidateCount);
    }
}
