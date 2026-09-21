using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Application.Features.Intelligence.Epistemic;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal — RESEARCH-ROUND TRANSACTION ROLLBACK (step 10, rollback slice).
//
// The bounded research loop commits each round atomically: retrieve → verify → prepare edge change →
// propagation → recompetition, where ApplyVerificationChangeAsync is the round's COMMIT boundary. The
// deterministic propagation step (propagationService.Apply) runs BEFORE the first authoritative write
// (UpdateEdgeVerificationAsync / PersistDependencyEventAsync / ReplaceCandidates / ReplaceBranches /
// UpdateSessionOutcome). So a fault injected into propagation is a clean "fault before commit".
//
// These tests prove the transaction protection is CAUSAL, not incidental:
//   • FAULT case: a throwing propagation service stops the loop with RESEARCH_ROUND_FAILED, performs
//     ZERO authoritative mutations, and leaves the previous decision state (entropy/margin/winner)
//     exactly as it was — no false recompetition, no output-authority change.
//   • POSITIVE CONTROL: a real propagation service lets a round reach and cross the commit boundary
//     (a dependency event is persisted), proving the FAULT — not the harness — is what causes the stop.
// ────────────────────────────────────────────────────────────────────────────────────────────────
[Collection("ResearchLoopSerial")]
public sealed class DecisionResearchRoundRollbackTests
{
    // ── FAULT: propagation throws before commit → RESEARCH_ROUND_FAILED, no partial mutation. ──────
    [Fact]
    public async Task RoundFaultBeforeCommit_StopsWithResearchRoundFailed_AndPreservesPriorState()
    {
        var repo = RollbackFixture.SeededRepository(out var baseline);
        var service = RollbackFixture.Service(repo, new ThrowingPropagationService());

        var result = await service.RunResearchLoopAsync(baseline.TenantId, baseline.SessionId, default);

        // The loop stopped for the RIGHT reason: a round faulted before commit.
        Assert.Equal(DecisionResearchLoopStopReasons.RoundFailed, result.StopReason);
        Assert.True(result.RoundsExecuted >= 1);

        // No partial authoritative mutation: the commit boundary was never crossed.
        Assert.Equal(0, repo.UpdateEdgeVerificationCount);
        Assert.Equal(0, repo.PersistDependencyEventCount);
        Assert.Equal(0, repo.ReplaceCandidatesCount);
        Assert.Equal(0, repo.ReplaceBranchesCount);
        Assert.Equal(0, repo.UpdateSessionOutcomeCount);
        Assert.Equal(0, repo.PersistRecompetitionCount);
        Assert.Equal(3, repo.PersistResearchNeedCount);

        // Retrieval attempts and their evidence attachments are audit provenance, not authoritative
        // decision-state mutations. They remain recorded even though propagation failed.
        Assert.Equal(1, repo.PersistResearchEvidenceCount);
        Assert.Equal(1, repo.UpdateResearchEvidenceCount);
        Assert.Equal(1, repo.PersistEvidenceAttachmentCount);
        Assert.Equal(1, repo.UpdateEvidenceAttachmentCount);
        var attachment = Assert.Single(repo.EvidenceAttachments);
        Assert.Equal(DecisionEvidenceAttachmentStates.SupportedBy, attachment.SupportStateCode);
        Assert.True(attachment.IsAuthoritative);

        // Previous authoritative decision state preserved exactly (entropy/margin/winner unchanged).
        var after = repo.Session;
        Assert.Equal(baseline.Entropy, after.CandidateEntropy);
        Assert.Equal(baseline.Margin, after.DecisionMargin);
        Assert.Equal(baseline.WinnerId, after.WinnerCandidateId);
        Assert.Equal(baseline.StatusCode, after.StatusCode);
    }

