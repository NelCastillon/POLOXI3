using System.Data;
using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Persistence.Repositories;

// DB-backed persistence for the POLOXI Hierarchy Execution Lineage & Authority layer (migration 0366).
// Tenant-scoped on every query/mutation. Each accepted run is immutable; authority is an explicit,
// transactional promotion that preserves the UX_Legal_DecHierAuth_Current filtered-unique invariant.
// POLOXI remains the authoritative evaluator; this repository never computes or overrides scoring.
public sealed class LegalHierarchyExecutionRepository(ISqlConnectionFactory connectionFactory) : ILegalHierarchyExecutionRepository
{
    private const string ExecutionColumns = """
        e.HierarchyExecutionId, e.DecisionMatterId, e.DecisionContractId, e.DecisionContractVersion,
        e.RunNumber, e.RunTypeCode, e.ProcessingStatusCode, e.ValidationStatusCode, e.AuthorityStatusCode,
        e.ModelCode, e.PromptCode, e.PromptVersion, e.AlgorithmVersion,
        e.StartedDateUtc, e.CompletedDateUtc, e.RowVersion
        """;

    public async Task<IReadOnlyList<HierarchyExecutionSummaryDto>> GetRunsAsync(
        Guid tenantId, Guid decisionMatterId, Guid decisionContractId, int decisionContractVersion,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ExecutionSummaryRow>(new CommandDefinition(
            $"""
            SELECT {ExecutionColumns},
                   (SELECT COUNT(1) FROM POLOXI.Legal_HierarchyNode n
                    WHERE n.HierarchyExecutionId = e.HierarchyExecutionId AND n.IsDeleted = 0) AS NodeCount,
                   ISNULL((SELECT MAX(n.Depth) FROM POLOXI.Legal_HierarchyNode n
                    WHERE n.HierarchyExecutionId = e.HierarchyExecutionId AND n.IsDeleted = 0), 0) AS MaxDepth
            FROM POLOXI.Legal_HierarchyExecution e
            WHERE e.TenantId = @TenantId AND e.DecisionMatterId = @MatterId
              AND e.DecisionContractId = @ContractId AND e.DecisionContractVersion = @Version
              AND e.IsDeleted = 0
            ORDER BY e.RunNumber DESC;
            """,
            new { TenantId = tenantId, MatterId = decisionMatterId, ContractId = decisionContractId, Version = decisionContractVersion },
            cancellationToken: cancellationToken));

        return rows.Select(MapSummary).ToArray();
    }

