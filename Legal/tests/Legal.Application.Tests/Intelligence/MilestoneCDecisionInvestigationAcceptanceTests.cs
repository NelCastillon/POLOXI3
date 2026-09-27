using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ── Milestone C — Decision Investigation Feedback Loop (items 5–9 acceptance test) ─────────────
//
// This is the consolidated "Decision-First Acceptance Test" the milestone calls for. It does not rely
// on an attractive ranking screen; it proves the crucial behavioral claim end-to-end:
//
//   • RELEVANT verified evidence changes the CORRECT proposition + candidate evaluation.
//   • IRRELEVANT evidence leaves the correct propositions + candidates UNAFFECTED.
//   • A CONTRADICTION is PRESERVED rather than silently resolved toward the leading candidate.
//
// Two layers are exercised:
//   1) Proposition/inventory layer — proposition-specific binding sensitivity, specificity, and
//      contradiction preservation (the validated Candidate × Factor matrix that feeds competition).
//   2) Closed-loop layer — ApplyVerificationChangeAsync drives dependency propagation → candidate
//      recompetition → next-investigation selection, and proves that a dispositive verified change
//      flips the correct winner while an inert change changes nothing (no false recompetition).
//
// No DB, no network, no live LLM — everything runs on the deterministic in-memory fixtures.
public sealed class MilestoneCDecisionInvestigationAcceptanceTests
{
    // A four-branch fixture: an admissible field establishes B3 (Confidentiality), an irrelevant field is
    // category-rejected against B4 (Demand accepted), and B2 (Damages documented) receives conflicting
    // values across two sources. B1 (Liability) is left with no case value.
    private const string FourBranchJson = """
    {
      "schemaVersion": "v2",
      "decisionIntent": { "decisionTarget": "Settlement posture", "decisionType": "EVALUATE" },
      "queryUnderstanding": "Evaluate outcomes of the settlement.",
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
        { "candidateId": "C1", "branchId": "B1", "relationType": "required", "rationale": "Liability is required for the leading outcome." },
        { "candidateId": "C1", "branchId": "B3", "relationType": "required", "rationale": "Confidentiality is required for enforcement." },
        { "candidateId": "C2", "branchId": "B2", "relationType": "required", "rationale": "Damages are required to reopen." },
        { "candidateId": "C2", "branchId": "B4", "relationType": "supports", "rationale": "Demand posture supports reopening." }
      ],
      "unresolvedPropositions": [],
      "factProvenance": []
    }
    """;

    // Confidentiality is admissible → establishes B3. Demand Status is category-mismatched vs B4 → rejected.
    // Damages documented carries two conflicting values across matter facts vs uploaded evidence.
    private static MatterContextSnapshot BuildMatterContext()
        => new(
            MatterId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            OriginalQuestion: "Evaluate the settlement posture.",
            DomainPackCode: "PI_GENERAL",
            PracticeAreaCode: "PI",
            Decision: [],
            LegalScope: [],
            PersonalInjuryProfile: [],
            Facts:
            [
                new MatterContextField("Confidentiality", "Executed NDA with liquidated-damages clause", MatterFieldProvenance.Supplied),
                new MatterContextField("Demand Status", "Responded", MatterFieldProvenance.Supplied),
                new MatterContextField("Damages documented", "Confirmed", MatterFieldProvenance.Supplied),
            ],
            Evidence:
            [
                new MatterContextField("Damages documented", "Disputed", MatterFieldProvenance.Supplied),
            ]);

