using System.Data;
using Dapper;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Persistence.Repositories;

// Per-matter Atomic Propositions (migration 0442: POLOXI.Legal_MatterAtomicProposition + lookups).
public sealed partial class LegalDecisionRepository
{
    private const string MatterAtomicPropositionColumns = """
        p.MatterAtomicPropositionId, p.DecisionMatterId, p.PackCode, p.ParentTargetKindCode, p.ParentTargetCode, p.ParentDisplayName, p.DimensionCode,
        p.ParentPropositionCode, p.PropositionCode, p.LevelNumber, p.DisplayName, p.Statement, p.SearchText, p.CapabilityCode, p.Confidence,
        p.SemanticTypeCode, p.BranchRoleCode, p.ContinueNarrowing, p.StopReason, p.DependsOnCodes, p.EquivalentToCodes, p.AnchorCount, p.AnchorsJson,
        p.WorkerName, p.BatchNumber, p.AttemptNumber, p.BatchTierCode, p.ModelCode, p.ProviderCode, p.InputTokenCount, p.OutputTokenCount, p.DurationMs, p.ProviderRequestId,
        p.ScopeStatusCode, p.DecidedByCode, p.UserNote, p.GenerationCount, p.LastWideBranchId, p.LastEvaluatedExecutionId, p.LastEvaluatedDateUtc,
        p.SortOrder, p.CreatedDateUtc, p.ModifiedDateUtc
        """;