    public async Task<HierarchyExecutionDetailDto?> GetExecutionAsync(
        Guid tenantId, Guid hierarchyExecutionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            $"""
            SELECT {ExecutionColumns},
                   (SELECT COUNT(1) FROM POLOXI.Legal_HierarchyNode n
                    WHERE n.HierarchyExecutionId = e.HierarchyExecutionId AND n.IsDeleted = 0) AS NodeCount,
                   ISNULL((SELECT MAX(n.Depth) FROM POLOXI.Legal_HierarchyNode n
                    WHERE n.HierarchyExecutionId = e.HierarchyExecutionId AND n.IsDeleted = 0), 0) AS MaxDepth
            FROM POLOXI.Legal_HierarchyExecution e
            WHERE e.TenantId = @TenantId AND e.HierarchyExecutionId = @Id AND e.IsDeleted = 0;

            SELECT HierarchyNodeId, ParentHierarchyNodeId, Depth, DisplayOrder, NodeTypeCode, NodeRoleCode,
                   Title, Statement, BranchStateCode, ContinueNarrowing, StopReasonCode, Confidence,
                   CapabilityCode, OriginCode
            FROM POLOXI.Legal_HierarchyNode
            WHERE TenantId = @TenantId AND HierarchyExecutionId = @Id AND IsDeleted = 0
            ORDER BY Depth, DisplayOrder;
            """,
            new { TenantId = tenantId, Id = hierarchyExecutionId }, cancellationToken: cancellationToken));

        var head = await multi.ReadSingleOrDefaultAsync<ExecutionSummaryRow>();
        if (head is null) return null;

        var nodes = (await multi.ReadAsync<HierarchyNodeDto>()).ToArray();
        return new HierarchyExecutionDetailDto(MapSummary(head), nodes);
    }

    public async Task<HierarchyAuthorityDto?> GetCurrentAuthorityAsync(
        Guid tenantId, Guid decisionMatterId, Guid decisionContractId, int decisionContractVersion,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<HierarchyAuthorityDto>(new CommandDefinition(
            """
            SELECT a.DecisionHierarchyAuthorityId, a.DecisionMatterId, a.DecisionContractId,
                   a.DecisionContractVersion, a.HierarchyExecutionId, e.RunNumber,
                   a.AuthorityReasonCode, a.EffectiveDateUtc
            FROM POLOXI.Legal_DecisionHierarchyAuthority a
            JOIN POLOXI.Legal_HierarchyExecution e ON e.HierarchyExecutionId = a.HierarchyExecutionId
            WHERE a.TenantId = @TenantId AND a.DecisionMatterId = @MatterId
              AND a.DecisionContractId = @ContractId AND a.DecisionContractVersion = @Version
              AND a.SupersededDateUtc IS NULL AND a.IsDeleted = 0;
            """,
            new { TenantId = tenantId, MatterId = decisionMatterId, ContractId = decisionContractId, Version = decisionContractVersion },
            cancellationToken: cancellationToken));
    }

    public async Task<HierarchyExecutionSummaryDto> RecordExecutionAsync(
        Guid tenantId, Guid userId, HierarchyExecutionRecord record,
        CancellationToken cancellationToken = default)
    {
        var command = record.Header;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();

        // Next monotonic RunNumber for this decision context (matter + contract + version).
        var nextRun = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT ISNULL(MAX(RunNumber), 0) + 1
            FROM POLOXI.Legal_HierarchyExecution WITH (UPDLOCK, HOLDLOCK)
            WHERE TenantId = @TenantId AND DecisionMatterId = @MatterId
              AND DecisionContractId = @ContractId AND DecisionContractVersion = @Version;
            """,
            new { TenantId = tenantId, MatterId = command.DecisionMatterId, ContractId = command.DecisionContractId, Version = command.DecisionContractVersion },
            tx, cancellationToken: cancellationToken));

        var executionId = Guid.NewGuid();
        var completed = command.ProcessingStatusCode is "GENERATED" or "FAILED" or "CANCELLED"
            ? (DateTime?)DateTime.UtcNow : null;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_HierarchyExecution
                (HierarchyExecutionId, DecisionMatterId, DecisionContractId, DecisionContractVersion,
                 RunNumber, RunTypeCode, ProcessingStatusCode, ValidationStatusCode, AuthorityStatusCode,
                 ModelCode, ModelVersion, PromptCode, PromptVersion, AlgorithmVersion, ConfigurationVersion,
                 InputSnapshotHash, StartedDateUtc, CompletedDateUtc, TenantId, CreatedByUserId)
            VALUES
                (@HierarchyExecutionId, @MatterId, @ContractId, @Version,
                 @RunNumber, @RunTypeCode, @ProcessingStatusCode, @ValidationStatusCode, N'CANDIDATE',
                 @ModelCode, @ModelVersion, @PromptCode, @PromptVersion, @AlgorithmVersion, @ConfigurationVersion,
                 @InputSnapshotHash, SYSUTCDATETIME(), @CompletedDateUtc, @TenantId, @UserId);
            """,
            new
            {
                HierarchyExecutionId = executionId,
                MatterId = command.DecisionMatterId,
                ContractId = command.DecisionContractId,
                Version = command.DecisionContractVersion,
                RunNumber = nextRun,
                command.RunTypeCode,
                command.ProcessingStatusCode,
                command.ValidationStatusCode,
                command.ModelCode,
                command.ModelVersion,
                command.PromptCode,
                command.PromptVersion,
                command.AlgorithmVersion,
                command.ConfigurationVersion,
                command.InputSnapshotHash,
                CompletedDateUtc = completed,
                TenantId = tenantId,
                UserId = userId
            },
            tx, cancellationToken: cancellationToken));

        // Nodes — caller-assigned NodeIds preserve intra-record referential integrity. Insert roots
        // before children is unnecessary (self-referencing FK is deferred at COMMIT), but we insert in
        // depth/display order to keep the physical layout aligned with the accepted run ordering.
        foreach (var node in record.Nodes.OrderBy(n => n.Depth).ThenBy(n => n.DisplayOrder))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_HierarchyNode
                    (HierarchyNodeId, HierarchyExecutionId, ParentHierarchyNodeId, Depth, DisplayOrder,
                     NodeTypeCode, NodeRoleCode, Title, Statement, SearchText, BranchStateCode,
                     ContinueNarrowing, StopReasonCode, Confidence, CapabilityCode, OriginCode,
                     OriginPromptCode, OriginPromptVersion, OriginModelCode, SemanticHash, TenantId, CreatedByUserId)
                VALUES
                    (@NodeId, @ExecutionId, @ParentNodeId, @Depth, @DisplayOrder,
                     @NodeTypeCode, @NodeRoleCode, @Title, @Statement, @SearchText, @BranchStateCode,
                     @ContinueNarrowing, @StopReasonCode, @Confidence, @CapabilityCode, @OriginCode,
                     @OriginPromptCode, @OriginPromptVersion, @OriginModelCode, @SemanticHash, @TenantId, @UserId);
                """,
                new
                {
                    node.NodeId,
                    ExecutionId = executionId,
                    node.ParentNodeId,
                    node.Depth,
                    node.DisplayOrder,
                    node.NodeTypeCode,
                    node.NodeRoleCode,
                    node.Title,
                    node.Statement,
                    node.SearchText,
                    node.BranchStateCode,
                    node.ContinueNarrowing,
                    node.StopReasonCode,
                    node.Confidence,
                    node.CapabilityCode,
                    node.OriginCode,
                    node.OriginPromptCode,
                    node.OriginPromptVersion,
                    node.OriginModelCode,
                    node.SemanticHash,
                    TenantId = tenantId,
                    UserId = userId
                },
                tx, cancellationToken: cancellationToken));
        }

        // Edges — materialized non-parent relationships (may be empty for a valid run).
        foreach (var edge in record.Edges)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_HierarchyEdge
                    (HierarchyEdgeId, HierarchyExecutionId, FromHierarchyNodeId, ToHierarchyNodeId,
                     EdgeTypeCode, StateCode, TenantId, CreatedByUserId)
                VALUES
                    (NEWID(), @ExecutionId, @FromNodeId, @ToNodeId, @EdgeTypeCode, @StateCode, @TenantId, @UserId);
                """,
                new
                {
                    ExecutionId = executionId,
                    edge.FromNodeId,
                    edge.ToNodeId,
                    edge.EdgeTypeCode,
                    edge.StateCode,
                    TenantId = tenantId,
                    UserId = userId
                },
                tx, cancellationToken: cancellationToken));
        }

        // APR resolutions — proposition atomicity determinations (may be empty for a valid run).
        foreach (var resolution in record.Resolutions)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_PropositionResolution
                    (PropositionResolutionId, HierarchyNodeId, ResolutionRevision, AtomicityStateCode,
                     ContextResolved, ParentFidelityPassed, IndependentlyTestable, MaterialSplitRemaining,
                     ResolutionStateCode, ResolutionDepth, ResolutionMethodCode, StopReasonCode, TenantId, CreatedByUserId)
                VALUES
                    (NEWID(), @NodeId, @ResolutionRevision, @AtomicityStateCode,
                     @ContextResolved, @ParentFidelityPassed, @IndependentlyTestable, @MaterialSplitRemaining,
                     @ResolutionStateCode, @ResolutionDepth, @ResolutionMethodCode, @StopReasonCode, @TenantId, @UserId);
                """,
                new
                {
                    resolution.NodeId,
                    resolution.ResolutionRevision,
                    resolution.AtomicityStateCode,
                    resolution.ContextResolved,
                    resolution.ParentFidelityPassed,
                    resolution.IndependentlyTestable,
                    resolution.MaterialSplitRemaining,
                    resolution.ResolutionStateCode,
                    resolution.ResolutionDepth,
                    resolution.ResolutionMethodCode,
                    resolution.StopReasonCode,
                    TenantId = tenantId,
                    UserId = userId
                },
                tx, cancellationToken: cancellationToken));
        }

        await InsertOutboxAsync(connection, tx, tenantId, command.DecisionMatterId,
            "HierarchyExecutionCompleted", "HierarchyExecution", executionId,
            $$"""{"hierarchyExecutionId":"{{executionId}}","runNumber":{{nextRun}},"nodeCount":{{record.Nodes.Count}}}""", cancellationToken);

        tx.Commit();

        var detail = await GetExecutionAsync(tenantId, executionId, cancellationToken);
        return detail!.Execution;
    }

    public async Task<PromoteHierarchyAuthorityResult> PromoteAuthorityAsync(
        Guid tenantId, Guid userId, PromoteHierarchyAuthorityCommand command,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();

        var target = await connection.QuerySingleOrDefaultAsync<ExecutionAuthorityRow>(new CommandDefinition(
            """
            SELECT HierarchyExecutionId, DecisionMatterId, DecisionContractId, DecisionContractVersion,
                   RunNumber, ValidationStatusCode, RowVersion
            FROM POLOXI.Legal_HierarchyExecution WITH (UPDLOCK, HOLDLOCK)
            WHERE TenantId = @TenantId AND HierarchyExecutionId = @Id AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, Id = command.HierarchyExecutionId },
            tx, cancellationToken: cancellationToken));

        if (target is null)
            throw new HierarchyExecutionStateException("Hierarchy execution not found for the current tenant.");

        if (!target.RowVersion.SequenceEqual(command.RowVersion))
            throw new HierarchyExecutionConcurrencyException("The hierarchy execution was modified by another process. Reload and retry.");

        if (target.ValidationStatusCode is not ("VALID" or "VALID_WITH_WARNINGS"))
            throw new HierarchyExecutionStateException(
                $"Only validated executions can be promoted (current validation status: {target.ValidationStatusCode}).");

        // Supersede the prior current-authority row (if any) before inserting the new one.
        var superseded = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT TOP 1 HierarchyExecutionId
            FROM POLOXI.Legal_DecisionHierarchyAuthority WITH (UPDLOCK, HOLDLOCK)
            WHERE TenantId = @TenantId AND DecisionMatterId = @MatterId
              AND DecisionContractId = @ContractId AND DecisionContractVersion = @Version
              AND SupersededDateUtc IS NULL AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, MatterId = target.DecisionMatterId, ContractId = target.DecisionContractId, Version = target.DecisionContractVersion },
            tx, cancellationToken: cancellationToken));

        if (superseded == command.HierarchyExecutionId)
            throw new HierarchyExecutionStateException("This execution is already the authoritative hierarchy for its decision context.");

        if (superseded is { } priorExecutionId)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE POLOXI.Legal_DecisionHierarchyAuthority
                SET SupersededDateUtc = SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
                WHERE TenantId = @TenantId AND DecisionMatterId = @MatterId
                  AND DecisionContractId = @ContractId AND DecisionContractVersion = @Version
                  AND SupersededDateUtc IS NULL AND IsDeleted = 0;

                UPDATE POLOXI.Legal_HierarchyExecution
                SET AuthorityStatusCode = N'SUPERSEDED', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
                WHERE TenantId = @TenantId AND HierarchyExecutionId = @PriorId;
                """,
                new { TenantId = tenantId, MatterId = target.DecisionMatterId, ContractId = target.DecisionContractId, Version = target.DecisionContractVersion, PriorId = priorExecutionId, UserId = userId },
                tx, cancellationToken: cancellationToken));

            await InsertOutboxAsync(connection, tx, tenantId, target.DecisionMatterId,
                "HierarchyAuthoritySuperseded", "HierarchyExecution", priorExecutionId,
                $$"""{"hierarchyExecutionId":"{{priorExecutionId}}"}""", cancellationToken);
        }

        var authorityId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_DecisionHierarchyAuthority
                (DecisionHierarchyAuthorityId, DecisionMatterId, DecisionContractId, DecisionContractVersion,
                 HierarchyExecutionId, EffectiveDateUtc, AuthorityReasonCode, TenantId, CreatedByUserId)
            VALUES
                (@AuthorityId, @MatterId, @ContractId, @Version,
                 @ExecutionId, SYSUTCDATETIME(), @ReasonCode, @TenantId, @UserId);

            UPDATE POLOXI.Legal_HierarchyExecution
            SET AuthorityStatusCode = N'AUTHORITATIVE', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
            WHERE TenantId = @TenantId AND HierarchyExecutionId = @ExecutionId;
            """,
            new
            {
                AuthorityId = authorityId,
                MatterId = target.DecisionMatterId,
                ContractId = target.DecisionContractId,
                Version = target.DecisionContractVersion,
                ExecutionId = command.HierarchyExecutionId,
                ReasonCode = command.AuthorityReasonCode,
                TenantId = tenantId,
                UserId = userId
            },
            tx, cancellationToken: cancellationToken));

        await InsertOutboxAsync(connection, tx, tenantId, target.DecisionMatterId,
            "HierarchyAuthorityPromoted", "HierarchyExecution", command.HierarchyExecutionId,
            $$"""{"hierarchyExecutionId":"{{command.HierarchyExecutionId}}","authorityReasonCode":"{{command.AuthorityReasonCode}}"}""", cancellationToken);

        tx.Commit();

        var authority = await GetCurrentAuthorityAsync(tenantId, target.DecisionMatterId, target.DecisionContractId, target.DecisionContractVersion, cancellationToken);
        return new PromoteHierarchyAuthorityResult(authority!, superseded);
    }

    private static Task InsertOutboxAsync(
        IDbConnection connection, IDbTransaction tx, Guid tenantId, Guid? decisionMatterId,
        string eventTypeCode, string aggregateTypeCode, Guid aggregateId, string payloadJson,
        CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_HierarchyOutbox
                (TenantId, DecisionMatterId, EventTypeCode, AggregateTypeCode, AggregateId, PayloadJson)
            VALUES
                (@TenantId, @MatterId, @EventTypeCode, @AggregateTypeCode, @AggregateId, @PayloadJson);
            """,
            new { TenantId = tenantId, MatterId = decisionMatterId, EventTypeCode = eventTypeCode, AggregateTypeCode = aggregateTypeCode, AggregateId = aggregateId, PayloadJson = payloadJson },
            tx, cancellationToken: cancellationToken));

    private static HierarchyExecutionSummaryDto MapSummary(ExecutionSummaryRow r) => new(
        r.HierarchyExecutionId, r.DecisionMatterId, r.DecisionContractId, r.DecisionContractVersion,
        r.RunNumber, r.RunTypeCode, r.ProcessingStatusCode, r.ValidationStatusCode, r.AuthorityStatusCode,
        r.ModelCode, r.PromptCode, r.PromptVersion, r.AlgorithmVersion, r.NodeCount, r.MaxDepth,
        r.StartedDateUtc, r.CompletedDateUtc, r.RowVersion);

    // Constructor parameter order MUST match the SELECT projection column order in GetRunsAsync
    // ({ExecutionColumns} then NodeCount, MaxDepth) so Dapper can bind the positional record constructor.
    private sealed record ExecutionSummaryRow(
        Guid HierarchyExecutionId, Guid DecisionMatterId, Guid DecisionContractId, int DecisionContractVersion,
        int RunNumber, string RunTypeCode, string ProcessingStatusCode, string ValidationStatusCode, string AuthorityStatusCode,
        string? ModelCode, string PromptCode, int PromptVersion, string AlgorithmVersion,
        DateTime StartedDateUtc, DateTime? CompletedDateUtc, byte[] RowVersion, int NodeCount, int MaxDepth);

    private sealed record ExecutionAuthorityRow(
        Guid HierarchyExecutionId, Guid DecisionMatterId, Guid DecisionContractId, int DecisionContractVersion,
        int RunNumber, string ValidationStatusCode, byte[] RowVersion);
}
