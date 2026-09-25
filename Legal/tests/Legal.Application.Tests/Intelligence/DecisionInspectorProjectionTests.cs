using Legal.Application;
using Legal.Application.Features.Intelligence;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ── Legal Decision Processing Inspector projection (R5) ──────────────────────────────────────────
// Proves the inspector renders ACTUAL execution data — never inferred placeholders — by projecting the
// real, deterministic normalization-gate registration plan (built from the committed Harper/Mendoza
// fixtures) into the structured inspector DTOs the PI UI binds to. These tests exercise the same
// instance-free projection helpers the live IntelligenceWide2Service uses, so no additional LLM call,
// no network, and no DB are required. Missing/withheld state must surface explicitly, not be fabricated.
public sealed class DecisionInspectorProjectionTests
{
    // Same Harper fixture the gate tests use: premises-liability policy-limits demand. Distinct
    // settlement outcomes survive; "demand pending" is baseline; "litigation preparation" is a pathway.
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

    [Fact]
    public void Candidates_ProjectAllNormalizedRows_WithExplicitEligibilityAndProvenance()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16);

        // Only the two eligible resolutions (C1, C2) actually reached the delivered scoring set. Use
        // their real normalized display names so the projection is validated against actual gate output.
        var scoredNames = gate.Plan.Candidates
            .Where(c => c.RepresentativeSemanticId is "C1" or "C2")
            .Select(c => c.DisplayName)
            .ToArray();
        var rows = IntelligenceWide2Service.ProjectInspectorCandidates(gate.Plan, scoredNames);

        // Every normalized candidate is surfaced — none silently dropped.
        Assert.Equal(gate.Plan.Candidates.Count, rows.Count);
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.NormalizedCandidateId)));
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.MaterialDistinction)));

        var c1 = rows.Single(r => r.NormalizedCandidateId == "C1");
        var c3 = rows.Single(r => r.NormalizedCandidateId == "C3");

        // Eligible resolution that competed: reached scoring is TRUE and derived from the delivered set.
        Assert.True(c1.CompetitionEligible);
        Assert.True(c1.ReachedScoring);

        // Baseline is registered for provenance but explicitly withheld from competition/scoring.
        Assert.False(c3.CompetitionEligible);
        Assert.False(c3.ReachedScoring);
        Assert.Contains("O3", c3.OriginatingOutcomeNodeIds);
    }

    [Fact]
    public void Candidates_EligibleButNotDelivered_ShowReachedScoringFalse_NotFabricated()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16);

        // Simulate an eligible candidate that never appeared in the delivered set.
        var rows = IntelligenceWide2Service.ProjectInspectorCandidates(gate.Plan, scoredNames: []);
        var c1 = rows.Single(r => r.NormalizedCandidateId == "C1");

        Assert.True(c1.CompetitionEligible);
        Assert.False(c1.ReachedScoring); // eligible ≠ scored; not inferred TRUE.
    }

    [Fact]
    public void Dependencies_AppearOnce_WithDistinctRelatedCandidates()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16);

        var deps = IntelligenceWide2Service.ProjectInspectorDependencies(gate.Plan);

        // Each dependency id appears exactly once — never duplicated beneath each outcome.
        Assert.Equal(deps.Select(d => d.DependencyId).Distinct(StringComparer.OrdinalIgnoreCase).Count(), deps.Count);
        Assert.All(deps, d => Assert.All(
            d.RelatedCandidateIds,
            _ => Assert.Equal(d.RelatedCandidateIds.Count, d.RelatedCandidateIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())));

        // Notice (B1) and coverage (B4) are shared dependencies related to the full-recovery candidate.
        var notice = deps.Single(d => d.DependencyId == "B1");
        Assert.Contains("C1", notice.RelatedCandidateIds);
    }

    [Fact]
    public void Projection_MakesNoAdditionalModelCall_PureFromRegistrationPlan()
    {
        // The projection helpers accept only the deterministic plan + delivered names. There is no
        // provider/router/LLM dependency in their signature, which structurally guarantees the inspector
        // cannot trigger an extra live model call.
        var gate = LegalDecisionService.RunNormalizationGateForTest(HarperJson, 16);

        var candidates = IntelligenceWide2Service.ProjectInspectorCandidates(gate.Plan, []);
        var dependencies = IntelligenceWide2Service.ProjectInspectorDependencies(gate.Plan);
        var rejected = IntelligenceWide2Service.ProjectInspectorRejected(gate.Plan);

        Assert.NotEmpty(candidates);
        Assert.NotEmpty(dependencies);
        Assert.NotNull(rejected);
    }
}
