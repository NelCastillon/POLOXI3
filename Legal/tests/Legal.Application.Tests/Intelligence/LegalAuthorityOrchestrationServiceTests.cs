using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Application.Features.Intelligence.Decision.Lpi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ──────────────────────────────────────────────────────────────────────────────────────────────────
// LegalAuthorityOrchestrationService — park-only driver for the LegalAuthority channel.
//
// These tests pin the invariants the production service promises:
//   • Only VERIFIED LEGAL_AUTHORITY evidence (with a non-empty summary) becomes a parked proposition;
//     non-authority, non-verified, and empty-summary evidence are ignored.
//   • A summary that shares ≥2 tokens with an evidence-bearing node is parked ReviewRequired with a
//     Supports placement onto that node; an unmatched authority is parked NeedsHierarchyReview with NO
//     placement (never force-fit).
//   • Parking preserves provenance (LegalDocumentVersionId + stable source locator), a stable
//     idempotency key, and the LegalAuthorityDirected retrieval mode — and NEVER scores/commits.
// ──────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LegalAuthorityOrchestrationServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Matter = Guid.NewGuid();

    [Fact]
    public async Task MatchingVerifiedAuthority_IsParkedReviewRequired_WithSupportsPlacement()
    {
        var node = Node("The comparative negligence statute governs apportionment of fault.");
        var authority = Authority("Delaware comparative negligence statute governs apportionment of damages.");
        var repo = new CapturingIntegrationRepository();
        var service = Service(repo, Execution(node), [authority]);

        var result = await service.RunAsync(Request());

        Assert.Equal("Completed", result.StatusCode);
        Assert.Equal(1, result.AuthoritiesScanned);
        Assert.Equal(1, result.AuthoritiesMatched);
        Assert.Equal(1, result.ItemsParked);

        var park = Assert.Single(repo.Parks);
        Assert.Equal(nameof(LpiProposalState.ReviewRequired), park.ReviewStateCode);
        var placement = Assert.Single(park.Placements);
        Assert.Equal(node.HierarchyNodeId, placement.TargetNodeId);
        Assert.Equal(LpiRelationship.Supports, placement.Relationship);
        Assert.Equal(nameof(LpiRetrievalMode.LegalAuthorityDirected), park.RetrievalModeCode);
        Assert.Equal($"legal-authority:{authority.LegalEvidenceItemId:N}", park.Proposition.SourceLocator);
        Assert.Equal(authority.LegalDocumentVersionId, park.Proposition.DocumentVersionId);
    }

    [Fact]
    public async Task UnmatchedVerifiedAuthority_IsParkedNeedsHierarchyReview_WithoutPlacement()
    {
        var node = Node("The policy excludes flood damage from coverage.");
        var authority = Authority("Workers compensation exclusive-remedy bars the tort claim entirely.");
        var repo = new CapturingIntegrationRepository();
        var service = Service(repo, Execution(node), [authority]);

        var result = await service.RunAsync(Request());

        Assert.Equal("Completed", result.StatusCode);
        Assert.Equal(1, result.AuthoritiesScanned);
        Assert.Equal(0, result.AuthoritiesMatched);
        Assert.Equal(1, result.ItemsParked);

        var park = Assert.Single(repo.Parks);
        Assert.Equal(nameof(LpiProposalState.NeedsHierarchyReview), park.ReviewStateCode);
        Assert.Empty(park.Placements);
    }

    [Fact]
    public async Task NonAuthorityAndNonVerifiedEvidence_AreIgnored()
    {
        var node = Node("The comparative negligence statute governs apportionment of fault.");
        var evidence = new[]
        {
            Authority("Comparative negligence statute governs apportionment.", state: LegalEvidenceStates.Proposed),
            Authority("Comparative negligence statute governs apportionment.", typeCode: "MEDICAL_RECORDS"),
            Authority("   ", state: LegalEvidenceStates.Verified),
        };
        var repo = new CapturingIntegrationRepository();
        var service = Service(repo, Execution(node), evidence);

        var result = await service.RunAsync(Request());

        Assert.Equal("NoAuthorities", result.StatusCode);
        Assert.Equal(0, result.ItemsParked);
        Assert.Empty(repo.Parks);
    }

    [Fact]
    public async Task Parking_UsesStableIdempotencyKey_AndNeverCommits()
    {
        var node = Node("The comparative negligence statute governs apportionment of fault.");
        var authority = Authority("Delaware comparative negligence statute governs apportionment of damages.");
        var repo = new CapturingIntegrationRepository();
        var service = Service(repo, Execution(node), [authority]);

        await service.RunAsync(Request());

        var park = Assert.Single(repo.Parks);
        Assert.Equal($"legal-authority-park:{authority.LegalEvidenceItemId:N}", park.Context.IdempotencyKey);
        Assert.Equal(0, repo.CommitCount);
    }

    [Fact]
    public async Task NoAuthoritativeHierarchy_ReturnsNoHierarchy_WithoutParking()
    {
        var authority = Authority("Delaware comparative negligence statute governs apportionment of damages.");
        var repo = new CapturingIntegrationRepository();
        var service = Service(repo, execution: null, [authority]);

        var result = await service.RunAsync(Request());

        Assert.Equal("NoHierarchy", result.StatusCode);
        Assert.Empty(repo.Parks);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────
    private static LegalAuthorityOrchestrationService Service(
        ILpiPropositionIntegrationRepository integrationRepository,
        HierarchyExecutionDetailDto? execution,
        IReadOnlyCollection<LegalEvidenceGraphItemDto> evidence) =>
        new(
            new FakeCorpusRepository(evidence),
            new FakeHierarchyRepository(execution),
            new FakeContractRepository(),
            new FakeRevisionResolver(),
            integrationRepository,
            NullLogger<LegalAuthorityOrchestrationService>.Instance);

    private static LegalAuthorityOrchestrationRequest Request() =>
        new(Tenant, Actor, Matter, DecisionContractRevision: 1, CandidateSetRevision: 1,
            HierarchyRevision: 1, ScoringConfigurationVersion: "SCORE_V1");

    private static HierarchyNodeDto Node(string statement) =>
        new(
            HierarchyNodeId: Guid.NewGuid(),
            ParentHierarchyNodeId: null,
            Depth: 1,
            DisplayOrder: 1,
            NodeTypeCode: "PROPOSITION",
            NodeRoleCode: "PROPOSITION",
            Title: null,
            Statement: statement,
            BranchStateCode: "ACTIVE",
            ContinueNarrowing: false,
            StopReasonCode: null,
            Confidence: null,
            CapabilityCode: null,
            OriginCode: "LLM");

    private static HierarchyExecutionDetailDto Execution(params HierarchyNodeDto[] nodes)
    {
        var summary = new HierarchyExecutionSummaryDto(
            HierarchyExecutionId: Guid.NewGuid(),
            DecisionMatterId: Matter,
            DecisionContractId: Guid.NewGuid(),
            DecisionContractVersion: 1,
            RunNumber: 1,
            RunTypeCode: "FULL",
            ProcessingStatusCode: "COMPLETED",
            ValidationStatusCode: "VALID",
            AuthorityStatusCode: "AUTHORITATIVE",
            ModelCode: null,
            PromptCode: "P",
            PromptVersion: 1,
            AlgorithmVersion: "ALG_V1",
            NodeCount: nodes.Length,
            MaxDepth: 1,
            StartedDateUtc: DateTime.UtcNow,
            CompletedDateUtc: DateTime.UtcNow,
            RowVersion: [0, 0, 0, 0, 0, 0, 0, 1]);
        return new HierarchyExecutionDetailDto(summary, nodes);
    }

    private static LegalEvidenceGraphItemDto Authority(
        string summary,
        string typeCode = "LEGAL_AUTHORITY",
        string state = LegalEvidenceStates.Verified) =>
        new(
            LegalEvidenceItemId: Guid.NewGuid(),
            MatterId: Matter,
            LegalDocumentVersionId: Guid.NewGuid(),
            LegalDocumentPassageId: null,
            EvidenceTypeCode: typeCode,
            DimensionCode: "LEGAL_AUTHORITY",
            Summary: summary,
            EvidenceStateCode: state,
            Confidence: 0.9m,
            GenerationOriginCode: "DYNAMIC_LLM",
            DomainConceptCode: null,
            VerificationProfileCode: null,
            LegalDocumentId: Guid.NewGuid(),
            DocumentFileName: "authority.pdf",
            DocumentTypeCode: "STATUTE",
            DocumentVersionNumber: 1,
            PageNumber: 1,
            SectionPath: null,
            PassageText: summary,
            ExtractionConfidence: 0.9m);

    private sealed class FakeCorpusRepository(IReadOnlyCollection<LegalEvidenceGraphItemDto> evidence) : StubCorpusRepository
    {
        public override Task<LegalMatterEvidenceGraphDto> GetMatterEvidenceGraphAsync(
            Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new LegalMatterEvidenceGraphDto(matterId, 1, evidence, [], []));
    }

    private sealed class FakeHierarchyRepository(HierarchyExecutionDetailDto? execution) : ILegalHierarchyExecutionRepository
    {
        public Task<HierarchyAuthorityDto?> GetCurrentAuthorityAsync(
            Guid tenantId, Guid decisionMatterId, Guid decisionContractId, int decisionContractVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(execution is null
                ? null
                : new HierarchyAuthorityDto(Guid.NewGuid(), decisionMatterId, decisionContractId,
                    decisionContractVersion, execution.Execution.HierarchyExecutionId, 1, "VALIDATED", DateTime.UtcNow));

        public Task<HierarchyExecutionDetailDto?> GetExecutionAsync(
            Guid tenantId, Guid hierarchyExecutionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(execution);

        public Task<IReadOnlyList<HierarchyExecutionSummaryDto>> GetRunsAsync(
            Guid tenantId, Guid decisionMatterId, Guid decisionContractId, int decisionContractVersion,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HierarchyExecutionSummaryDto> RecordExecutionAsync(
            Guid tenantId, Guid userId, HierarchyExecutionRecord record,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PromoteHierarchyAuthorityResult> PromoteAuthorityAsync(
            Guid tenantId, Guid userId, PromoteHierarchyAuthorityCommand command,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeContractRepository : ILegalDecisionContractRepository
    {
        public Task<DecisionContractDto?> GetCurrentContractAsync(
            Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) =>
            Task.FromResult<DecisionContractDto?>(new DecisionContractDto(
                DecisionContractId: Guid.NewGuid(), MatterId: matterId, VersionNumber: 1, StatusCode: "ACTIVE",
                DecisionQuestion: null, ClientObjective: null, SuccessDefinition: null, DecisionDate: null,
                Jurisdiction: null, CourtOrForum: null, GoverningLaw: null, ProceduralPosture: null, CaseType: null,
                ApplicableLegalFramework: null, MovingParty: null, InitialBurden: null, UltimateBurden: null,
                StandardOfProofOrReview: null, BurdenNotes: null, EvidenceBoundary: null, AuthorityBoundary: null,
                SourceRestrictions: null, AuthorityCutoffDate: null, DecisionHorizon: null,
                ExternalResearchPermitted: false, ReviewBeforeActivation: false, ReviewBeforeFinal: false,
                SemanticValidationMode: "STRICT", Notes: null, CreatedByUserId: null, CreatedByDisplayName: null,
                CreatedDateUtc: DateTime.UtcNow, ApprovedByUserId: null, ApprovedByDisplayName: null,
                ApprovedDateUtc: null, RowVersion: [0, 0, 0, 0, 0, 0, 0, 1], Candidates: [], FactBoundaries: [], Tags: []));

        public Task<DecisionContractDto?> GetContractByIdAsync(Guid tenantId, Guid decisionContractId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DecisionContractDto> ProvisionAsync(Guid tenantId, Guid userId, Guid matterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DecisionContractVersionSummaryDto>> GetVersionHistoryAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DecisionContractReviewDto>> GetReviewsAsync(Guid tenantId, Guid decisionContractId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DecisionContractOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> GetMatterTitleAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PoloxiWeightDto>> GetPoloxiWeightsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateDecisionAsync(Guid tenantId, Guid userId, DecisionContractDecisionCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateLegalContextAsync(Guid tenantId, Guid userId, DecisionContractLegalContextCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateBurdenAsync(Guid tenantId, Guid userId, DecisionContractBurdenCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateBoundariesAsync(Guid tenantId, Guid userId, DecisionContractBoundariesCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateCandidatesAsync(Guid tenantId, Guid userId, DecisionContractCandidatesCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateSettingsAsync(Guid tenantId, Guid userId, DecisionContractSettingsCommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task TransitionStatusAsync(Guid tenantId, Guid userId, Guid decisionContractId, byte[] rowVersion, string fromStatus, string toStatus, string reviewAction, string? comment, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DecisionContractDto> CreateNewVersionAsync(Guid tenantId, Guid userId, Guid decisionContractId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ActivateAsync(Guid tenantId, Guid userId, Guid decisionContractId, byte[] rowVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeRevisionResolver : IDecisionRevisionResolver
    {
        public Task<DecisionRevisionSnapshot> ResolveAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DecisionRevisionSnapshot(1, 1, 1, "SCORE_V1"));
    }

    private sealed class CapturingIntegrationRepository : ILpiPropositionIntegrationRepository
    {
        public List<LpiReviewPark> Parks { get; } = [];
        public int CommitCount { get; private set; }

        public Task<Guid> ParkForReviewAsync(LpiReviewPark park, CancellationToken cancellationToken = default)
        {
            Parks.Add(park);
            return Task.FromResult(park.Proposition.ProposalId);
        }

        public Task<LpiIntegrationCommitResult> CommitIntegrationAsync(LpiIntegrationCommit commit, CancellationToken cancellationToken = default)
        {
            CommitCount++;
            throw new NotSupportedException("Parking must never commit.");
        }

        public Task<LpiIntegrationResult?> TryGetOperationAsync(Guid tenantId, string idempotencyKey, CancellationToken cancellationToken = default) => Task.FromResult<LpiIntegrationResult?>(null);
        public Task<long> GetHierarchyVersionAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default) => Task.FromResult(1L);
        public Task<IReadOnlyCollection<string>> GetAcceptedPropositionTextsAsync(Guid tenantId, Guid decisionMatterId, IReadOnlyCollection<Guid> targetNodeIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LpiAncestorScore>> GetAncestorScoresAsync(Guid tenantId, Guid decisionMatterId, Guid parentNodeId, int maxDepth, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LpiReviewItem>> GetPendingReviewItemsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LpiReviewItem?> GetReviewItemAsync(Guid tenantId, Guid retrievedPropositionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RejectReviewItemAsync(Guid tenantId, Guid actorUserId, Guid retrievedPropositionId, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LpiReviewItem?> GetAcceptedPropositionAsync(Guid tenantId, Guid retrievedPropositionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LpiIntegrationCommitResult> WithdrawAcceptedAsync(LpiWithdrawCommit commit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