    [Fact]
    public void RelevantEvidenceEstablishesCorrectProposition_IrrelevantIsRejected_ContradictionIsPreserved()
    {
        var gate = LegalDecisionService.RunNormalizationGateForTest(FourBranchJson, 16);

        var inventory = IntelligenceWide2Service.ProjectFactorInventory(
            gate.Plan, BuildMatterContext(), domainPackResolved: true, domainPackCode: "PI_GENERAL", anyCandidateDelivered: true);

        WideFactorDto Factor(string label) =>
            inventory.Factors.Single(f => string.Equals(f.FactorName, label, StringComparison.OrdinalIgnoreCase));

        // ── SENSITIVITY: the relevant, admissible field establishes the CORRECT proposition (B3). ──────
        var confidentiality = Factor("Confidentiality enforceable");
        Assert.Equal("Executed NDA with liquidated-damages clause", confidentiality.ActualValue);
        Assert.Equal("VALID", confidentiality.ValidationStatus);
        Assert.Equal("AVAILABLE", confidentiality.Availability);
        Assert.Equal("SUPPLIED", confidentiality.VerificationStatus);
        Assert.Equal("ADMITTED", confidentiality.BindingAdmissibility);

        // ── SPECIFICITY: an unrelated administrative value cannot satisfy a different proposition (B4). ─
        var demand = Factor("Demand accepted");
        Assert.NotEqual("VALID", demand.ValidationStatus);
        Assert.False(string.Equals(demand.BindingAdmissibility, "ADMITTED", StringComparison.OrdinalIgnoreCase));
        Assert.True(inventory.RejectedBindingCount > 0);

        // ── CONTRADICTION: conflicting sources for B2 are preserved, never silently collapsed. ─────────
        var damages = Factor("Damages documented");
        Assert.NotEmpty(damages.Contradictions);
        Assert.Equal("CONTRADICTED", damages.EvidenceAdmissionState);
        Assert.NotEqual("VALID", damages.ValidationStatus);
        Assert.True(inventory.ContradictedFactorCount > 0);

        // ── READINESS: the analysis stays provisional/blocked and surfaces actionable obligations, so a
        // provisional ranking is never presented as an established legal determination. ────────────────
        Assert.True(inventory.IsProvisional);
        Assert.Equal("BLOCKED", inventory.DecisionReadinessStatus);
        Assert.NotEmpty(inventory.BlockingObligations);
        Assert.Contains(inventory.BlockingObligations, o => o.Contains("conflicting", StringComparison.OrdinalIgnoreCase));
    }

    // ── Closed-loop layer: a dispositive verified change flips the CORRECT winner and selects the next
    // investigation, driven fully through ApplyVerificationChangeAsync. ────────────────────────────────
    [Fact]
    public async Task VerificationChange_LosingEssentialSupport_FlipsWinner_AndSelectsNextInvestigation()
    {
        var repo = SeedTwoCandidateSession(out var tenantId, out var sessionId, out var c1Id, out var c2Id,
            out var essentialEdgeId, fullyCovered: false);
        var service = RollbackFixture.Service(repo, new DependencyPropagationService());

        var result = await service.ApplyVerificationChangeAsync(
            tenantId, RollbackFixture.User, sessionId,
            new DecisionVerificationChangeRequest(essentialEdgeId, DecisionVerificationStates.Invalidated),
            default);

        // The verified change was applied and produced a real, recompetition-requiring impact.
        Assert.True(result.Applied);
        Assert.False(result.AlreadyProcessed);
        Assert.True(result.Impact.RecompetitionRequired);
        Assert.Single(repo.DependencyEvents);

        // Recompetition ran on the UPDATED evidence and flipped C1 → C2 (the correct, evidence-grounded
        // change) — not because the formula changed, but because the factual basis did.
        Assert.NotNull(result.Recompetition);
        Assert.True(result.Recompetition!.WinnerChanged);
        Assert.Equal(c1Id, result.Recompetition.PreviousWinnerCandidateId);
        Assert.Equal(c2Id, result.Recompetition.CurrentWinnerCandidateId);
        Assert.Equal(1, repo.PersistRecompetitionCount);

        // The authoritative session state now reflects the new winner.
        Assert.Equal(c2Id, repo.Session.WinnerCandidateId);

        // The loop selects the next investigation from the highest-IV frontier (bounded, not unbounded).
        Assert.NotNull(result.ResearchNeed);
        Assert.True(repo.PersistResearchNeedCount >= 1);
    }

