using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Epistemic;
using Dapper;

namespace Legal.Infrastructure.Persistence.Repositories;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Verified Decision Signals — Dapper persistence (frozen slice-1 design).
//
// Persists verified decision-support signals into POLOXI.Legal_DecisionSupportSignal. Upsert is by
// SignalId; reads are scoped by session + tenant and ordered deterministically.
// ─────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionSupportSignalRepository(ISqlConnectionFactory connectionFactory) : IDecisionSupportSignalRepository
{
    public async Task UpsertAsync(DecisionSupportSignalPersistence signal, CancellationToken cancellationToken = default)
    {
        const string sql = """
            MERGE POLOXI.Legal_DecisionSupportSignal AS target
            USING (SELECT @SignalId AS SignalId) AS source ON target.SignalId = source.SignalId
            WHEN MATCHED THEN UPDATE SET
                target.[Statement] = @Statement,
                target.NormalizedStatement = @NormalizedStatement,
                target.OriginCode = @OriginCode,
                target.VerificationStateCode = @VerificationStateCode,
                target.RequiresVerification = @RequiresVerification,
                target.VerificationStrength = @VerificationStrength,
                target.DecisionImpact = @DecisionImpact,
                target.SourceBranchId = @SourceBranchId,
                target.SourceCandidateId = @SourceCandidateId,
                target.ProposedByModel = @ProposedByModel,
                target.PromptRunId = @PromptRunId,
                target.VerificationReason = @VerificationReason,
                target.ModifiedDateUtc = SYSUTCDATETIME(),
                target.ModifiedByUserId = @ActorUserId
            WHEN NOT MATCHED THEN INSERT
                (SignalId, DecisionSessionId, MatterId, [Statement], NormalizedStatement, OriginCode,
                 VerificationStateCode, RequiresVerification, VerificationStrength, DecisionImpact,
                 SourceBranchId, SourceCandidateId, ProposedByModel, PromptRunId, VerificationReason,
                 TenantId, CreatedByUserId)
            VALUES
                (@SignalId, @DecisionSessionId, @MatterId, @Statement, @NormalizedStatement, @OriginCode,
                 @VerificationStateCode, @RequiresVerification, @VerificationStrength, @DecisionImpact,
                 @SourceBranchId, @SourceCandidateId, @ProposedByModel, @PromptRunId, @VerificationReason,
                 @TenantId, @ActorUserId);
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, signal, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DecisionSupportSignalPersistence>> GetBySessionAsync(
        Guid decisionSessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT SignalId, DecisionSessionId, MatterId, [Statement], NormalizedStatement, OriginCode,
                   VerificationStateCode, RequiresVerification, VerificationStrength, DecisionImpact,
                   SourceBranchId, SourceCandidateId, ProposedByModel, PromptRunId, VerificationReason,
                   TenantId, CAST(NULL AS UNIQUEIDENTIFIER) AS ActorUserId
            FROM POLOXI.Legal_DecisionSupportSignal
            WHERE DecisionSessionId = @DecisionSessionId AND TenantId = @TenantId AND IsDeleted = 0
            ORDER BY CreatedDateUtc, SignalId;
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<DecisionSupportSignalPersistence>(
            new CommandDefinition(sql, new { DecisionSessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }
}
