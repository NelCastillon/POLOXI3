using Legal.Application;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Explicit hierarchy-completeness contract defects for the branch-first DECISION_DISCOVERY_V2 pipeline.
// These lock in the finer-grained completeness governance added to DiagnoseDualHierarchyContract and the
// single bounded recovery:
//   • L2_MISSING                 — a decomposed forest still leaves an L1 dimension as a bare leaf.
//   • L3_MISSING                 — a decomposed dimension carries no atomic (interrogative) leaf.
//   • INVALID_FACTOR_PROPOSITION — a shared-forest leaf duplicates an outcome title or candidate name.
//   • INVALID_PARENT_RELATIONSHIP— an outcome node's parentOutcomeNodeId references a non-existent node.
// All checks are deterministic and structural; a legitimately flat atomic forest must NOT be flagged.
public sealed class DecisionDiscoveryHierarchyCompletenessContractTests
{
    // A decomposed dimension B1 (with an atomic L2 child) sitting beside a bare-leaf dimension B2. The
    // presence of depth means B2 should have been decomposed too => L2_MISSING.
    private const string L2MissingJson = """
    {
      "outcomeProposalHierarchy": { "nodes": [ { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Settle", "description": null, "distinguishingProposition": null } ] },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "legal_element", "label": "Liability", "semanticQuestion": "Liability?", "children": [
          { "rootId": "B1.1", "rootKind": "legal_element", "label": "Notice", "semanticQuestion": "Did the store have notice of the hazard?", "children": [] }
        ] },
        { "rootId": "B2", "rootKind": "legal_element", "label": "Damages", "semanticQuestion": "Damages", "children": [] }
      ],
      "candidates": [ { "candidateId": "C1", "resolution": "Settle", "candidateType": "resolution", "rationaleSummary": "x", "score": 0.5, "originatingOutcomeNodeIds": ["O1"] } ]
    }
    """;

    // A decomposed dimension B1 whose only child B1.1 is itself a broad, non-interrogative label — the
    // subtree contains no atomic leaf => L3_MISSING.
    private const string L3MissingJson = """
    {
      "outcomeProposalHierarchy": { "nodes": [ { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Settle", "description": null, "distinguishingProposition": null } ] },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "legal_element", "label": "Settlement enforceability", "semanticQuestion": "Enforceable?", "children": [
          { "rootId": "B1.1", "rootKind": "legal_element", "label": "Offer and acceptance", "semanticQuestion": "Offer and acceptance", "children": [] }
        ] }
      ],
      "candidates": [ { "candidateId": "C1", "resolution": "Settle", "candidateType": "resolution", "rationaleSummary": "x", "score": 0.5, "originatingOutcomeNodeIds": ["O1"] } ]
    }
    """;

    // A decomposed dimension whose atomic children are genuine interrogative propositions — complete, so
    // neither L2_MISSING nor L3_MISSING should fire.
    private const string CompleteDecomposedJson = """
    {
      "outcomeProposalHierarchy": { "nodes": [ { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Settle", "description": null, "distinguishingProposition": null } ] },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "legal_element", "label": "Settlement enforceability", "semanticQuestion": "Enforceable?", "children": [
          { "rootId": "B1.1", "rootKind": "legal_element", "label": "Acceptance timing", "semanticQuestion": "Was acceptance timely under the applicable deadline?", "children": [] },
          { "rootId": "B1.2", "rootKind": "legal_element", "label": "Material terms", "semanticQuestion": "Were all material terms agreed?", "children": [] }
        ] }
      ],
      "candidates": [ { "candidateId": "C1", "resolution": "Settle", "candidateType": "resolution", "rationaleSummary": "x", "score": 0.5, "originatingOutcomeNodeIds": ["O1"] } ]
    }
    """;

    // A legitimately flat atomic forest — every root is a concrete interrogative proposition. This must
    // NOT be flagged by any completeness defect (it is governed solely by SHARED_DEPENDENCY_NOT_DECOMPOSED,
    // which itself does not fire because the leaves are atomic).
    private const string FlatAtomicForestJson = """
    {
      "outcomeProposalHierarchy": { "nodes": [ { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Settle", "description": null, "distinguishingProposition": null } ] },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "legal_element", "label": "Notice", "semanticQuestion": "Did the store have notice of the hazard?", "children": [] },
        { "rootId": "B2", "rootKind": "legal_element", "label": "Coverage", "semanticQuestion": "Is coverage available for the loss?", "children": [] }
      ],
      "candidates": [ { "candidateId": "C1", "resolution": "Settle", "candidateType": "resolution", "rationaleSummary": "x", "score": 0.5, "originatingOutcomeNodeIds": ["O1"] } ]
    }
    """;

