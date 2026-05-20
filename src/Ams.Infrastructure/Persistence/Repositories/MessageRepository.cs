using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Communications;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class MessageRepository : IMessageRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public MessageRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string ThreadColumns = @"
        t.ThreadId, t.TenantId, t.AccountName, t.AccountId,
        t.ContactName, t.ContactEmail, t.ContactPhone,
        t.Channel, t.Subject, t.BodyPreview, t.Status, t.Priority,
        t.AssignedTo, t.Producer, t.Branch,
        t.IsRead, t.IsEscalated, t.OptedOut, t.MessageCount,
        t.LastActivityAt, t.Sentiment, t.CsrOwner, t.AiSummary,
        t.QueueName, t.SlaStatus, t.SlaMinutesRemaining, t.DueDateUtc,
        t.ComplianceStatus, t.SourceSystem, t.LastSyncedDateUtc";

    private async Task EnsureCommandCenterSchemaAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Comms') EXEC(N'CREATE SCHEMA Comms');

IF OBJECT_ID(N'Comms.MessageThread', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.MessageThread (ThreadId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, AccountName NVARCHAR(200) NOT NULL, AccountId NVARCHAR(80) NULL, ContactName NVARCHAR(200) NOT NULL DEFAULT N'', ContactEmail NVARCHAR(254) NULL, ContactPhone NVARCHAR(50) NULL, Channel NVARCHAR(80) NOT NULL, Subject NVARCHAR(300) NOT NULL, BodyPreview NVARCHAR(1000) NOT NULL DEFAULT N'', Status NVARCHAR(50) NOT NULL DEFAULT N'Open', Priority NVARCHAR(30) NOT NULL DEFAULT N'Normal', AssignedTo NVARCHAR(150) NULL, Producer NVARCHAR(150) NULL, Branch NVARCHAR(150) NULL, IsRead BIT NOT NULL DEFAULT 0, IsEscalated BIT NOT NULL DEFAULT 0, OptedOut BIT NOT NULL DEFAULT 0, MessageCount INT NOT NULL DEFAULT 0, LastActivityAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), Sentiment NVARCHAR(50) NOT NULL DEFAULT N'Neutral', CsrOwner NVARCHAR(150) NULL, AiSummary NVARCHAR(2000) NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Comms.ThreadMessage', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.ThreadMessage (MessageId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, ThreadId UNIQUEIDENTIFIER NOT NULL, SenderName NVARCHAR(150) NOT NULL, Channel NVARCHAR(80) NOT NULL, Direction NVARCHAR(30) NOT NULL DEFAULT N'Inbound', Body NVARCHAR(MAX) NOT NULL, SentAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), DeliveryStatus NVARCHAR(50) NOT NULL DEFAULT N'Delivered', IsAutomated BIT NOT NULL DEFAULT 0, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF COL_LENGTH('Comms.MessageThread','QueueName') IS NULL ALTER TABLE Comms.MessageThread ADD QueueName NVARCHAR(120) NOT NULL CONSTRAINT DF_MessageThread_QueueName DEFAULT N'General Inbox';
IF COL_LENGTH('Comms.MessageThread','SlaStatus') IS NULL ALTER TABLE Comms.MessageThread ADD SlaStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_MessageThread_SlaStatus DEFAULT N'On Track';
IF COL_LENGTH('Comms.MessageThread','SlaMinutesRemaining') IS NULL ALTER TABLE Comms.MessageThread ADD SlaMinutesRemaining INT NOT NULL CONSTRAINT DF_MessageThread_SlaMinutesRemaining DEFAULT 240;
IF COL_LENGTH('Comms.MessageThread','DueDateUtc') IS NULL ALTER TABLE Comms.MessageThread ADD DueDateUtc DATETIME2 NULL;
IF COL_LENGTH('Comms.MessageThread','ComplianceStatus') IS NULL ALTER TABLE Comms.MessageThread ADD ComplianceStatus NVARCHAR(80) NOT NULL CONSTRAINT DF_MessageThread_ComplianceStatus DEFAULT N'Clear';
IF COL_LENGTH('Comms.MessageThread','SourceSystem') IS NULL ALTER TABLE Comms.MessageThread ADD SourceSystem NVARCHAR(80) NOT NULL CONSTRAINT DF_MessageThread_SourceSystem DEFAULT N'AMS';
IF COL_LENGTH('Comms.MessageThread','LastSyncedDateUtc') IS NULL ALTER TABLE Comms.MessageThread ADD LastSyncedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_MessageThread_LastSyncedDateUtc DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('Comms.MessageThread','CreatedDateUtc') IS NULL ALTER TABLE Comms.MessageThread ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_MessageThread_CreatedDateUtc DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('Comms.MessageThread','ModifiedDateUtc') IS NULL ALTER TABLE Comms.MessageThread ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH('Comms.MessageThread','IsDeleted') IS NULL ALTER TABLE Comms.MessageThread ADD IsDeleted BIT NOT NULL CONSTRAINT DF_MessageThread_IsDeleted DEFAULT 0;
IF COL_LENGTH('Comms.ThreadMessage','ExternalMessageId') IS NULL ALTER TABLE Comms.ThreadMessage ADD ExternalMessageId NVARCHAR(150) NOT NULL CONSTRAINT DF_ThreadMessage_ExternalMessageId DEFAULT N'';
IF COL_LENGTH('Comms.ThreadMessage','ProviderName') IS NULL ALTER TABLE Comms.ThreadMessage ADD ProviderName NVARCHAR(80) NOT NULL CONSTRAINT DF_ThreadMessage_ProviderName DEFAULT N'AMS';
IF COL_LENGTH('Comms.ThreadMessage','DeliveredAtUtc') IS NULL ALTER TABLE Comms.ThreadMessage ADD DeliveredAtUtc DATETIME2 NULL;
IF COL_LENGTH('Comms.ThreadMessage','ReadAtUtc') IS NULL ALTER TABLE Comms.ThreadMessage ADD ReadAtUtc DATETIME2 NULL;
IF COL_LENGTH('Comms.ThreadMessage','CreatedDateUtc') IS NULL ALTER TABLE Comms.ThreadMessage ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ThreadMessage_CreatedDateUtc DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('Comms.ThreadMessage','ModifiedDateUtc') IS NULL ALTER TABLE Comms.ThreadMessage ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH('Comms.ThreadMessage','IsDeleted') IS NULL ALTER TABLE Comms.ThreadMessage ADD IsDeleted BIT NOT NULL CONSTRAINT DF_ThreadMessage_IsDeleted DEFAULT 0;

IF OBJECT_ID(N'Comms.CommunicationQueue', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.CommunicationQueue (QueueId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, Name NVARCHAR(120) NOT NULL, Channel NVARCHAR(80) NOT NULL, OwnerRole NVARCHAR(120) NOT NULL, SlaMinutes INT NOT NULL DEFAULT 240, IsActive BIT NOT NULL DEFAULT 1, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Comms.CommunicationAuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.CommunicationAuditLog (AuditLogId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, ThreadId UNIQUEIDENTIFIER NULL, ActionName NVARCHAR(80) NOT NULL, ActorName NVARCHAR(150) NOT NULL, Details NVARCHAR(1000) NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Comms.CommunicationProviderSync', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.CommunicationProviderSync (ProviderSyncId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, ProviderName NVARCHAR(80) NOT NULL, Channel NVARCHAR(80) NOT NULL, SyncStatus NVARCHAR(50) NOT NULL, LastSyncDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), LastError NVARCHAR(1000) NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF COL_LENGTH('Comms.CommunicationQueue','IsDeleted') IS NULL ALTER TABLE Comms.CommunicationQueue ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommunicationQueue_IsDeleted DEFAULT 0;
IF COL_LENGTH('Comms.CommunicationAuditLog','IsDeleted') IS NULL ALTER TABLE Comms.CommunicationAuditLog ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommunicationAuditLog_IsDeleted DEFAULT 0;
IF COL_LENGTH('Comms.CommunicationProviderSync','IsDeleted') IS NULL ALTER TABLE Comms.CommunicationProviderSync ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommunicationProviderSync_IsDeleted DEFAULT 0;";

        const string seedSql = @"
IF NOT EXISTS (SELECT 1 FROM Comms.CommunicationQueue WHERE TenantId=@TenantId AND IsDeleted=0)
BEGIN
    INSERT INTO Comms.CommunicationQueue (QueueId,TenantId,Name,Channel,OwnerRole,SlaMinutes,IsActive,IsDeleted) VALUES
    (NEWID(),@TenantId,N'General Inbox',N'Email',N'CSR',240,1,0),
    (NEWID(),@TenantId,N'Claims Response',N'Email',N'Claims',120,1,0),
    (NEWID(),@TenantId,N'SMS Service Desk',N'SMS',N'CSR',60,1,0),
    (NEWID(),@TenantId,N'Portal Messages',N'Portal Message',N'Account Manager',240,1,0),
    (NEWID(),@TenantId,N'Escalations',N'Internal Note',N'Supervisor',60,1,0);
END;

IF NOT EXISTS (SELECT 1 FROM Comms.CommunicationProviderSync WHERE TenantId=@TenantId AND IsDeleted=0)
BEGIN
    INSERT INTO Comms.CommunicationProviderSync (ProviderSyncId,TenantId,ProviderName,Channel,SyncStatus,LastSyncDateUtc,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Microsoft Graph',N'Email',N'Healthy',DATEADD(minute,-8,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Twilio',N'SMS',N'Healthy',DATEADD(minute,-5,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'AMS Portal',N'Portal Message',N'Healthy',DATEADD(minute,-3,SYSUTCDATETIME()),0);
END;

IF NOT EXISTS (SELECT 1 FROM Comms.MessageThread WHERE TenantId=@TenantId AND IsDeleted=0)
BEGIN
    DECLARE @T1 UNIQUEIDENTIFIER = NEWID(), @T2 UNIQUEIDENTIFIER = NEWID(), @T3 UNIQUEIDENTIFIER = NEWID(), @T4 UNIQUEIDENTIFIER = NEWID();
    INSERT INTO Comms.MessageThread (ThreadId,TenantId,AccountName,AccountId,ContactName,ContactEmail,ContactPhone,Channel,Subject,BodyPreview,Status,Priority,AssignedTo,Producer,Branch,IsRead,IsEscalated,OptedOut,MessageCount,LastActivityAt,Sentiment,CsrOwner,AiSummary,QueueName,SlaStatus,SlaMinutesRemaining,DueDateUtc,ComplianceStatus,SourceSystem,LastSyncedDateUtc,IsDeleted) VALUES
    (@T1,@TenantId,N'Sullivan Mfg. LLC',N'ACCT-1001',N'Mark Sullivan',N'mark@sullivanmfg.local',N'555-0101',N'Email',N'Certificate request for new vendor',N'Please send a certificate of insurance for Apex Distribution by end of day.',N'Open',N'High',N'Sarah Kim',N'Beth Nguyen',N'Dallas',0,0,0,2,DATEADD(hour,-2,SYSUTCDATETIME()),N'Neutral',N'Maria Santos',N'Client needs COI today for a vendor onboarding deadline.',N'General Inbox',N'At Risk',95,DATEADD(minute,95,SYSUTCDATETIME()),N'Clear',N'Microsoft Graph',DATEADD(minute,-8,SYSUTCDATETIME()),0),
    (@T2,@TenantId,N'Bridgewater Hotels',N'ACCT-1002',N'Anna Reeves',N'anna@bridgewater.local',N'555-0102',N'SMS',N'Claim status follow-up',N'Any update on claim CLM-44219? Guest is asking for documentation.',N'Open',N'Urgent',N'',N'Jake Park',N'Phoenix',0,1,0,3,DATEADD(hour,-7,SYSUTCDATETIME()),N'Urgent',N'Kevin Obi',N'Escalated claim follow-up with unanswered inbound SMS.',N'Claims Response',N'Breached',-180,DATEADD(hour,-3,SYSUTCDATETIME()),N'Clear',N'Twilio',DATEADD(minute,-5,SYSUTCDATETIME()),0),
    (@T3,@TenantId,N'Apex Medical Group',N'ACCT-1003',N'Dr. Patel',N'patel@apexmedical.local',N'555-0103',N'Portal Message',N'Renewal proposal questions',N'Can you clarify the cyber liability retention and employee benefits options?',N'Pending',N'Normal',N'Lisa Chen',N'Sara Kim',N'Irvine',1,0,0,4,DATEADD(day,-1,SYSUTCDATETIME()),N'Positive',N'Lisa Chen',N'Portal discussion about renewal proposal details.',N'Portal Messages',N'On Track',360,DATEADD(hour,6,SYSUTCDATETIME()),N'Clear',N'AMS Portal',DATEADD(minute,-3,SYSUTCDATETIME()),0),
    (@T4,@TenantId,N'Metro Freight Co.',N'ACCT-1004',N'Carlos Mendez',N'carlos@metrofreight.local',N'555-0104',N'Internal Note',N'Coverage concern escalated by producer',N'Producer flagged a possible coverage gap before renewal meeting.',N'Open',N'High',N'Diana Perez',N'Beth Nguyen',N'Chicago',0,1,0,1,DATEADD(hour,-4,SYSUTCDATETIME()),N'Negative',N'Diana Perez',N'Internal escalation on possible coverage gap.',N'Escalations',N'At Risk',45,DATEADD(minute,45,SYSUTCDATETIME()),N'Review Required',N'AMS',SYSUTCDATETIME(),0);

    INSERT INTO Comms.ThreadMessage (MessageId,ThreadId,SenderName,Channel,Direction,Body,SentAt,DeliveryStatus,IsAutomated,ProviderName,ExternalMessageId,DeliveredAtUtc,IsDeleted) VALUES
    (NEWID(),@T1,N'Mark Sullivan',N'Email',N'Inbound',N'Please send a certificate of insurance for Apex Distribution by end of day.',DATEADD(hour,-2,SYSUTCDATETIME()),N'Delivered',0,N'Microsoft Graph',N'graph-seed-001',DATEADD(hour,-2,SYSUTCDATETIME()),0),
    (NEWID(),@T1,N'Sarah Kim',N'Email',N'Outbound',N'We received the request and are preparing the certificate now.',DATEADD(minute,-75,SYSUTCDATETIME()),N'Delivered',0,N'Microsoft Graph',N'graph-seed-002',DATEADD(minute,-74,SYSUTCDATETIME()),0),
    (NEWID(),@T2,N'Anna Reeves',N'SMS',N'Inbound',N'Any update on claim CLM-44219? Guest is asking for documentation.',DATEADD(hour,-7,SYSUTCDATETIME()),N'Delivered',0,N'Twilio',N'twilio-seed-001',DATEADD(hour,-7,SYSUTCDATETIME()),0),
    (NEWID(),@T2,N'System',N'Internal Note',N'Outbound',N'Escalated to claims supervisor due to SLA breach risk.',DATEADD(hour,-5,SYSUTCDATETIME()),N'Delivered',1,N'AMS',N'',DATEADD(hour,-5,SYSUTCDATETIME()),0),
    (NEWID(),@T2,N'Anna Reeves',N'SMS',N'Inbound',N'Please call me when available.',DATEADD(hour,-4,SYSUTCDATETIME()),N'Delivered',0,N'Twilio',N'twilio-seed-002',DATEADD(hour,-4,SYSUTCDATETIME()),0),
    (NEWID(),@T3,N'Dr. Patel',N'Portal Message',N'Inbound',N'Can you clarify the cyber liability retention and employee benefits options?',DATEADD(day,-1,SYSUTCDATETIME()),N'Delivered',0,N'AMS Portal',N'portal-seed-001',DATEADD(day,-1,SYSUTCDATETIME()),0),
    (NEWID(),@T3,N'Lisa Chen',N'Portal Message',N'Outbound',N'I added notes to the proposal and can review live tomorrow.',DATEADD(hour,-20,SYSUTCDATETIME()),N'Delivered',0,N'AMS Portal',N'portal-seed-002',DATEADD(hour,-20,SYSUTCDATETIME()),0),
    (NEWID(),@T4,N'Beth Nguyen',N'Internal Note',N'Inbound',N'Producer flagged a possible coverage gap before renewal meeting.',DATEADD(hour,-4,SYSUTCDATETIME()),N'Delivered',0,N'AMS',N'',DATEADD(hour,-4,SYSUTCDATETIME()),0);

    INSERT INTO Comms.CommunicationAuditLog (AuditLogId,TenantId,ThreadId,ActionName,ActorName,Details,IsDeleted) VALUES
    (NEWID(),@TenantId,@T1,N'Seeded',N'System',N'Initial enterprise command center sample conversation.',0),
    (NEWID(),@TenantId,@T2,N'Escalated',N'System',N'Seeded urgent claim escalation.',0),
    (NEWID(),@TenantId,@T3,N'Synced',N'AMS Portal',N'Seeded portal message sync.',0),
    (NEWID(),@TenantId,@T4,N'Escalated',N'System',N'Seeded internal escalation.',0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MessageThreadDto>> GetThreadsAsync(GetThreadsRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCommandCenterSchemaAsync(request.TenantId, cancellationToken);
        var channel = NormalizeFilter(request.Channel);
        var status = NormalizeFilter(request.Status);
        var assignedTo = NormalizeFilter(request.AssignedTo);
        var searchTerm = NormalizeFilter(request.SearchTerm);

        var sql = $@"
SELECT {ThreadColumns}
FROM Comms.MessageThread t
WHERE t.TenantId = @TenantId AND t.IsDeleted = 0
  AND (@Channel IS NULL OR t.Channel = @Channel)
  AND (@Status IS NULL OR t.Status = @Status)
  AND (@AssignedTo IS NULL OR t.AssignedTo = @AssignedTo)
  AND (@SearchTerm IS NULL OR t.AccountName LIKE '%' + @SearchTerm + '%'
       OR t.Subject LIKE '%' + @SearchTerm + '%'
       OR t.ContactName LIKE '%' + @SearchTerm + '%')
ORDER BY t.LastActivityAt DESC;

SELECT m.MessageId, m.ThreadId, m.SenderName, m.Channel, m.Direction,
       m.Body, m.SentAt, m.DeliveryStatus, m.IsAutomated,
       m.ExternalMessageId, m.ProviderName, m.DeliveredAtUtc, m.ReadAtUtc
FROM Comms.ThreadMessage m
INNER JOIN Comms.MessageThread t ON t.ThreadId = m.ThreadId
WHERE t.TenantId = @TenantId AND t.IsDeleted = 0
ORDER BY m.SentAt ASC;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { request.TenantId, Channel = channel, Status = status, AssignedTo = assignedTo, SearchTerm = searchTerm },
            cancellationToken: cancellationToken));

        var threads = (await multi.ReadAsync<MessageThreadDto>()).AsList();
        var messages = (await multi.ReadAsync<ThreadMessageDto>()).AsList();

        var lookup = messages.GroupBy(m => m.ThreadId)
                             .ToDictionary(g => g.Key, g => (IReadOnlyList<ThreadMessageDto>)g.ToList());

        return threads.Select(t => new MessageThreadDto
        {
            ThreadId       = t.ThreadId,
            TenantId       = t.TenantId,
            AccountName    = t.AccountName,
            AccountId      = t.AccountId,
            ContactName    = t.ContactName,
            ContactEmail   = t.ContactEmail,
            ContactPhone   = t.ContactPhone,
            Channel        = t.Channel,
            Subject        = t.Subject,
            BodyPreview    = t.BodyPreview,
            Status         = t.Status,
            Priority       = t.Priority,
            AssignedTo     = t.AssignedTo,
            Producer       = t.Producer,
            Branch         = t.Branch,
            IsRead         = t.IsRead,
            IsEscalated    = t.IsEscalated,
            OptedOut       = t.OptedOut,
            MessageCount   = t.MessageCount,
            LastActivityAt = t.LastActivityAt,
            Sentiment      = t.Sentiment,
            CsrOwner       = t.CsrOwner,
            AiSummary      = t.AiSummary,
            QueueName      = t.QueueName,
            SlaStatus      = t.SlaStatus,
            SlaMinutesRemaining = t.SlaMinutesRemaining,
            DueDateUtc     = t.DueDateUtc,
            ComplianceStatus = t.ComplianceStatus,
            SourceSystem   = t.SourceSystem,
            LastSyncedDateUtc = t.LastSyncedDateUtc,
            Messages       = lookup.GetValueOrDefault(t.ThreadId, [])
        }).ToList();
    }

    private static string? NormalizeFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    public async Task<MessageThreadDto?> GetThreadByIdAsync(Guid threadId, CancellationToken cancellationToken = default)
    {
        await EnsureCommandCenterSchemaForThreadAsync(threadId, cancellationToken);
        var sql = $@"
SELECT {ThreadColumns} FROM Comms.MessageThread t WHERE t.ThreadId = @ThreadId AND t.IsDeleted = 0;

SELECT m.MessageId, m.ThreadId, m.SenderName, m.Channel, m.Direction,
       m.Body, m.SentAt, m.DeliveryStatus, m.IsAutomated,
       m.ExternalMessageId, m.ProviderName, m.DeliveredAtUtc, m.ReadAtUtc
FROM Comms.ThreadMessage m WHERE m.ThreadId = @ThreadId ORDER BY m.SentAt ASC;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { ThreadId = threadId }, cancellationToken: cancellationToken));

        var thread = await multi.ReadSingleOrDefaultAsync<MessageThreadDto>();
        if (thread is null) return null;
        var messages = (await multi.ReadAsync<ThreadMessageDto>()).AsList();
        return new MessageThreadDto
        {
            ThreadId       = thread.ThreadId,
            TenantId       = thread.TenantId,
            AccountName    = thread.AccountName,
            AccountId      = thread.AccountId,
            ContactName    = thread.ContactName,
            ContactEmail   = thread.ContactEmail,
            ContactPhone   = thread.ContactPhone,
            Channel        = thread.Channel,
            Subject        = thread.Subject,
            BodyPreview    = thread.BodyPreview,
            Status         = thread.Status,
            Priority       = thread.Priority,
            AssignedTo     = thread.AssignedTo,
            Producer       = thread.Producer,
            Branch         = thread.Branch,
            IsRead         = thread.IsRead,
            IsEscalated    = thread.IsEscalated,
            OptedOut       = thread.OptedOut,
            MessageCount   = thread.MessageCount,
            LastActivityAt = thread.LastActivityAt,
            Sentiment      = thread.Sentiment,
            CsrOwner       = thread.CsrOwner,
            AiSummary      = thread.AiSummary,
            QueueName      = thread.QueueName,
            SlaStatus      = thread.SlaStatus,
            SlaMinutesRemaining = thread.SlaMinutesRemaining,
            DueDateUtc     = thread.DueDateUtc,
            ComplianceStatus = thread.ComplianceStatus,
            SourceSystem   = thread.SourceSystem,
            LastSyncedDateUtc = thread.LastSyncedDateUtc,
            Messages       = messages
        };
    }

    private async Task EnsureCommandCenterSchemaForThreadAsync(Guid threadId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT TOP 1 TenantId FROM Comms.MessageThread WHERE ThreadId=@ThreadId AND IsDeleted=0;";
        try
        {
            using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var tenantId = await cn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, new { ThreadId = threadId }, cancellationToken: cancellationToken));
            if (tenantId.HasValue) await EnsureCommandCenterSchemaAsync(tenantId.Value, cancellationToken);
        }
        catch
        {
            // Thread lookup can proceed to return not found when schema is absent.
        }
    }

    public async Task<Guid> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCommandCenterSchemaAsync(request.TenantId, cancellationToken);
        var threadId  = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var preview   = request.Body.Length > 120 ? request.Body[..120] : request.Body;
        var sql = @"
