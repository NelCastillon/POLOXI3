using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// R1 Outcome Proposal Hierarchy (DECISION_DISCOVERY §2). Locks in that the ADDITIVE outcome-discovery
// enhancement to DECISION_DISCOVERY_V2:
//   • parses the outcomeProposalHierarchy into first-class DISCOVERY nodes and normalizes/validates them
//     (unique ids, valid parents, no cycles, deterministic depth),
//   • normalizes candidates into ONE global pool while preserving originatingOutcomeNodeIds provenance,
//   • keeps the outcome-discovery hierarchy SEPARATE from the shared legal-dependency (evaluation)
//     hierarchy (outcome nodes are never materialized as branches / candidate-branch relations), and
//   • degrades to empty on legacy / malformed shapes so the prior branch-first path stays intact.
public sealed class DecisionDiscoveryOutcomeHierarchyContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    // Harper v. Voss (fictional PI slip-and-fall) fixture: five materially-distinct interpretations with
    // one L2 refinement, plus a shared legal-dependency forest of substantive propositions (NOT outcome
    // labels), and explicit Candidate × Branch relations that reference the SHARED branch ids.
    private const string HarperDiscoveryJson = """
    {
      "schemaVersion": "v2",
      "decisionIntent": { "decisionTarget": "Harper policy-limits demand", "decisionType": "EVALUATE" },
      "queryUnderstanding": "Evaluate the prospective outcomes of the Harper premises-liability demand.",
      "outcomeProposalHierarchy": {
        "nodes": [
          { "outcomeNodeId": "O1", "parentOutcomeNodeId": null, "level": 1, "title": "Complete policy-limits settlement", "description": "A completed settlement resolving the claims.", "distinguishingProposition": "A completed settlement is not merely a demand or offer." },
          { "outcomeNodeId": "O1.1", "parentOutcomeNodeId": "O1", "level": 2, "title": "Executed and funded settlement", "description": "Settlement executed and payment completed.", "distinguishingProposition": "Execution and funding distinguish completion from an outstanding offer." },
          { "outcomeNodeId": "O2", "parentOutcomeNodeId": null, "level": 1, "title": "Partial / below-limits settlement", "description": "A negotiated settlement below policy limits.", "distinguishingProposition": "Below-limits distinguishes it from a full policy-limits recovery." },
          { "outcomeNodeId": "O3", "parentOutcomeNodeId": null, "level": 1, "title": "Litigation after demand rejected", "description": "Filing suit following a rejected demand.", "distinguishingProposition": "Litigation distinguishes it from any pre-suit resolution." },
          { "outcomeNodeId": "O4", "parentOutcomeNodeId": null, "level": 1, "title": "Claim abandoned / no recovery", "description": "The claim is dropped with no recovery.", "distinguishingProposition": "No recovery distinguishes it from every settlement outcome." },
          { "outcomeNodeId": "O5", "parentOutcomeNodeId": null, "level": 1, "title": "Coverage denied by carrier", "description": "The carrier denies coverage entirely.", "distinguishingProposition": "Coverage denial distinguishes it from a disputed-but-covered claim." }
        ]
      },
      "semanticRoots": [
        { "rootId": "B1", "rootKind": "legal_element", "ambiguityType": null, "label": "Liability & notice of hazard", "semanticQuestion": "Did the store have notice of the hazard?", "whyOutcomeRelevant": "Notice governs premises liability.", "children": [] },
        { "rootId": "B2", "rootKind": "applicability_question", "ambiguityType": null, "label": "Available insurance coverage", "semanticQuestion": "Is there coverage available to fund a settlement?", "whyOutcomeRelevant": "Coverage caps recovery.", "children": [] }
      ],
      "candidates": [
        { "candidateId": "C1", "resolution": "Complete policy-limits settlement", "candidateType": "resolution", "rationaleSummary": "Strong liability + adequate limits.", "score": 0.6, "originatingOutcomeNodeIds": ["O1", "O1.1"] },
        { "candidateId": "C2", "resolution": "Partial / below-limits settlement", "candidateType": "resolution", "rationaleSummary": "Comparative fault risk.", "score": 0.5, "originatingOutcomeNodeIds": ["O2"] },
        { "candidateId": "C3", "resolution": "Litigation after demand rejected", "candidateType": "procedural", "rationaleSummary": "If demand rejected.", "score": 0.4, "originatingOutcomeNodeIds": ["O3"] },
        { "candidateId": "C4", "resolution": "Claim abandoned / no recovery", "candidateType": "resolution", "rationaleSummary": "If liability fails.", "score": 0.2, "originatingOutcomeNodeIds": ["O4"] },
        { "candidateId": "C5", "resolution": "Coverage denied by carrier", "candidateType": "resolution", "rationaleSummary": "If coverage disputed.", "score": 0.3, "originatingOutcomeNodeIds": ["O5"] }
      ],
      "candidateBranchRelations": [
        { "candidateId": "C1", "branchId": "B1", "relationType": "required", "rationale": "Liability required for recovery." },
        { "candidateId": "C1", "branchId": "B2", "relationType": "required", "rationale": "Coverage required to fund limits." }
      ],
      "unresolvedPropositions": [],
      "factProvenance": []
    }
    """;

    [Fact]
    public void Migration0319_AnchorsMatchLiveV2Prompt_SoTheReplaceActuallyApplies()
    {
        // GAP GUARD: 0319 patches the prompt via REPLACE() over anchor strings. If a future prompt
        // rewrite changes those anchors, REPLACE would silently no-op and the outcome hierarchy would
        // never reach Astra. This locks the anchors to the live prompt source (0317, the last writer).
        var baseline = ReadMigration("0317_LegalDecisionDiscoveryDecisionIntent.sql");

        // Section 2 insertion anchor (verbatim) — the sentence we append after.
        Assert.Contains(
            "Generate a GLOBAL candidate set of genuinely distinct outcomes responsive to the proposed decisionIntent.",
            baseline, StringComparison.Ordinal);

        // Section 3 replacement anchor (verbatim) — the exact phrase we rewrite.
        Assert.Contains(
            "Do NOT use outcome candidates, candidate categories, decision-frame instructions, or generic answer headings as hierarchy branches.",
            baseline, StringComparison.Ordinal);

        // Output-schema anchors (verbatim) — top-level required list, candidate required list, and the
        // candidate score → candidateBranchRelations seam we splice the new definitions into.
        Assert.Contains(
            "\"required\":[\"schemaVersion\",\"decisionIntent\",\"queryUnderstanding\",\"semanticRoots\",\"candidates\",\"candidateBranchRelations\",\"unresolvedPropositions\",\"factProvenance\",\"proposalSummary\"]",
            baseline, StringComparison.Ordinal);
        Assert.Contains(
            "\"required\":[\"candidateId\",\"resolution\",\"candidateType\",\"rationaleSummary\",\"score\"]",
            baseline, StringComparison.Ordinal);
        Assert.Contains(
            "\"score\":{\"type\":[\"number\",\"null\"],\"minimum\":0,\"maximum\":1}}}},\"candidateBranchRelations\"",
            baseline, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0319_PatchIsIdempotent_AndScopedToV2Only()
    {
        var sql = ReadMigration("0319_LegalDecisionDiscoveryOutcomeHierarchy.sql");

        // Re-run safety: the prompt UPDATE is guarded so a second apply is a no-op, and the DDL/column
        // are IF-guarded. Never touches the v1 prompt or the general Wide engine.
        Assert.Contains(
            "SystemPrompt NOT LIKE N'%The outcomeProposalHierarchy is a first-class discovery structure, separate from the shared legal and factual dependency hierarchy.%'",
            sql, StringComparison.Ordinal);
        Assert.Contains("WHERE PromptCode = N'DECISION_DISCOVERY_V2'", sql, StringComparison.Ordinal);
        Assert.Contains("IF COL_LENGTH(N'POLOXI.Legal_DecisionCandidate', N'OriginatingOutcomeNodeIds') IS NULL", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DECISION_DISCOVERY'", sql.Replace("DECISION_DISCOVERY_V2", string.Empty), StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0319_UpdatesV2Prompt_AdditivelyAndExtendsSchema()

    {
        var sql = ReadMigration("0319_LegalDecisionDiscoveryOutcomeHierarchy.sql");

        // Targets ONLY DECISION_DISCOVERY_V2 — never the general Wide engine or the v1 prompt.
        Assert.Contains("DECISION_DISCOVERY_V2", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE POLOXI.Legal_WidePrompt", sql, StringComparison.Ordinal);

        // The prompt patch is ADDITIVE (a REPLACE over the existing prompt), never a wholesale rewrite.
        Assert.Contains("SET SystemPrompt = REPLACE(", sql, StringComparison.Ordinal);
        Assert.Contains("outcomeProposalHierarchy is a first-class discovery structure", sql, StringComparison.Ordinal);
        Assert.Contains("originatingOutcomeNodeIds", sql, StringComparison.Ordinal);

        // Section 3 clarification: shared-dependency branches, not outcome branches.
        Assert.Contains("as branches within the shared legal-dependency hierarchy", sql, StringComparison.Ordinal);

        // Mode gating: EVALUATE generates by default; IMPLEMENT_DRAFT is not forced.
        Assert.Contains("LEGAL_DECISION / EVALUATE", sql, StringComparison.Ordinal);
        Assert.Contains("LEGAL_DECISION / IMPLEMENT_DRAFT, do not force competing outcome branches", sql, StringComparison.Ordinal);

        // Persistence: first-class DISCOVERY table + candidate origin column, both idempotent.
        Assert.Contains("POLOXI.Legal_DecisionOutcomeNode", sql, StringComparison.Ordinal);
        Assert.Contains("ADD OriginatingOutcomeNodeIds", sql, StringComparison.Ordinal);
        Assert.Contains("IF OBJECT_ID(N'POLOXI.Legal_DecisionOutcomeNode', N'U') IS NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AdaptOutcomeHierarchy_PreservesFiveDistinctInterpretations_WithStableIdentities()
    {
        var nodes = LegalDecisionService.AdaptOutcomeHierarchy(HarperDiscoveryJson);

        // The five materially-distinct Harper interpretations remain discoverable (O1..O5 roots).
        var roots = nodes.Where(n => n.ParentOutcomeNodeId is null).Select(n => n.OutcomeNodeId).ToArray();
        Assert.Equal(new[] { "O1", "O2", "O3", "O4", "O5" }, roots);

        // The L2 refinement is preserved with its parent link and a derived depth of 2.
        var refinement = Assert.Single(nodes, n => n.OutcomeNodeId == "O1.1");
        Assert.Equal("O1", refinement.ParentOutcomeNodeId);
        Assert.Equal(2, refinement.Level);

        // Stable identities and distinguishing propositions survive parsing.
        Assert.All(nodes, n => Assert.False(string.IsNullOrWhiteSpace(n.Title)));
    }

    [Fact]
    public void AdaptSemanticProposal_NormalizesCandidates_PreservingOutcomeOrigins()
    {
        var candidates = LegalDecisionService.AdaptSemanticProposalForTest(HarperDiscoveryJson, maxCandidates: 8);

        // One global candidate pool: five candidates, each retaining its originating outcome-node ids.
        Assert.Equal(5, candidates.Count);
        var c1 = Assert.Single(candidates, c => c.SemanticCandidateId == "C1");
        Assert.Equal(new[] { "O1", "O1.1" }, c1.OriginatingOutcomeNodeIds);
    }

    [Fact]
    public void AdaptSemanticEnrichment_CarriesOutcomeNodes_SeparateFromEvaluationBranches()
    {
        var enrichment = LegalDecisionService.AdaptSemanticEnrichment(HarperDiscoveryJson);

        // Outcome nodes ride on the enrichment as a first-class discovery structure.
        Assert.NotEmpty(enrichment.OutcomeNodes);
        Assert.Contains(enrichment.OutcomeNodes, n => n.OutcomeNodeId == "O1.1" && n.Level == 2);

        // They are NEVER converted into candidate × branch relations (those use SHARED branch ids only).
        var proposal = LegalDecisionService.AdaptSemanticProposalForTest(HarperDiscoveryJson, 8);
        var scored = proposal.Select((c, i) => new DecisionCandidatePersistence(
            Guid.NewGuid(), $"C{i + 1}", $"Candidate {i + 1}", "outcome", 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0m, 0.5m, 0.5m, i + 1, false, false)
        {
            SemanticCandidateId = c.SemanticCandidateId,
        }).ToArray();
        var branches = new[]
        {
            new DecisionBranchPersistence(Guid.NewGuid(), null, 1, "B1", "Liability & notice", "notice", "OPEN", 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, false, null, 0) { SemanticBranchId = "B1" },
            new DecisionBranchPersistence(Guid.NewGuid(), null, 1, "B2", "Available coverage", "coverage", "OPEN", 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, false, null, 1) { SemanticBranchId = "B2" },
        };

        var (relations, _) = LegalDecisionService.MaterializeSemanticEnrichment(enrichment, scored, branches);

        // Relations reference SHARED branch ids (B1/B2), never outcome-node ids (O1/O1.1).
        Assert.NotEmpty(relations);
        var branchGuids = branches.Select(b => b.DecisionBranchId).ToHashSet();
        Assert.All(relations, r => Assert.Contains(r.DecisionBranchId, branchGuids));
    }

    [Fact]
    public void NormalizeOutcomeHierarchy_BreaksCycles_AndDemotesInvalidParents()
    {
        // A self-cycle (O1 -> O1), a two-node cycle (O2 <-> O3), a dangling parent (O4 -> missing),
        // and a duplicate id must all resolve deterministically to a valid forest.
        const string json = """
        {
          "outcomeProposalHierarchy": {
            "nodes": [
              { "outcomeNodeId": "O1", "parentOutcomeNodeId": "O1", "level": 1, "title": "Self cycle", "description": null, "distinguishingProposition": null },
              { "outcomeNodeId": "O2", "parentOutcomeNodeId": "O3", "level": 1, "title": "Cycle A", "description": null, "distinguishingProposition": null },
              { "outcomeNodeId": "O3", "parentOutcomeNodeId": "O2", "level": 1, "title": "Cycle B", "description": null, "distinguishingProposition": null },
              { "outcomeNodeId": "O4", "parentOutcomeNodeId": "OZ", "level": 1, "title": "Dangling parent", "description": null, "distinguishingProposition": null },
              { "outcomeNodeId": "O4", "parentOutcomeNodeId": null, "level": 1, "title": "Duplicate id", "description": null, "distinguishingProposition": null }
            ]
          }
        }
        """;

        var nodes = LegalDecisionService.AdaptOutcomeHierarchy(json);

        // Duplicate ids collapse to a single node; the forest never contains a cycle.
        Assert.Equal(4, nodes.Count);
        Assert.Single(nodes, n => n.OutcomeNodeId == "O4");

        // Self-cycle and dangling-parent nodes are demoted to roots.
        Assert.Null(Assert.Single(nodes, n => n.OutcomeNodeId == "O1").ParentOutcomeNodeId);
        Assert.Null(Assert.Single(nodes, n => n.OutcomeNodeId == "O4").ParentOutcomeNodeId);

        // The two-node cycle is broken: exactly one of O2/O3 keeps its parent so no cycle remains.
        var o2 = Assert.Single(nodes, n => n.OutcomeNodeId == "O2");
        var o3 = Assert.Single(nodes, n => n.OutcomeNodeId == "O3");
        Assert.False(o2.ParentOutcomeNodeId == "O3" && o3.ParentOutcomeNodeId == "O2");
    }

    [Fact]
    public void AdaptOutcomeHierarchy_ReturnsEmpty_OnLegacyShape() =>
        Assert.Empty(LegalDecisionService.AdaptOutcomeHierarchy("""{ "candidates": [] }"""));

    [Fact]
    public void MaterializeOutcomeHierarchy_ReturnsEmpty_WhenNoOutcomeNodesProposed() =>
        Assert.Empty(LegalDecisionService.MaterializeOutcomeHierarchy(
            LegalDecisionService.SemanticProposalEnrichment.Empty));

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
