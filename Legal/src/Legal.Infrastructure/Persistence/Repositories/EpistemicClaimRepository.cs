using Legal.Application.Abstractions.Persistence;
using Dapper;

namespace Legal.Infrastructure.Persistence.Repositories;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-1 Dapper persistence.
//
// Persists authoritative claim propositions, support edges, and idempotent verification events into
// the POLOXI schema. Verification events are written under UX_Legal_ClaimVerificationEvent_IdempotencyKey
// so a replayed event produces exactly one transition (callers run propagation only when true).
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class EpistemicClaimRepository(ISqlConnectionFactory connectionFactory) : IEpistemicClaimRepository
{
    public async Task UpsertClaimAsync(ClaimPropositionPersistence claim, CancellationToken cancellationToken = default)
    {
        const string sql = """
            MERGE POLOXI.Legal_ClaimProposition AS target
            USING (SELECT @ClaimId AS ClaimId) AS source ON target.ClaimId = source.ClaimId
            WHEN MATCHED THEN UPDATE SET
                target.[Text] = @Text,
                target.NormalizedText = @NormalizedText,
                target.ClaimTypeCode = @ClaimTypeCode,
                target.ClaimOriginCode = @ClaimOriginCode,
                target.VerificationStateCode = @VerificationStateCode,
                target.DecisionAuthorityCode = @DecisionAuthorityCode,
                target.VerificationStrength = @VerificationStrength,
                target.Materiality = @Materiality,
                target.DecisionImpact = @DecisionImpact,
                target.Discrimination = @Discrimination,
                target.Uncertainty = @Uncertainty,
                target.IsEssential = @IsEssential,
                target.SourceBranchId = @SourceBranchId,
                target.SourceCandidateId = @SourceCandidateId,
                target.ProposedByModel = @ProposedByModel,
                target.PromptRunId = @PromptRunId,
                target.VerificationReason = @VerificationReason,
                target.[Version] = @Version,
                target.ModifiedDateUtc = SYSUTCDATETIME(),
                target.ModifiedByUserId = @ActorUserId
            WHEN NOT MATCHED THEN INSERT
                (ClaimId, DecisionSessionId, MatterId, [Text], NormalizedText, ClaimTypeCode, ClaimOriginCode,
                 VerificationStateCode, DecisionAuthorityCode, VerificationStrength, Materiality, DecisionImpact,
                 Discrimination, Uncertainty, IsEssential, SourceBranchId, SourceCandidateId, ProposedByModel,
                 PromptRunId, VerificationReason, [Version], TenantId, CreatedByUserId)
            VALUES
                (@ClaimId, @DecisionSessionId, @MatterId, @Text, @NormalizedText, @ClaimTypeCode, @ClaimOriginCode,
                 @VerificationStateCode, @DecisionAuthorityCode, @VerificationStrength, @Materiality, @DecisionImpact,
                 @Discrimination, @Uncertainty, @IsEssential, @SourceBranchId, @SourceCandidateId, @ProposedByModel,
                 @PromptRunId, @VerificationReason, @Version, @TenantId, @ActorUserId);
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, claim, cancellationToken: cancellationToken));
    }

    public async Task ReplaceSupportAsync(
        Guid claimId,
        IReadOnlyList<ClaimSupportPersistence> support,
        Guid tenantId,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE POLOXI.Legal_ClaimSupport SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId WHERE ClaimId = @ClaimId AND IsDeleted = 0;",
            new { ClaimId = claimId, ActorUserId = actorUserId },
            transaction, cancellationToken: cancellationToken));

        if (support.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_ClaimSupport
                    (ClaimSupportId, ClaimId, EvidenceId, AuthorityId, RelationshipCode, Strength, IndependentlyVerified,
                     SourceLocation, VerificationReason, TenantId, CreatedByUserId)
                VALUES
                    (@ClaimSupportId, @ClaimId, @EvidenceId, @AuthorityId, @RelationshipCode, @Strength, @IndependentlyVerified,
                     @SourceLocation, @VerificationReason, @TenantId, @ActorUserId);
                """,
                support,
                transaction, cancellationToken: cancellationToken));

        transaction.Commit();
    }

    public async Task<bool> TryRecordVerificationEventAsync(
        ClaimVerificationEventPersistence verificationEvent,
        CancellationToken cancellationToken = default)
    {
        // The unique idempotency key makes replays no-ops; INSERT ... WHERE NOT EXISTS returns the
        // affected row count so the caller runs downstream propagation exactly once.
        const string sql = """
            INSERT INTO POLOXI.Legal_ClaimVerificationEvent
                (EventId, ClaimId, PreviousStateCode, NewStateCode, Reason, EvidenceIdsJson, AuthorityIdsJson,
                 IdempotencyKey, OccurredAt, TenantId, CreatedByUserId)
            SELECT @EventId, @ClaimId, @PreviousStateCode, @NewStateCode, @Reason, @EvidenceIdsJson, @AuthorityIdsJson,
                   @IdempotencyKey, @OccurredAt, @TenantId, @ActorUserId
            WHERE NOT EXISTS
                (SELECT 1 FROM POLOXI.Legal_ClaimVerificationEvent WHERE IdempotencyKey = @IdempotencyKey);
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, verificationEvent, cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<IReadOnlyList<ClaimPropositionPersistence>> GetClaimsForSessionAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ClaimId, DecisionSessionId, MatterId, [Text], NormalizedText, ClaimTypeCode, ClaimOriginCode,
                   VerificationStateCode, DecisionAuthorityCode, VerificationStrength, Materiality, DecisionImpact,
                   Discrimination, Uncertainty, IsEssential, SourceBranchId, SourceCandidateId, ProposedByModel,
                   PromptRunId, VerificationReason, [Version], TenantId, CAST(NULL AS UNIQUEIDENTIFIER) AS ActorUserId
            FROM POLOXI.Legal_ClaimProposition
            WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND IsDeleted = 0
            ORDER BY CreatedDateUtc;
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ClaimPropositionPersistence>(
            new CommandDefinition(sql, new { SessionId = sessionId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<ClaimPropositionPersistence?> GetClaimAsync(
        Guid claimId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ClaimId, DecisionSessionId, MatterId, [Text], NormalizedText, ClaimTypeCode, ClaimOriginCode,
                   VerificationStateCode, DecisionAuthorityCode, VerificationStrength, Materiality, DecisionImpact,
                   Discrimination, Uncertainty, IsEssential, SourceBranchId, SourceCandidateId, ProposedByModel,
                   PromptRunId, VerificationReason, [Version], TenantId, CAST(NULL AS UNIQUEIDENTIFIER) AS ActorUserId
            FROM POLOXI.Legal_ClaimProposition
            WHERE ClaimId = @ClaimId AND TenantId = @TenantId AND IsDeleted = 0;
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ClaimPropositionPersistence>(
            new CommandDefinition(sql, new { ClaimId = claimId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ClaimSupportPersistence>> GetSupportForClaimAsync(
        Guid claimId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ClaimSupportId, ClaimId, EvidenceId, AuthorityId, RelationshipCode, Strength, IndependentlyVerified,
                   SourceLocation, VerificationReason, TenantId, CAST(NULL AS UNIQUEIDENTIFIER) AS ActorUserId
            FROM POLOXI.Legal_ClaimSupport
            WHERE ClaimId = @ClaimId AND TenantId = @TenantId AND IsDeleted = 0
            ORDER BY CreatedDateUtc;
            """;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ClaimSupportPersistence>(
            new CommandDefinition(sql, new { ClaimId = claimId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }
}