INSERT INTO Comms.MessageThread
    (ThreadId, TenantId, AccountName, AccountId, Channel, Subject, BodyPreview,
     Status, Priority, AssignedTo, IsRead, IsEscalated, OptedOut, MessageCount,
     LastActivityAt, Sentiment, QueueName, SlaStatus, SlaMinutesRemaining, DueDateUtc,
     ComplianceStatus, SourceSystem, LastSyncedDateUtc, IsDeleted, CreatedDateUtc)
VALUES
    (@ThreadId, @TenantId, @AccountName, @AccountId, @Channel, @Subject, @Preview,
     'Open', @Priority, @AssignedTo, 0, 0, 0, 1, SYSUTCDATETIME(), 'Neutral', @QueueName, 'On Track', @SlaMinutesRemaining,
     DATEADD(minute,@SlaMinutesRemaining,SYSUTCDATETIME()), @ComplianceStatus, @SourceSystem, SYSUTCDATETIME(), 0, SYSUTCDATETIME());

INSERT INTO Comms.ThreadMessage
    (MessageId, ThreadId, SenderName, Channel, Direction, Body, SentAt, DeliveryStatus, IsAutomated, ProviderName, ExternalMessageId, DeliveredAtUtc)
VALUES
    (@MessageId, @ThreadId, @SenderName, @Channel, 'Outbound', @Body, SYSUTCDATETIME(), 'Delivered', 0, @ProviderName, @ExternalMessageId, SYSUTCDATETIME());