    // ── Closed-loop layer: an inert change (fully-covered proposition, no support delta) must NOT
    // recompete or change the winner — irrelevant evidence leaves the decision unaffected. ─────────────
    [Fact]
    public async Task VerificationChange_OnFullyCoveredProposition_DoesNotRecompete_OrChangeWinner()
    {
        var repo = SeedTwoCandidateSession(out var tenantId, out var sessionId, out var c1Id, out _,
            out var coveredEdgeId, fullyCovered: true);
        var service = RollbackFixture.Service(repo, new DependencyPropagationService());

        var result = await service.ApplyVerificationChangeAsync(
            tenantId, RollbackFixture.User, sessionId,
            new DecisionVerificationChangeRequest(coveredEdgeId, DecisionVerificationStates.Invalidated),
            default);

        // The edge change is still recorded (auditable), but it produces no essential failure / signal.
        Assert.True(result.Applied);
        Assert.Single(repo.DependencyEvents);
        Assert.False(result.Impact.RecompetitionRequired);

        // No false recompetition, no winner change, no session outcome mutation.
        Assert.Null(result.Recompetition);
        Assert.Equal(0, repo.PersistRecompetitionCount);
        Assert.Equal(0, repo.UpdateSessionOutcomeCount);
        Assert.Equal(c1Id, repo.Session.WinnerCandidateId);
    }

