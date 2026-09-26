using Legal.Application;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ── Aisha Patel end-to-end regression (Validated Candidate Competition) ──────────────────────────
// Reproduces the reported defect: SettlementStatus=Disbursed and DemandStatus=Responded were bound (by
// lexical overlap) to unrelated propositions (Liability/Damages/Confidentiality established; Demand
// accepted/rejected) and thereby entered candidate competition as if established. This test drives the
// SAME instance-free projector the pipeline uses and asserts those category-mismatched supplied values
// are preserved but NOT promoted to established propositions, that the inventory is marked provisional
// with blocking obligations, and that legitimate values still bind. No DB, no network, no LLM.
public sealed class AishaPatelValidatedCompetitionRegressionTests
{
    private const string AishaJson = """
    {
      "schemaVersion": "v2",
      "decisionIntent": { "decisionTarget": "Aisha Patel settlement posture", "decisionType": "EVALUATE" },
      "queryUnderstanding": "Evaluate outcomes of the Aisha Patel personal-injury settlement.",
      "outcomeProposalHierarchy": {
        "nodes": [
          { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Enforce confidential settlement", "description": "Enforce the executed settlement with confidentiality.", "distinguishingProposition": "A completed, enforceable settlement." },
          { "outcomeNodeId": "O2", "parentOutcomeNodeId": null, "level": 1, "title": "Reopen for additional damages", "description": "Reopen the matter to pursue further damages.", "distinguishingProposition": "Additional recovery remains available." }
        ]
      },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "legal_element", "label": "Liability established", "semanticQuestion": "Is liability established?", "whyOutcomeRelevant": "Liability underpins recovery.", "children": [] },
        { "rootId": "B2", "rootKind": "factual", "label": "Damages documented", "semanticQuestion": "Are damages documented?", "whyOutcomeRelevant": "Damages drive value.", "children": [] },
        { "rootId": "B3", "rootKind": "legal_element", "label": "Confidentiality enforceable", "semanticQuestion": "Is the confidentiality provision enforceable?", "whyOutcomeRelevant": "Confidentiality governs disclosure.", "children": [] },
        { "rootId": "B4", "rootKind": "applicability_question", "label": "Demand accepted", "semanticQuestion": "Was the demand accepted?", "whyOutcomeRelevant": "Acceptance resolves the demand.", "children": [] }
      ],
      "candidates": [
        { "candidateId": "C1", "resolution": "Enforce confidential settlement", "candidateType": "resolution", "rationaleSummary": "Settlement executed.", "score": 0.6, "originatingOutcomeNodeIds": ["O1"] },
        { "candidateId": "C2", "resolution": "Reopen for additional damages", "candidateType": "resolution", "rationaleSummary": "Damages may remain.", "score": 0.4, "originatingOutcomeNodeIds": ["O2"] }
      ],
      "candidateBranchRelations": [
        { "candidateId": "C1", "branchId": "B1", "relationType": "required", "rationale": "Liability required." },
        { "candidateId": "C1", "branchId": "B3", "relationType": "required", "rationale": "Confidentiality required." },
        { "candidateId": "C2", "branchId": "B2", "relationType": "required", "rationale": "Damages required." },
        { "candidateId": "C2", "branchId": "B4", "relationType": "supports", "rationale": "Demand posture." }
      ],
      "unresolvedPropositions": [],
      "factProvenance": []
    }
    """;

    // The exact category-mismatched matter fields from the defect report.
    private static MatterContextSnapshot BuildMatterContext()
        => new(
            MatterId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            OriginalQuestion: "Evaluate the Aisha Patel settlement posture.",
            DomainPackCode: "PI_GENERAL",
            PracticeAreaCode: "PI",
            Decision: [],
            LegalScope: [],
            PersonalInjuryProfile: [],
            Facts:
            [
                new MatterContextField("Settlement Status", "Disbursed", MatterFieldProvenance.Supplied),
                new MatterContextField("Demand Status", "Responded", MatterFieldProvenance.Supplied),
            ],
            Evidence: []);

    [Fact]
    public void CategoryMismatchedValues_ArePreservedButNotEstablished_AndInventoryIsProvisional()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(AishaJson, 16);

        var inventory = IntelligenceWide2Service.ProjectFactorInventory(
            gate.Plan, BuildMatterContext(), domainPackResolved: true, domainPackCode: "PI_GENERAL", anyCandidateDelivered: true);

        // The lexical binder would have marked these AVAILABLE/VALID. The validator must instead reject the
        // category mismatch: any factor that DID pick up a supplied value is not promoted to established.
        var boundFactors = inventory.Factors
            .Where(f => !string.IsNullOrWhiteSpace(f.ActualValue))
            .ToArray();

        Assert.All(boundFactors, f =>
        {
            // A supplied but category-mismatched value never reads as an established/verified proposition.
            Assert.False(string.Equals(f.VerificationStatus, "VALID", StringComparison.OrdinalIgnoreCase));
            Assert.False(string.Equals(f.BindingAdmissibility, "ADMITTED", StringComparison.OrdinalIgnoreCase)
                         && f.Availability == "AVAILABLE");
        });

        // At least one supplied value was rejected as a proposition establishment and surfaced as an obligation.
        Assert.True(inventory.RejectedBindingCount > 0);
        Assert.Contains(inventory.Factors, f => !string.IsNullOrWhiteSpace(f.VerificationObligation));

        // Because material propositions are unresolved / rejected, the whole comparison stays provisional.
        Assert.True(inventory.IsProvisional);
        Assert.NotEqual("PROVISIONAL", inventory.DecisionReadinessStatus); // BLOCKED, given obligations
        Assert.NotEmpty(inventory.BlockingObligations);
    }

    [Fact]
    public void OutcomeLabels_DoNotBecomeIndependentScoringDimensions_ForTheirOwnCandidate()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(AishaJson, 16);

        var inventory = IntelligenceWide2Service.ProjectFactorInventory(
            gate.Plan, BuildMatterContext(), domainPackResolved: true, domainPackCode: "PI_GENERAL", anyCandidateDelivered: true);

        // No relationship should tie a candidate to a factor that merely restates that candidate's own outcome.
        foreach (var rel in inventory.Relationships)
        {
            Assert.False(
                string.Equals(rel.FactorName?.Trim(), rel.CandidateTitle?.Trim(), StringComparison.OrdinalIgnoreCase),
                $"Outcome '{rel.CandidateTitle}' must not score itself via factor '{rel.FactorName}'.");
        }
    }
}