    // A shared-forest leaf that duplicates an outcome-node title ("Settle the claim") — an
    // outcome/candidate leaked in as a factor => INVALID_FACTOR_PROPOSITION.
    private const string InvalidFactorPropositionJson = """
    {
      "outcomeProposalHierarchy": { "nodes": [ { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Settle the claim", "description": null, "distinguishingProposition": null } ] },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "legal_element", "label": "Notice", "semanticQuestion": "Did the store have notice of the hazard?", "children": [] },
        { "rootId": "B2", "rootKind": "legal_element", "label": "Settle the claim", "semanticQuestion": "Settle the claim", "children": [] }
      ],
      "candidates": [ { "candidateId": "C1", "resolution": "Settle", "candidateType": "resolution", "rationaleSummary": "x", "score": 0.5, "originatingOutcomeNodeIds": ["O1"] } ]
    }
    """;

    // An outcome node O2 whose parentOutcomeNodeId points at a node that does not exist =>
    // INVALID_PARENT_RELATIONSHIP.
    private const string InvalidParentRelationshipJson = """
    {
      "outcomeProposalHierarchy": { "nodes": [
        { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Settle", "description": null, "distinguishingProposition": null },
        { "outcomeNodeId": "O2", "parentOutcomeNodeId": "O_MISSING", "level": 2, "title": "Below-limits settle", "description": null, "distinguishingProposition": null }
      ] },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "legal_element", "label": "Notice", "semanticQuestion": "Did the store have notice of the hazard?", "children": [] }
      ],
      "candidates": [ { "candidateId": "C1", "resolution": "Settle", "candidateType": "resolution", "rationaleSummary": "x", "score": 0.5, "originatingOutcomeNodeIds": ["O1", "O2"] } ]
    }
    """;

    [Fact]
    public void DiagnoseDualHierarchyContract_FlagsL2Missing_WhenDecomposedForestLeavesBareRootDimension() =>
        Assert.Contains("L2_MISSING",
            LegalDecisionService.DiagnoseDualHierarchyContractForTest(L2MissingJson, 8));

    [Fact]
    public void DiagnoseDualHierarchyContract_FlagsL3Missing_WhenDimensionHasNoAtomicLeaf() =>
        Assert.Contains("L3_MISSING",
            LegalDecisionService.DiagnoseDualHierarchyContractForTest(L3MissingJson, 8));

    [Fact]
    public void DiagnoseDualHierarchyContract_DoesNotFlagDepth_ForCompleteDecomposedForest()
    {
        var defects = LegalDecisionService.DiagnoseDualHierarchyContractForTest(CompleteDecomposedJson, 8);
        Assert.DoesNotContain("L2_MISSING", defects);
        Assert.DoesNotContain("L3_MISSING", defects);
    }

    [Fact]
    public void DiagnoseDualHierarchyContract_DoesNotFlagCompleteness_ForFlatAtomicForest()
    {
        var defects = LegalDecisionService.DiagnoseDualHierarchyContractForTest(FlatAtomicForestJson, 8);
        Assert.DoesNotContain("L2_MISSING", defects);
        Assert.DoesNotContain("L3_MISSING", defects);
        Assert.DoesNotContain("SHARED_DEPENDENCY_NOT_DECOMPOSED", defects);
        Assert.DoesNotContain("INVALID_FACTOR_PROPOSITION", defects);
    }

    [Fact]
    public void DiagnoseDualHierarchyContract_FlagsInvalidFactorProposition_WhenLeafDuplicatesOutcomeTitle() =>
        Assert.Contains("INVALID_FACTOR_PROPOSITION",
            LegalDecisionService.DiagnoseDualHierarchyContractForTest(InvalidFactorPropositionJson, 8));

    [Fact]
    public void DiagnoseDualHierarchyContract_FlagsInvalidParentRelationship_WhenOutcomeParentIsDangling() =>
        Assert.Contains("INVALID_PARENT_RELATIONSHIP",
            LegalDecisionService.DiagnoseDualHierarchyContractForTest(InvalidParentRelationshipJson, 8));
}
