using System.Data;
using Dapper;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Persistence.Repositories;

// Per-matter Evidence Type scope (migration 0441: POLOXI.Legal_MatterEvidenceScope + lookups).
public sealed partial class LegalDecisionRepository
{
    private const string MatterEvidenceScopeColumns = """
        s.MatterEvidenceScopeId, s.DecisionMatterId, s.PackCode, s.TargetKindCode, s.TargetCode, s.DimensionCode, s.DisplayName,
        s.ScopeStatusCode, s.DecidedByCode, s.DeterministicScore, s.LlmScore, s.BlendedScore, s.TierCode, s.RelevanceReasons, s.LlmReason,
        s.IsRequiredCoverage, s.IsPrioritized, s.DocumentPresent, s.MappedPassageCount, s.UserNote, s.LastEvaluatedExecutionId, s.LastEvaluatedDateUtc,
        s.SortOrder, s.CreatedDateUtc, s.ModifiedDateUtc
        """;

    public async Task<IReadOnlyCollection<MatterEvidenceScopeDto>> GetMatterEvidenceScopesAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatterEvidenceScopeDto>(new CommandDefinition(
            $"""
            SELECT {MatterEvidenceScopeColumns}
            FROM POLOXI.Legal_MatterEvidenceScope s
            WHERE s.TenantId = @TenantId AND s.DecisionMatterId = @DecisionMatterId AND s.IsDeleted = 0
            ORDER BY s.DimensionCode, s.SortOrder, s.DisplayName;
            """,
            new { TenantId = tenantId, DecisionMatterId = decisionMatterId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<IReadOnlyCollection<MatterEvidenceScopeStatusDto>> GetMatterEvidenceScopeStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatterEvidenceScopeStatusDto>(new CommandDefinition(
            """
            SELECT StatusCode, Name, Description, ParticipatesInL3, SortOrder
            FROM POLOXI.Legal_MatterEvidenceScopeStatus
            WHERE IsDeleted = 0 AND IsActive = 1 AND (TenantId IS NULL OR TenantId = @TenantId)
            ORDER BY SortOrder, Name;
            """,
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<IReadOnlyCollection<MatterEvidenceScopeDeciderDto>> GetMatterEvidenceScopeDecidersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatterEvidenceScopeDeciderDto>(new CommandDefinition(
            """
            SELECT DeciderCode, Name, Description, IsAuthoritative, SortOrder
            FROM POLOXI.Legal_MatterEvidenceScopeDecider
            WHERE IsDeleted = 0 AND IsActive = 1 AND (TenantId IS NULL OR TenantId = @TenantId)
            ORDER BY SortOrder, Name;
            """,
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<Guid> UpsertUserMatterEvidenceScopeAsync(Guid tenantId, Guid userId, Guid decisionMatterId, MatterEvidenceScopeEvaluation row, string? userNote, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            """
            DECLARE @Id UNIQUEIDENTIFIER = (SELECT TOP(1) MatterEvidenceScopeId FROM POLOXI.Legal_MatterEvidenceScope
                WHERE TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId AND PackCode = @PackCode
                  AND TargetKindCode = @TargetKindCode AND TargetCode = @TargetCode AND IsDeleted = 0);
            IF @Id IS NULL
            BEGIN
                SET @Id = NEWID();
                INSERT POLOXI.Legal_MatterEvidenceScope(MatterEvidenceScopeId, TenantId, DecisionMatterId, PackCode, TargetKindCode, TargetCode, DimensionCode, DisplayName,
                    ScopeStatusCode, DecidedByCode, IsRequiredCoverage, UserNote, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
                VALUES(@Id, @TenantId, @DecisionMatterId, @PackCode, @TargetKindCode, @TargetCode, @DimensionCode, @DisplayName,
                    @ScopeStatusCode, 'USER', @IsRequiredCoverage, @UserNote, @SortOrder, SYSUTCDATETIME(), @UserId, 0);
            END
            ELSE
            BEGIN
                UPDATE POLOXI.Legal_MatterEvidenceScope
                   SET ScopeStatusCode = @ScopeStatusCode, DecidedByCode = 'USER', UserNote = @UserNote,
                       ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
                 WHERE MatterEvidenceScopeId = @Id;
            END
            SELECT @Id;
            """,
            new
            {
                TenantId = tenantId, UserId = userId, DecisionMatterId = decisionMatterId, row.PackCode, row.TargetKindCode, row.TargetCode,
                row.DimensionCode, row.DisplayName, row.ScopeStatusCode, row.IsRequiredCoverage, UserNote = userNote, row.SortOrder
            }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<int> SetMatterEvidenceScopeStatusAsync(Guid tenantId, Guid userId, Guid decisionMatterId, IReadOnlyCollection<Guid> ids, string scopeStatusCode, string? userNote, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return 0;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_MatterEvidenceScope
               SET ScopeStatusCode = @ScopeStatusCode, DecidedByCode = 'USER',
                   UserNote = COALESCE(@UserNote, UserNote),
                   ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
             WHERE TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId AND IsDeleted = 0 AND MatterEvidenceScopeId IN @Ids;
            """,
            new { TenantId = tenantId, UserId = userId, DecisionMatterId = decisionMatterId, Ids = ids.ToArray(), ScopeStatusCode = scopeStatusCode, UserNote = userNote },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> SoftDeleteMatterEvidenceScopeAsync(Guid tenantId, Guid userId, Guid decisionMatterId, Guid matterEvidenceScopeId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_MatterEvidenceScope
               SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
             WHERE TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId AND MatterEvidenceScopeId = @Id AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, UserId = userId, DecisionMatterId = decisionMatterId, Id = matterEvidenceScopeId }, cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task UpsertSystemMatterEvidenceScopesAsync(Guid tenantId, Guid userId, Guid decisionMatterId, IReadOnlyCollection<MatterEvidenceScopeEvaluation> rows, CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0) return;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        const string sql = """
            DECLARE @Id UNIQUEIDENTIFIER = (SELECT TOP(1) MatterEvidenceScopeId FROM POLOXI.Legal_MatterEvidenceScope
                WHERE TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId AND PackCode = @PackCode
                  AND TargetKindCode = @TargetKindCode AND TargetCode = @TargetCode AND IsDeleted = 0);
            IF @Id IS NULL
                INSERT POLOXI.Legal_MatterEvidenceScope(MatterEvidenceScopeId, TenantId, DecisionMatterId, PackCode, TargetKindCode, TargetCode, DimensionCode, DisplayName,
                    ScopeStatusCode, DecidedByCode, DeterministicScore, LlmScore, BlendedScore, TierCode, RelevanceReasons, LlmReason,
                    IsRequiredCoverage, IsPrioritized, DocumentPresent, MappedPassageCount, LastEvaluatedExecutionId, LastEvaluatedDateUtc, SortOrder,
                    CreatedDateUtc, CreatedByUserId, IsDeleted)
                VALUES(NEWID(), @TenantId, @DecisionMatterId, @PackCode, @TargetKindCode, @TargetCode, @DimensionCode, @DisplayName,
                    @ScopeStatusCode, @DecidedByCode, @DeterministicScore, @LlmScore, @BlendedScore, @TierCode, @RelevanceReasons, @LlmReason,
                    @IsRequiredCoverage, @IsPrioritized, @DocumentPresent, @MappedPassageCount, @LastEvaluatedExecutionId, SYSUTCDATETIME(), @SortOrder,
                    SYSUTCDATETIME(), @UserId, 0);
            ELSE
                UPDATE POLOXI.Legal_MatterEvidenceScope
                   SET DimensionCode = @DimensionCode, DisplayName = @DisplayName,
                       -- USER decisions are authoritative: keep status/decider/note; refresh scores and provenance only.
                       ScopeStatusCode = CASE WHEN DecidedByCode = 'USER' THEN ScopeStatusCode ELSE @ScopeStatusCode END,
                       DecidedByCode   = CASE WHEN DecidedByCode = 'USER' THEN DecidedByCode   ELSE @DecidedByCode   END,
                       DeterministicScore = @DeterministicScore, LlmScore = @LlmScore, BlendedScore = @BlendedScore, TierCode = @TierCode,
                       RelevanceReasons = @RelevanceReasons, LlmReason = @LlmReason,
                       IsRequiredCoverage = @IsRequiredCoverage, IsPrioritized = @IsPrioritized, DocumentPresent = @DocumentPresent,
                       MappedPassageCount = @MappedPassageCount, LastEvaluatedExecutionId = @LastEvaluatedExecutionId, LastEvaluatedDateUtc = SYSUTCDATETIME(),
                       SortOrder = @SortOrder, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
                 WHERE MatterEvidenceScopeId = @Id;
            """;
        foreach (var row in rows)
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                TenantId = tenantId, UserId = userId, DecisionMatterId = decisionMatterId,
                row.PackCode, row.TargetKindCode, row.TargetCode, row.DimensionCode, row.DisplayName, row.ScopeStatusCode, row.DecidedByCode,
                row.DeterministicScore, row.LlmScore, row.BlendedScore, row.TierCode, row.RelevanceReasons, row.LlmReason,
                row.IsRequiredCoverage, row.IsPrioritized, row.DocumentPresent, row.MappedPassageCount, row.LastEvaluatedExecutionId, row.SortOrder
            }, transaction, cancellationToken: cancellationToken));
        }
        transaction.Commit();
    }
}