    public async Task<IReadOnlyCollection<MatterAtomicPropositionDto>> GetMatterAtomicPropositionsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatterAtomicPropositionDto>(new CommandDefinition(
            $"""
            SELECT {MatterAtomicPropositionColumns}
            FROM POLOXI.Legal_MatterAtomicProposition p
            WHERE p.TenantId = @TenantId AND p.DecisionMatterId = @DecisionMatterId AND p.IsDeleted = 0
            ORDER BY p.DimensionCode, p.ParentDisplayName, p.LevelNumber, p.SortOrder, p.DisplayName;
            """,
            new { TenantId = tenantId, DecisionMatterId = decisionMatterId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<IReadOnlyCollection<MatterAtomicPropositionStatusDto>> GetMatterAtomicPropositionStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatterAtomicPropositionStatusDto>(new CommandDefinition(
            """
            SELECT StatusCode, Name, Description, ParticipatesInNarrowing, SortOrder
            FROM POLOXI.Legal_MatterAtomicPropositionStatus
            WHERE IsDeleted = 0 AND IsActive = 1 AND (TenantId IS NULL OR TenantId = @TenantId)
            ORDER BY SortOrder, Name;
            """,
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<IReadOnlyCollection<MatterAtomicPropositionDeciderDto>> GetMatterAtomicPropositionDecidersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatterAtomicPropositionDeciderDto>(new CommandDefinition(
            """
            SELECT DeciderCode, Name, Description, IsAuthoritative, SortOrder
            FROM POLOXI.Legal_MatterAtomicPropositionDecider
            WHERE IsDeleted = 0 AND IsActive = 1 AND (TenantId IS NULL OR TenantId = @TenantId)
            ORDER BY SortOrder, Name;
            """,
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<Guid> UpsertUserMatterAtomicPropositionAsync(Guid tenantId, Guid userId, Guid decisionMatterId, MatterAtomicPropositionEvaluation row, string scopeStatusCode, string? userNote, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            """
            DECLARE @Id UNIQUEIDENTIFIER = (SELECT TOP(1) MatterAtomicPropositionId FROM POLOXI.Legal_MatterAtomicProposition
                WHERE TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId AND PackCode = @PackCode
                  AND ParentTargetKindCode = @ParentTargetKindCode AND ParentTargetCode = @ParentTargetCode AND PropositionCode = @PropositionCode AND IsDeleted = 0);
            IF @Id IS NULL
            BEGIN
                SET @Id = NEWID();
                INSERT POLOXI.Legal_MatterAtomicProposition(MatterAtomicPropositionId, TenantId, DecisionMatterId, PackCode, ParentTargetKindCode, ParentTargetCode, ParentDisplayName, DimensionCode,
                    PropositionCode, LevelNumber, DisplayName, Statement, Confidence, SemanticTypeCode, BranchRoleCode, WorkerName, BatchNumber, AttemptNumber,
                    ScopeStatusCode, DecidedByCode, UserNote, GenerationCount, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
                VALUES(@Id, @TenantId, @DecisionMatterId, @PackCode, @ParentTargetKindCode, @ParentTargetCode, @ParentDisplayName, @DimensionCode,
                    @PropositionCode, @LevelNumber, @DisplayName, @Statement, @Confidence, @SemanticTypeCode, @BranchRoleCode, @WorkerName, 0, 1,
                    @ScopeStatusCode, 'USER', @UserNote, 0, @SortOrder, SYSUTCDATETIME(), @UserId, 0);
            END
            ELSE
            BEGIN
                UPDATE POLOXI.Legal_MatterAtomicProposition
                   SET DisplayName = @DisplayName, Statement = @Statement, Confidence = @Confidence,
                       SemanticTypeCode = COALESCE(@SemanticTypeCode, SemanticTypeCode), BranchRoleCode = COALESCE(@BranchRoleCode, BranchRoleCode),
                       ScopeStatusCode = @ScopeStatusCode, DecidedByCode = 'USER', UserNote = @UserNote,
                       ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
                 WHERE MatterAtomicPropositionId = @Id;
            END
            SELECT @Id;
            """,
            new
            {
                TenantId = tenantId, UserId = userId, DecisionMatterId = decisionMatterId, row.PackCode, row.ParentTargetKindCode, row.ParentTargetCode, row.ParentDisplayName,
                row.DimensionCode, row.PropositionCode, row.LevelNumber, row.DisplayName, row.Statement, row.Confidence, row.SemanticTypeCode, row.BranchRoleCode,
                row.WorkerName, ScopeStatusCode = scopeStatusCode, UserNote = userNote, row.SortOrder
            }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<int> SetMatterAtomicPropositionStatusAsync(Guid tenantId, Guid userId, Guid decisionMatterId, IReadOnlyCollection<Guid> ids, string scopeStatusCode, string? userNote, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return 0;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_MatterAtomicProposition
               SET ScopeStatusCode = @ScopeStatusCode, DecidedByCode = 'USER',
                   UserNote = COALESCE(@UserNote, UserNote),
                   ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
             WHERE TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId AND IsDeleted = 0 AND MatterAtomicPropositionId IN @Ids;
            """,
            new { TenantId = tenantId, UserId = userId, DecisionMatterId = decisionMatterId, Ids = ids.ToArray(), ScopeStatusCode = scopeStatusCode, UserNote = userNote },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> SoftDeleteMatterAtomicPropositionAsync(Guid tenantId, Guid userId, Guid decisionMatterId, Guid matterAtomicPropositionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_MatterAtomicProposition
               SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
             WHERE TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId AND MatterAtomicPropositionId = @Id AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, UserId = userId, DecisionMatterId = decisionMatterId, Id = matterAtomicPropositionId }, cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task UpsertSystemMatterAtomicPropositionsAsync(Guid tenantId, Guid userId, Guid decisionMatterId, IReadOnlyCollection<MatterAtomicPropositionEvaluation> rows, CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0) return;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        const string sql = """
            DECLARE @Id UNIQUEIDENTIFIER = (SELECT TOP(1) MatterAtomicPropositionId FROM POLOXI.Legal_MatterAtomicProposition
                WHERE TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId AND PackCode = @PackCode
                  AND ParentTargetKindCode = @ParentTargetKindCode AND ParentTargetCode = @ParentTargetCode AND PropositionCode = @PropositionCode AND IsDeleted = 0);
            IF @Id IS NULL
                INSERT POLOXI.Legal_MatterAtomicProposition(MatterAtomicPropositionId, TenantId, DecisionMatterId, PackCode, ParentTargetKindCode, ParentTargetCode, ParentDisplayName, DimensionCode,
                    ParentPropositionCode, PropositionCode, LevelNumber, DisplayName, Statement, SearchText, CapabilityCode, Confidence, SemanticTypeCode, BranchRoleCode,
                    ContinueNarrowing, StopReason, DependsOnCodes, EquivalentToCodes, AnchorCount, AnchorsJson,
                    WorkerName, BatchNumber, AttemptNumber, BatchTierCode, ModelCode, ProviderCode, InputTokenCount, OutputTokenCount, DurationMs, ProviderRequestId,
                    ScopeStatusCode, DecidedByCode, GenerationCount, LastWideBranchId, LastEvaluatedExecutionId, LastEvaluatedDateUtc, SortOrder,
                    CreatedDateUtc, CreatedByUserId, IsDeleted)
                VALUES(NEWID(), @TenantId, @DecisionMatterId, @PackCode, @ParentTargetKindCode, @ParentTargetCode, @ParentDisplayName, @DimensionCode,
                    @ParentPropositionCode, @PropositionCode, @LevelNumber, @DisplayName, @Statement, @SearchText, @CapabilityCode, @Confidence, @SemanticTypeCode, @BranchRoleCode,
                    @ContinueNarrowing, @StopReason, @DependsOnCodes, @EquivalentToCodes, @AnchorCount, @AnchorsJson,
                    @WorkerName, @BatchNumber, @AttemptNumber, @BatchTierCode, @ModelCode, @ProviderCode, @InputTokenCount, @OutputTokenCount, @DurationMs, @ProviderRequestId,
                    'PENDING_REVIEW', 'LLM', 1, @WideBranchId, @WideExecutionId, SYSUTCDATETIME(), @SortOrder,
                    SYSUTCDATETIME(), @UserId, 0);
            ELSE
                UPDATE POLOXI.Legal_MatterAtomicProposition
                   SET ParentDisplayName = @ParentDisplayName, DimensionCode = @DimensionCode, ParentPropositionCode = @ParentPropositionCode, LevelNumber = @LevelNumber,
                       -- USER rows are authoritative: keep the attorney's title/statement/status/note; refresh model provenance only.
                       DisplayName = CASE WHEN DecidedByCode = 'USER' THEN DisplayName ELSE @DisplayName END,
                       Statement   = CASE WHEN DecidedByCode = 'USER' THEN Statement   ELSE @Statement   END,
                       ScopeStatusCode = CASE WHEN DecidedByCode = 'USER' THEN ScopeStatusCode ELSE ScopeStatusCode END,
                       SearchText = @SearchText, CapabilityCode = @CapabilityCode, Confidence = @Confidence, SemanticTypeCode = @SemanticTypeCode, BranchRoleCode = @BranchRoleCode,
                       ContinueNarrowing = @ContinueNarrowing, StopReason = @StopReason, DependsOnCodes = @DependsOnCodes, EquivalentToCodes = @EquivalentToCodes,
                       AnchorCount = @AnchorCount, AnchorsJson = @AnchorsJson,
                       WorkerName = @WorkerName, BatchNumber = @BatchNumber, AttemptNumber = @AttemptNumber, BatchTierCode = @BatchTierCode,
                       ModelCode = @ModelCode, ProviderCode = @ProviderCode, InputTokenCount = @InputTokenCount, OutputTokenCount = @OutputTokenCount, DurationMs = @DurationMs, ProviderRequestId = @ProviderRequestId,
                       GenerationCount = GenerationCount + 1, LastWideBranchId = @WideBranchId, LastEvaluatedExecutionId = @WideExecutionId, LastEvaluatedDateUtc = SYSUTCDATETIME(),
                       SortOrder = @SortOrder, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
                 WHERE MatterAtomicPropositionId = @Id;
            """;
        foreach (var row in rows)
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                TenantId = tenantId, UserId = userId, DecisionMatterId = decisionMatterId,
                row.PackCode, row.ParentTargetKindCode, row.ParentTargetCode, row.ParentDisplayName, row.DimensionCode, row.ParentPropositionCode,
                row.PropositionCode, row.LevelNumber, row.DisplayName, row.Statement, row.SearchText, row.CapabilityCode, row.Confidence, row.SemanticTypeCode, row.BranchRoleCode,
                row.ContinueNarrowing, row.StopReason, row.DependsOnCodes, row.EquivalentToCodes, row.AnchorCount, row.AnchorsJson,
                row.WorkerName, row.BatchNumber, row.AttemptNumber, row.BatchTierCode, row.ModelCode, row.ProviderCode, row.InputTokenCount, row.OutputTokenCount, row.DurationMs, row.ProviderRequestId,
                row.WideBranchId, row.WideExecutionId, row.SortOrder
            }, transaction, cancellationToken: cancellationToken));
        }
        transaction.Commit();
    }
}
