using Legal.Application.Abstractions.Persistence;
using Dapper;

namespace Legal.Infrastructure.Persistence.Repositories;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-7 governance verdict Dapper persistence.
//
// Non-destructive: an upsert soft-deletes any prior active verdict for the session (preserving it as
// history) and inserts a fresh authoritative row. The V2 verdict and the underlying claims are never
// touched by this repository.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionGovernanceRepository(ISqlConnectionFactory connectionFactory) : IDecisionGovernanceRepository
{
    public async Task UpsertVerdictAsync(
        DecisionGovernanceVerdictPersistence verdict,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // Retire the prior active verdict as history rather than deleting it.
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE POLOXI.Legal_DecisionGovernanceVerdict SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId WHERE DecisionSessionId = @DecisionSessionId AND IsDeleted = 0;",
            new { verdict.DecisionSessionId, verdict.ActorUserId },
            transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_DecisionGovernanceVerdict
                (GovernanceVerdictId, DecisionSessionId, MatterId, OverrideModeCode, V2ReadinessSatisfied,
                 EaReadinessReady, EaReadinessEnforced, OutputClean, EffectiveReadinessSatisfied, OverrideApplied,
                 ProjectedClaimCount, AuthorizedClaimCount, BlockerCount, ViolationCount,
                 BlockersJson, ViolationsJson, InvolvedClaimsJson, NarrativeJson, TenantId, CreatedByUserId)
            VALUES
                (@GovernanceVerdictId, @DecisionSessionId, @MatterId, @OverrideModeCode, @V2ReadinessSatisfied,
                 @EaReadinessReady, @EaReadinessEnforced, @OutputClean, @EffectiveReadinessSatisfied, @OverrideApplied,
                 @ProjectedClaimCount, @AuthorizedClaimCount, @BlockerCount, @ViolationCount,
                 @BlockersJson, @ViolationsJson, @InvolvedClaimsJson, @NarrativeJson, @TenantId, @ActorUserId);
            """,
            verdict,
            transaction, cancellationToken: cancellationToken));

        transaction.Commit();
    }

    public async Task<DecisionGovernanceVerdictPersistence?> GetVerdictForSessionAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP 1
                GovernanceVerdictId, DecisionSessionId, MatterId, OverrideModeCode, V2ReadinessSatisfied,
                EaReadinessReady, EaReadinessEnforced, OutputClean, EffectiveReadinessSatisfied, OverrideApplied,
                ProjectedClaimCount, AuthorizedClaimCount, BlockerCount, ViolationCount,
                BlockersJson, ViolationsJson, InvolvedClaimsJson, NarrativeJson, TenantId,
                COALESCE(ModifiedByUserId, CreatedByUserId) AS ActorUserId
            FROM POLOXI.Legal_DecisionGovernanceVerdict
            WHERE DecisionSessionId = @sessionId AND TenantId = @tenantId AND IsDeleted = 0
            ORDER BY CreatedDateUtc DESC;
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DecisionGovernanceVerdictPersistence>(
            new CommandDefinition(sql, new { sessionId, tenantId }, cancellationToken: cancellationToken));
    }
}
