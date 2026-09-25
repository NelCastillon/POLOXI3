using Legal.Application;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// R2 Dual-Hierarchy Semantic Proposal Contract (DECISION_DISCOVERY_V2 / LEGAL_DECISION EVALUATE).
// Locks in that the R2 hardening:
//   • rewrites the DECISION_DISCOVERY_V2 SystemPrompt WHOLESALE to the "TWO DISTINCT semantic hierarchies"
//     contract and adds the three additive outcome-node columns (migration 0320),
//   • deterministically VALIDATES the returned proposal actually carries both hierarchies + a normalized
//     global candidate pool with valid originatingOutcomeNodeIds lineage (not prompt compliance alone), and
//   • emits targeted contract-defect codes (fed into the existing bounded gate/recovery) rather than
//     silently accepting the old nested-branch Harper shape.
public sealed class DecisionDiscoveryDualHierarchyR2ContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    // Well-formed dual-hierarchy discovery object: an outcomeProposalHierarchy (O*), a SEPARATE shared
    // semanticRoots dependency forest (B*), one global candidate pool with originatingOutcomeNodeIds, and
    // candidateBranchRelations that reference the SHARED branch ids only.
    private const string ValidDualHierarchyJson = """
    {
      "schemaVersion": "v2",
      "decisionIntent": { "decisionTarget": "Harper policy-limits demand", "decisionType": "EVALUATE" },
      "queryUnderstanding": "Evaluate the prospective outcomes of the Harper premises-liability demand.",
      "outcomeProposalHierarchy": {
        "nodes": [
          { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Complete policy-limits settlement", "description": "A completed settlement.", "distinguishingProposition": "A completed settlement is not merely a demand.", "relationshipToDecisionTarget": "Directly answers the demand outcome.", "normalizationStatus": "admitted", "normalizationReason": "Distinct decision-responsive outcome." },
          { "outcomeNodeId": "O2", "parentOutcomeNodeId": null, "level": 1, "title": "Below-limits settlement", "description": "A negotiated settlement below limits.", "distinguishingProposition": "Below-limits distinguishes it from full recovery.", "relationshipToDecisionTarget": "Alternative answer.", "normalizationStatus": "admitted", "normalizationReason": "Distinct outcome." }
        ]
      },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "legal_element", "ambiguityType": null, "label": "Liability & notice", "semanticQuestion": "Did the store have notice of the hazard?", "whyOutcomeRelevant": "Notice governs premises liability.", "children": [] },
        { "rootId": "B2", "rootKind": "applicability_question", "ambiguityType": null, "label": "Available coverage", "semanticQuestion": "Is coverage available?", "whyOutcomeRelevant": "Coverage caps recovery.", "children": [] }
      ],
      "candidates": [
        { "candidateId": "C1", "resolution": "Complete policy-limits settlement", "candidateType": "resolution", "rationaleSummary": "Strong liability.", "score": 0.6, "originatingOutcomeNodeIds": ["O1"] },
        { "candidateId": "C2", "resolution": "Below-limits settlement", "candidateType": "resolution", "rationaleSummary": "Comparative fault risk.", "score": 0.5, "originatingOutcomeNodeIds": ["O2"] }
      ],
      "candidateBranchRelations": [
        { "candidateId": "C1", "branchId": "B1", "relationType": "required", "rationale": "Liability required." },
        { "candidateId": "C1", "branchId": "B2", "relationType": "required", "rationale": "Coverage required." }
      ],
      "unresolvedPropositions": [],
      "factProvenance": []
    }
    """;

    [Fact]
    public void Migration0320_RewritesV2Prompt_ToDualHierarchyContract_AndAddsOutcomeColumns()
    {
        var sql = ReadMigration("0320_LegalDecisionDiscoveryDualHierarchyR2.sql");

        // Wholesale rewrite is guarded by the unique R2 marker so it applies exactly once.
        Assert.Contains("produce TWO DISTINCT semantic hierarchies", sql, StringComparison.Ordinal);
        Assert.Contains("SystemPrompt NOT LIKE N'%produce TWO DISTINCT semantic hierarchies%'", sql, StringComparison.Ordinal);
        Assert.Contains("PromptCode = N'DECISION_DISCOVERY_V2'", sql, StringComparison.Ordinal);

        // The two exclusion rules and the mandatory pre-return validation are present.
        Assert.Contains("OUTCOME-HIERARCHY EXCLUSION RULE", sql, StringComparison.Ordinal);
        Assert.Contains("SHARED-DEPENDENCY EXCLUSION RULE", sql, StringComparison.Ordinal);
        Assert.Contains("Mandatory structural validation before returning", sql, StringComparison.Ordinal);

        // The three additive outcome-node columns are IF-guarded (idempotent).
        Assert.Contains("RelationshipToDecisionTarget", sql, StringComparison.Ordinal);
        Assert.Contains("NormalizationStatus", sql, StringComparison.Ordinal);
        Assert.Contains("NormalizationReason", sql, StringComparison.Ordinal);
        Assert.Contains("IF COL_LENGTH(N'POLOXI.Legal_DecisionOutcomeNode', N'NormalizationStatus') IS NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnoseDualHierarchyContract_ReturnsNoDefects_ForWellFormedDualHierarchy() =>
        Assert.Empty(LegalDecisionService.DiagnoseDualHierarchyContractForTest(ValidDualHierarchyJson, 8));

    [Fact]
    public void DiagnoseDualHierarchyContract_FlagsMissingOutcomeHierarchy()
    {
        // Legacy candidate-first shape: candidates + shared roots but NO outcomeProposalHierarchy.
        const string json = """
        {
          "semanticRoots": [ { "rootId": "B1", "rootKind": "legal_element", "label": "Liability", "semanticQuestion": "?", "children": [] } ],
          "candidates": [ { "candidateId": "C1", "resolution": "Settle", "candidateType": "resolution", "rationaleSummary": "x", "score": 0.5 } ]
        }
        """;
        var defects = LegalDecisionService.DiagnoseDualHierarchyContractForTest(json, 8);
        Assert.Contains("MISSING_OUTCOME_HIERARCHY", defects);
    }

    [Fact]
    public void DiagnoseDualHierarchyContract_FlagsMissingSharedDependencyHierarchy()
    {
        // Outcome hierarchy + candidates present, but NO shared semanticRoots dependency forest.
        const string json = """
        {
          "outcomeProposalHierarchy": { "nodes": [ { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Settle", "description": null, "distinguishingProposition": null } ] },
          "semanticRoots": [],
          "candidates": [ { "candidateId": "C1", "resolution": "Settle", "candidateType": "resolution", "rationaleSummary": "x", "score": 0.5, "originatingOutcomeNodeIds": ["O1"] } ]
        }
        """;
        Assert.Contains("MISSING_SHARED_DEPENDENCY_HIERARCHY",
            LegalDecisionService.DiagnoseDualHierarchyContractForTest(json, 8));
    }

    [Fact]
    public void DiagnoseDualHierarchyContract_FlagsDanglingOutcomeLineage()
    {
        // A candidate references an outcome node id that does not exist in the outcome hierarchy.
        const string json = """
        {
          "outcomeProposalHierarchy": { "nodes": [ { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Settle", "description": null, "distinguishingProposition": null } ] },
          "semanticRoots": [ { "rootId": "B1", "rootKind": "legal_element", "label": "Liability", "semanticQuestion": "?", "children": [] } ],
          "candidates": [ { "candidateId": "C1", "resolution": "Settle", "candidateType": "resolution", "rationaleSummary": "x", "score": 0.5, "originatingOutcomeNodeIds": ["O_MISSING"] } ]
        }
        """;
        Assert.Contains("CANDIDATE_DANGLING_OUTCOME_LINEAGE",
            LegalDecisionService.DiagnoseDualHierarchyContractForTest(json, 8));
    }

    [Fact]
    public void DiagnoseDualHierarchyContract_FlagsRelationPointingAtOutcomeNode()
    {
        // The two hierarchies are conflated: a candidateBranchRelation.branchId points at an OUTCOME node.
        const string json = """
        {
          "outcomeProposalHierarchy": { "nodes": [ { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Settle", "description": null, "distinguishingProposition": null } ] },
          "semanticRoots": [ { "rootId": "B1", "rootKind": "legal_element", "label": "Liability", "semanticQuestion": "?", "children": [] } ],
          "candidates": [ { "candidateId": "C1", "resolution": "Settle", "candidateType": "resolution", "rationaleSummary": "x", "score": 0.5, "originatingOutcomeNodeIds": ["O1"] } ],
          "candidateBranchRelations": [ { "candidateId": "C1", "branchId": "O1", "relationType": "required", "rationale": "conflated" } ]
        }
        """;
        Assert.Contains("RELATION_BRANCH_IS_OUTCOME_NODE",
            LegalDecisionService.DiagnoseDualHierarchyContractForTest(json, 8));
    }

    [Fact]
    public void DiagnoseDualHierarchyContract_FlagsEmptyOutcomeHierarchy_MendozaNegotiatedVsAdjudicated()
    {
        // Mendoza fixture: the model returned an outcomeProposalHierarchy OBJECT but with an EMPTY nodes
        // array (the schema loophole 0321 closes). An empty hierarchy must NOT be silently accepted \u2014 it is
        // a MISSING_OUTCOME_HIERARCHY contract defect exactly like an omitted hierarchy.
        const string json = """
        {
          "decisionIntent": { "decisionTarget": "Mendoza agreement enforceability", "decisionType": "EVALUATE" },
          "outcomeProposalHierarchy": { "nodes": [] },
          "semanticRoots": [ { "rootId": "B1", "rootKind": "legal_element", "label": "Agreement enforceability", "semanticQuestion": "Is the settlement agreement enforceable?", "children": [] } ],
          "candidates": [ { "candidateId": "C1", "resolution": "Enforce negotiated settlement", "candidateType": "resolution", "rationaleSummary": "x", "score": 0.5 } ]
        }
        """;
        Assert.Contains("MISSING_OUTCOME_HIERARCHY",
            LegalDecisionService.DiagnoseDualHierarchyContractForTest(json, 8));
    }

    [Fact]
    public void Migration0321_MakesOutcomeHierarchyMandatory_NonNull_WithMinItemsNodes()
    {
        var sql = ReadMigration("0321_LegalDecisionDiscoveryOutcomeHierarchyRequired.sql");

        // The tightening is scoped to DECISION_DISCOVERY_V2 and guarded so it applies exactly once.
        Assert.Contains("PromptCode = N'DECISION_DISCOVERY_V2'", sql, StringComparison.Ordinal);
        Assert.Contains("\"outcomeProposalHierarchy\":{\"type\":[\"object\",\"null\"]", sql, StringComparison.Ordinal);

        // The replacement removes the null option and requires at least one node.
        Assert.Contains("\"outcomeProposalHierarchy\":{\"type\":\"object\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"nodes\":{\"type\":\"array\",\"minItems\":1", sql, StringComparison.Ordinal);

        // outcomeProposalHierarchy is promoted into the top-level required list.
        Assert.Contains("\"candidates\",\"outcomeProposalHierarchy\",\"candidateBranchRelations\"", sql, StringComparison.Ordinal);
    }

    private static string ReadMigration(string fileName) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Legal.Infrastructure", "Migrations", fileName));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Legal.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Legal solution root.");
    }
}
