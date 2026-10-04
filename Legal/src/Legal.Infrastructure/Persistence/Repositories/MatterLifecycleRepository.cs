using System.Data;
using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.MatterLifecycle;

namespace Legal.Infrastructure.Persistence.Repositories;

// Dapper repository for Judz Matter Lifecycle (POLOXI.Legal_Matter* tables, migrations 0385/0386).
// Tenant-scoped throughout. Lifecycle is operational stage context only — it never reads or writes
// POLOXI Core decision state. History is append-only and never destroyed.
public sealed class MatterLifecycleRepository(ISqlConnectionFactory connectionFactory) : IMatterLifecycleRepository
{
    public async Task<MatterLifecycleSnapshotDto?> GetSnapshotAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await BuildSnapshotAsync(connection, null, tenantId, decisionMatterId, cancellationToken);
    }

    public async Task<Guid> EnsureLifecycleAsync(Guid tenantId, Guid userId, EnsureMatterLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var existing = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT TOP 1 MatterLifecycleId FROM POLOXI.Legal_MatterLifecycle
            WHERE DecisionMatterId = @MatterId AND TenantId = @TenantId AND IsPrimary = 1 AND IsDeleted = 0;
            """,
            new { MatterId = request.DecisionMatterId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (existing is { } found && found != Guid.Empty)
            return found;

        // Resolve the lifecycle version: explicit code, else the matter-type default, else any default.
        var matterTypeCode = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            """
            SELECT TOP 1 MatterTypeCode FROM POLOXI.Legal_DecisionMatter
            WHERE DecisionMatterId = @MatterId AND TenantId = @TenantId AND IsDeleted = 0;
            """,
            new { MatterId = request.DecisionMatterId, TenantId = tenantId }, cancellationToken: cancellationToken));

        var version = await connection.QuerySingleOrDefaultAsync<VersionRow>(new CommandDefinition(
            """
            SELECT TOP 1 ver.MatterLifecycleVersionId, ver.MatterLifecycleDefinitionId
            FROM POLOXI.Legal_MatterLifecycleVersion ver
            JOIN POLOXI.Legal_MatterLifecycleDefinition def ON def.MatterLifecycleDefinitionId = ver.MatterLifecycleDefinitionId AND def.IsDeleted = 0 AND def.IsActive = 1
            WHERE ver.IsDeleted = 0 AND ver.StatusCode = N'PUBLISHED'
              AND (@Code IS NULL OR def.Code = @Code)
              AND (@MatterType IS NULL OR def.MatterTypeCode IS NULL
                   OR UPPER(REPLACE(REPLACE(def.MatterTypeCode, N' ', N'_'), N'-', N'_')) = UPPER(REPLACE(REPLACE(@MatterType, N' ', N'_'), N'-', N'_')))
              AND (def.TenantId = @TenantId OR def.TenantId IS NULL)
            ORDER BY
                CASE WHEN @Code IS NOT NULL AND def.Code = @Code THEN 0 ELSE 1 END,
                CASE WHEN UPPER(REPLACE(REPLACE(def.MatterTypeCode, N' ', N'_'), N'-', N'_')) = UPPER(REPLACE(REPLACE(@MatterType, N' ', N'_'), N'-', N'_')) THEN 0 ELSE 1 END,
                def.IsDefault DESC,
                CASE WHEN def.TenantId = @TenantId THEN 0 ELSE 1 END,
                ver.VersionNumber DESC;
            """,
            new { Code = request.LifecycleDefinitionCode, MatterType = matterTypeCode, TenantId = tenantId },
            cancellationToken: cancellationToken));
        if (version is null)
            throw new InvalidOperationException("No published lifecycle definition is available for this matter.");

        var initialStageId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT TOP 1 MatterStageDefinitionId FROM POLOXI.Legal_MatterStageDefinition
            WHERE MatterLifecycleVersionId = @VersionId AND IsInitial = 1 AND IsActive = 1 AND IsDeleted = 0
            ORDER BY DisplayOrder;
            """,
            new { VersionId = version.MatterLifecycleVersionId }, cancellationToken: cancellationToken));
        if (initialStageId is not { } startStageId || startStageId == Guid.Empty)
            throw new InvalidOperationException("The resolved lifecycle version has no initial stage.");

        using var tx = connection.BeginTransaction();
        try
        {
            var lifecycleId = Guid.NewGuid();
            var nowUser = userId == Guid.Empty ? (Guid?)null : userId;
            var authority = string.IsNullOrWhiteSpace(request.AuthorityMode) ? "JUDZ_AUTHORITATIVE" : request.AuthorityMode;

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_MatterLifecycle
                    (MatterLifecycleId, DecisionMatterId, MatterLifecycleVersionId, CurrentStageDefinitionId, StatusCode,
                     AuthorityMode, ExternalSystemId, ExternalStageCode, StartedUtc, CurrentStageEnteredUtc, IsPrimary, TenantId, CreatedByUserId)
                VALUES
                    (@LifecycleId, @MatterId, @VersionId, @StageId, N'ACTIVE',
                     @Authority, @ExternalSystemId, @ExternalStageCode, SYSUTCDATETIME(), SYSUTCDATETIME(), 1, @TenantId, @UserId);
                """,
                new
                {
                    LifecycleId = lifecycleId,
                    MatterId = request.DecisionMatterId,
                    VersionId = version.MatterLifecycleVersionId,
                    StageId = startStageId,
                    Authority = authority,
                    request.ExternalSystemId,
                    request.ExternalStageCode,
                    TenantId = tenantId,
                    UserId = nowUser
                }, tx, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_MatterStageHistory
                    (MatterStageHistoryId, MatterLifecycleId, MatterStageDefinitionId, EnteredUtc, EntryReasonCode, ChangedByType, ChangedByUserId, TenantId, CreatedByUserId)
                VALUES
                    (NEWID(), @LifecycleId, @StageId, SYSUTCDATETIME(), N'LIFECYCLE_STARTED', N'USER', @UserId, @TenantId, @UserId);
                """,
                new { LifecycleId = lifecycleId, StageId = startStageId, TenantId = tenantId, UserId = nowUser }, tx, cancellationToken: cancellationToken));

            await AppendEventAsync(connection, tx, tenantId, nowUser, lifecycleId, startStageId, "STAGE_ENTERED", "Lifecycle started", null, cancellationToken);
            await SeedRequirementInstancesAsync(connection, tx, tenantId, nowUser, lifecycleId, startStageId, cancellationToken);

            tx.Commit();
            return lifecycleId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> PerformTransitionAsync(Guid tenantId, Guid userId, PerformMatterLifecycleTransitionRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var lifecycle = await connection.QuerySingleOrDefaultAsync<LifecycleRow>(new CommandDefinition(
            """
            SELECT TOP 1 MatterLifecycleId, MatterLifecycleVersionId, CurrentStageDefinitionId
            FROM POLOXI.Legal_MatterLifecycle
            WHERE DecisionMatterId = @MatterId AND TenantId = @TenantId AND IsPrimary = 1 AND IsDeleted = 0;
            """,
            new { MatterId = request.DecisionMatterId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (lifecycle is null)
            return false;

        // The transition MUST be an active directed edge FROM the current stage (never inferred).
        var transition = await connection.QuerySingleOrDefaultAsync<TransitionRow>(new CommandDefinition(
            """
            SELECT TOP 1 MatterStageTransitionDefinitionId, FromStageDefinitionId, ToStageDefinitionId, Code
            FROM POLOXI.Legal_MatterStageTransitionDefinition
            WHERE MatterStageTransitionDefinitionId = @TransitionId
              AND MatterLifecycleVersionId = @VersionId
              AND FromStageDefinitionId = @CurrentStageId
              AND IsActive = 1 AND IsDeleted = 0;
            """,
            new
            {
                TransitionId = request.TransitionDefinitionId,
                VersionId = lifecycle.MatterLifecycleVersionId,
                CurrentStageId = lifecycle.CurrentStageDefinitionId
            }, cancellationToken: cancellationToken));
        if (transition is null)
            throw new InvalidMatterLifecycleTransitionException(
                "The requested transition is not available from the matter's current stage.");

        using var tx = connection.BeginTransaction();
        try
        {
            var nowUser = userId == Guid.Empty ? (Guid?)null : userId;

            // Close the open history row for the current stage.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE POLOXI.Legal_MatterStageHistory
                SET ExitedUtc = SYSUTCDATETIME(), ExitReasonCode = @Code, TransitionDefinitionId = @TransitionId,
                    ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
                WHERE MatterLifecycleId = @LifecycleId AND MatterStageDefinitionId = @FromStageId
                  AND ExitedUtc IS NULL AND IsDeleted = 0;
                """,
                new
                {
                    Code = transition.Code,
                    TransitionId = transition.MatterStageTransitionDefinitionId,
                    LifecycleId = lifecycle.MatterLifecycleId,
                    FromStageId = transition.FromStageDefinitionId,
                    UserId = nowUser
                }, tx, cancellationToken: cancellationToken));

            await AppendEventAsync(connection, tx, tenantId, nowUser, lifecycle.MatterLifecycleId, transition.FromStageDefinitionId, "STAGE_EXITED", "Stage exited", request.Notes, cancellationToken);

            // Open a new immutable history row for the target stage.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_MatterStageHistory
                    (MatterStageHistoryId, MatterLifecycleId, MatterStageDefinitionId, EnteredUtc, EntryReasonCode, TransitionDefinitionId, ChangedByType, ChangedByUserId, Notes, TenantId, CreatedByUserId)
                VALUES
                    (NEWID(), @LifecycleId, @ToStageId, SYSUTCDATETIME(), @Code, @TransitionId, N'USER', @UserId, @Notes, @TenantId, @UserId);
                """,
                new
                {
                    LifecycleId = lifecycle.MatterLifecycleId,
                    ToStageId = transition.ToStageDefinitionId,
                    Code = transition.Code,
                    TransitionId = transition.MatterStageTransitionDefinitionId,
                    UserId = nowUser,
                    request.Notes,
                    TenantId = tenantId
                }, tx, cancellationToken: cancellationToken));

            await AppendEventAsync(connection, tx, tenantId, nowUser, lifecycle.MatterLifecycleId, transition.ToStageDefinitionId, "STAGE_ENTERED", "Stage entered", request.Notes, cancellationToken);
            await SeedRequirementInstancesAsync(connection, tx, tenantId, nowUser, lifecycle.MatterLifecycleId, transition.ToStageDefinitionId, cancellationToken);

            // Update the lifecycle's current stage. CLOSED (terminal) completes the lifecycle.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE lc
                SET lc.CurrentStageDefinitionId = @ToStageId,
                    lc.CurrentStageEnteredUtc = SYSUTCDATETIME(),
                    lc.StatusCode = CASE WHEN stg.IsTerminal = 1 THEN N'COMPLETED' ELSE lc.StatusCode END,
                    lc.CompletedUtc = CASE WHEN stg.IsTerminal = 1 THEN SYSUTCDATETIME() ELSE lc.CompletedUtc END,
                    lc.ModifiedDateUtc = SYSUTCDATETIME(),
                    lc.ModifiedByUserId = @UserId
                FROM POLOXI.Legal_MatterLifecycle lc
                JOIN POLOXI.Legal_MatterStageDefinition stg ON stg.MatterStageDefinitionId = @ToStageId
                WHERE lc.MatterLifecycleId = @LifecycleId AND lc.TenantId = @TenantId AND lc.IsDeleted = 0;
                """,
                new { ToStageId = transition.ToStageDefinitionId, LifecycleId = lifecycle.MatterLifecycleId, TenantId = tenantId, UserId = nowUser },
                tx, cancellationToken: cancellationToken));

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ── Snapshot composition ────────────────────────────────────────────────
    private static async Task<MatterLifecycleSnapshotDto?> BuildSnapshotAsync(
        IDbConnection connection, IDbTransaction? tx, Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken)
    {
        var header = await connection.QuerySingleOrDefaultAsync<SnapshotHeaderRow>(new CommandDefinition(
            """
            SELECT TOP 1
                lc.MatterLifecycleId, lc.DecisionMatterId, lc.MatterLifecycleVersionId, lc.StatusCode, lc.AuthorityMode,
                lc.CurrentStageDefinitionId, lc.CurrentStageEnteredUtc,
                def.Code AS LifecycleDefinitionCode, def.Name AS LifecycleDefinitionName, ver.VersionNumber,
                cur.Code AS CurrentStageCode, cur.Name AS CurrentStageName
            FROM POLOXI.Legal_MatterLifecycle lc
            JOIN POLOXI.Legal_MatterLifecycleVersion ver ON ver.MatterLifecycleVersionId = lc.MatterLifecycleVersionId
            JOIN POLOXI.Legal_MatterLifecycleDefinition def ON def.MatterLifecycleDefinitionId = ver.MatterLifecycleDefinitionId
            JOIN POLOXI.Legal_MatterStageDefinition cur ON cur.MatterStageDefinitionId = lc.CurrentStageDefinitionId
            WHERE lc.DecisionMatterId = @MatterId AND lc.TenantId = @TenantId AND lc.IsPrimary = 1 AND lc.IsDeleted = 0;
            """,
            new { MatterId = decisionMatterId, TenantId = tenantId }, tx, cancellationToken: cancellationToken));
        if (header is null)
            return null;

        var stages = (await connection.QueryAsync<MatterLifecycleStageDto>(new CommandDefinition(
            """
            SELECT MatterStageDefinitionId AS StageDefinitionId, Code, Name, StageCategory, DisplayOrder, IsInitial, IsTerminal, AllowReentry, DefaultSlaDays
            FROM POLOXI.Legal_MatterStageDefinition
            WHERE MatterLifecycleVersionId = @VersionId AND IsActive = 1 AND IsDeleted = 0
            ORDER BY DisplayOrder;
            """,
            new { VersionId = header.MatterLifecycleVersionId }, tx, cancellationToken: cancellationToken))).ToList();

        var history = (await connection.QueryAsync<MatterLifecycleHistoryDto>(new CommandDefinition(
            """
            SELECT h.MatterStageHistoryId AS HistoryId, h.MatterStageDefinitionId AS StageDefinitionId,
                   stg.Code AS StageCode, stg.Name AS StageName, h.EnteredUtc, h.ExitedUtc,
                   h.EntryReasonCode, h.ExitReasonCode, h.ChangedByType
            FROM POLOXI.Legal_MatterStageHistory h
            JOIN POLOXI.Legal_MatterStageDefinition stg ON stg.MatterStageDefinitionId = h.MatterStageDefinitionId
            WHERE h.MatterLifecycleId = @LifecycleId AND h.IsDeleted = 0
            ORDER BY h.EnteredUtc DESC;
            """,
            new { LifecycleId = header.MatterLifecycleId }, tx, cancellationToken: cancellationToken))).ToList();

        var requirements = (await connection.QueryAsync<MatterLifecycleRequirementDto>(new CommandDefinition(
            """
            SELECT rd.MatterStageRequirementDefinitionId AS RequirementDefinitionId, rd.Code, rd.Name,
                   rd.RequirementType, rd.RequirementLevel, rd.IsBlocking,
                   ISNULL(ri.StatusCode, N'NOT_STARTED') AS StatusCode,
                   CAST(CASE WHEN ri.StatusCode IN (N'SATISFIED', N'WAIVED', N'NOT_APPLICABLE') THEN 1 ELSE 0 END AS BIT) AS IsSatisfied
            FROM POLOXI.Legal_MatterStageRequirementDefinition rd
            LEFT JOIN POLOXI.Legal_MatterStageRequirementInstance ri
                   ON ri.MatterStageRequirementDefinitionId = rd.MatterStageRequirementDefinitionId
                  AND ri.MatterLifecycleId = @LifecycleId AND ri.IsDeleted = 0
            WHERE rd.MatterStageDefinitionId = @CurrentStageId AND rd.IsActive = 1 AND rd.IsDeleted = 0
            ORDER BY rd.DisplayOrder;
            """,
            new { LifecycleId = header.MatterLifecycleId, CurrentStageId = header.CurrentStageDefinitionId }, tx, cancellationToken: cancellationToken))).ToList();

        var transitions = (await connection.QueryAsync<MatterLifecycleTransitionDto>(new CommandDefinition(
            """
            SELECT t.MatterStageTransitionDefinitionId AS TransitionDefinitionId, t.FromStageDefinitionId, t.ToStageDefinitionId,
                   dst.Code AS ToStageCode, dst.Name AS ToStageName, t.Code, t.Name, t.TransitionType, t.RequiresApproval, t.Priority
            FROM POLOXI.Legal_MatterStageTransitionDefinition t
            JOIN POLOXI.Legal_MatterStageDefinition dst ON dst.MatterStageDefinitionId = t.ToStageDefinitionId
            WHERE t.MatterLifecycleVersionId = @VersionId AND t.FromStageDefinitionId = @CurrentStageId
              AND t.IsActive = 1 AND t.IsDeleted = 0
            ORDER BY t.Priority, dst.DisplayOrder;
            """,
            new { VersionId = header.MatterLifecycleVersionId, CurrentStageId = header.CurrentStageDefinitionId }, tx, cancellationToken: cancellationToken))).ToList();

        var events = (await connection.QueryAsync<MatterLifecycleEventDto>(new CommandDefinition(
            """
            SELECT TOP 25 e.MatterStageEventId AS EventId, e.EventType, e.OccurredUtc, e.Title, e.Description, stg.Code AS StageCode
            FROM POLOXI.Legal_MatterStageEvent e
            LEFT JOIN POLOXI.Legal_MatterStageDefinition stg ON stg.MatterStageDefinitionId = e.MatterStageDefinitionId
            WHERE e.MatterLifecycleId = @LifecycleId AND e.IsDeleted = 0
            ORDER BY e.OccurredUtc DESC;
            """,
            new { LifecycleId = header.MatterLifecycleId }, tx, cancellationToken: cancellationToken))).ToList();

        return new MatterLifecycleSnapshotDto(
            header.MatterLifecycleId, header.DecisionMatterId, header.MatterLifecycleVersionId,
            header.LifecycleDefinitionCode, header.LifecycleDefinitionName, header.VersionNumber,
            header.StatusCode, header.AuthorityMode,
            header.CurrentStageDefinitionId, header.CurrentStageCode, header.CurrentStageName, header.CurrentStageEnteredUtc,
            stages, history, requirements, transitions, events);
    }

    private static async Task AppendEventAsync(
        IDbConnection connection, IDbTransaction tx, Guid tenantId, Guid? userId, Guid lifecycleId, Guid stageId,
        string eventType, string title, string? description, CancellationToken cancellationToken)
        => await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_MatterStageEvent
                (MatterStageEventId, MatterLifecycleId, MatterStageDefinitionId, EventType, OccurredUtc, Title, Description, TenantId, CreatedByUserId)
            VALUES
                (NEWID(), @LifecycleId, @StageId, @EventType, SYSUTCDATETIME(), @Title, @Description, @TenantId, @UserId);
            """,
            new { LifecycleId = lifecycleId, StageId = stageId, EventType = eventType, Title = title, Description = description, TenantId = tenantId, UserId = userId },
            tx, cancellationToken: cancellationToken));

    // Materializes NOT_STARTED requirement instances for a stage so the UI/ARL can track satisfaction.
    private static async Task SeedRequirementInstancesAsync(
        IDbConnection connection, IDbTransaction tx, Guid tenantId, Guid? userId, Guid lifecycleId, Guid stageId, CancellationToken cancellationToken)
        => await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_MatterStageRequirementInstance
                (MatterStageRequirementInstanceId, MatterLifecycleId, MatterStageRequirementDefinitionId, StatusCode, TenantId, CreatedByUserId)
            SELECT NEWID(), @LifecycleId, rd.MatterStageRequirementDefinitionId, N'NOT_STARTED', @TenantId, @UserId
            FROM POLOXI.Legal_MatterStageRequirementDefinition rd
            WHERE rd.MatterStageDefinitionId = @StageId AND rd.IsActive = 1 AND rd.IsDeleted = 0
              AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterStageRequirementInstance ri
                              WHERE ri.MatterLifecycleId = @LifecycleId
                                AND ri.MatterStageRequirementDefinitionId = rd.MatterStageRequirementDefinitionId
                                AND ri.IsDeleted = 0);
            """,
            new { LifecycleId = lifecycleId, StageId = stageId, TenantId = tenantId, UserId = userId },
            tx, cancellationToken: cancellationToken));

    // ── Configuration admin (DB-backed lifecycle authoring) ──────────────────
    public async Task<IReadOnlyList<MatterLifecycleDefinitionDto>> GetDefinitionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<MatterLifecycleDefinitionDto>(new CommandDefinition(
            """
            SELECT def.MatterLifecycleDefinitionId, def.Code, def.Name, def.Description, def.MatterTypeCode, def.JurisdictionCode,
                   def.IsDefault, def.IsActive, def.SortOrder,
                   (SELECT COUNT(1) FROM POLOXI.Legal_MatterLifecycleVersion v WHERE v.MatterLifecycleDefinitionId = def.MatterLifecycleDefinitionId AND v.IsDeleted = 0) AS VersionCount,
                   (SELECT COUNT(1) FROM POLOXI.Legal_MatterLifecycleVersion v WHERE v.MatterLifecycleDefinitionId = def.MatterLifecycleDefinitionId AND v.IsDeleted = 0 AND v.StatusCode = N'PUBLISHED') AS PublishedVersionCount
            FROM POLOXI.Legal_MatterLifecycleDefinition def
            WHERE def.IsDeleted = 0 AND (def.TenantId = @TenantId OR def.TenantId IS NULL)
            ORDER BY def.SortOrder, def.Name;
            """,
            new { TenantId = tenantId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<MatterLifecycleDefinitionDetailDto?> GetDefinitionDetailAsync(Guid tenantId, Guid definitionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var definition = await connection.QuerySingleOrDefaultAsync<MatterLifecycleDefinitionDto>(new CommandDefinition(
            """
            SELECT def.MatterLifecycleDefinitionId, def.Code, def.Name, def.Description, def.MatterTypeCode, def.JurisdictionCode,
                   def.IsDefault, def.IsActive, def.SortOrder,
                   (SELECT COUNT(1) FROM POLOXI.Legal_MatterLifecycleVersion v WHERE v.MatterLifecycleDefinitionId = def.MatterLifecycleDefinitionId AND v.IsDeleted = 0) AS VersionCount,
                   (SELECT COUNT(1) FROM POLOXI.Legal_MatterLifecycleVersion v WHERE v.MatterLifecycleDefinitionId = def.MatterLifecycleDefinitionId AND v.IsDeleted = 0 AND v.StatusCode = N'PUBLISHED') AS PublishedVersionCount
            FROM POLOXI.Legal_MatterLifecycleDefinition def
            WHERE def.MatterLifecycleDefinitionId = @DefinitionId AND def.IsDeleted = 0
              AND (def.TenantId = @TenantId OR def.TenantId IS NULL);
            """,
            new { DefinitionId = definitionId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (definition is null)
            return null;

        var versions = (await connection.QueryAsync<MatterLifecycleVersionDto>(new CommandDefinition(
            """
            SELECT v.MatterLifecycleVersionId, v.MatterLifecycleDefinitionId, v.VersionNumber, v.VersionLabel, v.StatusCode, v.PublishedUtc,
                   (SELECT COUNT(1) FROM POLOXI.Legal_MatterStageDefinition s WHERE s.MatterLifecycleVersionId = v.MatterLifecycleVersionId AND s.IsDeleted = 0) AS StageCount,
                   (SELECT COUNT(1) FROM POLOXI.Legal_MatterStageTransitionDefinition t WHERE t.MatterLifecycleVersionId = v.MatterLifecycleVersionId AND t.IsDeleted = 0) AS TransitionCount
            FROM POLOXI.Legal_MatterLifecycleVersion v
            WHERE v.MatterLifecycleDefinitionId = @DefinitionId AND v.IsDeleted = 0
            ORDER BY v.VersionNumber DESC;
            """,
            new { DefinitionId = definitionId }, cancellationToken: cancellationToken))).ToList();

        var versionIds = versions.Select(v => v.MatterLifecycleVersionId).ToArray();

        var stages = (await connection.QueryAsync<MatterLifecycleStageDto>(new CommandDefinition(
            """
            SELECT MatterStageDefinitionId AS StageDefinitionId, Code, Name, StageCategory, DisplayOrder, IsInitial, IsTerminal, AllowReentry, DefaultSlaDays
            FROM POLOXI.Legal_MatterStageDefinition
            WHERE MatterLifecycleVersionId IN @VersionIds AND IsDeleted = 0
            ORDER BY DisplayOrder;
            """,
            new { VersionIds = versionIds.Length == 0 ? new[] { Guid.Empty } : versionIds }, cancellationToken: cancellationToken))).ToList();

        var transitions = (await connection.QueryAsync<MatterLifecycleTransitionDto>(new CommandDefinition(
            """
            SELECT t.MatterStageTransitionDefinitionId AS TransitionDefinitionId, t.FromStageDefinitionId, t.ToStageDefinitionId,
                   dst.Code AS ToStageCode, dst.Name AS ToStageName, t.Code, t.Name, t.TransitionType, t.RequiresApproval, t.Priority
            FROM POLOXI.Legal_MatterStageTransitionDefinition t
            JOIN POLOXI.Legal_MatterStageDefinition dst ON dst.MatterStageDefinitionId = t.ToStageDefinitionId
            WHERE t.MatterLifecycleVersionId IN @VersionIds AND t.IsDeleted = 0
            ORDER BY t.Priority;
            """,
            new { VersionIds = versionIds.Length == 0 ? new[] { Guid.Empty } : versionIds }, cancellationToken: cancellationToken))).ToList();

        return new MatterLifecycleDefinitionDetailDto(definition, versions, stages, transitions);
    }

    public async Task<Guid> SaveDefinitionAsync(Guid tenantId, Guid userId, SaveMatterLifecycleDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var nowUser = userId == Guid.Empty ? (Guid?)null : userId;

        if (request.MatterLifecycleDefinitionId is { } id && id != Guid.Empty)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE POLOXI.Legal_MatterLifecycleDefinition
                SET Name = @Name, Description = @Description, MatterTypeCode = @MatterTypeCode, JurisdictionCode = @JurisdictionCode,
                    IsDefault = @IsDefault, IsActive = @IsActive, SortOrder = @SortOrder,
                    ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
                WHERE MatterLifecycleDefinitionId = @Id AND IsDeleted = 0;
                """,
                new
                {
                    Id = id, request.Name, request.Description, request.MatterTypeCode, request.JurisdictionCode,
                    request.IsDefault, request.IsActive, request.SortOrder, UserId = nowUser
                }, cancellationToken: cancellationToken));
            return id;
        }

        var packId = string.IsNullOrWhiteSpace(request.DomainPackCode)
            ? (Guid?)null
            : await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                """
                SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
                WHERE PackCode = @PackCode AND IsDeleted = 0 AND (TenantId = @TenantId OR TenantId IS NULL)
                ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END;
                """,
                new { request.DomainPackCode, TenantId = tenantId }, cancellationToken: cancellationToken));

        var newId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_MatterLifecycleDefinition
                (MatterLifecycleDefinitionId, DecisionDomainPackId, Code, Name, Description, MatterTypeCode, JurisdictionCode, IsDefault, IsActive, SortOrder, TenantId, CreatedByUserId)
            VALUES
                (@Id, @PackId, @Code, @Name, @Description, @MatterTypeCode, @JurisdictionCode, @IsDefault, @IsActive, @SortOrder, @TenantId, @UserId);
            """,
            new
            {
                Id = newId, PackId = packId, request.Code, request.Name, request.Description, request.MatterTypeCode,
                request.JurisdictionCode, request.IsDefault, request.IsActive, request.SortOrder, TenantId = tenantId, UserId = nowUser
            }, cancellationToken: cancellationToken));
        return newId;
    }

    public async Task<Guid> SaveStageAsync(Guid tenantId, Guid userId, SaveMatterLifecycleStageRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var nowUser = userId == Guid.Empty ? (Guid?)null : userId;

        if (request.MatterStageDefinitionId is { } id && id != Guid.Empty)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE POLOXI.Legal_MatterStageDefinition
                SET Name = @Name, Description = @Description, StageCategory = @StageCategory, DisplayOrder = @DisplayOrder,
                    IsInitial = @IsInitial, IsTerminal = @IsTerminal, AllowReentry = @AllowReentry, DefaultSlaDays = @DefaultSlaDays,
                    IsActive = @IsActive, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
                WHERE MatterStageDefinitionId = @Id AND IsDeleted = 0;
                """,
                new
                {
                    Id = id, request.Name, request.Description, request.StageCategory, request.DisplayOrder,
                    request.IsInitial, request.IsTerminal, request.AllowReentry, request.DefaultSlaDays, request.IsActive, UserId = nowUser
                }, cancellationToken: cancellationToken));
            return id;
        }

        var newId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_MatterStageDefinition
                (MatterStageDefinitionId, MatterLifecycleVersionId, Code, Name, Description, StageCategory, DisplayOrder, IsInitial, IsTerminal, AllowReentry, DefaultSlaDays, IsActive, TenantId, CreatedByUserId)
            VALUES
                (@Id, @VersionId, @Code, @Name, @Description, @StageCategory, @DisplayOrder, @IsInitial, @IsTerminal, @AllowReentry, @DefaultSlaDays, @IsActive, @TenantId, @UserId);
            """,
            new
            {
                Id = newId, VersionId = request.MatterLifecycleVersionId, request.Code, request.Name, request.Description,
                request.StageCategory, request.DisplayOrder, request.IsInitial, request.IsTerminal, request.AllowReentry,
                request.DefaultSlaDays, request.IsActive, TenantId = tenantId, UserId = nowUser
            }, cancellationToken: cancellationToken));
        return newId;
    }

    public async Task<Guid> SaveTransitionAsync(Guid tenantId, Guid userId, SaveMatterLifecycleTransitionRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var nowUser = userId == Guid.Empty ? (Guid?)null : userId;

        if (request.MatterStageTransitionDefinitionId is { } id && id != Guid.Empty)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE POLOXI.Legal_MatterStageTransitionDefinition
                SET FromStageDefinitionId = @FromStageId, ToStageDefinitionId = @ToStageId, Name = @Name,
                    TransitionType = @TransitionType, RequiresApproval = @RequiresApproval, Priority = @Priority,
                    IsActive = @IsActive, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
                WHERE MatterStageTransitionDefinitionId = @Id AND IsDeleted = 0;
                """,
                new
                {
                    Id = id, FromStageId = request.FromStageDefinitionId, ToStageId = request.ToStageDefinitionId, request.Name,
                    request.TransitionType, request.RequiresApproval, request.Priority, request.IsActive, UserId = nowUser
                }, cancellationToken: cancellationToken));
            return id;
        }

        var newId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_MatterStageTransitionDefinition
                (MatterStageTransitionDefinitionId, MatterLifecycleVersionId, FromStageDefinitionId, ToStageDefinitionId, Code, Name, TransitionType, RequiresApproval, Priority, IsActive, TenantId, CreatedByUserId)
            VALUES
                (@Id, @VersionId, @FromStageId, @ToStageId, @Code, @Name, @TransitionType, @RequiresApproval, @Priority, @IsActive, @TenantId, @UserId);
            """,
            new
            {
                Id = newId, VersionId = request.MatterLifecycleVersionId, FromStageId = request.FromStageDefinitionId,
                ToStageId = request.ToStageDefinitionId, request.Code, request.Name, request.TransitionType,
                request.RequiresApproval, request.Priority, request.IsActive, TenantId = tenantId, UserId = nowUser
            }, cancellationToken: cancellationToken));
        return newId;
    }

    // ── Row projections ─────────────────────────────────────────────────────
    private sealed record VersionRow(Guid MatterLifecycleVersionId, Guid MatterLifecycleDefinitionId);
    private sealed record LifecycleRow(Guid MatterLifecycleId, Guid MatterLifecycleVersionId, Guid CurrentStageDefinitionId);
    private sealed record TransitionRow(Guid MatterStageTransitionDefinitionId, Guid FromStageDefinitionId, Guid ToStageDefinitionId, string Code);
    private sealed record SnapshotHeaderRow(
        Guid MatterLifecycleId, Guid DecisionMatterId, Guid MatterLifecycleVersionId, string StatusCode, string AuthorityMode,
        Guid CurrentStageDefinitionId, DateTime CurrentStageEnteredUtc,
        string LifecycleDefinitionCode, string LifecycleDefinitionName, int VersionNumber,
        string CurrentStageCode, string CurrentStageName);
}