INSERT INTO Comms.CommunicationAuditLog (AuditLogId,TenantId,ThreadId,ActionName,ActorName,Details,IsDeleted)
VALUES (NEWID(),@TenantId,@ThreadId,N'Sent',@SenderName,CONCAT('Created ',@Channel,' thread in ',@QueueName),0);

UPDATE Comms.CommunicationProviderSync
SET SyncStatus=N'Healthy', LastSyncDateUtc=SYSUTCDATETIME(), LastError=NULL, ModifiedDateUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND Channel=@Channel AND IsDeleted=0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ThreadId  = threadId,
            MessageId = messageId,
            request.TenantId,
            request.AccountName,
            AccountId = request.AccountId,
            request.Channel,
            Subject = string.IsNullOrEmpty(request.Subject) ? "(No subject)" : request.Subject,
            Preview = preview,
            request.Priority,
            request.AssignedTo,
            SenderName = request.AssignedTo ?? "Agent",
            Body = request.Body,
            QueueName = string.IsNullOrWhiteSpace(request.QueueName) ? ResolveQueueName(request.Channel, request.Priority) : request.QueueName,
            SlaMinutesRemaining = ResolveSlaMinutes(request.Channel, request.Priority),
            ComplianceStatus = string.IsNullOrWhiteSpace(request.ComplianceStatus) ? "Clear" : request.ComplianceStatus,
            SourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? ResolveProviderName(request.Channel) : request.SourceSystem,
            ProviderName = ResolveProviderName(request.Channel),
            ExternalMessageId = $"ams-{messageId:N}"
        }, cancellationToken: cancellationToken));
        return threadId;
    }

    public async Task<Guid> ReplyAsync(ReplyMessageRequest request, CancellationToken cancellationToken = default)
    {
        var messageId = Guid.NewGuid();
        var preview   = request.Body.Length > 120 ? request.Body[..120] : request.Body;
        var sql = @"
INSERT INTO Comms.ThreadMessage
    (MessageId, ThreadId, SenderName, Channel, Direction, Body, SentAt, DeliveryStatus, IsAutomated, ProviderName, ExternalMessageId, DeliveredAtUtc)
VALUES
    (@MessageId, @ThreadId, @SenderName, @Channel, 'Outbound', @Body, SYSUTCDATETIME(), 'Delivered', 0, @ProviderName, @ExternalMessageId, SYSUTCDATETIME());

UPDATE Comms.MessageThread
SET MessageCount    = MessageCount + 1,
    BodyPreview     = @Preview,
    LastActivityAt  = SYSUTCDATETIME(),
    Status          = 'Pending',
    SlaStatus       = 'On Track',
    SlaMinutesRemaining = CASE WHEN SlaMinutesRemaining < 0 THEN 240 ELSE SlaMinutesRemaining END,
    LastSyncedDateUtc = SYSUTCDATETIME(),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE ThreadId = @ThreadId;";

        sql += @"

INSERT INTO Comms.CommunicationAuditLog (AuditLogId,TenantId,ThreadId,ActionName,ActorName,Details,IsDeleted)
SELECT NEWID(), TenantId, @ThreadId, N'Replied', @SenderName, CONCAT('Sent ',@Channel,' reply'), 0 FROM Comms.MessageThread WHERE ThreadId=@ThreadId;

UPDATE s SET SyncStatus=N'Healthy', LastSyncDateUtc=SYSUTCDATETIME(), LastError=NULL, ModifiedDateUtc=SYSUTCDATETIME()
FROM Comms.CommunicationProviderSync s
INNER JOIN Comms.MessageThread t ON t.TenantId=s.TenantId
WHERE t.ThreadId=@ThreadId AND s.Channel=@Channel AND s.IsDeleted=0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            MessageId  = messageId,
            request.ThreadId,
            request.SenderName,
            request.Channel,
            Body = request.Body,
            Preview = preview,
            ProviderName = string.IsNullOrWhiteSpace(request.ProviderName) ? ResolveProviderName(request.Channel) : request.ProviderName,
            ExternalMessageId = string.IsNullOrWhiteSpace(request.ExternalMessageId) ? $"ams-{messageId:N}" : request.ExternalMessageId
        }, cancellationToken: cancellationToken));
        return messageId;
    }

    public async Task AssignAsync(AssignThreadRequest request, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE Comms.MessageThread
SET AssignedTo = @AssignedTo, QueueName = CASE WHEN QueueName = '' THEN 'General Inbox' ELSE QueueName END, LastSyncedDateUtc = SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME()
WHERE ThreadId = @ThreadId AND IsDeleted = 0;

INSERT INTO Comms.ThreadMessage
    (MessageId, ThreadId, SenderName, Channel, Direction, Body, SentAt, DeliveryStatus, IsAutomated, ProviderName, DeliveredAtUtc)
VALUES
    (NEWID(), @ThreadId, 'System', 'Internal Note', 'Outbound',
     CONCAT('Assigned to ', @AssignedTo, CASE WHEN @Note IS NULL OR @Note = '' THEN '' ELSE '. Note: ' + @Note END),
     SYSUTCDATETIME(), 'Delivered', 1, 'AMS', SYSUTCDATETIME());

UPDATE Comms.MessageThread
SET MessageCount = MessageCount + 1, LastActivityAt = SYSUTCDATETIME()
WHERE ThreadId = @ThreadId;

INSERT INTO Comms.CommunicationAuditLog (AuditLogId,TenantId,ThreadId,ActionName,ActorName,Details,IsDeleted)
SELECT NEWID(), TenantId, @ThreadId, N'Assigned', @AssignedTo, @Note, 0 FROM Comms.MessageThread WHERE ThreadId=@ThreadId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.ThreadId, request.AssignedTo, Note = request.Note }, cancellationToken: cancellationToken));
    }

    public async Task EscalateAsync(EscalateThreadRequest request, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE Comms.MessageThread
SET IsEscalated = 1, Priority = 'Urgent', AssignedTo = @EscalateTo, QueueName='Escalations', SlaStatus='At Risk', SlaMinutesRemaining = CASE WHEN SlaMinutesRemaining > 60 THEN 60 ELSE SlaMinutesRemaining END, DueDateUtc=DATEADD(minute,60,SYSUTCDATETIME()), LastSyncedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME()
WHERE ThreadId = @ThreadId AND IsDeleted = 0;

INSERT INTO Comms.ThreadMessage
    (MessageId, ThreadId, SenderName, Channel, Direction, Body, SentAt, DeliveryStatus, IsAutomated, ProviderName, DeliveredAtUtc)
VALUES
    (NEWID(), @ThreadId, 'System', 'Internal Note', 'Outbound',
     CONCAT('Escalated to ', @EscalateTo, '. Reason: ', @Reason,
            CASE WHEN @Note IS NULL OR @Note = '' THEN '' ELSE '. ' + @Note END),
     SYSUTCDATETIME(), 'Delivered', 1, 'AMS', SYSUTCDATETIME());

UPDATE Comms.MessageThread
SET MessageCount = MessageCount + 1, LastActivityAt = SYSUTCDATETIME()
WHERE ThreadId = @ThreadId;

INSERT INTO Comms.CommunicationAuditLog (AuditLogId,TenantId,ThreadId,ActionName,ActorName,Details,IsDeleted)
SELECT NEWID(), TenantId, @ThreadId, N'Escalated', @EscalateTo, CONCAT(@Reason, CASE WHEN @Note IS NULL OR @Note='' THEN '' ELSE ': ' + @Note END), 0 FROM Comms.MessageThread WHERE ThreadId=@ThreadId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.ThreadId, request.EscalateTo, request.Reason, Note = request.Note }, cancellationToken: cancellationToken));
    }

    public async Task ResolveAsync(ResolveThreadRequest request, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE Comms.MessageThread
SET Status = 'Resolved', SlaStatus='Completed', SlaMinutesRemaining=0, LastSyncedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME()
WHERE ThreadId = @ThreadId AND IsDeleted = 0;

INSERT INTO Comms.CommunicationAuditLog (AuditLogId,TenantId,ThreadId,ActionName,ActorName,Details,IsDeleted)
SELECT NEWID(), TenantId, @ThreadId, N'Resolved', N'System', N'Conversation resolved from command center.', 0 FROM Comms.MessageThread WHERE ThreadId=@ThreadId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.ThreadId }, cancellationToken: cancellationToken));
    }

    public async Task MarkReadAsync(MarkReadRequest request, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE Comms.MessageThread
SET IsRead = 1, LastSyncedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME()
WHERE ThreadId = @ThreadId AND IsDeleted = 0;

UPDATE Comms.ThreadMessage
SET ReadAtUtc = COALESCE(ReadAtUtc, SYSUTCDATETIME()), ModifiedDateUtc=SYSUTCDATETIME()
WHERE ThreadId=@ThreadId AND IsDeleted=0;

INSERT INTO Comms.CommunicationAuditLog (AuditLogId,TenantId,ThreadId,ActionName,ActorName,Details,IsDeleted)
SELECT NEWID(), TenantId, @ThreadId, N'Marked Read', N'System', N'Conversation marked read.', 0 FROM Comms.MessageThread WHERE ThreadId=@ThreadId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.ThreadId }, cancellationToken: cancellationToken));
    }

    private static string ResolveQueueName(string channel, string priority)
        => priority == "Urgent" ? "Escalations" : channel switch
        {
            "SMS" => "SMS Service Desk",
            "Portal Message" => "Portal Messages",
            "Internal Note" => "General Inbox",
            _ => "General Inbox"
        };

    private static int ResolveSlaMinutes(string channel, string priority)
        => priority == "Urgent" ? 60 : channel switch
        {
            "SMS" => 60,
            "Portal Message" => 240,
            _ => 240
        };

    private static string ResolveProviderName(string channel)
        => channel switch
        {
            "SMS" => "Twilio",
            "Portal Message" => "AMS Portal",
            "Internal Note" => "AMS",
            _ => "Microsoft Graph"
        };
}