    // ── POSITIVE CONTROL: real propagation crosses the commit boundary (a round actually runs). ────
    [Fact]
    public async Task RoundWithoutFault_ReachesCommitBoundary_AndDoesNotReportRoundFailed()
    {
        var repo = RollbackFixture.SeededRepository(out var baseline);
        var service = RollbackFixture.Service(repo, new DependencyPropagationService());

        var result = await service.RunResearchLoopAsync(baseline.TenantId, baseline.SessionId, default);

        // The harness is capable of reaching (and crossing) the round commit boundary: the fault case's
        // stop reason is NOT produced here, and an authoritative edge/event write actually occurred.
        Assert.NotEqual(DecisionResearchLoopStopReasons.RoundFailed, result.StopReason);
        Assert.True(result.RoundsExecuted >= 1);
        Assert.Equal(1, repo.UpdateEdgeVerificationCount);
        Assert.Equal(1, repo.PersistDependencyEventCount);
    }

    [Fact]
    public async Task StructurallyVerifiedGraphWithoutEvidenceEdges_StartsRoundOneFromFrontier()
    {
        var repo = RollbackFixture.SeededRepository(out var baseline, structuralEdgeVerified: true);
        var service = RollbackFixture.Service(repo, new DependencyPropagationService());

        var result = await service.RunResearchLoopAsync(baseline.TenantId, baseline.SessionId, default);

        Assert.True(result.RoundsExecuted >= 1);
        Assert.NotEqual(DecisionResearchLoopStopReasons.NoVerifiableEdge, result.StopReason);
        Assert.True(repo.PersistResearchNeedCount >= 1);
        Assert.True(repo.PersistResearchEvidenceCount >= 1);
        var attachment = Assert.Single(repo.EvidenceAttachments);
        Assert.Equal(DecisionEvidenceAttachmentStates.SupportedBy, attachment.SupportStateCode);
        Assert.True(attachment.IsAuthoritative);
        Assert.NotNull(attachment.AffectedGraphEdgeId);
    }

    [Fact]
    public async Task VerifiedEvidence_TraversesFrontierNeedAttachmentImpactRecompetitionAndNewFrontier()
    {
        var repo = RollbackFixture.SeededRepository(out var baseline);
        var service = RollbackFixture.Service(repo, new DependencyPropagationService());

        var result = await service.RunResearchLoopAsync(baseline.TenantId, baseline.SessionId, default);

        Assert.Equal(1, result.RoundsExecuted);
        Assert.Equal(1, result.TotalRetrievals);
        var evidence = Assert.Single(repo.ResearchEvidence);
        Assert.Equal(DecisionVerificationStates.Verified, evidence.VerificationStatus);
        var attachment = Assert.Single(repo.EvidenceAttachments);
        Assert.Equal(DecisionEvidenceAttachmentStates.SupportedBy, attachment.SupportStateCode);
        Assert.True(attachment.IsAuthoritative);
        Assert.NotNull(attachment.AffectedGraphEdgeId);
        Assert.Single(repo.DependencyEvents);
        Assert.Single(repo.Recompetitions);
        Assert.Single(repo.FrontierSnapshots);
        Assert.Equal(1, repo.ReplaceCandidatesCount);
        Assert.Equal(1, repo.ReplaceBranchesCount);
        Assert.Equal(1, repo.UpdateSessionOutcomeCount);
        Assert.NotEqual(baseline.Entropy, repo.Session.CandidateEntropy);
    }

    [Fact]
    public async Task FailedRequiredFactor_RecordsNonAuthoritativeEvidence_WithoutPropagationOrRecompetition()
    {
        var repo = RollbackFixture.SeededRepository(out var baseline);
        var pipeline = DeterministicEvidenceVerificationFixture.Pipeline(EvidenceVerificationFactor.Passage);
        var service = RollbackFixture.Service(repo, new DependencyPropagationService(), pipeline);

        var result = await service.RunResearchLoopAsync(baseline.TenantId, baseline.SessionId, default);

        Assert.Equal(1, result.RoundsExecuted);
        var evidence = Assert.Single(repo.ResearchEvidence);
        Assert.Equal(DecisionVerificationStates.Unverified, evidence.VerificationStatus);
        var verification = Assert.Single(repo.EvidenceVerifications);
        Assert.False(verification.IsVerified);
        Assert.False(verification.IsDecisionAuthorized);
        Assert.Contains(verification.Factors,
            factor => factor.FactorCode == "PASSAGE" && factor.StateCode == "FAILED");
        var attachment = Assert.Single(repo.EvidenceAttachments);
        Assert.Equal(DecisionEvidenceAttachmentStates.Unsupported, attachment.SupportStateCode);
        Assert.False(attachment.IsAuthoritative);
        Assert.Contains("PASSAGE:FAILED", attachment.AssessmentReason);
        Assert.Equal(0, repo.UpdateEdgeVerificationCount);
        Assert.Equal(0, repo.PersistDependencyEventCount);
        Assert.Equal(0, repo.PersistRecompetitionCount);
        Assert.Equal(0, repo.ReplaceCandidatesCount);
        Assert.Equal(0, repo.ReplaceBranchesCount);
        Assert.Equal(0, repo.UpdateSessionOutcomeCount);
        Assert.Equal(baseline.Entropy, repo.Session.CandidateEntropy);
        Assert.Equal(baseline.Margin, repo.Session.DecisionMargin);
        Assert.Equal(baseline.WinnerId, repo.Session.WinnerCandidateId);
    }

