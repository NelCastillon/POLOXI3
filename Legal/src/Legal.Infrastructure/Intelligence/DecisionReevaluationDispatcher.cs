using System.Data;
using Dapper;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Continuous Decision Integrity — Phase 2 Reevaluation Dispatcher (worker trigger).
//
// Phase 1 (MatterChangeProcessor) records candidate impacts and marks the change event PROCESSED.
// This dispatcher is the automatic trigger: it atomically claims material PROCESSED change events
// (flipping them to REEVALUATING so no two workers double-process — READPAST/UPDLOCK, idempotent),
// resolves the latest decision session for the affected matter, loads the current candidate/branch
// projection + core settings, then delegates the authoritative recompetition to the pure/orchestration
// DecisionReevaluationService. Each event is advanced to REEVALUATED or REEVALUATION_FAILED.
//
// The dispatcher never scores (POLOXI Core does) and never deletes history: DecisionReevaluationService
// appends superseding snapshots and marks prior ones SUPERSEDED. Reuses the existing ProcessingStatusCode
// column on Legal_MatterChangeEvent — no schema migration required.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionReevaluationDispatcher(
    ISqlConnectionFactory connectionFactory,
    ILegalDecisionRepository decisionRepository,
    DecisionReevaluationService reevaluationService,
    ILogger<DecisionReevaluationDispatcher> logger) : IDecisionReevaluationDispatcher
{
    public async Task<int> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Atomically claim material, already-PROCESSED change events (skip NO_MATERIAL_IMPACT: nothing to
        // recompete). Flip to REEVALUATING under lock so a retried/parallel poll never double-processes.
        // Also reclaim rows stuck in REEVALUATING for >10 minutes (a worker that died mid-flight), so a
        // crash never strands a change event permanently.
        using var claimTransaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        var claimed = (await connection.QueryAsync<ClaimedEvent>(new CommandDefinition(
            """
            ;WITH claim AS
            (
                SELECT TOP (@BatchSize) *
                FROM POLOXI.Legal_MatterChangeEvent WITH (UPDLOCK,READPAST,ROWLOCK,READCOMMITTEDLOCK)
                WHERE IsDeleted=0
                  AND ClassificationCode IS NOT NULL
                  AND ClassificationCode<>@NoMaterialImpact
                  AND (ProcessingStatusCode=@Processed
                       OR (ProcessingStatusCode=@Reevaluating
                           AND COALESCE(ModifiedDateUtc,CreatedDateUtc)<DATEADD(minute,-10,SYSUTCDATETIME())))
                ORDER BY CreatedDateUtc, MatterChangeEventId
            )
            UPDATE claim SET ProcessingStatusCode=@Reevaluating, ModifiedDateUtc=SYSUTCDATETIME()
            OUTPUT inserted.MatterChangeEventId, inserted.DecisionMatterId, inserted.TenantId,
                   inserted.CreatedByUserId, inserted.ClassificationCode;
            """,
            new
            {
                BatchSize = Math.Clamp(batchSize, 1, 100),
                Processed = MatterChangeProcessingStatus.Processed,
                Reevaluating = MatterChangeProcessingStatus.Reevaluating,
                NoMaterialImpact = MatterChangeClassification.NoMaterialImpact
            },
            claimTransaction, cancellationToken: cancellationToken))).ToArray();
        claimTransaction.Commit();

        foreach (var item in claimed)
        {
            try
            {
                var userId = item.CreatedByUserId ?? Guid.Empty;

                // Resolve the latest decision session for the matter to get the current candidate/branch state.
                var sessionId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                    """
                    SELECT TOP 1 DecisionSessionId
                    FROM POLOXI.Legal_DecisionSession
                    WHERE IsDeleted=0 AND TenantId=@TenantId AND MatterId=@MatterId
                    ORDER BY CreatedDateUtc DESC;
                    """,
                    new { item.TenantId, MatterId = item.DecisionMatterId },
                    cancellationToken: cancellationToken));

                if (sessionId is not { } sid)
                {
                    await MarkAsync(connection, item, MatterChangeProcessingStatus.Reevaluated,
                        "No decision session for matter; nothing to recompete.", cancellationToken);
                    continue;
                }

                var session = await decisionRepository.GetSessionAsync(item.TenantId, sid, cancellationToken);
                if (session is null || session.Candidates.Count == 0)
                {
                    await MarkAsync(connection, item, MatterChangeProcessingStatus.Reevaluated,
                        "Decision session has no candidates; nothing to recompete.", cancellationToken);
                    continue;
                }

                var settings = await decisionRepository.GetCoreSettingsAsync(cancellationToken);
                var candidates = session.Candidates.ToArray();
                var branches = session.Branches.ToArray();
                var reopenAllowed = branches.Select(b => b.DecisionBranchId).ToHashSet();

                var result = await reevaluationService.ReevaluateChangeAsync(
                    item.TenantId,
                    userId == Guid.Empty ? session.ActorUserId ?? Guid.Empty : userId,
                    item.DecisionMatterId,
                    item.MatterChangeEventId,
                    candidates,
                    branches,
                    settings,
                    reopenAllowed,
                    cancellationToken);

                await MarkAsync(connection, item, MatterChangeProcessingStatus.Reevaluated, result.Reason, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Reevaluation failed for change event {EventId}.", item.MatterChangeEventId);
                await MarkAsync(connection, item, MatterChangeProcessingStatus.ReevaluationFailed, ex.Message, CancellationToken.None);
            }
        }

        return claimed.Length;
    }

    private static Task MarkAsync(IDbConnection connection, ClaimedEvent item, string status, string? error, CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_MatterChangeEvent
               SET ProcessingStatusCode=@Status,
                   ProcessingError=CASE WHEN @Status=@Failed THEN LEFT(@Error,4000) ELSE NULL END,
                   ProcessedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME()
             WHERE MatterChangeEventId=@EventId AND IsDeleted=0;
            """,
            new
            {
                Status = status,
                Failed = MatterChangeProcessingStatus.ReevaluationFailed,
                Error = error,
                EventId = item.MatterChangeEventId
            },
            cancellationToken: cancellationToken));

    private sealed record ClaimedEvent(
        Guid MatterChangeEventId,
        Guid DecisionMatterId,
        Guid TenantId,
        Guid? CreatedByUserId,
        string? ClassificationCode);
}