    // Seeds a two-candidate session whose leader (C1) depends on an essential verified evidence edge.
    // When fullyCovered is false, invalidating that edge breaks C1's essential support (dispositive).
    // When fullyCovered is true, a second identical verified edge covers the proposition at its averaged
    // steady state, so invalidating one edge yields no support delta (inert).
    private static RecordingDecisionRepository SeedTwoCandidateSession(
        out Guid tenantId, out Guid sessionId, out Guid c1Id, out Guid c2Id, out Guid primaryEdgeId, bool fullyCovered)
    {
        tenantId = Guid.NewGuid();
        sessionId = Guid.NewGuid();
        c1Id = Guid.NewGuid();
        c2Id = Guid.NewGuid();
        var c1BranchId = Guid.NewGuid();
        var c2BranchId = Guid.NewGuid();

        var c1 = new DecisionCandidatePersistence(
            c1Id, "C1", "Enforce confidential settlement", "Enforce confidential settlement",
            LegalSupport: 0.60m, FactSupport: 0.60m, EvidenceSupport: 0.60m, AuthoritySupport: 0.60m,
            Verification: 0.82m, Uncertainty: 0.18m, Discrimination: 0.40m, RankingImpact: 0.40m,
            Diversity: 0.30m, RedundancyPenalty: 0.05m, CompositeScore: 0.64m, DecisionSupportCeiling: 0.95m,
            RankOrder: 1, IsWinner: true, IsEliminated: false);
        var c2 = new DecisionCandidatePersistence(
            c2Id, "C2", "Reopen for additional damages", "Reopen for additional damages",
            LegalSupport: 0.58m, FactSupport: 0.58m, EvidenceSupport: 0.55m, AuthoritySupport: 0.55m,
            Verification: 0.62m, Uncertainty: 0.38m, Discrimination: 0.40m, RankingImpact: 0.40m,
            Diversity: 0.30m, RedundancyPenalty: 0.05m, CompositeScore: 0.58m, DecisionSupportCeiling: 0.95m,
            RankOrder: 2, IsWinner: false, IsEliminated: false);

        var c1Branch = new DecisionBranchPersistence(
            c1BranchId, null, 1, "C1.B1", "Confidentiality frontier", "Establish confidentiality", "ACTIVE",
            InformationValue: 0.50m, DecisionRelevance: 0.55m, FlipPotential: 0.40m, EvidenceAvailability: 0.40m,
            AdvScore: 0.30m, Cost: 0.10m, IsOnFrontier: true, StopReason: null, SortOrder: 0);
        var c2Branch = new DecisionBranchPersistence(
            c2BranchId, null, 1, "C2.B1", "Damages frontier", "Establish damages", "ACTIVE",
            InformationValue: 0.45m, DecisionRelevance: 0.50m, FlipPotential: 0.35m, EvidenceAvailability: 0.40m,
            AdvScore: 0.30m, Cost: 0.10m, IsOnFrontier: true, StopReason: null, SortOrder: 1);

        var session = new DecisionSessionPersistence(
            sessionId, tenantId, RollbackFixture.User, "Evaluate the settlement posture.", DecisionContexts.General, null,
            UsePoloxiEngine: true, StatusCode: "PROVISIONAL_DECISION", TerminalStateCode: null,
            TerminationReason: "LEADING_OUTCOME_WITH_OPEN_HIGH_VALUE_FRONTIER",
            WinnerCandidateId: c1Id, ContractCompleteness: 1.0m,
            CandidateEntropy: 0.90m, DecisionMargin: 0.06m, DepthReached: 1, LlmCallCount: 1, DurationMs: 10,
            FinalAnswer: "Provisional answer.", ClarificationQuestion: null, ClarificationTarget: null,
            CorrelationId: null,
            Candidates: [c1, c2], Branches: [c1Branch, c2Branch], Evidence: [], FlipPoints: [], Events: [])
        {
            MatterId = Guid.NewGuid(),
            MatterJurisdiction = "Delaware",
            GoverningLaw = "Delaware",
        };

        // C1's essential proposition F1 established by a verified evidence edge on C1's branch.
        var propSupport = fullyCovered ? 0.9m : 0.85m;
        var propNode = new DecisionGraphNodePersistence(
            Guid.NewGuid(), DecisionGraphNodeKinds.Proposition, "F1", "Essential fact", "stmt",
            Support: propSupport, IsEssential: true, IsSatisfied: true, DecisionVerificationStates.Verified, 0)
        {
            SourceBranchId = c1BranchId,
        };
        var evidenceNode = new DecisionGraphNodePersistence(
            Guid.NewGuid(), DecisionGraphNodeKinds.Evidence, "E1", "Evidence", "stmt",
            Support: 1.0m, IsEssential: false, IsSatisfied: true, DecisionVerificationStates.Verified, 1)
        {
            SourceBranchId = c1BranchId,
        };

        primaryEdgeId = Guid.NewGuid();
        var primaryEdge = new DecisionGraphEdgePersistence(
            primaryEdgeId, "SUPPORTS", DecisionGraphNodeKinds.Evidence, evidenceNode.NodeId,
            DecisionGraphNodeKinds.Proposition, propNode.NodeId,
            SupportWeight: 0.9m, Materiality: 0.9m, IsEssential: true, IsDispositive: false,
            VerificationStatus: DecisionVerificationStates.Verified, VerificationNotes: null, PropagatedStateCode: null)
        {
            SourceBranchId = c1BranchId,
            AlternativePathAllowed = fullyCovered,
        };

        var nodes = new List<DecisionGraphNodePersistence> { propNode, evidenceNode };
        var edges = new List<DecisionGraphEdgePersistence> { primaryEdge };

        if (fullyCovered)
        {
            // A second identical verified edge covers F1 at its averaged steady state, so invalidating the
            // first edge yields no support delta → no signal → no recompetition.
            var evidenceNode2 = new DecisionGraphNodePersistence(
                Guid.NewGuid(), DecisionGraphNodeKinds.Evidence, "E2", "Alternative evidence", "stmt",
                Support: 1.0m, IsEssential: false, IsSatisfied: true, DecisionVerificationStates.Verified, 2)
            {
                SourceBranchId = c1BranchId,
            };
            var coverEdge = new DecisionGraphEdgePersistence(
                Guid.NewGuid(), "SUPPORTS", DecisionGraphNodeKinds.Evidence, evidenceNode2.NodeId,
                DecisionGraphNodeKinds.Proposition, propNode.NodeId,
                SupportWeight: 0.9m, Materiality: 0.9m, IsEssential: false, IsDispositive: false,
                VerificationStatus: DecisionVerificationStates.Verified, VerificationNotes: null, PropagatedStateCode: null)
            {
                SourceBranchId = c1BranchId,
            };
            nodes.Add(evidenceNode2);
            edges.Add(coverEdge);
        }

        var graph = new DecisionGraphPersistence(
            sessionId, tenantId, RollbackFixture.User, ReadinessSatisfied: false, ReadinessBlockersJson: null,
            Nodes: nodes, Edges: edges, LosingSideTest: null);

        return new RecordingDecisionRepository(
            session,
            graph,
            new DecisionPromptDefinition(
                "DECISION_RESEARCH_NEED", "RESEARCH_NEED", "Decompose the frontier.",
                "{{QUERY}}\n{{CANDIDATES}}\n{{FRONTIER}}", null),
            [new DecisionModelRouteDto(
                "DECISION_DEFAULT", "TEST", "test-model", "test-model", "test://local", null,
                "1", 10, 1000, 0m, 1)]);
    }
}