    [Theory]
    [InlineData("The governing exemption rule under federal law", DecisionResearchNeedTypes.LegalRule)]
    [InlineData("Payroll records showing hours worked", DecisionResearchNeedTypes.MatterEvidence)]
    [InlineData("The employee's actual duties involved discretion", DecisionResearchNeedTypes.MatterFact)]
    [InlineData("Whether the exemption rule applies to the employee's actual duties", DecisionResearchNeedTypes.Mixed)]
    public void ResearchNeedRouting_ClassifiesExistingFrontierProposition(string proposition, string expected)
    {
        Assert.Equal(expected, DecisionResearchNeedFactory.ClassifyResearchNeed(proposition));
    }
}

// A propagation service that faults deterministically at the round's pre-commit propagation step.
internal sealed class ThrowingPropagationService : IDependencyPropagationService
{
    public DependencyPropagationOutcome Apply(DecisionGraphPersistence snapshot, Guid edgeId, string newStatus, int maxDepth)
        => throw new InvalidOperationException("Injected fault: propagation failed before the round commit.");
}

// Deterministic retriever returning a single source whose snippet echoes the objective so
// VerifyRetrievedSource promotes it to VERIFIED and the round proceeds to the commit boundary.
internal sealed class EchoRetriever : ILegalDecisionRetriever
{
    public Task<IReadOnlyCollection<DecisionRetrievedSource>> RetrieveAsync(
        DecisionRetrievalRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DecisionRetrievedSource>>(
            [new DecisionRetrievedSource("https://authority.example/ruling-1", "Controlling authority", request.Objective)
            {
                SourceType = EvidenceSourceType.MatterDocument,
            }]);
}

internal static class RollbackFixture
{
    internal static readonly Guid User = Guid.NewGuid();

    internal sealed record Baseline(Guid TenantId, Guid SessionId, decimal Entropy, decimal Margin, Guid WinnerId, string StatusCode);

