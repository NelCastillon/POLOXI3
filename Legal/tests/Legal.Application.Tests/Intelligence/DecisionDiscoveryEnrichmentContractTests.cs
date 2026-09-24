using Legal.Application;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Branch-first discovery (v2) ENRICHMENT contract (§4,§5,§6). Locks in that DECISION_DISCOVERY_V2:
//   • parses Candidate × Branch relations, unresolved propositions, and fact provenance,
//   • resolves LLM-proposed semantic ids to POLOXI Core-assigned persisted GUIDs,
//   • emits NO scores and declares NO winner (descriptive metadata only), and
//   • degrades to Empty on legacy / malformed shapes so the v1 path stays intact.
public sealed class DecisionDiscoveryEnrichmentContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Migration0315_UpdatesV2Prompt_WithAllSixSectionsAndAgreeingSchema()
    {
        var sql = ReadMigration("0315_LegalDecisionDiscoveryRelationsProvenance.sql");

        // Targets ONLY DECISION_DISCOVERY_V2 — never the general Wide engine or the v1 prompt.
        Assert.Contains("DECISION_DISCOVERY_V2", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE POLOXI.Legal_WidePrompt", sql, StringComparison.Ordinal);

        // Prompt instruction + output schema AGREE on the six sections (§1-§6).
        foreach (var token in new[]
        {
            "semanticRoots", "candidates", "candidateBranchRelations",
            "unresolvedPropositions", "factProvenance",
        })
            Assert.Contains(token, sql, StringComparison.Ordinal);

        // §4 relation vocabulary is enumerated so the schema constrains the LLM.
        foreach (var relation in new[] { "required", "supporting", "opposing", "conditional", "distinguishing", "non_applicable" })
            Assert.Contains(relation, sql, StringComparison.Ordinal);

        // §5 evidence/authority needs and §6 provenance are part of the contract.
        Assert.Contains("evidenceNeeded", sql, StringComparison.Ordinal);
        Assert.Contains("authorityNeeded", sql, StringComparison.Ordinal);
        Assert.Contains("isVerified", sql, StringComparison.Ordinal);

        // Persistence: relation edge table + provenance columns on the dependency graph.
        Assert.Contains("POLOXI.Legal_DecisionCandidateBranchRelation", sql, StringComparison.Ordinal);
        Assert.Contains("ADD ProvenanceCode", sql, StringComparison.Ordinal);
        Assert.Contains("ADD EvidenceNeeded", sql, StringComparison.Ordinal);
        Assert.Contains("ADD AuthorityNeeded", sql, StringComparison.Ordinal);

        // The LLM never scores or declares a winner.
        Assert.DoesNotContain("candidateBranchScores", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AdaptSemanticEnrichment_ParsesRelationsPropositionsAndProvenance()
    {
        const string json = """
        {
          "candidateBranchRelations": [
            { "candidateId": "C1", "branchId": "B1", "relationType": "required", "rationale": "duty element" },
            { "candidateId": "C2", "branchId": "B1", "relationType": "not-applicable", "rationale": null }
          ],
          "unresolvedPropositions": [
            { "propositionId": "P1", "statement": "Defendant owed a duty", "linkedBranchId": "B1",
              "evidenceNeeded": "Contract", "authorityNeeded": "Rest. 2d Torts §324A", "provenance": "unverified" }
          ],
          "factProvenance": [
            { "factId": "F1", "statement": "Plaintiff was injured", "source": "supplied", "isVerified": false },
            { "factId": "F2", "statement": "Speed was 60mph", "source": "verified", "isVerified": true }
          ]
        }
        """;

        var enrichment = LegalDecisionService.AdaptSemanticEnrichment(json);

        Assert.Equal(2, enrichment.CandidateBranchRelations.Count);
        // relationType is normalized: "not-applicable" -> "non_applicable".
        Assert.Contains(enrichment.CandidateBranchRelations, r => r.RelationType == "required");
        Assert.Contains(enrichment.CandidateBranchRelations, r => r.RelationType == "non_applicable");

        var prop = Assert.Single(enrichment.UnresolvedPropositions);
        Assert.Equal("B1", prop.LinkedBranchId);
        Assert.Equal("Contract", prop.EvidenceNeeded);
        Assert.Equal("Rest. 2d Torts §324A", prop.AuthorityNeeded);

        Assert.Equal(2, enrichment.FactProvenance.Count);
        Assert.Contains(enrichment.FactProvenance, f => f.Source == "verified" && f.IsVerified);
        Assert.Contains(enrichment.FactProvenance, f => f.Source == "supplied" && !f.IsVerified);
    }

    [Fact]
    public void AdaptSemanticEnrichment_ReturnsEmpty_OnLegacyOrMalformedShapes()
    {
        Assert.Same(LegalDecisionService.SemanticProposalEnrichment.Empty,
            LegalDecisionService.AdaptSemanticEnrichment("{ \"candidates\": [] }"));
        Assert.Same(LegalDecisionService.SemanticProposalEnrichment.Empty,
            LegalDecisionService.AdaptSemanticEnrichment("not json"));
    }

    [Fact]
    public void MaterializeSemanticEnrichment_ResolvesSemanticIdsToPersistedGuids_AndSkipsUnresolvable()
    {
        var candidateGuid = Guid.NewGuid();
        var branchGuid = Guid.NewGuid();

        var candidates = new[]
        {
            new DecisionCandidatePersistence(candidateGuid, "C1", "Buyer may reject", "Buyer may reject",
                0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0m, 0.5m, 0.5m, 1, false, false)
            { SemanticCandidateId = "C1" },
        };
        var branches = new[]
        {
            new DecisionBranchPersistence(branchGuid, null, 1, "C1-B1", "Duty", "Duty question",
                DecisionBranchStates.Active, 0.5m, 0.5m, 0.5m, 0.5m, 0.5m, 0m, true, null, 0)
            { SemanticBranchId = "B1" },
        };

        var enrichment = new LegalDecisionService.SemanticProposalEnrichment(
            [
                new("C1", "B1", "required", "duty element"),
                // Unresolvable endpoints must be dropped, never invented.
                new("C9", "B1", "supporting", null),
                new("C1", "B9", "opposing", null),
            ],
            [new("P1", "Duty owed", "B1", "Contract", "Statute", "unverified")],
            [new("F1", "Injured", "supplied", false)]);

        var (relations, dependencies) =
            LegalDecisionService.MaterializeSemanticEnrichment(enrichment, candidates, branches);

        var relation = Assert.Single(relations);
        Assert.Equal(candidateGuid, relation.DecisionCandidateId);
        Assert.Equal(branchGuid, relation.DecisionBranchId);
        Assert.Equal("required", relation.RelationTypeCode);

        Assert.Contains(dependencies, d => d.NodeKind == "UNRESOLVED_PROPOSITION" && d.EvidenceNeeded == "Contract" && d.AuthorityNeeded == "Statute" && !d.IsVerified);
        Assert.Contains(dependencies, d => d.NodeKind == "FACT_PROVENANCE" && d.ProvenanceCode == "supplied");
    }

    [Fact]
    public void MaterializeSemanticEnrichment_ReturnsEmpty_WhenNothingProposed() =>
        Assert.Empty(LegalDecisionService.MaterializeSemanticEnrichment(
            LegalDecisionService.SemanticProposalEnrichment.Empty, [], []).Relations);

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
