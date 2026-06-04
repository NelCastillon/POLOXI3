using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Audit;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AuditRepository : IAuditRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AuditRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // ── CRUD history (AuditLog) ──────────────────────────────

    public async Task<AuditLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT AuditLogId, TenantId, EntityName, EntityId, EventTypeCode, ActionName, PerformedByUserId, PerformedDateUtc FROM Audit.AuditLog WHERE AuditLogId = @Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AuditLogDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AuditLogDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "Audit.AuditLog",
            "AuditLogId, TenantId, EntityName, EntityId, EventTypeCode, ActionName, PerformedByUserId, PerformedDateUtc",
            "EntityName LIKE '%' + @SearchTerm + '%' OR ActionName LIKE '%' + @SearchTerm + '%'",
            "PerformedDateUtc DESC",
            false);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<AuditLogDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<AuditLogDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<AuditLogDto>> GetEntityHistoryAsync(Guid tenantId, string entityName, Guid entityId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT AuditLogId, TenantId, EntityName, EntityId, EventTypeCode, ActionName, PerformedByUserId, PerformedDateUtc
                FROM Audit.AuditLog
                WHERE TenantId = @TenantId AND EntityName = @EntityName AND EntityId = @EntityId
            )
            SELECT * FROM Cte ORDER BY PerformedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Audit.AuditLog
            WHERE TenantId = @TenantId AND EntityName = @EntityName AND EntityId = @EntityId;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, EntityName = entityName, EntityId = entityId, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<AuditLogDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AuditLogDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    // ── Field-level change tracking ──────────────────────────

    public async Task<PagedResult<FieldChangeLogDto>> SearchFieldChangesAsync(Guid tenantId, string? entityName, Guid? entityId, string? fieldName, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT FieldChangeLogId, TenantId, EntityName, EntityId, FieldName,
                       OldValue, NewValue, ChangedByUserId, ChangedDateUtc, ChangeSource, IpAddress
                FROM Audit.FieldChangeLog
                WHERE TenantId = @TenantId AND IsDeleted = 0
                  AND (@EntityName IS NULL OR EntityName = @EntityName)
                  AND (@EntityId   IS NULL OR EntityId   = @EntityId)
                  AND (@FieldName  IS NULL OR FieldName  = @FieldName)
            )
            SELECT * FROM Cte ORDER BY ChangedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Audit.FieldChangeLog
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (@EntityName IS NULL OR EntityName = @EntityName)
              AND (@EntityId   IS NULL OR EntityId   = @EntityId)
              AND (@FieldName  IS NULL OR FieldName  = @FieldName);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, EntityName = entityName, EntityId = entityId, FieldName = fieldName, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<FieldChangeLogDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<FieldChangeLogDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    // ── Approval history ─────────────────────────────────────

    public async Task<PagedResult<WorkflowApprovalHistoryDto>> SearchApprovalHistoryAsync(Guid tenantId, Guid? workflowInstanceId, string? actionCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT Id AS HistoryId, TenantId, WorkflowInstanceId, ApprovalStepId, ActorUserId,
                       ActionCode, Notes, PreviousStatusCode, NewStatusCode, IsDelegated, DelegatedByUserId,
                       ActionDateUtc, CreatedDateUtc
                FROM Audit.WorkflowApprovalHistory
                WHERE TenantId = @TenantId AND IsDeleted = 0
                  AND (@WorkflowInstanceId IS NULL OR WorkflowInstanceId = @WorkflowInstanceId)
                  AND (@ActionCode         IS NULL OR ActionCode         = @ActionCode)
            )
            SELECT * FROM Cte ORDER BY ActionDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Audit.WorkflowApprovalHistory
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (@WorkflowInstanceId IS NULL OR WorkflowInstanceId = @WorkflowInstanceId)
              AND (@ActionCode         IS NULL OR ActionCode         = @ActionCode);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, WorkflowInstanceId = workflowInstanceId, ActionCode = actionCode, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<WorkflowApprovalHistoryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<WorkflowApprovalHistoryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    // ── Security events ──────────────────────────────────────

    public async Task<PagedResult<SecurityEventLogDto>> SearchSecurityEventsAsync(Guid tenantId, string? searchTerm, bool? isSuccess, string? eventTypeCode = null, int? riskScoreMin = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT SecurityEventId, TenantId, UserId, EventTypeCode, EventDescription,
                       IpAddress, UserAgent, IsSuccess, RiskScore, SessionId, CreatedDateUtc
                FROM Audit.SecurityEventLog
                WHERE TenantId = @TenantId AND IsDeleted = 0
                  AND (@IsSuccess      IS NULL OR IsSuccess      =  @IsSuccess)
                  AND (@EventTypeCode  IS NULL OR EventTypeCode  =  @EventTypeCode)
                  AND (@RiskScoreMin   IS NULL OR RiskScore      >= @RiskScoreMin)
                  AND (@SearchTerm IS NULL OR EventTypeCode    LIKE '%' + @SearchTerm + '%'
                                          OR EventDescription LIKE '%' + @SearchTerm + '%'
                                          OR IpAddress        LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Cte ORDER BY CreatedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Audit.SecurityEventLog
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (@IsSuccess      IS NULL OR IsSuccess      =  @IsSuccess)
              AND (@EventTypeCode  IS NULL OR EventTypeCode  =  @EventTypeCode)
              AND (@RiskScoreMin   IS NULL OR RiskScore      >= @RiskScoreMin)
              AND (@SearchTerm IS NULL OR EventTypeCode    LIKE '%' + @SearchTerm + '%'
                                      OR EventDescription LIKE '%' + @SearchTerm + '%'
                                      OR IpAddress        LIKE '%' + @SearchTerm + '%');
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, IsSuccess = isSuccess, EventTypeCode = eventTypeCode, RiskScoreMin = riskScoreMin, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<SecurityEventLogDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<SecurityEventLogDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<SecurityEventSummaryDto> GetSecurityEventSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                COUNT(CASE WHEN RiskScore >= 70                                    THEN 1 END) AS TotalHighSeverity,
                COUNT(CASE WHEN EventTypeCode = 'LOGIN_FAILED'                     THEN 1 END) AS TotalFailedLogins,
                COUNT(CASE WHEN EventTypeCode = 'MFA_RESET'                        THEN 1 END) AS TotalMfaResets,
                COUNT(CASE WHEN EventTypeCode IN ('ROLE_ASSIGNED','ROLE_REMOVED')  THEN 1 END) AS TotalRoleAssignments,
                COUNT(CASE WHEN EventTypeCode = 'IMPERSONATION_STARTED'            THEN 1 END) AS TotalImpersonations,
                COUNT(CASE WHEN EventTypeCode = 'EXPORT'                           THEN 1 END) AS TotalExports,
                COUNT(CASE WHEN CreatedDateUtc >= DATEADD(HOUR,-24,SYSUTCDATETIME()) THEN 1 END) AS Total24h,
                COUNT(1) AS GrandTotal
            FROM Audit.SecurityEventLog
            WHERE TenantId = @TenantId AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<SecurityEventSummaryDto>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SecurityEventTrendDto>> GetSecurityEventTrendAsync(Guid tenantId, int days = 14, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                CAST(CreatedDateUtc AS DATE)                                       AS EventDate,
                COUNT(CASE WHEN EventTypeCode = 'LOGIN_FAILED' THEN 1 END)         AS FailedLoginCount,
                COUNT(CASE WHEN RiskScore >= 70                THEN 1 END)         AS HighSeverityCount,
                COUNT(1)                                                           AS TotalCount
            FROM Audit.SecurityEventLog
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND CreatedDateUtc >= DATEADD(DAY, -@Days, SYSUTCDATETIME())
            GROUP BY CAST(CreatedDateUtc AS DATE)
            ORDER BY EventDate DESC;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await cn.QueryAsync<SecurityEventTrendDto>(
            new CommandDefinition(sql, new { TenantId = tenantId, Days = days }, cancellationToken: cancellationToken));
        return items.AsList();
    }

    // ── Export/download history ──────────────────────────────

    public async Task<PagedResult<ExportLogDto>> SearchExportLogsAsync(Guid tenantId, string? entityName, string? exportTypeCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT ExportLogId, TenantId, EntityName, EntityId, ExportTypeCode,
                       FileName, FormatCode, RecordCount, PerformedByUserId, IpAddress, CreatedDateUtc
                FROM Audit.ExportLog
                WHERE TenantId = @TenantId AND IsDeleted = 0
                  AND (@EntityName     IS NULL OR EntityName     = @EntityName)
                  AND (@ExportTypeCode IS NULL OR ExportTypeCode = @ExportTypeCode)
            )
            SELECT * FROM Cte ORDER BY CreatedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Audit.ExportLog
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (@EntityName     IS NULL OR EntityName     = @EntityName)
              AND (@ExportTypeCode IS NULL OR ExportTypeCode = @ExportTypeCode);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, EntityName = entityName, ExportTypeCode = exportTypeCode, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ExportLogDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ExportLogDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> LogExportAsync(LogExportRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = """
            INSERT INTO Audit.ExportLog (ExportLogId, TenantId, EntityName, EntityId, ExportTypeCode,
                                         FileName, FormatCode, RecordCount, PerformedByUserId, IpAddress, CreatedDateUtc, IsDeleted)
            VALUES (@ExportLogId, @TenantId, @EntityName, @EntityId, @ExportTypeCode,
                    @FileName, @FormatCode, @RecordCount, @PerformedByUserId, @IpAddress, SYSUTCDATETIME(), 0);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ExportLogId = id,
            request.TenantId,
            request.EntityName,
            request.EntityId,
            request.ExportTypeCode,
            request.FileName,
            request.FormatCode,
            request.RecordCount,
            request.PerformedByUserId,
            request.IpAddress
        }, cancellationToken: cancellationToken));
        return id;
    }

    // ── Full record timeline ─────────────────────────────────

    public async Task<IReadOnlyList<RecordTimelineEntryDto>> GetRecordTimelineAsync(Guid tenantId, string entityName, Guid entityId, int top = 100, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP(@Top) * FROM (
                SELECT AuditLogId AS EntryId, 'Audit' AS SourceType, EntityName, EntityId,
                       ActionName + ' (' + EventTypeCode + ')' AS Summary, NULL AS Detail,
                       PerformedByUserId, PerformedDateUtc AS OccurredDateUtc
                FROM Audit.AuditLog
                WHERE TenantId = @TenantId AND EntityName = @EntityName AND EntityId = @EntityId

                UNION ALL

                SELECT FieldChangeLogId AS EntryId, 'FieldChange' AS SourceType, EntityName, EntityId,
                       'Field changed: ' + FieldName AS Summary,
                       'Old: ' + ISNULL(OldValue,'(null)') + ' → New: ' + ISNULL(NewValue,'(null)') AS Detail,
                       ChangedByUserId AS PerformedByUserId, ChangedDateUtc AS OccurredDateUtc
                FROM Audit.FieldChangeLog
                WHERE TenantId = @TenantId AND EntityName = @EntityName AND EntityId = @EntityId AND IsDeleted = 0

                UNION ALL

                SELECT ExportLogId AS EntryId, 'Export' AS SourceType, EntityName, ISNULL(EntityId, @EntityId) AS EntityId,
                       ExportTypeCode + ': ' + ISNULL(FileName,'') AS Summary,
                       'Format: ' + ISNULL(FormatCode,'') + ', Records: ' + CAST(RecordCount AS VARCHAR) AS Detail,
                       PerformedByUserId, CreatedDateUtc AS OccurredDateUtc
                FROM Audit.ExportLog
                WHERE TenantId = @TenantId AND EntityName = @EntityName AND EntityId = @EntityId AND IsDeleted = 0
            ) AS Timeline
            ORDER BY OccurredDateUtc DESC;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await cn.QueryAsync<RecordTimelineEntryDto>(
            new CommandDefinition(sql, new { TenantId = tenantId, EntityName = entityName, EntityId = entityId, Top = top }, cancellationToken: cancellationToken));
        return items.AsList();
    }

    // ── Retention policies ───────────────────────────────────

    public async Task<PagedResult<RetentionPolicyDto>> SearchRetentionPoliciesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT RetentionPolicyId,
                   TenantId,
                   COALESCE(ApplicableEntityType, ApplicableCategory, PolicyName) AS EntityName,
                   RetentionPeriodYears * 365 AS RetentionDays,
                   ActionOnExpiry AS ActionCode,
                   IsActive AS IsEnabled,
                   Description,
                   CAST(NULL AS DATETIME2) AS LastAppliedDateUtc,
                   CAST(NULL AS INT) AS LastAppliedCount,
                   CreatedDateUtc,
                   ModifiedDateUtc
            FROM DMS.DocumentRetentionPolicy
            WHERE TenantId = @TenantId
              AND IsDeleted = 0
              AND (@SearchTerm IS NULL OR @SearchTerm = '' OR PolicyName LIKE '%' + @SearchTerm + '%' OR PolicyCode LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%' OR ApplicableCategory LIKE '%' + @SearchTerm + '%')
            ORDER BY PolicyName ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1)
            FROM DMS.DocumentRetentionPolicy
            WHERE TenantId = @TenantId
              AND IsDeleted = 0
              AND (@SearchTerm IS NULL OR @SearchTerm = '' OR PolicyName LIKE '%' + @SearchTerm + '%' OR PolicyCode LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%' OR ApplicableCategory LIKE '%' + @SearchTerm + '%');
            """;

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<RetentionPolicyDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<RetentionPolicyDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<RetentionPolicyDto?> GetRetentionPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT RetentionPolicyId,
                   TenantId,
                   COALESCE(ApplicableEntityType, ApplicableCategory, PolicyName) AS EntityName,
                   RetentionPeriodYears * 365 AS RetentionDays,
                   ActionOnExpiry AS ActionCode,
                   IsActive AS IsEnabled,
                   Description,
                   CAST(NULL AS DATETIME2) AS LastAppliedDateUtc,
                   CAST(NULL AS INT) AS LastAppliedCount,
                   CreatedDateUtc,
                   ModifiedDateUtc
            FROM DMS.DocumentRetentionPolicy
            WHERE RetentionPolicyId = @Id AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<RetentionPolicyDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateRetentionPolicyAsync(CreateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = """
            INSERT INTO DMS.DocumentRetentionPolicy (RetentionPolicyId, TenantId, PolicyName, PolicyCode, Description, ApplicableEntityType,
                                                     RetentionPeriodYears, RetentionStartTrigger, ActionOnExpiry, RequireApprovalToDelete,
                                                     IsActive, EffectiveDate, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES (@Id, @TenantId, @EntityName, @PolicyCode, @Description, @EntityName,
                    @RetentionPeriodYears, N'Creation', @ActionCode, 1,
                    1, CAST(SYSUTCDATETIME() AS DATE), SYSUTCDATETIME(), @CreatedByUserId, 0);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.TenantId,
            request.EntityName,
            PolicyCode = $"AUDIT-{request.EntityName}-{Guid.NewGuid():N}"[..50],
            RetentionPeriodYears = Math.Max(1, (int)Math.Ceiling(request.RetentionDays / 365d)),
            request.ActionCode,
            request.Description,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateRetentionPolicyAsync(UpdateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE DMS.DocumentRetentionPolicy
            SET RetentionPeriodYears = @RetentionPeriodYears,
                ActionOnExpiry       = @ActionCode,
                IsActive             = @IsEnabled,
                Description          = @Description,
                ModifiedDateUtc      = SYSUTCDATETIME(),
                ModifiedByUserId     = @ModifiedByUserId
            WHERE RetentionPolicyId = @RetentionPolicyId AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            request.RetentionPolicyId,
            RetentionPeriodYears = Math.Max(1, (int)Math.Ceiling(request.RetentionDays / 365d)),
            request.ActionCode,
            request.IsEnabled,
            request.Description,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<int> ApplyRetentionPolicyAsync(Guid retentionPolicyId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DECLARE @EntityName NVARCHAR(256), @RetentionDays INT, @ActionCode NVARCHAR(50), @TenantId UNIQUEIDENTIFIER;
            SELECT @EntityName = COALESCE(ApplicableEntityType, ApplicableCategory, PolicyName),
                   @RetentionDays = RetentionPeriodYears * 365,
                   @ActionCode = ActionOnExpiry,
                   @TenantId = TenantId
            FROM DMS.DocumentRetentionPolicy WHERE RetentionPolicyId = @PolicyId AND IsDeleted = 0 AND IsActive = 1;

            DECLARE @Affected INT = 0;

            IF @EntityName = 'AuditLog'
            BEGIN
                IF @ActionCode = 'Delete'
                    DELETE FROM Audit.AuditLog WHERE TenantId = @TenantId AND PerformedDateUtc < DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
                ELSE
                    UPDATE Audit.AuditLog SET IsDeleted = 1 WHERE TenantId = @TenantId AND IsDeleted = 0 AND PerformedDateUtc < DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
                SET @Affected = @@ROWCOUNT;
            END
            ELSE IF @EntityName = 'FieldChangeLog'
            BEGIN
                IF @ActionCode = 'Delete'
                    DELETE FROM Audit.FieldChangeLog WHERE TenantId = @TenantId AND ChangedDateUtc < DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
                ELSE
                    UPDATE Audit.FieldChangeLog SET IsDeleted = 1 WHERE TenantId = @TenantId AND IsDeleted = 0 AND ChangedDateUtc < DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
                SET @Affected = @@ROWCOUNT;
            END
            ELSE IF @EntityName = 'SecurityEventLog'
            BEGIN
                IF @ActionCode = 'Delete'
                    DELETE FROM Audit.SecurityEventLog WHERE TenantId = @TenantId AND CreatedDateUtc < DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
                ELSE
                    UPDATE Audit.SecurityEventLog SET IsDeleted = 1 WHERE TenantId = @TenantId AND IsDeleted = 0 AND CreatedDateUtc < DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
                SET @Affected = @@ROWCOUNT;
            END
            ELSE IF @EntityName = 'ExportLog'
            BEGIN
                IF @ActionCode = 'Delete'
                    DELETE FROM Audit.ExportLog WHERE TenantId = @TenantId AND CreatedDateUtc < DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
                ELSE
                    UPDATE Audit.ExportLog SET IsDeleted = 1 WHERE TenantId = @TenantId AND IsDeleted = 0 AND CreatedDateUtc < DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
                SET @Affected = @@ROWCOUNT;
            END

            SELECT @Affected;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { PolicyId = retentionPolicyId }, cancellationToken: cancellationToken));
    }

    // ── Write-path (event logging) ───────────────────────────

    public async Task<Guid> LogAuditEventAsync(LogAuditEventRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = """
            INSERT INTO Audit.AuditLog
                (AuditLogId, TenantId, EntityName, EntityId, EventTypeCode, ActionName, PerformedByUserId, PerformedDateUtc, IsDeleted)
            VALUES
                (@AuditLogId, @TenantId, @EntityName, @EntityId, @EventTypeCode, @ActionName, @PerformedByUserId, SYSUTCDATETIME(), 0);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            AuditLogId = id,
            request.TenantId,
            request.EntityName,
            request.EntityId,
            request.EventTypeCode,
            request.ActionName,
            request.PerformedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<Guid> LogFieldChangeAsync(LogFieldChangeRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = """
            INSERT INTO Audit.FieldChangeLog
                (FieldChangeLogId, TenantId, EntityName, EntityId, FieldName, OldValue, NewValue,
                 ChangedByUserId, ChangedDateUtc, ChangeSource, IpAddress, IsDeleted)
            VALUES
                (@FieldChangeLogId, @TenantId, @EntityName, @EntityId, @FieldName, @OldValue, @NewValue,
                 @ChangedByUserId, SYSUTCDATETIME(), @ChangeSource, @IpAddress, 0);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            FieldChangeLogId = id,
            request.TenantId,
            request.EntityName,
            request.EntityId,
            request.FieldName,
            request.OldValue,
            request.NewValue,
            request.ChangedByUserId,
            request.ChangeSource,
            request.IpAddress
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<Guid> LogApprovalHistoryAsync(LogApprovalHistoryRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = """
            INSERT INTO Audit.WorkflowApprovalHistory
                (Id, TenantId, WorkflowInstanceId, ApprovalStepId, ActorUserId, ActionCode, Notes,
                 PreviousStatusCode, NewStatusCode, IsDelegated, DelegatedByUserId, ActionDateUtc, CreatedDateUtc, IsDeleted)
            VALUES
                (@Id, @TenantId, @WorkflowInstanceId, @ApprovalStepId, @ActorUserId, @ActionCode, @Notes,
                 @PreviousStatusCode, @NewStatusCode, @IsDelegated, @DelegatedByUserId, SYSUTCDATETIME(), SYSUTCDATETIME(), 0);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.TenantId,
            request.WorkflowInstanceId,
            request.ApprovalStepId,
            request.ActorUserId,
            request.ActionCode,
            request.Notes,
            request.PreviousStatusCode,
            request.NewStatusCode,
            request.IsDelegated,
            request.DelegatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<Guid> LogSecurityEventAsync(LogSecurityEventRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = """
            INSERT INTO Audit.SecurityEventLog
                (SecurityEventId, TenantId, UserId, EventTypeCode, EventDescription,
                 IpAddress, UserAgent, IsSuccess, RiskScore, SessionId, CreatedDateUtc, IsDeleted)
            VALUES
                (@SecurityEventId, @TenantId, @UserId, @EventTypeCode, @EventDescription,
                 @IpAddress, @UserAgent, @IsSuccess, @RiskScore, @SessionId, SYSUTCDATETIME(), 0);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            SecurityEventId = id,
            request.TenantId,
            request.UserId,
            request.EventTypeCode,
            request.EventDescription,
            request.IpAddress,
            request.UserAgent,
            request.IsSuccess,
            request.RiskScore,
            request.SessionId
        }, cancellationToken: cancellationToken));
        return id;
    }
}
