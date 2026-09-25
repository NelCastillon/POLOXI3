using Legal.Application;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Atomic-L3 completeness gate (DECISION_DISCOVERY_V2 / LEGAL_DECISION EVALUATE).
// Locks in the conservative, purely-structural SHARED_DEPENDENCY_NOT_DECOMPOSED contract defect:
// a shared semanticRoots forest that exists but never decomposes below broad L1/L2 dimensions
// (entirely flat AND no interrogative/atomic leaf) is flagged so the existing bounded single-shot
// recovery fires. A single atomic (interrogative) leaf makes a flat forest already-atomic and is
// NOT flagged — this preserves legitimately flat atomic forests without any lexical dimension list.
public sealed class DecisionDiscoveryAtomicL3CompletenessContractTests
{
    // Broad, undecomposed dimensions: flat semanticRoots whose labels are dimension headings and whose
    // interpretations carry no concrete testable question. This is the Mendoza/Harper defect.
    private const string BroadFlatDimensionsJson = """
    {
      "schemaVersion": "v2",
      "decisionIntent": { "decisionTarget": "Mendoza enforceability", "decisionType": "EVALUATE" },
      "queryUnderstanding": "Evaluate the Mendoza settlement enforceability outcomes.",
      "outcomeProposalHierarchy": {
        "nodes": [
          { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Enforceable settlement", "description": "The agreement is enforceable.", "distinguishingProposition": "Enforceable, not void." },
          { "outcomeNodeId": "O2", "parentOutcomeNodeId": null, "level": 1, "title": "Unenforceable settlement", "description": "The agreement is not enforceable.", "distinguishingProposition": "Void, not enforceable." }
        ]
      },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "grouping", "label": "Settlement enforceability", "interpretation": "The enforceability dimension.", "children": [] },
        { "rootId": "B2", "rootKind": "grouping", "label": "Damages", "interpretation": "The damages dimension.", "children": [] }
      ],
      "candidates": [
        { "candidateId": "C1", "resolution": "Enforceable settlement", "candidateType": "resolution", "rationaleSummary": "Terms agreed.", "score": 0.6, "originatingOutcomeNodeIds": ["O1"] },
        { "candidateId": "C2", "resolution": "Unenforceable settlement", "candidateType": "resolution", "rationaleSummary": "No writing.", "score": 0.4, "originatingOutcomeNodeIds": ["O2"] }
      ],
      "candidateBranchRelations": [
        { "candidateId": "C1", "branchId": "B1", "relationType": "required", "rationale": "Enforceability required." }
      ],
      "unresolvedPropositions": [],
      "factProvenance": []
    }
    """;

    // Legitimately flat atomic forest: flat semanticRoots but each leaf poses a concrete testable
    // question (interrogative semanticQuestion). Must NOT be flagged.
    private const string FlatAtomicJson = """
    {
      "schemaVersion": "v2",
      "decisionIntent": { "decisionTarget": "Mendoza enforceability", "decisionType": "EVALUATE" },
      "queryUnderstanding": "Evaluate the Mendoza settlement enforceability outcomes.",
      "outcomeProposalHierarchy": {
        "nodes": [
          { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Enforceable settlement", "description": "Enforceable.", "distinguishingProposition": "Enforceable, not void." },
          { "outcomeNodeId": "O2", "parentOutcomeNodeId": null, "level": 1, "title": "Unenforceable settlement", "description": "Not enforceable.", "distinguishingProposition": "Void, not enforceable." }
        ]
      },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "legal_element", "label": "Offer accepted", "semanticQuestion": "Was a definite offer communicated and unequivocally accepted?", "whyOutcomeRelevant": "Acceptance forms the contract.", "children": [] },
        { "rootId": "B2", "rootKind": "legal_element", "label": "Statute of frauds", "semanticQuestion": "Is the agreement barred by any writing requirement?", "whyOutcomeRelevant": "A writing may be required.", "children": [] }
      ],
      "candidates": [
        { "candidateId": "C1", "resolution": "Enforceable settlement", "candidateType": "resolution", "rationaleSummary": "Terms agreed.", "score": 0.6, "originatingOutcomeNodeIds": ["O1"] },
        { "candidateId": "C2", "resolution": "Unenforceable settlement", "candidateType": "resolution", "rationaleSummary": "No writing.", "score": 0.4, "originatingOutcomeNodeIds": ["O2"] }
      ],
      "candidateBranchRelations": [
        { "candidateId": "C1", "branchId": "B1", "relationType": "required", "rationale": "Acceptance required." }
      ],
      "unresolvedPropositions": [],
      "factProvenance": []
    }
    """;

    // Nested forest: a broad Damages grouping decomposed into atomic L3 children. Must NOT be flagged
    // (the forest has depth).
    private const string NestedForestJson = """
    {
      "schemaVersion": "v2",
      "decisionIntent": { "decisionTarget": "Recovery", "decisionType": "EVALUATE" },
      "queryUnderstanding": "Evaluate recovery outcomes.",
      "outcomeProposalHierarchy": {
        "nodes": [
          { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Plaintiff recovery", "description": "Recovers.", "distinguishingProposition": "Recovery, not defense verdict." },
          { "outcomeNodeId": "O2", "parentOutcomeNodeId": null, "level": 1, "title": "Defense verdict", "description": "No recovery.", "distinguishingProposition": "No recovery." }
        ]
      },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "grouping", "label": "Damages", "interpretation": "The damages dimension.", "children": [
          { "rootId": "B1a", "rootKind": "factual", "label": "Documented economic damages", "semanticQuestion": "Are the claimed economic damages documented?", "children": [] },
          { "rootId": "B1b", "rootKind": "legal_element", "label": "Injury threshold", "semanticQuestion": "Do the injuries meet the applicable threshold?", "children": [] }
        ] }
      ],
      "candidates": [
        { "candidateId": "C1", "resolution": "Plaintiff recovery", "candidateType": "resolution", "rationaleSummary": "Strong.", "score": 0.6, "originatingOutcomeNodeIds": ["O1"] },
        { "candidateId": "C2", "resolution": "Defense verdict", "candidateType": "resolution", "rationaleSummary": "Weak.", "score": 0.4, "originatingOutcomeNodeIds": ["O2"] }
      ],
      "candidateBranchRelations": [
        { "candidateId": "C1", "branchId": "B1a", "relationType": "supports", "rationale": "Economic damages." }
      ],
      "unresolvedPropositions": [],
      "factProvenance": []
    }
    """;

    [Fact]
    public void BroadUndecomposedDimensions_AreFlagged_AsNotDecomposed()
    {
        var defects = LegalDecisionService.DiagnoseDualHierarchyContractForTest(BroadFlatDimensionsJson, 8);
        Assert.Contains("SHARED_DEPENDENCY_NOT_DECOMPOSED", defects);
        // The shared hierarchy IS present — this is a depth defect, not a missing-hierarchy defect.
        Assert.DoesNotContain("MISSING_SHARED_DEPENDENCY_HIERARCHY", defects);
    }

    [Fact]
    public void FlatAtomicForest_IsNotFlagged()
    {
        var defects = LegalDecisionService.DiagnoseDualHierarchyContractForTest(FlatAtomicJson, 8);
        Assert.DoesNotContain("SHARED_DEPENDENCY_NOT_DECOMPOSED", defects);
    }

    [Fact]
    public void NestedForest_WithAtomicChildren_IsNotFlagged()
    {
        var defects = LegalDecisionService.DiagnoseDualHierarchyContractForTest(NestedForestJson, 8);
        Assert.DoesNotContain("SHARED_DEPENDENCY_NOT_DECOMPOSED", defects);
    }
}