    internal static RecordingDecisionRepository SeededRepository(out Baseline baseline, bool structuralEdgeVerified = false)
    {
        var sessionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var winnerId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var winner = new DecisionCandidatePersistence(
            winnerId, "C1", "Leading outcome", "Leading outcome",
            LegalSupport: 0.60m, FactSupport: 0.60m, EvidenceSupport: 0.60m, AuthoritySupport: 0.60m,
            Verification: 0.80m, Uncertainty: 0.30m, Discrimination: 0.40m, RankingImpact: 0.40m,
            Diversity: 0.30m, RedundancyPenalty: 0.05m, CompositeScore: 0.64m, DecisionSupportCeiling: 0.95m,
            RankOrder: 1, IsWinner: true, IsEliminated: false);

        var branch = new DecisionBranchPersistence(
            branchId, null, 1, "C1.B1", "Frontier branch", "Frontier objective", "ACTIVE",
            InformationValue: 0.50m, DecisionRelevance: 0.50m, FlipPotential: 0.35m, EvidenceAvailability: 0.40m,
            AdvScore: 0.30m, Cost: 0.10m, IsOnFrontier: true, StopReason: null, SortOrder: 0);

        var session = new DecisionSessionPersistence(
            sessionId, tenantId, User, "Frontier objective", DecisionContexts.General, null,
            UsePoloxiEngine: true, StatusCode: "PROVISIONAL_DECISION", TerminalStateCode: null,
            TerminationReason: "LEADING_OUTCOME_WITH_OPEN_HIGH_VALUE_FRONTIER",
            WinnerCandidateId: winnerId, ContractCompleteness: 1.0m,
            CandidateEntropy: 0.90m, DecisionMargin: 0.06m, DepthReached: 1, LlmCallCount: 1, DurationMs: 10,
            FinalAnswer: "Provisional answer.", ClarificationQuestion: null, ClarificationTarget: null,
            CorrelationId: null,
            Candidates: [winner], Branches: [branch], Evidence: [], FlipPoints: [], Events: [])
        {
            MatterId = Guid.NewGuid(),
        };

        var propNode = new DecisionGraphNodePersistence(
            Guid.NewGuid(), DecisionGraphNodeKinds.Proposition, "F1", "Essential fact", "stmt",
            Support: 0.85m, IsEssential: true, IsSatisfied: true, DecisionVerificationStates.Verified, 0)
        {
            SourceBranchId = branchId,
        };
        var evidenceNode = new DecisionGraphNodePersistence(
            Guid.NewGuid(), DecisionGraphNodeKinds.Evidence, "E1", "Evidence", "stmt",
            Support: 0.5m, IsEssential: false, IsSatisfied: true, DecisionVerificationStates.Unverified, 1)
        {
            SourceBranchId = branchId,
        };
        var edge = new DecisionGraphEdgePersistence(
            Guid.NewGuid(), "SUPPORTS", DecisionGraphNodeKinds.Evidence, evidenceNode.NodeId,
            DecisionGraphNodeKinds.Proposition, propNode.NodeId,
            SupportWeight: 0.9m, Materiality: 0.9m, IsEssential: false, IsDispositive: false,
            VerificationStatus: structuralEdgeVerified ? DecisionVerificationStates.Verified : DecisionVerificationStates.Unverified,
            VerificationNotes: null, PropagatedStateCode: null)
        {
            SourceBranchId = branchId,
        };

        var graph = new DecisionGraphPersistence(
            sessionId, tenantId, User, ReadinessSatisfied: false, ReadinessBlockersJson: null,
            Nodes: [propNode, evidenceNode], Edges: [edge], LosingSideTest: null);

        baseline = new Baseline(tenantId, sessionId, session.CandidateEntropy, session.DecisionMargin, winnerId, session.StatusCode);
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

    internal static LegalDecisionService Service(
        RecordingDecisionRepository repo,
        IDependencyPropagationService propagation,
        IIndependentEvidenceVerificationPipeline? verificationPipeline = null)
        => new(
            repo,
            new AtomicResearchNeedAiProvider(),
            new EchoRetriever(),
            propagation,
            new LegalDecisionImpactMapper(),
            new UnusedEpistemicBridge(),
            new UnusedGovernanceRepository(),
            new UnusedMaterialSignalExtractor(),
            new VerifiedDecisionSignalService(),
            new EmptySupportSignalRepository(),
            verificationPipeline ?? DeterministicEvidenceVerificationFixture.Pipeline(),
            NullLogger<LegalDecisionService>.Instance);
}

// ── Unused-on-the-loop-path dependencies: fail loudly if the loop ever touches them unexpectedly. ──
internal sealed class AtomicResearchNeedAiProvider : ILegalDecisionAiProvider
{
    public Task<DecisionAiResult> GenerateAsync(DecisionAiRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new DecisionAiResult(
            """
            {"leaves":[{"researchKey":"LEGAL_RULE_1","researchNeedType":"LEGAL_RULE","researchQuestion":"What governing rule resolves the frontier objective?","proposition":"The governing rule resolves the frontier objective under the stated legal standard.","sourceClass":"LEGAL_AUTHORITY","researchable":true,"searchQuery":"governing rule frontier legal standard","searchConcepts":["governing rule","legal standard"],"authorityKinds":["CASE_LAW"],"applicationDeferred":false,"candidateDiscrimination":["C1"],"parentResearchKey":null,"requires":[]},{"researchKey":"MATTER_FACT_1","researchNeedType":"MATTER_FACT","researchQuestion":"What matter fact bears on the frontier objective?","proposition":"The identified matter fact bears on the frontier objective.","sourceClass":"MATTER_DOCUMENT","researchable":true,"searchQuery":"matter fact frontier objective","searchConcepts":["matter fact","frontier objective"],"authorityKinds":[],"applicationDeferred":false,"candidateDiscrimination":["C1"],"parentResearchKey":null,"requires":[]},{"researchKey":"APPLICATION_1","researchNeedType":"APPLICATION","researchQuestion":"Does the established matter fact satisfy the governing rule?","proposition":"The established matter fact satisfies the governing rule.","sourceClass":"NONE","researchable":false,"searchQuery":null,"searchConcepts":[],"authorityKinds":[],"applicationDeferred":true,"candidateDiscrimination":["C1"],"parentResearchKey":null,"requires":["LEGAL_RULE_1","MATTER_FACT_1"]}]}
            """,
            """
            {"leaves":[{"researchKey":"LEGAL_RULE_1","researchNeedType":"LEGAL_RULE","researchQuestion":"What governing rule resolves the frontier objective?","proposition":"The governing rule resolves the frontier objective under the stated legal standard.","sourceClass":"LEGAL_AUTHORITY","researchable":true,"searchQuery":"governing rule frontier legal standard","searchConcepts":["governing rule","legal standard"],"authorityKinds":["CASE_LAW"],"applicationDeferred":false,"candidateDiscrimination":["C1"],"parentResearchKey":null,"requires":[]},{"researchKey":"MATTER_FACT_1","researchNeedType":"MATTER_FACT","researchQuestion":"What matter fact bears on the frontier objective?","proposition":"The identified matter fact bears on the frontier objective.","sourceClass":"MATTER_DOCUMENT","researchable":true,"searchQuery":"matter fact frontier objective","searchConcepts":["matter fact","frontier objective"],"authorityKinds":[],"applicationDeferred":false,"candidateDiscrimination":["C1"],"parentResearchKey":null,"requires":[]},{"researchKey":"APPLICATION_1","researchNeedType":"APPLICATION","researchQuestion":"Does the established matter fact satisfy the governing rule?","proposition":"The established matter fact satisfies the governing rule.","sourceClass":"NONE","researchable":false,"searchQuery":null,"searchConcepts":[],"authorityKinds":[],"applicationDeferred":true,"candidateDiscrimination":["C1"],"parentResearchKey":null,"requires":["LEGAL_RULE_1","MATTER_FACT_1"]}]}
            """,
            10,
            10,
            "research-need-test",
            TimeSpan.FromMilliseconds(1)));
}

internal sealed class UnusedEpistemicBridge : IEpistemicDecisionBridge
{
    public Task<EpistemicGovernanceResult> ProjectAndGovernAsync(EpistemicDecisionContext context, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Epistemic bridge must not be called on the research-loop path.");
}

internal sealed class UnusedGovernanceRepository : IDecisionGovernanceRepository
{
    public Task UpsertVerdictAsync(DecisionGovernanceVerdictPersistence verdict, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Governance repository must not be called on the research-loop path.");

    public Task<DecisionGovernanceVerdictPersistence?> GetVerdictForSessionAsync(Guid sessionId, Guid tenantId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Governance repository must not be called on the research-loop path.");
}

internal sealed class UnusedMaterialSignalExtractor : IMaterialSignalExtractor
{
    public IReadOnlyList<DecisionSupportSignal> Extract(MaterialSignalExtractionContext context)
        => throw new NotSupportedException("Material signal extractor must not be called on the research-loop path.");
}

internal sealed class EmptySupportSignalRepository : IDecisionSupportSignalRepository
{
    public Task UpsertAsync(DecisionSupportSignalPersistence signal, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<DecisionSupportSignalPersistence>> GetBySessionAsync(Guid decisionSessionId, Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DecisionSupportSignalPersistence>>([]);
}
