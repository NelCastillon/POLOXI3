using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Channels;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// ChannelContributionProjectionService integration tests — the END-TO-END channel→decision join.
//
// Proves the full chain works without POLOXI Core: persisted channel contributions (0367) + durable
// node→decision lineage (0368) → rehydrate → resolve lineage → typed DecisionBranchSignals ready for
// recompetition. Uses in-memory fakes of the two repositories so no DB is required. Fail-soft paths
// (no lineage, no contributions) yield an empty signal set.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class ChannelContributionProjectionServiceTests
{
    private sealed class FakeContributionRepository(IReadOnlyCollection<ChannelContributionDto> byExecution)
        : IChannelContributionRepository
    {
        public Task SaveContributionsAsync(IReadOnlyCollection<ChannelContributionPersistence> contributions, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyCollection<ChannelContributionDto>> GetContributionsForNodeAsync(Guid tenantId, Guid hierarchyNodeId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ChannelContributionDto>>(byExecution.Where(c => c.HierarchyNodeId == hierarchyNodeId).ToArray());

        public Task<IReadOnlyCollection<ChannelContributionDto>> GetContributionsForExecutionAsync(Guid tenantId, Guid hierarchyExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ChannelContributionDto>>(byExecution.Where(c => c.HierarchyExecutionId == hierarchyExecutionId).ToArray());

        public Task<IReadOnlyCollection<ChannelContributionDto>> GetContributionsForMatterAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ChannelContributionDto>>(byExecution.ToArray());
    }

    private sealed class FakeLineageRepository(IReadOnlyCollection<HierarchyNodeDecisionLineageDto> rows)
        : IHierarchyNodeDecisionLineageRepository
    {
        public Task<IReadOnlyCollection<HierarchyNodeDecisionLineageDto>> GetLineageForNodeAsync(Guid tenantId, Guid hierarchyExecutionId, Guid hierarchyNodeId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<HierarchyNodeDecisionLineageDto>>(
                rows.Where(r => r.HierarchyExecutionId == hierarchyExecutionId && r.HierarchyNodeId == hierarchyNodeId).ToArray());

        public Task<IReadOnlyCollection<HierarchyNodeDecisionLineageDto>> GetLineageForSessionAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<HierarchyNodeDecisionLineageDto>>(
                rows.Where(r => r.DecisionSessionId == decisionSessionId).ToArray());
    }

    private static ChannelContributionDto VerifiedEvidenceDto(Guid executionId, Guid nodeId, Guid matterId) => new(
        ChannelContributionId: Guid.NewGuid(),
        DecisionMatterId: matterId,
        HierarchyExecutionId: executionId,
        HierarchyNodeId: nodeId,
        ChannelTypeCode: "DocumentEvidence",
        RelationCode: "SUPPORTS",
        VerificationStateCode: "Verified",
        TargetSignalCode: DecisionChannelCodes.TargetSignal.EvidenceSupport,
        ApplicabilityCode: null,
        DirectnessCode: null,
        SourceTypeCode: "Test",
        SourceId: null,
        SourceLabel: null,
        LegalDocumentId: null,
        LegalDocumentVersionId: null,
        LegalDocumentPassageId: null,
        ProposedByModel: null,
        PromptRunId: null,
        VerificationReason: null,
        EffectiveFromUtc: null,
        EffectiveToUtc: null,
        PlacementMagnitude: null,
        CreatedDateUtc: DateTime.UtcNow);

    [Fact]
    public async Task PersistedVerifiedEvidence_ReachesTypedRecompetition_ViaSessionLineage()
    {
        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var matterId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        var contributions = new[] { VerifiedEvidenceDto(executionId, nodeId, matterId) };
        var lineage = new[]
        {
            new HierarchyNodeDecisionLineageDto(
                HierarchyNodeDecisionLineageId: Guid.NewGuid(),
                DecisionMatterId: matterId,
                HierarchyExecutionId: executionId,
                HierarchyNodeId: nodeId,
                DecisionSessionId: sessionId,
                DecisionBranchId: branchId,
                DecisionCandidateId: null,
                LineageSourceCode: "SYSTEM"),
        };

        var service = new ChannelContributionProjectionService(
            new FakeContributionRepository(contributions),
            new FakeLineageRepository(lineage));

        var signals = await service.ProjectForSessionAsync(tenantId, sessionId);

        var signal = Assert.Single(signals);
        Assert.Equal(branchId, signal.BranchId);
        Assert.Null(signal.CandidateId);
        Assert.True(signal.SupportDelta > 0);
        Assert.Equal(DecisionSignalTarget.Evidence, signal.TargetSignal);
    }

    [Fact]
    public async Task NoLineageForSession_YieldsNoSignals_FailSoft()
    {
        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var matterId = Guid.NewGuid();

        var service = new ChannelContributionProjectionService(
            new FakeContributionRepository(new[] { VerifiedEvidenceDto(executionId, nodeId, matterId) }),
            new FakeLineageRepository([]));

        var signals = await service.ProjectForSessionAsync(tenantId, sessionId);

        Assert.Empty(signals);
    }

    [Fact]
    public async Task ContributionNodeNotMappedForSession_IsExcluded()
    {
        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var mappedNode = Guid.NewGuid();
        var unmappedNode = Guid.NewGuid();
        var matterId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        // Two contributions in the same execution; only the mapped node has a session lineage row.
        var contributions = new[]
        {
            VerifiedEvidenceDto(executionId, mappedNode, matterId),
            VerifiedEvidenceDto(executionId, unmappedNode, matterId),
        };
        var lineage = new[]
        {
            new HierarchyNodeDecisionLineageDto(Guid.NewGuid(), matterId, executionId, mappedNode, sessionId, branchId, null, "SYSTEM"),
        };

        var service = new ChannelContributionProjectionService(
            new FakeContributionRepository(contributions),
            new FakeLineageRepository(lineage));

        var signals = await service.ProjectForSessionAsync(tenantId, sessionId);

        Assert.Equal(branchId, Assert.Single(signals).BranchId);
    }
}
