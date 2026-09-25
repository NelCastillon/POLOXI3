using System;
using System.Linq;
using Legal.Application;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Parent-vs-Atomic tagging for the shared-dependency (Universal Factor) inventory.
// After the atomic-L3 prompt enhancement (migration 0323), the shared dependency forest can carry
// broad L1/L2 groupings that further decompose into atomic L3 factor propositions. Both levels are
// retained in the inventory for hierarchical context, but each NormalizedDependency must be tagged
// NodeKind = Parent (has children) or Atomic (leaf) so the UI/inventory can filter to atomic factors.
public sealed class DependencyNodeKindTaggingTests
{
    // Damages (L2 grouping) decomposes into two atomic L3 factors; Liability is a flat atomic factor.
    private const string NestedJson = """
    {
      "schemaVersion": "v2",
      "decisionIntent": { "decisionTarget": "Nested factor forest", "decisionType": "EVALUATE" },
      "queryUnderstanding": "Evaluate outcomes with a nested shared-dependency forest.",
      "outcomeProposalHierarchy": {
        "nodes": [
          { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Plaintiff recovery", "description": "Plaintiff recovers damages.", "distinguishingProposition": "Recovery, not defense verdict." },
          { "outcomeNodeId": "O2", "parentOutcomeNodeId": null, "level": 1, "title": "Defense verdict", "description": "Defense prevails.", "distinguishingProposition": "No recovery." }
        ]
      },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "legal_element", "label": "Liability established", "semanticQuestion": "Is the defendant liable?", "whyOutcomeRelevant": "Liability is a precondition for recovery.", "children": [] },
        { "rootId": "B2", "rootKind": "grouping", "label": "Damages", "semanticQuestion": "What damages are recoverable?", "whyOutcomeRelevant": "Damages drive the recovery amount.", "children": [
          { "rootId": "B2a", "rootKind": "factual", "label": "Documented economic damages", "semanticQuestion": "Are the claimed economic damages documented?", "whyOutcomeRelevant": "Documentation supports the economic award.", "children": [] },
          { "rootId": "B2b", "rootKind": "legal_element", "label": "Injury threshold met", "semanticQuestion": "Do the injuries meet the applicable threshold?", "whyOutcomeRelevant": "Threshold gates non-economic damages.", "children": [] }
        ] }
      ],
      "candidates": [
        { "candidateId": "C1", "resolution": "Plaintiff recovery", "candidateType": "resolution", "rationaleSummary": "Strong case.", "score": 0.6, "originatingOutcomeNodeIds": ["O1"] },
        { "candidateId": "C2", "resolution": "Defense verdict", "candidateType": "resolution", "rationaleSummary": "Weak proof.", "score": 0.4, "originatingOutcomeNodeIds": ["O2"] }
      ],
      "candidateBranchRelations": [
        { "candidateId": "C1", "branchId": "B1", "relationType": "required", "rationale": "Liability required." },
        { "candidateId": "C1", "branchId": "B2a", "relationType": "supports", "rationale": "Economic damages." }
      ],
      "unresolvedPropositions": [],
      "factProvenance": []
    }
    """;

    [Fact]
    public void NestedForest_TagsParentGroupings_AndAtomicLeaves()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(NestedJson, 16);
        var deps = gate.Plan.Dependencies.ToDictionary(d => d.DependencyId, StringComparer.OrdinalIgnoreCase);

        // Both levels retained: broad parent + its atomic children + the flat atomic factor.
        Assert.True(deps.ContainsKey("B1"));
        Assert.True(deps.ContainsKey("B2"));
        Assert.True(deps.ContainsKey("B2a"));
        Assert.True(deps.ContainsKey("B2b"));

        // The Damages grouping is a Parent; its children and the flat Liability node are Atomic.
        Assert.Equal(LegalDecisionService.DependencyNodeKind.Parent, deps["B2"].NodeKind);
        Assert.Equal(LegalDecisionService.DependencyNodeKind.Atomic, deps["B2a"].NodeKind);
        Assert.Equal(LegalDecisionService.DependencyNodeKind.Atomic, deps["B2b"].NodeKind);
        Assert.Equal(LegalDecisionService.DependencyNodeKind.Atomic, deps["B1"].NodeKind);
    }

    [Fact]
    public void FlatAtomicForest_TagsEveryLeafAsAtomic()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(NestedJson, 16);

        // Filtering to atomic-only preserves genuine factors and hides only the broad grouping.
        var atomic = gate.Plan.Dependencies
            .Where(d => d.NodeKind == LegalDecisionService.DependencyNodeKind.Atomic)
            .Select(d => d.DependencyId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("B1", atomic);
        Assert.Contains("B2a", atomic);
        Assert.Contains("B2b", atomic);
        Assert.DoesNotContain("B2", atomic);
    }
}
