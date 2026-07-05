using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Audit;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class EnterpriseAuditRepository : IEnterpriseAuditRepository
{
    private static readonly JsonSerializerOptions ChangesJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISqlConnectionFactory _connectionFactory;

    public EnterpriseAuditRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> LogEntityAuditAsync(LogEntityAuditRequest request, CancellationToken cancellationToken = default)
    {
        var auditTrailId = Guid.NewGuid();

        const string sql = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            -- 1. Resolve actor identity from the database (IAM.[User] / IAM.UserRole / IAM.Role):
            --    claim user id -> name/email hint -> tenant admin (first active user of tenant).
            DECLARE @ResolvedUserId UNIQUEIDENTIFIER, @ResolvedFullName NVARCHAR(200), @ResolvedUserName NVARCHAR(200), @ResolvedEmail NVARCHAR(320), @UserExists BIT = 0;
            SELECT @UserExists = 1,
                   @ResolvedUserId = u.UserId,
                   @ResolvedFullName = u.FullName,
                   @ResolvedUserName = u.UserName,
                   @ResolvedEmail = u.Email
            FROM IAM.[User] u
            WHERE u.UserId = @ActorUserId AND u.IsDeleted = 0;

            IF @UserExists = 0 AND NULLIF(@ActorUserNameHint, N'') IS NOT NULL
                SELECT TOP 1 @UserExists = 1,
                       @ResolvedUserId = u.UserId,
                       @ResolvedFullName = u.FullName,
                       @ResolvedUserName = u.UserName,
                       @ResolvedEmail = u.Email
                FROM IAM.[User] u
                WHERE u.TenantId = @TenantId AND u.IsDeleted = 0
                  AND (u.UserName = @ActorUserNameHint OR u.FullName = @ActorUserNameHint OR u.Email = @ActorUserNameHint)
                ORDER BY u.CreatedDateUtc;

            IF @UserExists = 0
                SELECT TOP 1 @UserExists = 1,
                       @ResolvedUserId = u.UserId,
                       @ResolvedFullName = u.FullName,
                       @ResolvedUserName = u.UserName,
                       @ResolvedEmail = u.Email
                FROM IAM.[User] u
                WHERE u.TenantId = @TenantId AND u.IsDeleted = 0 AND u.IsActive = 1
                ORDER BY u.CreatedDateUtc;

            DECLARE @EffectiveUserId UNIQUEIDENTIFIER = COALESCE(@ResolvedUserId, @ActorUserId);
            DECLARE @ActorName NVARCHAR(300) = COALESCE(NULLIF(@ResolvedFullName, N''), NULLIF(@ResolvedUserName, N''), NULLIF(@ResolvedEmail, N''), NULLIF(@ActorUserNameHint, N''));
            DECLARE @ActorType NVARCHAR(100) = CASE WHEN @UserExists = 1 THEN N'User' ELSE N'System' END;
            DECLARE @ActorLabel NVARCHAR(300) = COALESCE(@ActorName, @ActorType);
            DECLARE @ActorRole NVARCHAR(200) =
                LEFT((SELECT STRING_AGG(r.RoleName, N', ') WITHIN GROUP (ORDER BY r.RoleName)
                      FROM IAM.UserRole ur
                      JOIN IAM.Role r ON r.RoleId = ur.RoleId AND r.IsDeleted = 0 AND r.IsActive = 1
                      WHERE ur.UserId = @EffectiveUserId AND ur.IsDeleted = 0 AND ur.IsActive = 1
                        AND (ur.EffectiveStartDateUtc IS NULL OR ur.EffectiveStartDateUtc <= SYSUTCDATETIME())
                        AND (ur.EffectiveEndDateUtc IS NULL OR ur.EffectiveEndDateUtc > SYSUTCDATETIME())), 200);

            -- 2. Resolve legal hold state from Audit.AuditLegalHold.
            DECLARE @IsLegalHold BIT = CASE WHEN EXISTS (
                SELECT 1 FROM Audit.AuditLegalHold h
                WHERE h.TenantId = @TenantId
                  AND h.StatusCode = N'Active'
                  AND (h.EntityName IS NULL OR h.EntityName = @EntityName)
                  AND (h.EntityId IS NULL OR h.EntityId = @EntityId)
                  AND h.StartUtc <= SYSUTCDATETIME()
                  AND (h.EndUtc IS NULL OR h.EndUtc > SYSUTCDATETIME())) THEN 1 ELSE 0 END;

            -- 3. Shred field changes and flag sensitive fields from the Audit.AuditSensitiveField catalog.
            DECLARE @Changes TABLE (
                EventId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
                FieldName NVARCHAR(256) NOT NULL,
                OldValue NVARCHAR(MAX) NULL,
                NewValue NVARCHAR(MAX) NULL,
                DataTypeCode NVARCHAR(50) NOT NULL,
                ActionType NVARCHAR(100) NOT NULL,
                IsSnapshot BIT NOT NULL,
                IsSensitive BIT NOT NULL DEFAULT 0);

            INSERT INTO @Changes (FieldName, OldValue, NewValue, DataTypeCode, ActionType, IsSnapshot)
            SELECT j.FieldName, j.OldValue, j.NewValue, COALESCE(NULLIF(j.DataTypeCode, N''), N'String'), j.ActionType, COALESCE(j.IsSnapshot, 0)
            FROM OPENJSON(@ChangesJson) WITH (
                FieldName NVARCHAR(256) '$.fieldName',
                OldValue NVARCHAR(MAX) '$.oldValue',
                NewValue NVARCHAR(MAX) '$.newValue',
                DataTypeCode NVARCHAR(50) '$.dataTypeCode',
                ActionType NVARCHAR(100) '$.actionType',
                IsSnapshot BIT '$.isSnapshot') AS j;

            UPDATE c SET IsSensitive = 1
            FROM @Changes c
            WHERE c.IsSnapshot = 0
              AND EXISTS (SELECT 1 FROM Audit.AuditSensitiveField s
                          WHERE (s.TenantId = @TenantId OR s.TenantId IS NULL)
                            AND s.IsActive = 1 AND s.IsDeleted = 0
                            AND (s.EntityName IS NULL OR s.EntityName = @EntityName)
                            AND c.FieldName LIKE s.FieldNamePattern);

            -- 4. Mask sensitive values in field diffs and JSON snapshots so stored data honors IsPiiMasked.
            DECLARE @MaskedOldSnapshot NVARCHAR(MAX) = @OldValue, @MaskedNewSnapshot NVARCHAR(MAX) = @NewValue, @SnapshotHasSensitive BIT = 0;
            DECLARE @SensitiveJsonFields TABLE (JsonPath NVARCHAR(300) PRIMARY KEY);
            INSERT INTO @SensitiveJsonFields (JsonPath)
            SELECT DISTINCT N'$."' + LOWER(LEFT(s.FieldNamePattern, 1)) + SUBSTRING(REPLACE(s.FieldNamePattern, N'%', N''), 2, 250) + N'"'
            FROM Audit.AuditSensitiveField s
            WHERE (s.TenantId = @TenantId OR s.TenantId IS NULL)
              AND s.IsActive = 1 AND s.IsDeleted = 0
              AND (s.EntityName IS NULL OR s.EntityName = @EntityName)
              AND s.FieldNamePattern NOT LIKE N'[%]%'
              AND s.FieldNamePattern NOT LIKE N'%[%]';

            INSERT INTO @SensitiveJsonFields (JsonPath)
            SELECT DISTINCT N'$."' + LOWER(LEFT(c.FieldName, 1)) + SUBSTRING(c.FieldName, 2, 250) + N'"'
            FROM @Changes c
            WHERE c.IsSensitive = 1
              AND NOT EXISTS (SELECT 1 FROM @SensitiveJsonFields f WHERE f.JsonPath = N'$."' + LOWER(LEFT(c.FieldName, 1)) + SUBSTRING(c.FieldName, 2, 250) + N'"');

            DECLARE @JsonPath NVARCHAR(300);
            WHILE EXISTS (SELECT 1 FROM @SensitiveJsonFields)
            BEGIN
                SELECT TOP 1 @JsonPath = JsonPath FROM @SensitiveJsonFields;
                IF @MaskedOldSnapshot IS NOT NULL AND ISJSON(@MaskedOldSnapshot) > 0 AND JSON_VALUE(@MaskedOldSnapshot, @JsonPath) IS NOT NULL
                BEGIN
                    SET @MaskedOldSnapshot = JSON_MODIFY(@MaskedOldSnapshot, @JsonPath, N'***MASKED***');
                    SET @SnapshotHasSensitive = 1;
                END;
                IF @MaskedNewSnapshot IS NOT NULL AND ISJSON(@MaskedNewSnapshot) > 0 AND JSON_VALUE(@MaskedNewSnapshot, @JsonPath) IS NOT NULL
                BEGIN
                    SET @MaskedNewSnapshot = JSON_MODIFY(@MaskedNewSnapshot, @JsonPath, N'***MASKED***');
                    SET @SnapshotHasSensitive = 1;
                END;
                DELETE FROM @SensitiveJsonFields WHERE JsonPath = @JsonPath;
            END;

            UPDATE @Changes SET
                OldValue = CASE WHEN IsSensitive = 1 THEN N'***MASKED***' WHEN IsSnapshot = 1 THEN @MaskedOldSnapshot ELSE OldValue END,
                NewValue = CASE WHEN IsSensitive = 1 THEN N'***MASKED***' WHEN IsSnapshot = 1 THEN @MaskedNewSnapshot ELSE NewValue END,
                IsSensitive = CASE WHEN IsSnapshot = 1 AND @SnapshotHasSensitive = 1 THEN 1 ELSE IsSensitive END;

            -- 5. One IAM.UserAuditTrail row for the whole operation, attributed to the resolved DB user.
            INSERT INTO IAM.UserAuditTrail
                (AuditTrailId, TenantId, UserId, ActionCode, ActionDescription, OldValue, NewValue,
                 ChangedByUserId, IpAddress, UserAgent, SessionId, StatusCode, ErrorDetails, CreatedDateUtc, IsDeleted)
            VALUES
                (@AuditTrailId, @TenantId, COALESCE(@EffectiveUserId, '00000000-0000-0000-0000-000000000000'), @UserActionCode,
                 @UserActionDescription, @MaskedOldSnapshot, @MaskedNewSnapshot, @EffectiveUserId, @IpAddress, @UserAgent,
                 @SessionId, @StatusCode, @ErrorDetails, SYSUTCDATETIME(), 0);

            -- 6. One Audit.AuditEvent per field change, fully resolved and DB-synced.
            INSERT INTO Audit.AuditEvent
                (AuditEventId, TenantId, ActorUserId, ActorUserName, ActorRole, ActorType, ActionType,
                 ActionCategory, ModuleName, EntityName, EntityId, EntityDisplayName, ParentEntityName,
                 ParentEntityId, OldValue, NewValue, IpAddress, UserAgent, CorrelationId, RequestId,
                 SourceSystem, Severity, StatusCode, IsSensitiveData, IsPiiMasked, IsLegalHold,
                 ChangeReason, VersionNumber, MetadataJson, CreatedUtc)
            SELECT c.EventId, @TenantId, @EffectiveUserId, @ActorName, @ActorRole, @ActorType, c.ActionType,
                   @ActionCategory, @ModuleName, @EntityName, @EntityId, @EntityDisplayName, @ParentEntityName,
                   @ParentEntityId, c.OldValue, c.NewValue, @IpAddress, @UserAgent, @CorrelationId, @RequestId,
                   @SourceSystem, @Severity, @StatusCode, c.IsSensitive, c.IsSensitive, @IsLegalHold,
                   CASE WHEN c.IsSnapshot = 1
                        THEN @ActorLabel + N' ' + LOWER(@ActionVerb) + N' ' + @EntityName + N' ''' + COALESCE(@EntityDisplayName, @EntityName) + N''' via ' + @SourceSystem + N'.'
                        ELSE @ActorLabel + N' changed ' + @EntityName + N'.' + c.FieldName + N' for ''' + COALESCE(@EntityDisplayName, @EntityName) + N'''.' END,
                   @VersionNumber,
                   (SELECT N'Global entity audit' AS feature, @ControllerName AS [controller], @ActionName AS [action],
                           @HttpMethod AS httpMethod, @ActorName AS actor, @EffectiveUserId AS actorUserId,
                           @ActorRole AS actorRole, @ActorType AS actorType, @EntityName AS entity, @EntityId AS entityId,
                           @EntityDisplayName AS entityDisplayName, @ParentEntityName AS parentEntityName,
                           @ParentEntityId AS parentEntityId, c.FieldName AS field, c.DataTypeCode AS dataType,
                           c.IsSensitive AS isSensitive, @IsLegalHold AS isLegalHold, @CorrelationId AS correlationId,
                           @RequestId AS requestId, @SessionId AS sessionId, @SourceSystem AS sourceSystem,
                           @VersionNumber AS versionNumber, @AuditTrailId AS userAuditTrailId
                    FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES),
                   SYSUTCDATETIME()
            FROM @Changes c;

            -- 7. Field-level detail rows.
            INSERT INTO Audit.AuditEventDetail
                (AuditEventDetailId, TenantId, AuditEventId, DetailName, OldValue, NewValue, DataTypeCode, IsSensitive, IsMasked, CreatedUtc)
            SELECT NEWID(), @TenantId, c.EventId, CASE WHEN c.IsSnapshot = 1 THEN @EntityName ELSE c.FieldName END,
                   c.OldValue, c.NewValue, c.DataTypeCode, c.IsSensitive, c.IsSensitive, SYSUTCDATETIME()
            FROM @Changes c;

            -- 8. Entity change history rows for data changes.
            IF @ActionCategory = N'Data Change' AND @EntityId IS NOT NULL
            BEGIN
                INSERT INTO Audit.AuditEntityChange
                    (AuditEntityChangeId, TenantId, AuditEventId, EntityName, EntityId, ParentEntityName, ParentEntityId,
                     FieldName, OldValue, NewValue, ChangeReason, VersionNumber, ChangedByUserId, ChangedUtc)
                SELECT NEWID(), @TenantId, c.EventId, @EntityName, @EntityId, @ParentEntityName, @ParentEntityId,
                       CASE WHEN c.IsSnapshot = 1 THEN @EntityName ELSE c.FieldName END, c.OldValue, c.NewValue,
                       @ActorLabel + N' ' + CASE WHEN c.IsSnapshot = 1 THEN LOWER(@ActionVerb) + N' ' + @EntityName ELSE N'changed ' + @EntityName + N'.' + c.FieldName END + N'.',
                       @VersionNumber, @EffectiveUserId, SYSUTCDATETIME()
                FROM @Changes c;
            END;

            -- 9. Alert rows for high-severity security failures.
            IF @ActionCategory = N'Security' AND @Severity IN (N'High', N'Critical') AND @StatusCode IN (N'Open', N'Failed')
            BEGIN
                INSERT INTO Audit.AuditAlertEvent
                    (AuditAlertEventId, TenantId, AuditEventId, AlertCode, AlertName, Severity, StatusCode, Description, AssignedToUserId, CreatedUtc)
                SELECT NEWID(), @TenantId, c.EventId, c.ActionType, REPLACE(c.ActionType, N'_', N' '), @Severity, N'Open',
                       @ActorLabel + N' triggered ' + REPLACE(c.ActionType, N'_', N' ') + N' on ' + @EntityName + N'.', NULL, SYSUTCDATETIME()
                FROM @Changes c;
            END;

            COMMIT TRANSACTION;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            AuditTrailId = auditTrailId,
            request.TenantId,
            request.ActorUserId,
            request.ActorUserNameHint,
            request.UserActionCode,
            request.UserActionDescription,
            ActionVerb = GetActionVerb(request.ActionType),
            request.ActionCategory,
            request.ModuleName,
            request.EntityName,
            request.EntityId,
            request.EntityDisplayName,
            request.ParentEntityName,
            request.ParentEntityId,
            request.OldValue,
            request.NewValue,
            request.IpAddress,
            request.UserAgent,
            request.SessionId,
            request.CorrelationId,
            request.RequestId,
            SourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? "API" : request.SourceSystem,
            Severity = string.IsNullOrWhiteSpace(request.Severity) ? "Info" : request.Severity,
            StatusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "Success" : request.StatusCode,
            request.ErrorDetails,
            request.VersionNumber,
            request.ControllerName,
            request.ActionName,
            request.HttpMethod,
            ChangesJson = JsonSerializer.Serialize(request.Changes, ChangesJsonOptions)
        }, cancellationToken: cancellationToken));

        return auditTrailId;
    }

    private static string GetActionVerb(string actionType)
    {
        if (actionType.EndsWith("_CREATED", StringComparison.OrdinalIgnoreCase)) return "created";
        if (actionType.EndsWith("_DELETED", StringComparison.OrdinalIgnoreCase)) return "deleted";
        return "updated";
    }

    public async Task<Guid> LogAsync(LogEnterpriseAuditEventRequest request, CancellationToken cancellationToken = default)
    {
        var auditEventId = Guid.NewGuid();

        const string sql = """
            INSERT INTO Audit.AuditEvent
                (AuditEventId, TenantId, ActorUserId, ActorUserName, ActorRole, ActorType, ActionType,
                 ActionCategory, ModuleName, EntityName, EntityId, EntityDisplayName, ParentEntityName,
                 ParentEntityId, OldValue, NewValue, IpAddress, UserAgent, CorrelationId, RequestId,
                 SourceSystem, Severity, StatusCode, IsSensitiveData, IsPiiMasked, IsLegalHold,
                 ChangeReason, VersionNumber, MetadataJson, CreatedUtc)
            VALUES
                (@AuditEventId, @TenantId, @ActorUserId, @ActorUserName, @ActorRole, @ActorType, @ActionType,
                 @ActionCategory, @ModuleName, @EntityName, @EntityId, @EntityDisplayName, @ParentEntityName,
                 @ParentEntityId, @OldValue, @NewValue, @IpAddress, @UserAgent, @CorrelationId, @RequestId,
                 @SourceSystem, @Severity, @StatusCode, @IsSensitiveData, @IsPiiMasked, @IsLegalHold,
                 @ChangeReason, @VersionNumber, @MetadataJson, SYSUTCDATETIME());

            IF @OldValue IS NOT NULL OR @NewValue IS NOT NULL OR @DetailName IS NOT NULL
            BEGIN
                INSERT INTO Audit.AuditEventDetail
                    (AuditEventDetailId, TenantId, AuditEventId, DetailName, OldValue, NewValue, DataTypeCode, IsSensitive, IsMasked, CreatedUtc)
                VALUES
                    (NEWID(), @TenantId, @AuditEventId, COALESCE(@DetailName, @ActionType), @OldValue, @NewValue,
                     @DetailDataTypeCode, @IsSensitiveData, @IsPiiMasked, SYSUTCDATETIME());
            END;

            IF @ActionCategory = N'Data Change' AND @EntityName IS NOT NULL AND @EntityId IS NOT NULL
            BEGIN
                INSERT INTO Audit.AuditEntityChange
                    (AuditEntityChangeId, TenantId, AuditEventId, EntityName, EntityId, ParentEntityName, ParentEntityId,
                     FieldName, OldValue, NewValue, ChangeReason, VersionNumber, ChangedByUserId, ChangedUtc)
                VALUES
                    (NEWID(), @TenantId, @AuditEventId, @EntityName, @EntityId, @ParentEntityName, @ParentEntityId,
                     COALESCE(@DetailName, @ActionType), @OldValue, @NewValue, @ChangeReason, @VersionNumber, @ActorUserId, SYSUTCDATETIME());
            END;

            IF @ActionCategory = N'Security' AND @Severity IN (N'High', N'Critical') AND @StatusCode IN (N'Open', N'Failed')
            BEGIN
                INSERT INTO Audit.AuditAlertEvent
                    (AuditAlertEventId, TenantId, AuditEventId, AlertCode, AlertName, Severity, StatusCode, Description, AssignedToUserId, CreatedUtc)
                VALUES
                    (NEWID(), @TenantId, @AuditEventId, @ActionType, REPLACE(@ActionType, N'_', N' '), @Severity, N'Open',
                     COALESCE(@ChangeReason, @NewValue, @MetadataJson), NULL, SYSUTCDATETIME());
            END;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            AuditEventId = auditEventId,
            request.TenantId,
            request.ActorUserId,
            request.ActorUserName,
            request.ActorRole,
            ActorType = string.IsNullOrWhiteSpace(request.ActorType) ? "User" : request.ActorType,
            request.ActionType,
            request.ActionCategory,
            request.ModuleName,
            request.EntityName,
            request.EntityId,
            request.EntityDisplayName,
            request.ParentEntityName,
            request.ParentEntityId,
            request.OldValue,
            request.NewValue,
            request.IpAddress,
            request.UserAgent,
            request.CorrelationId,
            request.RequestId,
            SourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? "Web" : request.SourceSystem,
            Severity = string.IsNullOrWhiteSpace(request.Severity) ? "Info" : request.Severity,
            StatusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "Success" : request.StatusCode,
            request.IsSensitiveData,
            request.IsPiiMasked,
            request.IsLegalHold,
            request.ChangeReason,
            request.VersionNumber,
            request.MetadataJson,
            request.DetailName,
            request.DetailDataTypeCode
        }, cancellationToken: cancellationToken));

        return auditEventId;
    }

    public async Task<PagedResult<EnterpriseAuditEventDto>> SearchAsync(SearchEnterpriseAuditEventsRequest request, CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(request.PageNumber, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        const string sql = """
            ;WITH Filtered AS
            (
                SELECT AuditEventId, TenantId, ActorUserId, ActorUserName, ActorRole, ActorType, ActionType,
                       ActionCategory, ModuleName, EntityName, EntityId, EntityDisplayName, ParentEntityName,
                       ParentEntityId, OldValue, NewValue, IpAddress, UserAgent, CorrelationId, RequestId,
                       SourceSystem, Severity, StatusCode, IsSensitiveData, IsPiiMasked, IsLegalHold,
                       ChangeReason, VersionNumber, MetadataJson, CreatedUtc
                FROM Audit.AuditEvent
                WHERE TenantId = @TenantId
                  AND (@ActorUserId IS NULL OR ActorUserId = @ActorUserId)
                  AND (@ActorType IS NULL OR @ActorType = '' OR ActorType = @ActorType)
                  AND (@ActionType IS NULL OR @ActionType = '' OR ActionType = @ActionType)
                  AND (@ActionCategory IS NULL OR @ActionCategory = '' OR ActionCategory = @ActionCategory)
                  AND (@ModuleName IS NULL OR @ModuleName = '' OR ModuleName = @ModuleName)
                  AND (@EntityName IS NULL OR @EntityName = '' OR EntityName = @EntityName)
                  AND (@EntityId IS NULL OR EntityId = @EntityId)
                  AND (@Severity IS NULL OR @Severity = '' OR Severity = @Severity)
                  AND (@SourceSystem IS NULL OR @SourceSystem = '' OR SourceSystem = @SourceSystem)
                  AND (@IsSensitiveData IS NULL OR IsSensitiveData = @IsSensitiveData)
                  AND (@IsLegalHold IS NULL OR IsLegalHold = @IsLegalHold)
                  AND (@FromUtc IS NULL OR CreatedUtc >= @FromUtc)
                  AND (@ToUtc IS NULL OR CreatedUtc <= @ToUtc)
                  AND (@SearchTerm IS NULL OR @SearchTerm = ''
                       OR ActorUserName LIKE '%' + @SearchTerm + '%'
                       OR ActionType LIKE '%' + @SearchTerm + '%'
                       OR ActionCategory LIKE '%' + @SearchTerm + '%'
                       OR ModuleName LIKE '%' + @SearchTerm + '%'
                       OR EntityName LIKE '%' + @SearchTerm + '%'
                       OR EntityDisplayName LIKE '%' + @SearchTerm + '%'
                       OR IpAddress LIKE '%' + @SearchTerm + '%'
                       OR CorrelationId LIKE '%' + @SearchTerm + '%'
                       OR MetadataJson LIKE '%' + @SearchTerm + '%')
            )
            SELECT COUNT(1) FROM Filtered;

            ;WITH Filtered AS
            (
                SELECT AuditEventId, TenantId, ActorUserId, ActorUserName, ActorRole, ActorType, ActionType,
                       ActionCategory, ModuleName, EntityName, EntityId, EntityDisplayName, ParentEntityName,
                       ParentEntityId, OldValue, NewValue, IpAddress, UserAgent, CorrelationId, RequestId,
                       SourceSystem, Severity, StatusCode, IsSensitiveData, IsPiiMasked, IsLegalHold,
                       ChangeReason, VersionNumber, MetadataJson, CreatedUtc
                FROM Audit.AuditEvent
                WHERE TenantId = @TenantId
                  AND (@ActorUserId IS NULL OR ActorUserId = @ActorUserId)
                  AND (@ActorType IS NULL OR @ActorType = '' OR ActorType = @ActorType)
                  AND (@ActionType IS NULL OR @ActionType = '' OR ActionType = @ActionType)
                  AND (@ActionCategory IS NULL OR @ActionCategory = '' OR ActionCategory = @ActionCategory)
                  AND (@ModuleName IS NULL OR @ModuleName = '' OR ModuleName = @ModuleName)
                  AND (@EntityName IS NULL OR @EntityName = '' OR EntityName = @EntityName)
                  AND (@EntityId IS NULL OR EntityId = @EntityId)
                  AND (@Severity IS NULL OR @Severity = '' OR Severity = @Severity)
                  AND (@SourceSystem IS NULL OR @SourceSystem = '' OR SourceSystem = @SourceSystem)
                  AND (@IsSensitiveData IS NULL OR IsSensitiveData = @IsSensitiveData)
                  AND (@IsLegalHold IS NULL OR IsLegalHold = @IsLegalHold)
                  AND (@FromUtc IS NULL OR CreatedUtc >= @FromUtc)
                  AND (@ToUtc IS NULL OR CreatedUtc <= @ToUtc)
                  AND (@SearchTerm IS NULL OR @SearchTerm = ''
                       OR ActorUserName LIKE '%' + @SearchTerm + '%'
                       OR ActionType LIKE '%' + @SearchTerm + '%'
                       OR ActionCategory LIKE '%' + @SearchTerm + '%'
                       OR ModuleName LIKE '%' + @SearchTerm + '%'
                       OR EntityName LIKE '%' + @SearchTerm + '%'
                       OR EntityDisplayName LIKE '%' + @SearchTerm + '%'
                       OR IpAddress LIKE '%' + @SearchTerm + '%'
                       OR CorrelationId LIKE '%' + @SearchTerm + '%'
                       OR MetadataJson LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Filtered
            ORDER BY CreatedUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            request.TenantId,
            request.ActorUserId,
            SearchTerm = request.SearchTerm?.Trim(),
            request.ActorType,
            request.ActionType,
            request.ActionCategory,
            request.ModuleName,
            request.EntityName,
            request.EntityId,
            request.Severity,
            request.SourceSystem,
            request.IsSensitiveData,
            request.IsLegalHold,
            request.FromUtc,
            request.ToUtc,
            Offset = (pageNumber - 1) * pageSize,
            PageSize = pageSize
        }, cancellationToken: cancellationToken));

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<EnterpriseAuditEventDto>()).AsList();

        return new PagedResult<EnterpriseAuditEventDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<EnterpriseAuditEventDto?> GetByIdAsync(Guid tenantId, Guid auditEventId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT AuditEventId, TenantId, ActorUserId, ActorUserName, ActorRole, ActorType, ActionType,
                   ActionCategory, ModuleName, EntityName, EntityId, EntityDisplayName, ParentEntityName,
                   ParentEntityId, OldValue, NewValue, IpAddress, UserAgent, CorrelationId, RequestId,
                   SourceSystem, Severity, StatusCode, IsSensitiveData, IsPiiMasked, IsLegalHold,
                   ChangeReason, VersionNumber, MetadataJson, CreatedUtc
            FROM Audit.AuditEvent
            WHERE TenantId = @TenantId AND AuditEventId = @AuditEventId;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<EnterpriseAuditEventDto>(
            new CommandDefinition(sql, new { TenantId = tenantId, AuditEventId = auditEventId }, cancellationToken: cancellationToken));
    }

    public async Task<EnterpriseAuditSummaryDto> GetSummaryAsync(Guid tenantId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                COUNT(1) AS TotalEvents,
                COUNT(CASE WHEN ActionCategory = 'User Activity' THEN 1 END) AS UserActivityEvents,
                COUNT(CASE WHEN ActionCategory = 'Data Change' THEN 1 END) AS DataChangeEvents,
                COUNT(CASE WHEN ActionCategory = 'Security' THEN 1 END) AS SecurityEvents,
                COUNT(CASE WHEN ActionCategory = 'Tenant' THEN 1 END) AS TenantEvents,
                COUNT(CASE WHEN ActionCategory = 'Business Workflow' THEN 1 END) AS WorkflowEvents,
                COUNT(CASE WHEN ActionCategory = 'Document' THEN 1 END) AS DocumentEvents,
                COUNT(CASE WHEN ActionCategory = 'Compliance' THEN 1 END) AS ComplianceEvents,
                COUNT(CASE WHEN Severity IN ('High', 'Critical') THEN 1 END) AS HighSeverityEvents,
                COUNT(CASE WHEN IsLegalHold = 1 THEN 1 END) AS LegalHoldEvents,
                COUNT(CASE WHEN IsSensitiveData = 1 THEN 1 END) AS SensitiveAccessEvents,
                (SELECT COUNT(1) FROM Audit.AuditAlertEvent alert WHERE alert.TenantId = @TenantId AND alert.StatusCode = 'Open') AS OpenAlertEvents,
                MAX(CreatedUtc) AS LastEventUtc
            FROM Audit.AuditEvent
            WHERE TenantId = @TenantId
              AND (@FromUtc IS NULL OR CreatedUtc >= @FromUtc)
              AND (@ToUtc IS NULL OR CreatedUtc <= @ToUtc);
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<EnterpriseAuditSummaryDto>(
            new CommandDefinition(sql, new { TenantId = tenantId, FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<EnterpriseAuditAlertDto>> GetOpenAlertsAsync(Guid tenantId, int top = 10, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (@Top) AuditAlertEventId, TenantId, AuditEventId, AlertCode, AlertName, Severity,
                   StatusCode, Description, AssignedToUserId, CreatedUtc
            FROM Audit.AuditAlertEvent
            WHERE TenantId = @TenantId AND StatusCode = 'Open'
            ORDER BY CASE Severity WHEN 'Critical' THEN 1 WHEN 'High' THEN 2 WHEN 'Medium' THEN 3 ELSE 4 END, CreatedUtc DESC;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<EnterpriseAuditAlertDto>(
            new CommandDefinition(sql, new { TenantId = tenantId, Top = Math.Clamp(top, 1, 50) }, cancellationToken: cancellationToken));
        return items.AsList();
    }

    public async Task<EnterpriseAuditOptionsDto> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT ActionCategory
            FROM Audit.AuditEvent
            WHERE TenantId = @TenantId AND ActionCategory IS NOT NULL AND LTRIM(RTRIM(ActionCategory)) <> ''
            ORDER BY ActionCategory;

            SELECT DISTINCT ModuleName
            FROM Audit.AuditEvent
            WHERE TenantId = @TenantId AND ModuleName IS NOT NULL AND LTRIM(RTRIM(ModuleName)) <> ''
            ORDER BY ModuleName;

            SELECT Severity
            FROM
            (
                SELECT Severity,
                       MIN(CASE Severity WHEN 'Critical' THEN 1 WHEN 'High' THEN 2 WHEN 'Medium' THEN 3 WHEN 'Info' THEN 4 ELSE 5 END) AS SortOrder
                FROM Audit.AuditEvent
                WHERE TenantId = @TenantId AND Severity IS NOT NULL AND LTRIM(RTRIM(Severity)) <> ''
                GROUP BY Severity
            ) AS SeverityOptions
            ORDER BY SortOrder, Severity;

            SELECT DISTINCT StatusCode
            FROM Audit.AuditEvent
            WHERE TenantId = @TenantId AND StatusCode IS NOT NULL AND LTRIM(RTRIM(StatusCode)) <> ''
            ORDER BY StatusCode;

            SELECT DISTINCT SourceSystem
            FROM Audit.AuditEvent
            WHERE TenantId = @TenantId AND SourceSystem IS NOT NULL AND LTRIM(RTRIM(SourceSystem)) <> ''
            ORDER BY SourceSystem;

            SELECT DISTINCT ActorType
            FROM Audit.AuditEvent
            WHERE TenantId = @TenantId AND ActorType IS NOT NULL AND LTRIM(RTRIM(ActorType)) <> ''
            ORDER BY ActorType;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));

        return new EnterpriseAuditOptionsDto
        {
            Categories = (await multi.ReadAsync<string>()).AsList(),
            Modules = (await multi.ReadAsync<string>()).AsList(),
            Severities = (await multi.ReadAsync<string>()).AsList(),
            Statuses = (await multi.ReadAsync<string>()).AsList(),
            SourceSystems = (await multi.ReadAsync<string>()).AsList(),
            ActorTypes = (await multi.ReadAsync<string>()).AsList()
        };
    }

    public async Task<IReadOnlyList<EnterpriseAuditCapabilityDto>> GetCapabilitiesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT AuditCapabilityId, CapabilityArea, FeatureName, Purpose, ModuleName, ActionType,
                   IsImplemented, IsSeeded, RequiresInstrumentation, DisplayOrder
            FROM Audit.AuditCapability
            WHERE TenantId = @TenantId OR TenantId IS NULL
            ORDER BY DisplayOrder, CapabilityArea, FeatureName;
            """;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<EnterpriseAuditCapabilityDto>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return items.AsList();
    }
}
