using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Communications;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    private const string SelectColumns = """
        NotificationId, TenantId, RecipientUserId, TemplateId, ChannelCode,
        Subject, Body, EntityName, EntityId, StatusCode,
        IsRead, ReadDateUtc, SentDateUtc, ErrorMessage, CreatedDateUtc,
        COALESCE(Priority,'Normal') AS Priority, COALESCE(Category,'General') AS Category,
        COALESCE(DeliveryProvider,'AMS') AS DeliveryProvider, COALESCE(DeliveryStatus,StatusCode) AS DeliveryStatus,
        COALESCE(PolicyStatus,'Compliant') AS PolicyStatus, COALESCE(SyncStatus,'Synced') AS SyncStatus,
        COALESCE(AttemptCount,0) AS AttemptCount, LastAttemptDateUtc, DeliveredDateUtc, LastSyncedDateUtc
        """;

    public NotificationRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    private async Task EnsureNotificationSchemaAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string schemaSql = """
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Core') EXEC(N'CREATE SCHEMA Core');

IF OBJECT_ID(N'Core.Notification', N'U') IS NULL
BEGIN
    CREATE TABLE Core.Notification (NotificationId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, RecipientUserId UNIQUEIDENTIFIER NOT NULL, TemplateId UNIQUEIDENTIFIER NULL, ChannelCode NVARCHAR(50) NOT NULL DEFAULT N'InApp', Subject NVARCHAR(200) NULL, Body NVARCHAR(2000) NOT NULL, EntityName NVARCHAR(100) NULL, EntityId UNIQUEIDENTIFIER NULL, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Delivered', IsRead BIT NOT NULL DEFAULT 0, ReadDateUtc DATETIME2 NULL, SentDateUtc DATETIME2 NULL, ErrorMessage NVARCHAR(1000) NULL, Priority NVARCHAR(40) NOT NULL DEFAULT N'Normal', Category NVARCHAR(80) NOT NULL DEFAULT N'General', DeliveryProvider NVARCHAR(120) NOT NULL DEFAULT N'AMS', DeliveryStatus NVARCHAR(60) NOT NULL DEFAULT N'Queued', PolicyStatus NVARCHAR(60) NOT NULL DEFAULT N'Compliant', SyncStatus NVARCHAR(60) NOT NULL DEFAULT N'Synced', AttemptCount INT NOT NULL DEFAULT 0, LastAttemptDateUtc DATETIME2 NULL, DeliveredDateUtc DATETIME2 NULL, LastSyncedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF COL_LENGTH('Core.Notification','Priority') IS NULL ALTER TABLE Core.Notification ADD Priority NVARCHAR(40) NOT NULL CONSTRAINT DF_CoreNotification_Priority DEFAULT N'Normal';
IF COL_LENGTH('Core.Notification','Category') IS NULL ALTER TABLE Core.Notification ADD Category NVARCHAR(80) NOT NULL CONSTRAINT DF_CoreNotification_Category DEFAULT N'General';
IF COL_LENGTH('Core.Notification','DeliveryProvider') IS NULL ALTER TABLE Core.Notification ADD DeliveryProvider NVARCHAR(120) NOT NULL CONSTRAINT DF_CoreNotification_DeliveryProvider DEFAULT N'AMS';
IF COL_LENGTH('Core.Notification','DeliveryStatus') IS NULL ALTER TABLE Core.Notification ADD DeliveryStatus NVARCHAR(60) NOT NULL CONSTRAINT DF_CoreNotification_DeliveryStatus DEFAULT N'Queued';
IF COL_LENGTH('Core.Notification','PolicyStatus') IS NULL ALTER TABLE Core.Notification ADD PolicyStatus NVARCHAR(60) NOT NULL CONSTRAINT DF_CoreNotification_PolicyStatus DEFAULT N'Compliant';
IF COL_LENGTH('Core.Notification','SyncStatus') IS NULL ALTER TABLE Core.Notification ADD SyncStatus NVARCHAR(60) NOT NULL CONSTRAINT DF_CoreNotification_SyncStatus DEFAULT N'Synced';
IF COL_LENGTH('Core.Notification','AttemptCount') IS NULL ALTER TABLE Core.Notification ADD AttemptCount INT NOT NULL CONSTRAINT DF_CoreNotification_AttemptCount DEFAULT 0;
IF COL_LENGTH('Core.Notification','LastAttemptDateUtc') IS NULL ALTER TABLE Core.Notification ADD LastAttemptDateUtc DATETIME2 NULL;
IF COL_LENGTH('Core.Notification','DeliveredDateUtc') IS NULL ALTER TABLE Core.Notification ADD DeliveredDateUtc DATETIME2 NULL;
IF COL_LENGTH('Core.Notification','LastSyncedDateUtc') IS NULL ALTER TABLE Core.Notification ADD LastSyncedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CoreNotification_LastSynced DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('Core.Notification','ModifiedDateUtc') IS NULL ALTER TABLE Core.Notification ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH('Core.Notification','ModifiedByUserId') IS NULL ALTER TABLE Core.Notification ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('Core.Notification','IsDeleted') IS NULL ALTER TABLE Core.Notification ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CoreNotification_IsDeleted DEFAULT 0;

IF OBJECT_ID(N'Core.NotificationTemplate', N'U') IS NULL
BEGIN
    CREATE TABLE Core.NotificationTemplate (TemplateId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NULL, TemplateCode NVARCHAR(100) NOT NULL, TemplateName NVARCHAR(200) NOT NULL, ChannelCode NVARCHAR(50) NOT NULL, SubjectTemplate NVARCHAR(300) NULL, BodyTemplate NVARCHAR(2000) NOT NULL, IsSystemTemplate BIT NOT NULL DEFAULT 0, IsActive BIT NOT NULL DEFAULT 1, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Core.NotificationAuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE Core.NotificationAuditLog (NotificationAuditLogId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, NotificationId UNIQUEIDENTIFIER NOT NULL, ActionName NVARCHAR(80) NOT NULL, Details NVARCHAR(2000) NOT NULL DEFAULT N'', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Core.NotificationDeliveryAttempt', N'U') IS NULL
BEGIN
    CREATE TABLE Core.NotificationDeliveryAttempt (NotificationDeliveryAttemptId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, NotificationId UNIQUEIDENTIFIER NOT NULL, ProviderName NVARCHAR(120) NOT NULL, ChannelCode NVARCHAR(50) NOT NULL, StatusCode NVARCHAR(60) NOT NULL, AttemptDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), ErrorMessage NVARCHAR(1000) NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Core.NotificationProviderSync', N'U') IS NULL
BEGIN
    CREATE TABLE Core.NotificationProviderSync (NotificationProviderSyncId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, NotificationId UNIQUEIDENTIFIER NOT NULL, ProviderName NVARCHAR(120) NOT NULL, SyncStatus NVARCHAR(60) NOT NULL, ExternalNotificationId NVARCHAR(160) NULL, LastSyncDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), Details NVARCHAR(1000) NOT NULL DEFAULT N'', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Core.NotificationSubscription', N'U') IS NULL
BEGIN
    CREATE TABLE Core.NotificationSubscription (NotificationSubscriptionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, RecipientUserId UNIQUEIDENTIFIER NOT NULL, Category NVARCHAR(80) NOT NULL, ChannelCode NVARCHAR(50) NOT NULL, IsEnabled BIT NOT NULL DEFAULT 1, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), ModifiedDateUtc DATETIME2 NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;
""";

        const string seedSql = """
IF NOT EXISTS (SELECT 1 FROM Core.NotificationTemplate WHERE IsDeleted = 0)
BEGIN
    INSERT INTO Core.NotificationTemplate (TemplateId,TenantId,TemplateCode,TemplateName,ChannelCode,SubjectTemplate,BodyTemplate,IsSystemTemplate,IsActive,CreatedDateUtc,IsDeleted) VALUES
    (NEWID(),NULL,N'COMM_FAILURE',N'Communication Delivery Failure',N'InApp',N'Delivery failed',N'A tenant communication could not be delivered and requires review.',1,1,SYSUTCDATETIME(),0),
    (NEWID(),NULL,N'APPOINTMENT_REMINDER',N'Appointment Reminder',N'Email',N'Upcoming appointment reminder',N'Your upcoming appointment is scheduled and ready for review.',1,1,SYSUTCDATETIME(),0),
    (NEWID(),NULL,N'CAMPAIGN_SYNC',N'Campaign Sync Complete',N'InApp',N'Campaign sync complete',N'Marketing campaign data was synchronized successfully.',1,1,SYSUTCDATETIME(),0);
END;

IF NOT EXISTS (SELECT 1 FROM Core.Notification WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Core.Notification (NotificationId,TenantId,RecipientUserId,TemplateId,ChannelCode,Subject,Body,EntityName,EntityId,StatusCode,IsRead,ReadDateUtc,SentDateUtc,ErrorMessage,Priority,Category,DeliveryProvider,DeliveryStatus,PolicyStatus,SyncStatus,AttemptCount,LastAttemptDateUtc,DeliveredDateUtc,LastSyncedDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES
    (NEWID(),@TenantId,@RecipientUserId,NULL,N'InApp',N'Inbox thread requires review',N'A high priority client thread is approaching SLA and needs tenant admin review.',N'MessageThread',NULL,N'Delivered',0,NULL,DATEADD(minute,-45,SYSUTCDATETIME()),NULL,N'High',N'Communications',N'AMS',N'Delivered',N'Compliant',N'Synced',1,DATEADD(minute,-45,SYSUTCDATETIME()),DATEADD(minute,-45,SYSUTCDATETIME()),SYSUTCDATETIME(),DATEADD(minute,-45,SYSUTCDATETIME()),@RecipientUserId,0),
    (NEWID(),@TenantId,@RecipientUserId,NULL,N'Email',N'Appointment reminder queued',N'A renewal appointment reminder has been queued for delivery.',N'Appointment',NULL,N'Sent',1,DATEADD(hour,-2,SYSUTCDATETIME()),DATEADD(hour,-2,SYSUTCDATETIME()),NULL,N'Normal',N'Appointments',N'AMS Email',N'Sent',N'Compliant',N'Synced',1,DATEADD(hour,-2,SYSUTCDATETIME()),NULL,SYSUTCDATETIME(),DATEADD(hour,-2,SYSUTCDATETIME()),@RecipientUserId,0),
    (NEWID(),@TenantId,@RecipientUserId,NULL,N'SMS',N'Policy service SMS failed',N'SMS delivery failed because the provider returned an invalid destination response.',N'Communication',NULL,N'Failed',0,NULL,DATEADD(hour,-5,SYSUTCDATETIME()),N'Provider rejected destination number.',N'Critical',N'Delivery Failure',N'AMS SMS',N'Failed',N'Review Required',N'Action Required',2,DATEADD(hour,-5,SYSUTCDATETIME()),NULL,SYSUTCDATETIME(),DATEADD(hour,-5,SYSUTCDATETIME()),@RecipientUserId,0),
    (NEWID(),@TenantId,@RecipientUserId,NULL,N'InApp',N'Campaign sync complete',N'Campaign audience, content, and automation records were synchronized.',N'Campaign',NULL,N'Delivered',1,DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(day,-1,SYSUTCDATETIME()),NULL,N'Low',N'Marketing',N'AMS',N'Delivered',N'Compliant',N'Synced',1,DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(day,-1,SYSUTCDATETIME()),SYSUTCDATETIME(),DATEADD(day,-1,SYSUTCDATETIME()),@RecipientUserId,0);
END;

IF NOT EXISTS (SELECT 1 FROM Core.NotificationSubscription WHERE TenantId = @TenantId AND RecipientUserId = @RecipientUserId AND IsDeleted = 0)
BEGIN
    INSERT INTO Core.NotificationSubscription (TenantId,RecipientUserId,Category,ChannelCode,IsEnabled,CreatedDateUtc,IsDeleted) VALUES
    (@TenantId,@RecipientUserId,N'Communications',N'InApp',1,SYSUTCDATETIME(),0),
    (@TenantId,@RecipientUserId,N'Appointments',N'Email',1,SYSUTCDATETIME(),0),
    (@TenantId,@RecipientUserId,N'Delivery Failure',N'InApp',1,SYSUTCDATETIME(),0),
    (@TenantId,@RecipientUserId,N'Marketing',N'InApp',1,SYSUTCDATETIME(),0);
END;
""";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, cancellationToken: cancellationToken));
        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId, RecipientUserId = Guid.Empty }, cancellationToken: cancellationToken));
    }

    public async Task<NotificationDto?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var tenantId = await cn.ExecuteScalarAsync<Guid?>(new CommandDefinition("SELECT TenantId FROM Core.Notification WHERE NotificationId=@NotificationId AND IsDeleted=0", new { NotificationId = notificationId }, cancellationToken: cancellationToken));
        if (tenantId.HasValue)
        {
            await EnsureNotificationSchemaAsync(tenantId.Value, cancellationToken);
        }

        var sql = $"""
            SELECT {SelectColumns}
            FROM Core.Notification
            WHERE NotificationId = @NotificationId AND IsDeleted = 0
            """;
        return await cn.QuerySingleOrDefaultAsync<NotificationDto>(new CommandDefinition(sql, new { NotificationId = notificationId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<NotificationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureNotificationSchemaAsync(tenantId, cancellationToken);
        var sql = $"""
            ;WITH Cte AS (
                SELECT {SelectColumns}
                FROM Core.Notification
                WHERE TenantId = @TenantId AND IsDeleted = 0
                  AND (@SearchTerm IS NULL OR Subject LIKE '%' + @SearchTerm + '%'
                                          OR Body     LIKE '%' + @SearchTerm + '%'
                                           OR StatusCode = @SearchTerm
                                           OR ChannelCode = @SearchTerm
                                           OR Category LIKE '%' + @SearchTerm + '%'
                                           OR Priority = @SearchTerm)
            )
            SELECT * FROM Cte ORDER BY CreatedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Core.Notification
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (@SearchTerm IS NULL OR Subject LIKE '%' + @SearchTerm + '%'
                                      OR Body     LIKE '%' + @SearchTerm + '%'
                                       OR StatusCode = @SearchTerm
                                       OR ChannelCode = @SearchTerm
                                       OR Category LIKE '%' + @SearchTerm + '%'
                                       OR Priority = @SearchTerm);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<NotificationDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<NotificationDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<PagedResult<NotificationTemplateDto>> SearchTemplatesAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureNotificationSchemaAsync(Guid.Empty, cancellationToken);
        const string sql = """
            ;WITH Cte AS (
                SELECT TemplateId, TenantId, TemplateCode, TemplateName, ChannelCode,
                       SubjectTemplate, BodyTemplate, IsSystemTemplate, IsActive, CreatedDateUtc
                FROM Core.NotificationTemplate
                WHERE IsDeleted = 0
                  AND (@SearchTerm IS NULL OR TemplateName   LIKE '%' + @SearchTerm + '%'
                                          OR TemplateCode   LIKE '%' + @SearchTerm + '%'
                                          OR ChannelCode    = @SearchTerm)
            )
            SELECT * FROM Cte ORDER BY IsSystemTemplate DESC, TemplateName ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Core.NotificationTemplate
            WHERE IsDeleted = 0
              AND (@SearchTerm IS NULL OR TemplateName LIKE '%' + @SearchTerm + '%'
                                      OR TemplateCode LIKE '%' + @SearchTerm + '%'
                                      OR ChannelCode  = @SearchTerm);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { SearchTerm = searchTerm, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<NotificationTemplateDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<NotificationTemplateDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureNotificationSchemaAsync(request.TenantId, cancellationToken);
        const string sql = """
            INSERT INTO Core.Notification
                (NotificationId, TenantId, RecipientUserId, TemplateId, ChannelCode, Subject, Body, EntityName, EntityId, StatusCode, IsRead, ReadDateUtc, SentDateUtc, ErrorMessage, Priority, Category, DeliveryProvider, DeliveryStatus, PolicyStatus, SyncStatus, AttemptCount, LastAttemptDateUtc, DeliveredDateUtc, LastSyncedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES
                (@NotificationId, @TenantId, @RecipientUserId, @TemplateId, @ChannelCode, @Subject, @Body, @EntityName, @EntityId, @StatusCode, 0, NULL, @SentDateUtc, @ErrorMessage, @Priority, @Category, @DeliveryProvider, @DeliveryStatus, @PolicyStatus, N'Synced', @AttemptCount, @LastAttemptDateUtc, @DeliveredDateUtc, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);
            INSERT INTO Core.NotificationAuditLog (TenantId,NotificationId,ActionName,Details,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES (@TenantId,@NotificationId,N'Created',N'Notification created from notification service.',SYSUTCDATETIME(),@CreatedByUserId,0);
            INSERT INTO Core.NotificationDeliveryAttempt (TenantId,NotificationId,ProviderName,ChannelCode,StatusCode,AttemptDateUtc,ErrorMessage,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@NotificationId,@DeliveryProvider,@ChannelCode,@DeliveryStatus,SYSUTCDATETIME(),@ErrorMessage,SYSUTCDATETIME(),0);
            """;
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var deliveryStatus = request.StatusCode is "Sent" or "Delivered" ? request.StatusCode : request.StatusCode == "Failed" ? "Failed" : "Queued";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { NotificationId = id, request.TenantId, request.RecipientUserId, request.TemplateId, request.ChannelCode, request.Subject, request.Body, request.EntityName, request.EntityId, request.StatusCode, request.SentDateUtc, request.ErrorMessage, request.CreatedByUserId, request.Priority, request.Category, DeliveryProvider = ResolveProvider(request.ChannelCode), DeliveryStatus = deliveryStatus, PolicyStatus = string.IsNullOrWhiteSpace(request.ErrorMessage) ? "Compliant" : "Review Required", AttemptCount = request.SentDateUtc.HasValue ? 1 : 0, LastAttemptDateUtc = request.SentDateUtc, DeliveredDateUtc = request.StatusCode == "Delivered" ? request.SentDateUtc : null }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task SetReadAsync(Guid notificationId, bool isRead, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var tenantId = await cn.ExecuteScalarAsync<Guid?>(new CommandDefinition("SELECT TenantId FROM Core.Notification WHERE NotificationId=@NotificationId AND IsDeleted=0", new { NotificationId = notificationId }, cancellationToken: cancellationToken));
        if (tenantId.HasValue) await EnsureNotificationSchemaAsync(tenantId.Value, cancellationToken);
        const string sql = "UPDATE Core.Notification SET IsRead = @IsRead, ReadDateUtc = CASE WHEN @IsRead = 1 THEN SYSUTCDATETIME() ELSE NULL END, ModifiedDateUtc=SYSUTCDATETIME(), LastSyncedDateUtc=SYSUTCDATETIME() WHERE NotificationId = @NotificationId AND IsDeleted = 0; INSERT INTO Core.NotificationAuditLog (TenantId,NotificationId,ActionName,Details,CreatedDateUtc,IsDeleted) SELECT TenantId,NotificationId,CASE WHEN @IsRead=1 THEN N'Marked Read' ELSE N'Marked Unread' END,N'Read state changed.',SYSUTCDATETIME(),0 FROM Core.Notification WHERE NotificationId=@NotificationId;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { NotificationId = notificationId, IsRead = isRead }, cancellationToken: cancellationToken));
    }

    public async Task SetStatusAsync(Guid notificationId, string statusCode, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var tenantId = await cn.ExecuteScalarAsync<Guid?>(new CommandDefinition("SELECT TenantId FROM Core.Notification WHERE NotificationId=@NotificationId AND IsDeleted=0", new { NotificationId = notificationId }, cancellationToken: cancellationToken));
        if (tenantId.HasValue) await EnsureNotificationSchemaAsync(tenantId.Value, cancellationToken);
        const string sql = "UPDATE Core.Notification SET StatusCode = @StatusCode, DeliveryStatus=@StatusCode, IsRead = 1, ReadDateUtc = COALESCE(ReadDateUtc, SYSUTCDATETIME()), DeliveredDateUtc=CASE WHEN @StatusCode IN ('Delivered','Sent') THEN COALESCE(DeliveredDateUtc,SYSUTCDATETIME()) ELSE DeliveredDateUtc END, SyncStatus=N'Synced', ModifiedDateUtc=SYSUTCDATETIME(), LastSyncedDateUtc=SYSUTCDATETIME() WHERE NotificationId = @NotificationId AND IsDeleted = 0; INSERT INTO Core.NotificationAuditLog (TenantId,NotificationId,ActionName,Details,CreatedDateUtc,IsDeleted) SELECT TenantId,NotificationId,N'Status Updated',@StatusCode,SYSUTCDATETIME(),0 FROM Core.Notification WHERE NotificationId=@NotificationId;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { NotificationId = notificationId, StatusCode = statusCode }, cancellationToken: cancellationToken));
    }

    public async Task RetryAsync(Guid notificationId, string? providerName = null, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var current = await cn.QuerySingleOrDefaultAsync<NotificationDto>(new CommandDefinition($"SELECT {SelectColumns} FROM Core.Notification WHERE NotificationId=@NotificationId AND IsDeleted=0", new { NotificationId = notificationId }, cancellationToken: cancellationToken));
        if (current is null) return;
        await EnsureNotificationSchemaAsync(current.TenantId, cancellationToken);
        var provider = string.IsNullOrWhiteSpace(providerName) ? ResolveProvider(current.ChannelCode) : providerName;
        const string sql = """
UPDATE Core.Notification SET StatusCode=N'Queued', DeliveryStatus=N'Retrying', DeliveryProvider=@ProviderName, AttemptCount=AttemptCount+1, LastAttemptDateUtc=SYSUTCDATETIME(), ErrorMessage=NULL, SyncStatus=N'Synced', LastSyncedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME() WHERE NotificationId=@NotificationId AND IsDeleted=0;
INSERT INTO Core.NotificationDeliveryAttempt (TenantId,NotificationId,ProviderName,ChannelCode,StatusCode,AttemptDateUtc,ErrorMessage,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@NotificationId,@ProviderName,@ChannelCode,N'Retrying',SYSUTCDATETIME(),NULL,SYSUTCDATETIME(),0);
INSERT INTO Core.NotificationProviderSync (TenantId,NotificationId,ProviderName,SyncStatus,ExternalNotificationId,LastSyncDateUtc,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@NotificationId,@ProviderName,N'Retrying',CONVERT(nvarchar(160),@NotificationId),SYSUTCDATETIME(),N'Retry queued from Tenant Notification Control Center.',SYSUTCDATETIME(),0);
INSERT INTO Core.NotificationAuditLog (TenantId,NotificationId,ActionName,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@NotificationId,N'Retry Queued',@ProviderName,SYSUTCDATETIME(),0);
""";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { current.TenantId, current.NotificationId, ProviderName = provider, current.ChannelCode }, cancellationToken: cancellationToken));
    }

    public async Task MarkAllReadAsync(Guid tenantId, Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        await EnsureNotificationSchemaAsync(tenantId, cancellationToken);
        const string sql = "UPDATE Core.Notification SET IsRead = 1, ReadDateUtc = COALESCE(ReadDateUtc, SYSUTCDATETIME()), ModifiedDateUtc=SYSUTCDATETIME(), LastSyncedDateUtc=SYSUTCDATETIME() WHERE TenantId = @TenantId AND (@RecipientUserId='00000000-0000-0000-0000-000000000000' OR RecipientUserId = @RecipientUserId) AND IsRead = 0 AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, RecipientUserId = recipientUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.Notification SET IsDeleted = 1, ModifiedDateUtc=SYSUTCDATETIME() WHERE NotificationId = @NotificationId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { NotificationId = notificationId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteReadAsync(Guid tenantId, Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        await EnsureNotificationSchemaAsync(tenantId, cancellationToken);
        const string sql = "UPDATE Core.Notification SET IsDeleted = 1, ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId = @TenantId AND (@RecipientUserId='00000000-0000-0000-0000-000000000000' OR RecipientUserId = @RecipientUserId) AND IsRead = 1 AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, RecipientUserId = recipientUserId }, cancellationToken: cancellationToken));
    }

    private static string ResolveProvider(string channelCode) => channelCode switch
    {
        "Email" => "AMS Email",
        "SMS" => "AMS SMS",
        "Portal" => "AMS Portal",
        _ => "AMS"
    };
}
