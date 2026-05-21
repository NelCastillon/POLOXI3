using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/communications")]
public sealed class CommunicationPagesController : ControllerBase
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public CommunicationPagesController(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private async Task EnsureMarketingDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Marketing') EXEC(N'CREATE SCHEMA Marketing');

IF OBJECT_ID(N'Marketing.Segment', N'U') IS NULL
BEGIN
    CREATE TABLE Marketing.Segment (SegmentId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, Name NVARCHAR(200) NOT NULL, Icon NVARCHAR(80) NOT NULL, ColorCss NVARCHAR(80) NOT NULL, Description NVARCHAR(1000) NULL, ContactCount INT NOT NULL DEFAULT 0, IsDynamic BIT NOT NULL DEFAULT 1, Rules NVARCHAR(2000) NULL, UpdatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Marketing.EmailBlast', N'U') IS NULL
BEGIN
    CREATE TABLE Marketing.EmailBlast (EmailBlastId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, CampaignId UNIQUEIDENTIFIER NULL, Name NVARCHAR(200) NOT NULL, Subject NVARCHAR(300) NOT NULL, PreviewText NVARCHAR(500) NULL, AudienceSegment NVARCHAR(200) NOT NULL, SenderName NVARCHAR(150) NOT NULL, SenderEmail NVARCHAR(254) NOT NULL, Status NVARCHAR(50) NOT NULL DEFAULT N'Draft', ScheduledDateUtc DATETIME2 NULL, SentDateUtc DATETIME2 NULL, RecipientCount INT NOT NULL DEFAULT 0, SentCount INT NOT NULL DEFAULT 0, OpenCount INT NOT NULL DEFAULT 0, ClickCount INT NOT NULL DEFAULT 0, BounceCount INT NOT NULL DEFAULT 0, UnsubscribeCount INT NOT NULL DEFAULT 0, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Marketing.LandingPage', N'U') IS NULL
BEGIN
    CREATE TABLE Marketing.LandingPage (LandingPageId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, CampaignId UNIQUEIDENTIFIER NULL, Name NVARCHAR(200) NOT NULL, Slug NVARCHAR(200) NOT NULL, TemplateName NVARCHAR(150) NOT NULL, Status NVARCHAR(50) NOT NULL DEFAULT N'Draft', PublishedUrl NVARCHAR(500) NULL, PrimaryCta NVARCHAR(150) NULL, ViewCount INT NOT NULL DEFAULT 0, ConversionCount INT NOT NULL DEFAULT 0, ConversionRate DECIMAL(9,2) NOT NULL DEFAULT 0, LastPublishedDateUtc DATETIME2 NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF NOT EXISTS (SELECT 1 FROM Marketing.Segment WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Marketing.Segment (SegmentId,TenantId,Name,Icon,ColorCss,Description,ContactCount,IsDynamic,Rules,UpdatedDateUtc,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Active Commercial Clients',N'bi-building',N'mks-ic-blue',N'Commercial clients with active policies and cross-sell appetite',3840,1,N'Status = Active|Type = Commercial',SYSUTCDATETIME(),0),
    (NEWID(),@TenantId,N'Personal Lines Households',N'bi-house',N'mks-ic-purple',N'Households with personal auto, home, umbrella, or package opportunities',12400,1,N'Type = Personal|Policy: Auto OR HO',SYSUTCDATETIME(),0),
    (NEWID(),@TenantId,N'Lapsed — 60–180d',N'bi-arrow-counterclockwise',N'mks-ic-amber',N'Lapsed accounts in the win-back window',6300,1,N'LapseDate BETWEEN 60 AND 180 days',SYSUTCDATETIME(),0),
    (NEWID(),@TenantId,N'NPS Promoters',N'bi-star-fill',N'mks-ic-gold',N'Promoters eligible for review and referral requests',2100,1,N'NPS >= 9',SYSUTCDATETIME(),0);
END;

IF NOT EXISTS (SELECT 1 FROM Marketing.EmailBlast WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Marketing.EmailBlast (EmailBlastId,TenantId,CampaignId,Name,Subject,PreviewText,AudienceSegment,SenderName,SenderEmail,Status,ScheduledDateUtc,SentDateUtc,RecipientCount,SentCount,OpenCount,ClickCount,BounceCount,UnsubscribeCount,CreatedDateUtc,IsDeleted) VALUES
    (NEWID(),@TenantId,NULL,N'Home + Auto Bundle Launch',N'Bundle your home and auto coverage this month',N'Personal lines households with package opportunities.',N'Personal Lines Households',N'AgencyBinder Team',N'marketing@agencybinder.local',N'Sent',DATEADD(day,-21,SYSUTCDATETIME()),DATEADD(day,-21,SYSUTCDATETIME()),12400,11200,3237,884,42,18,DATEADD(day,-30,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,NULL,N'Umbrella Cross-Sell Preview',N'Is your liability protection enough?',N'Commercial client umbrella cross-sell workflow.',N'Active Commercial Clients',N'AgencyBinder Team',N'marketing@agencybinder.local',N'Scheduled',DATEADD(day,4,SYSUTCDATETIME()),NULL,3840,0,0,0,0,0,DATEADD(day,-7,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,NULL,N'Lapsed Policy Win-Back',N'We can help restart your protection',N'Win-back sequence for recently lapsed accounts.',N'Lapsed — 60–180d',N'AgencyBinder Team',N'marketing@agencybinder.local',N'Draft',DATEADD(day,8,SYSUTCDATETIME()),NULL,6300,0,0,0,0,0,DATEADD(day,-3,SYSUTCDATETIME()),0);
END;

IF NOT EXISTS (SELECT 1 FROM Marketing.LandingPage WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Marketing.LandingPage (LandingPageId,TenantId,CampaignId,Name,Slug,TemplateName,Status,PublishedUrl,PrimaryCta,ViewCount,ConversionCount,ConversionRate,LastPublishedDateUtc,CreatedDateUtc,IsDeleted) VALUES
    (NEWID(),@TenantId,NULL,N'Bundle Review Landing Page',N'home-auto-bundle-review',N'Insurance Benefit Card',N'Published',N'https://agencybinder.local/lp/home-auto-bundle-review',N'Review My Bundle',1840,126,6.85,DATEADD(day,-20,SYSUTCDATETIME()),DATEADD(day,-31,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,NULL,N'Commercial Umbrella Review',N'commercial-umbrella-review',N'Modern Promo',N'Draft',N'https://agencybinder.local/lp/commercial-umbrella-review',N'Schedule a Review',0,0,0,NULL,DATEADD(day,-6,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,NULL,N'Lapsed Policy Restart',N'lapsed-policy-restart',N'Win-Back Offer',N'Draft',N'https://agencybinder.local/lp/lapsed-policy-restart',N'Restart Coverage',0,0,0,NULL,DATEADD(day,-3,SYSUTCDATETIME()),0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private async Task EnsureCampaignDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Comms') EXEC(N'CREATE SCHEMA Comms');

IF OBJECT_ID(N'Comms.Campaign', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.Campaign (CampaignId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, Name NVARCHAR(200) NOT NULL, Type NVARCHAR(80) NOT NULL, Status NVARCHAR(50) NOT NULL DEFAULT N'Draft', Segment NVARCHAR(200) NOT NULL, StartDate DATETIME2 NOT NULL, Reached INT NOT NULL DEFAULT 0, OpenRate DECIMAL(9,2) NOT NULL DEFAULT 0, Conversions INT NOT NULL DEFAULT 0, Revenue DECIMAL(18,2) NOT NULL DEFAULT 0, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), ModifiedDateUtc DATETIME2 NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF COL_LENGTH('Comms.Campaign','Goal') IS NULL ALTER TABLE Comms.Campaign ADD Goal NVARCHAR(100) NOT NULL CONSTRAINT DF_CommsCampaign_Goal DEFAULT N'Cross-Sell';
IF COL_LENGTH('Comms.Campaign','Description') IS NULL ALTER TABLE Comms.Campaign ADD Description NVARCHAR(1000) NOT NULL CONSTRAINT DF_CommsCampaign_Description DEFAULT N'';
IF COL_LENGTH('Comms.Campaign','Subject') IS NULL ALTER TABLE Comms.Campaign ADD Subject NVARCHAR(300) NOT NULL CONSTRAINT DF_CommsCampaign_Subject DEFAULT N'';
IF COL_LENGTH('Comms.Campaign','SenderName') IS NULL ALTER TABLE Comms.Campaign ADD SenderName NVARCHAR(150) NOT NULL CONSTRAINT DF_CommsCampaign_SenderName DEFAULT N'AgencyBinder Team';
IF COL_LENGTH('Comms.Campaign','ReplyToEmail') IS NULL ALTER TABLE Comms.Campaign ADD ReplyToEmail NVARCHAR(254) NOT NULL CONSTRAINT DF_CommsCampaign_ReplyToEmail DEFAULT N'marketing@agencybinder.local';
IF COL_LENGTH('Comms.Campaign','TemplateName') IS NULL ALTER TABLE Comms.Campaign ADD TemplateName NVARCHAR(150) NOT NULL CONSTRAINT DF_CommsCampaign_TemplateName DEFAULT N'Insurance Benefit Card';
IF COL_LENGTH('Comms.Campaign','CtaLabel') IS NULL ALTER TABLE Comms.Campaign ADD CtaLabel NVARCHAR(150) NOT NULL CONSTRAINT DF_CommsCampaign_CtaLabel DEFAULT N'Get a Quote';
IF COL_LENGTH('Comms.Campaign','SendMode') IS NULL ALTER TABLE Comms.Campaign ADD SendMode NVARCHAR(50) NOT NULL CONSTRAINT DF_CommsCampaign_SendMode DEFAULT N'Scheduled';
IF COL_LENGTH('Comms.Campaign','Timezone') IS NULL ALTER TABLE Comms.Campaign ADD Timezone NVARCHAR(80) NOT NULL CONSTRAINT DF_CommsCampaign_Timezone DEFAULT N'Eastern';
IF COL_LENGTH('Comms.Campaign','FollowUpDays') IS NULL ALTER TABLE Comms.Campaign ADD FollowUpDays INT NOT NULL CONSTRAINT DF_CommsCampaign_FollowUpDays DEFAULT 7;
IF COL_LENGTH('Comms.Campaign','SendFollowUp') IS NULL ALTER TABLE Comms.Campaign ADD SendFollowUp BIT NOT NULL CONSTRAINT DF_CommsCampaign_SendFollowUp DEFAULT 1;
IF COL_LENGTH('Comms.Campaign','SuppressRecentContacts') IS NULL ALTER TABLE Comms.Campaign ADD SuppressRecentContacts BIT NOT NULL CONSTRAINT DF_CommsCampaign_SuppressRecentContacts DEFAULT 1;
IF COL_LENGTH('Comms.Campaign','SuppressOptOut') IS NULL ALTER TABLE Comms.Campaign ADD SuppressOptOut BIT NOT NULL CONSTRAINT DF_CommsCampaign_SuppressOptOut DEFAULT 1;
IF COL_LENGTH('Comms.Campaign','AbTestSubject') IS NULL ALTER TABLE Comms.Campaign ADD AbTestSubject BIT NOT NULL CONSTRAINT DF_CommsCampaign_AbTestSubject DEFAULT 0;
IF COL_LENGTH('Comms.Campaign','EndDate') IS NULL ALTER TABLE Comms.Campaign ADD EndDate DATETIME2 NULL;
IF COL_LENGTH('Comms.Campaign','ScheduledDateUtc') IS NULL ALTER TABLE Comms.Campaign ADD ScheduledDateUtc DATETIME2 NULL;
IF COL_LENGTH('Comms.Campaign','LandingPageSlug') IS NULL ALTER TABLE Comms.Campaign ADD LandingPageSlug NVARCHAR(200) NOT NULL CONSTRAINT DF_CommsCampaign_LandingPageSlug DEFAULT N'';
IF COL_LENGTH('Comms.Campaign','LandingPageUrl') IS NULL ALTER TABLE Comms.Campaign ADD LandingPageUrl NVARCHAR(500) NOT NULL CONSTRAINT DF_CommsCampaign_LandingPageUrl DEFAULT N'';

IF OBJECT_ID(N'Comms.CampaignAudience', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.CampaignAudience (CampaignAudienceId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, CampaignId UNIQUEIDENTIFIER NOT NULL, SegmentName NVARCHAR(200) NOT NULL, EstimatedContacts INT NOT NULL DEFAULT 0, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Comms.CampaignContent', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.CampaignContent (CampaignContentId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, CampaignId UNIQUEIDENTIFIER NOT NULL, Channel NVARCHAR(80) NOT NULL, Subject NVARCHAR(300) NOT NULL, TemplateName NVARCHAR(150) NOT NULL, CtaLabel NVARCHAR(150) NULL, BodyHtml NVARCHAR(MAX) NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Comms.CampaignAutomation', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.CampaignAutomation (CampaignAutomationId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, CampaignId UNIQUEIDENTIFIER NOT NULL, SendMode NVARCHAR(50) NOT NULL, Timezone NVARCHAR(80) NOT NULL, FollowUpDays INT NOT NULL DEFAULT 7, SendFollowUp BIT NOT NULL DEFAULT 1, SuppressRecentContacts BIT NOT NULL DEFAULT 1, SuppressOptOut BIT NOT NULL DEFAULT 1, AbTestSubject BIT NOT NULL DEFAULT 0, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF NOT EXISTS (SELECT 1 FROM Comms.Campaign WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Comms.Campaign (CampaignId,TenantId,Name,Type,Status,Segment,StartDate,Reached,OpenRate,Conversions,Revenue,CreatedDateUtc,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Home+Auto Bundle Push',N'Email',N'Active',N'Personal Lines Households',DATEADD(day,-21,SYSUTCDATETIME()),11200,28.9,412,206000,DATEADD(day,-28,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Q2 Cross-Sell — Umbrella',N'Multi-Channel',N'Active',N'Active Commercial Clients',DATEADD(day,-16,SYSUTCDATETIME()),4820,31.4,187,94000,DATEADD(day,-24,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Lapsed Policy Win-Back',N'Email',N'Scheduled',N'Lapsed — 60–180d',DATEADD(day,4,SYSUTCDATETIME()),6300,24.6,231,115500,DATEADD(day,-8,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Google Review Request — NPS 9+',N'SMS',N'Paused',N'NPS Promoters',DATEADD(day,-5,SYSUTCDATETIME()),2100,41.2,680,0,DATEADD(day,-12,SYSUTCDATETIME()),0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private async Task EnsureAppointmentDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Comms') EXEC(N'CREATE SCHEMA Comms');

IF OBJECT_ID(N'Comms.Appointment', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.Appointment (AppointmentId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, AccountName NVARCHAR(200) NOT NULL, ContactName NVARCHAR(160) NOT NULL, Type NVARCHAR(80) NOT NULL, Channel NVARCHAR(80) NOT NULL DEFAULT N'Phone Call', Status NVARCHAR(50) NOT NULL DEFAULT N'Scheduled', Duration NVARCHAR(40) NOT NULL DEFAULT N'30 min', Producer NVARCHAR(160) NOT NULL DEFAULT N'', CsrOwner NVARCHAR(160) NOT NULL DEFAULT N'', Branch NVARCHAR(120) NOT NULL DEFAULT N'', Notes NVARCHAR(2000) NOT NULL DEFAULT N'', Outcome NVARCHAR(160) NOT NULL DEFAULT N'', OutcomeNotes NVARCHAR(2000) NOT NULL DEFAULT N'', FollowUp NVARCHAR(160) NOT NULL DEFAULT N'', SendConfirmation BIT NOT NULL DEFAULT 1, SendReminder BIT NOT NULL DEFAULT 1, ScheduledDate DATETIME2 NULL, ScheduledTime DATETIME2 NULL, ConfirmationStatus NVARCHAR(60) NOT NULL DEFAULT N'Pending', ReminderStatus NVARCHAR(60) NOT NULL DEFAULT N'Scheduled', SlaStatus NVARCHAR(60) NOT NULL DEFAULT N'On Track', SourceSystem NVARCHAR(80) NOT NULL DEFAULT N'AMS', SyncStatus NVARCHAR(60) NOT NULL DEFAULT N'Synced', LastReminderSentUtc DATETIME2 NULL, LastSyncedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF COL_LENGTH('Comms.Appointment','TenantId') IS NULL ALTER TABLE Comms.Appointment ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommsAppointment_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH('Comms.Appointment','AccountName') IS NULL ALTER TABLE Comms.Appointment ADD AccountName NVARCHAR(200) NOT NULL CONSTRAINT DF_CommsAppointment_AccountName DEFAULT N'';
IF COL_LENGTH('Comms.Appointment','ContactName') IS NULL ALTER TABLE Comms.Appointment ADD ContactName NVARCHAR(160) NOT NULL CONSTRAINT DF_CommsAppointment_ContactName DEFAULT N'';
IF COL_LENGTH('Comms.Appointment','Type') IS NULL ALTER TABLE Comms.Appointment ADD Type NVARCHAR(80) NOT NULL CONSTRAINT DF_CommsAppointment_Type DEFAULT N'Coverage Review';
IF COL_LENGTH('Comms.Appointment','Channel') IS NULL ALTER TABLE Comms.Appointment ADD Channel NVARCHAR(80) NOT NULL CONSTRAINT DF_CommsAppointment_Channel DEFAULT N'Phone Call';
IF COL_LENGTH('Comms.Appointment','Status') IS NULL ALTER TABLE Comms.Appointment ADD Status NVARCHAR(50) NOT NULL CONSTRAINT DF_CommsAppointment_Status DEFAULT N'Scheduled';
IF COL_LENGTH('Comms.Appointment','Duration') IS NULL ALTER TABLE Comms.Appointment ADD Duration NVARCHAR(40) NOT NULL CONSTRAINT DF_CommsAppointment_Duration DEFAULT N'30 min';
IF COL_LENGTH('Comms.Appointment','Producer') IS NULL ALTER TABLE Comms.Appointment ADD Producer NVARCHAR(160) NOT NULL CONSTRAINT DF_CommsAppointment_Producer DEFAULT N'';
IF COL_LENGTH('Comms.Appointment','CsrOwner') IS NULL ALTER TABLE Comms.Appointment ADD CsrOwner NVARCHAR(160) NOT NULL CONSTRAINT DF_CommsAppointment_CsrOwner DEFAULT N'';
IF COL_LENGTH('Comms.Appointment','Branch') IS NULL ALTER TABLE Comms.Appointment ADD Branch NVARCHAR(120) NOT NULL CONSTRAINT DF_CommsAppointment_Branch DEFAULT N'';
IF COL_LENGTH('Comms.Appointment','Notes') IS NULL ALTER TABLE Comms.Appointment ADD Notes NVARCHAR(2000) NOT NULL CONSTRAINT DF_CommsAppointment_Notes DEFAULT N'';
IF COL_LENGTH('Comms.Appointment','Outcome') IS NULL ALTER TABLE Comms.Appointment ADD Outcome NVARCHAR(160) NOT NULL CONSTRAINT DF_CommsAppointment_Outcome DEFAULT N'';
IF COL_LENGTH('Comms.Appointment','OutcomeNotes') IS NULL ALTER TABLE Comms.Appointment ADD OutcomeNotes NVARCHAR(2000) NOT NULL CONSTRAINT DF_CommsAppointment_OutcomeNotes DEFAULT N'';
IF COL_LENGTH('Comms.Appointment','FollowUp') IS NULL ALTER TABLE Comms.Appointment ADD FollowUp NVARCHAR(160) NOT NULL CONSTRAINT DF_CommsAppointment_FollowUp DEFAULT N'';
IF COL_LENGTH('Comms.Appointment','SendConfirmation') IS NULL ALTER TABLE Comms.Appointment ADD SendConfirmation BIT NOT NULL CONSTRAINT DF_CommsAppointment_SendConfirmation DEFAULT 1;
IF COL_LENGTH('Comms.Appointment','SendReminder') IS NULL ALTER TABLE Comms.Appointment ADD SendReminder BIT NOT NULL CONSTRAINT DF_CommsAppointment_SendReminder DEFAULT 1;
IF COL_LENGTH('Comms.Appointment','ScheduledDate') IS NULL ALTER TABLE Comms.Appointment ADD ScheduledDate DATETIME2 NULL;
IF COL_LENGTH('Comms.Appointment','ScheduledTime') IS NULL ALTER TABLE Comms.Appointment ADD ScheduledTime DATETIME2 NULL;
IF COL_LENGTH('Comms.Appointment','ConfirmationStatus') IS NULL ALTER TABLE Comms.Appointment ADD ConfirmationStatus NVARCHAR(60) NOT NULL CONSTRAINT DF_CommsAppointment_ConfirmationStatus DEFAULT N'Pending';
IF COL_LENGTH('Comms.Appointment','ReminderStatus') IS NULL ALTER TABLE Comms.Appointment ADD ReminderStatus NVARCHAR(60) NOT NULL CONSTRAINT DF_CommsAppointment_ReminderStatus DEFAULT N'Scheduled';
IF COL_LENGTH('Comms.Appointment','SlaStatus') IS NULL ALTER TABLE Comms.Appointment ADD SlaStatus NVARCHAR(60) NOT NULL CONSTRAINT DF_CommsAppointment_SlaStatus DEFAULT N'On Track';
IF COL_LENGTH('Comms.Appointment','SourceSystem') IS NULL ALTER TABLE Comms.Appointment ADD SourceSystem NVARCHAR(80) NOT NULL CONSTRAINT DF_CommsAppointment_SourceSystem DEFAULT N'AMS';
IF COL_LENGTH('Comms.Appointment','SyncStatus') IS NULL ALTER TABLE Comms.Appointment ADD SyncStatus NVARCHAR(60) NOT NULL CONSTRAINT DF_CommsAppointment_SyncStatus DEFAULT N'Synced';
IF COL_LENGTH('Comms.Appointment','LastReminderSentUtc') IS NULL ALTER TABLE Comms.Appointment ADD LastReminderSentUtc DATETIME2 NULL;
IF COL_LENGTH('Comms.Appointment','LastSyncedDateUtc') IS NULL ALTER TABLE Comms.Appointment ADD LastSyncedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommsAppointment_LastSynced DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('Comms.Appointment','CreatedDateUtc') IS NULL ALTER TABLE Comms.Appointment ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommsAppointment_Created DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('Comms.Appointment','CreatedByUserId') IS NULL ALTER TABLE Comms.Appointment ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('Comms.Appointment','ModifiedDateUtc') IS NULL ALTER TABLE Comms.Appointment ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH('Comms.Appointment','ModifiedByUserId') IS NULL ALTER TABLE Comms.Appointment ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('Comms.Appointment','IsDeleted') IS NULL ALTER TABLE Comms.Appointment ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommsAppointment_IsDeleted DEFAULT 0;

IF OBJECT_ID(N'Comms.AppointmentAuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.AppointmentAuditLog (AppointmentAuditLogId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, AppointmentId UNIQUEIDENTIFIER NOT NULL, ActionName NVARCHAR(80) NOT NULL, Details NVARCHAR(2000) NOT NULL DEFAULT N'', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Comms.AppointmentReminder', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.AppointmentReminder (AppointmentReminderId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, AppointmentId UNIQUEIDENTIFIER NOT NULL, Channel NVARCHAR(80) NOT NULL, Status NVARCHAR(60) NOT NULL, ScheduledDateUtc DATETIME2 NULL, SentDateUtc DATETIME2 NULL, Message NVARCHAR(1000) NOT NULL DEFAULT N'', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Comms.AppointmentProviderSync', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.AppointmentProviderSync (AppointmentProviderSyncId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, AppointmentId UNIQUEIDENTIFIER NOT NULL, ProviderName NVARCHAR(120) NOT NULL, SyncStatus NVARCHAR(60) NOT NULL, ExternalAppointmentId NVARCHAR(160) NULL, LastSyncDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), Details NVARCHAR(1000) NOT NULL DEFAULT N'', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;";

        const string seedSql = @"
IF NOT EXISTS (SELECT 1 FROM Comms.Appointment WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Comms.Appointment (AppointmentId,TenantId,AccountName,ContactName,Type,Channel,Status,Duration,Producer,CsrOwner,Branch,Notes,Outcome,OutcomeNotes,FollowUp,SendConfirmation,SendReminder,ScheduledDate,ScheduledTime,ConfirmationStatus,ReminderStatus,SlaStatus,SourceSystem,SyncStatus,LastReminderSentUtc,LastSyncedDateUtc,CreatedDateUtc,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Sullivan Mfg. LLC',N'Robert Sullivan',N'Renewal Discussion',N'Phone Call',N'Scheduled',N'30 min',N'Maria Santos',N'Sarah Kim',N'Gulf Coast',N'Discuss split-limit GL structure. Client CFO will be on the call. Review aggregate options.',N'',N'',N'',1,1,CAST(SYSUTCDATETIME() AS date),DATEADD(hour,9,CAST(CAST(SYSUTCDATETIME() AS date) AS datetime2)),N'Sent',N'Scheduled',N'On Track',N'AMS',N'Synced',NULL,SYSUTCDATETIME(),DATEADD(day,-2,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Apex Medical Group',N'Sandra Kim',N'Renewal Discussion',N'Video Call',N'Awaiting Confirmation',N'45 min',N'Maria Santos',N'Sarah Kim',N'Gulf Coast',N'28% premium increase concern. Discuss remarketing options. Dr. Chen may join.',N'',N'',N'',1,1,CAST(SYSUTCDATETIME() AS date),DATEADD(hour,11,CAST(CAST(SYSUTCDATETIME() AS date) AS datetime2)),N'Pending',N'Scheduled',N'At Risk',N'AMS',N'Synced',NULL,SYSUTCDATETIME(),DATEADD(day,-1,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Bridgewater Hotels',N'Patricia Howe',N'Claims Follow-Up',N'Phone Call',N'Scheduled',N'30 min',N'Diana Perez',N'Maria Santos',N'Northeast',N'CLM-2025-00131 status update. Adjuster report expected by EOD.',N'',N'',N'',1,1,CAST(SYSUTCDATETIME() AS date),DATEADD(hour,14,CAST(CAST(SYSUTCDATETIME() AS date) AS datetime2)),N'Sent',N'Scheduled',N'On Track',N'AMS',N'Synced',NULL,SYSUTCDATETIME(),DATEADD(day,-1,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Dallas Roofing LLC',N'Marcus Webb',N'Policy Service',N'Phone Call',N'Completed',N'15 min',N'James Park',N'Kevin Obi',N'North Texas',N'COI follow-up.',N'Completed — Client Reached',N'COI delivered. Marcus confirmed the certificate holder name.',N'None',1,0,DATEADD(day,-1,CAST(SYSUTCDATETIME() AS date)),DATEADD(hour,10,DATEADD(day,-1,CAST(CAST(SYSUTCDATETIME() AS date) AS datetime2))),N'Sent',N'Not Required',N'Met',N'AMS',N'Synced',NULL,SYSUTCDATETIME(),DATEADD(day,-3,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Metro Freight Co.',N'Dale Foster',N'Coverage Review',N'In-Person',N'Scheduled',N'60 min',N'Robert Yamamoto',N'James Park',N'North Texas',N'Annual coverage review. Fleet has grown to 22 units. Discuss inland marine for cargo.',N'',N'',N'',1,1,DATEADD(day,1,CAST(SYSUTCDATETIME() AS date)),DATEADD(hour,10,DATEADD(day,1,CAST(CAST(SYSUTCDATETIME() AS date) AS datetime2))),N'Sent',N'Scheduled',N'On Track',N'AMS',N'Synced',NULL,SYSUTCDATETIME(),DATEADD(day,-1,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Harbor View Marina',N'Tony Marcellis',N'CAT Site Visit',N'In-Person',N'Scheduled',N'90 min',N'Diana Perez',N'Lisa Chen',N'Gulf Coast',N'CAT field inspection with Ramirez. Estimated damage $115K. Call insured morning of.',N'',N'',N'',1,1,DATEADD(day,2,CAST(SYSUTCDATETIME() AS date)),DATEADD(hour,9,DATEADD(day,2,CAST(CAST(SYSUTCDATETIME() AS date) AS datetime2))),N'Sent',N'Scheduled',N'On Track',N'AMS',N'Synced',NULL,SYSUTCDATETIME(),DATEADD(day,-1,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Sunrise Healthcare',N'Nadia Patel',N'Claims Follow-Up',N'Phone Call',N'Cancelled',N'30 min',N'Diana Perez',N'Kevin Obi',N'Gulf Coast',N'Cancelled — attorney representation. All contact through John Cruz (832) 555-1111.',N'Cancelled by Client',N'Client has retained legal counsel.',N'None',0,0,DATEADD(day,-2,CAST(SYSUTCDATETIME() AS date)),DATEADD(hour,9,DATEADD(day,-2,CAST(CAST(SYSUTCDATETIME() AS date) AS datetime2))),N'Cancelled',N'Cancelled',N'Closed',N'AMS',N'Synced',NULL,SYSUTCDATETIME(),DATEADD(day,-4,SYSUTCDATETIME()),0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, cancellationToken: cancellationToken));
        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private async Task EnsureOutreachDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Comms') EXEC(N'CREATE SCHEMA Comms');

IF OBJECT_ID(N'Comms.OutreachContact', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.OutreachContact (OutreachContactId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, AccountName NVARCHAR(200) NOT NULL, ContactName NVARCHAR(160) NOT NULL, Email NVARCHAR(254) NOT NULL DEFAULT N'', Phone NVARCHAR(50) NOT NULL DEFAULT N'', Reason NVARCHAR(120) NOT NULL, Priority NVARCHAR(40) NOT NULL DEFAULT N'Medium', AssignedTo NVARCHAR(160) NOT NULL DEFAULT N'', Producer NVARCHAR(160) NOT NULL DEFAULT N'', Branch NVARCHAR(120) NOT NULL DEFAULT N'', Status NVARCHAR(50) NOT NULL DEFAULT N'Open', LastOutcome NVARCHAR(160) NOT NULL DEFAULT N'', Notes NVARCHAR(2000) NOT NULL DEFAULT N'', Attempts INT NOT NULL DEFAULT 0, OptedOut BIT NOT NULL DEFAULT 0, LastContactDate DATETIME2 NULL, NextContactDate DATETIME2 NULL, SourceSystem NVARCHAR(80) NOT NULL DEFAULT N'AMS', SyncStatus NVARCHAR(60) NOT NULL DEFAULT N'Synced', LastSyncedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF COL_LENGTH('Comms.OutreachContact','TenantId') IS NULL ALTER TABLE Comms.OutreachContact ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommsOutreach_TenantId DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH('Comms.OutreachContact','AccountName') IS NULL ALTER TABLE Comms.OutreachContact ADD AccountName NVARCHAR(200) NOT NULL CONSTRAINT DF_CommsOutreach_AccountName DEFAULT N'';
IF COL_LENGTH('Comms.OutreachContact','ContactName') IS NULL ALTER TABLE Comms.OutreachContact ADD ContactName NVARCHAR(160) NOT NULL CONSTRAINT DF_CommsOutreach_ContactName DEFAULT N'';
IF COL_LENGTH('Comms.OutreachContact','Email') IS NULL ALTER TABLE Comms.OutreachContact ADD Email NVARCHAR(254) NOT NULL CONSTRAINT DF_CommsOutreach_Email DEFAULT N'';
IF COL_LENGTH('Comms.OutreachContact','Phone') IS NULL ALTER TABLE Comms.OutreachContact ADD Phone NVARCHAR(50) NOT NULL CONSTRAINT DF_CommsOutreach_Phone DEFAULT N'';
IF COL_LENGTH('Comms.OutreachContact','Reason') IS NULL ALTER TABLE Comms.OutreachContact ADD Reason NVARCHAR(120) NOT NULL CONSTRAINT DF_CommsOutreach_Reason DEFAULT N'General Follow-Up';
IF COL_LENGTH('Comms.OutreachContact','Priority') IS NULL ALTER TABLE Comms.OutreachContact ADD Priority NVARCHAR(40) NOT NULL CONSTRAINT DF_CommsOutreach_Priority DEFAULT N'Medium';
IF COL_LENGTH('Comms.OutreachContact','AssignedTo') IS NULL ALTER TABLE Comms.OutreachContact ADD AssignedTo NVARCHAR(160) NOT NULL CONSTRAINT DF_CommsOutreach_AssignedTo DEFAULT N'';
IF COL_LENGTH('Comms.OutreachContact','Producer') IS NULL ALTER TABLE Comms.OutreachContact ADD Producer NVARCHAR(160) NOT NULL CONSTRAINT DF_CommsOutreach_Producer DEFAULT N'';
IF COL_LENGTH('Comms.OutreachContact','Branch') IS NULL ALTER TABLE Comms.OutreachContact ADD Branch NVARCHAR(120) NOT NULL CONSTRAINT DF_CommsOutreach_Branch DEFAULT N'';
IF COL_LENGTH('Comms.OutreachContact','Status') IS NULL ALTER TABLE Comms.OutreachContact ADD Status NVARCHAR(50) NOT NULL CONSTRAINT DF_CommsOutreach_Status DEFAULT N'Open';
IF COL_LENGTH('Comms.OutreachContact','LastOutcome') IS NULL ALTER TABLE Comms.OutreachContact ADD LastOutcome NVARCHAR(160) NOT NULL CONSTRAINT DF_CommsOutreach_LastOutcome DEFAULT N'';
IF COL_LENGTH('Comms.OutreachContact','Notes') IS NULL ALTER TABLE Comms.OutreachContact ADD Notes NVARCHAR(2000) NOT NULL CONSTRAINT DF_CommsOutreach_Notes DEFAULT N'';
IF COL_LENGTH('Comms.OutreachContact','Attempts') IS NULL ALTER TABLE Comms.OutreachContact ADD Attempts INT NOT NULL CONSTRAINT DF_CommsOutreach_Attempts DEFAULT 0;
IF COL_LENGTH('Comms.OutreachContact','OptedOut') IS NULL ALTER TABLE Comms.OutreachContact ADD OptedOut BIT NOT NULL CONSTRAINT DF_CommsOutreach_OptedOut DEFAULT 0;
IF COL_LENGTH('Comms.OutreachContact','LastContactDate') IS NULL ALTER TABLE Comms.OutreachContact ADD LastContactDate DATETIME2 NULL;
IF COL_LENGTH('Comms.OutreachContact','NextContactDate') IS NULL ALTER TABLE Comms.OutreachContact ADD NextContactDate DATETIME2 NULL;
IF COL_LENGTH('Comms.OutreachContact','SourceSystem') IS NULL ALTER TABLE Comms.OutreachContact ADD SourceSystem NVARCHAR(80) NOT NULL CONSTRAINT DF_CommsOutreach_SourceSystem DEFAULT N'AMS';
IF COL_LENGTH('Comms.OutreachContact','SyncStatus') IS NULL ALTER TABLE Comms.OutreachContact ADD SyncStatus NVARCHAR(60) NOT NULL CONSTRAINT DF_CommsOutreach_SyncStatus DEFAULT N'Synced';
IF COL_LENGTH('Comms.OutreachContact','LastSyncedDateUtc') IS NULL ALTER TABLE Comms.OutreachContact ADD LastSyncedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommsOutreach_LastSynced DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('Comms.OutreachContact','CreatedDateUtc') IS NULL ALTER TABLE Comms.OutreachContact ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CommsOutreach_Created DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('Comms.OutreachContact','CreatedByUserId') IS NULL ALTER TABLE Comms.OutreachContact ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('Comms.OutreachContact','ModifiedDateUtc') IS NULL ALTER TABLE Comms.OutreachContact ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH('Comms.OutreachContact','ModifiedByUserId') IS NULL ALTER TABLE Comms.OutreachContact ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH('Comms.OutreachContact','IsDeleted') IS NULL ALTER TABLE Comms.OutreachContact ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CommsOutreach_IsDeleted DEFAULT 0;

IF OBJECT_ID(N'Comms.OutreachAuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.OutreachAuditLog (OutreachAuditLogId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, OutreachContactId UNIQUEIDENTIFIER NULL, ActionName NVARCHAR(80) NOT NULL, Details NVARCHAR(2000) NOT NULL DEFAULT N'', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Comms.OutreachDelivery', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.OutreachDelivery (OutreachDeliveryId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, OutreachContactId UNIQUEIDENTIFIER NOT NULL, Channel NVARCHAR(80) NOT NULL, TemplateName NVARCHAR(160) NOT NULL DEFAULT N'', Message NVARCHAR(1000) NOT NULL DEFAULT N'', Status NVARCHAR(60) NOT NULL, SentDateUtc DATETIME2 NULL, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;

IF OBJECT_ID(N'Comms.OutreachProviderSync', N'U') IS NULL
BEGIN
    CREATE TABLE Comms.OutreachProviderSync (OutreachProviderSyncId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY, TenantId UNIQUEIDENTIFIER NOT NULL, OutreachContactId UNIQUEIDENTIFIER NULL, ProviderName NVARCHAR(120) NOT NULL, SyncStatus NVARCHAR(60) NOT NULL, ExternalReference NVARCHAR(160) NULL, LastSyncDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), Details NVARCHAR(1000) NOT NULL DEFAULT N'', CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);
END;";

        const string seedSql = @"
IF NOT EXISTS (SELECT 1 FROM Comms.OutreachContact WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Comms.OutreachContact (OutreachContactId,TenantId,AccountName,ContactName,Email,Phone,Reason,Priority,AssignedTo,Producer,Branch,Status,LastOutcome,Notes,Attempts,OptedOut,LastContactDate,NextContactDate,SourceSystem,SyncStatus,LastSyncedDateUtc,CreatedDateUtc,IsDeleted) VALUES
    (NEWID(),@TenantId,N'Bridgewater Hotels',N'Patricia Howe',N'phowe@bwhotels.com',N'(212) 555-0188',N'Claims Follow-Up',N'Critical',N'Maria Santos',N'Maria Santos',N'Northeast',N'Open',N'No Answer — Voicemail Left',N'Claims follow-up after water damage notice. Escalate if no response today.',2,0,DATEADD(day,-3,SYSUTCDATETIME()),CAST(SYSUTCDATETIME() AS date),N'AMS',N'Synced',SYSUTCDATETIME(),DATEADD(day,-4,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Apex Medical Group',N'Sandra Kim',N'sandrakim@apexmed.com',N'(832) 555-0377',N'Renewal — 30 Days',N'Critical',N'Sarah Kim',N'Maria Santos',N'Gulf Coast',N'Open',N'',N'Premium increase concern. Renewal strategy call required.',0,0,NULL,CAST(SYSUTCDATETIME() AS date),N'AMS',N'Synced',SYSUTCDATETIME(),DATEADD(day,-2,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Sunrise Healthcare',N'Nadia Patel',N'nadia@sunrisehc.com',N'(713) 555-0921',N'Claims Follow-Up',N'Critical',N'Kevin Obi',N'Diana Perez',N'Gulf Coast',N'Opted Out',N'No Answer — Voicemail Left',N'Client opted out of automated outreach. Manual review only.',2,1,DATEADD(day,-1,SYSUTCDATETIME()),NULL,N'AMS',N'Synced',SYSUTCDATETIME(),DATEADD(day,-5,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Pacific Coast Builders',N'Jorge Medina',N'jmedina@pcbuilders.com',N'(619) 555-0812',N'Audit Due',N'High',N'Robert Yamamoto',N'Robert Yamamoto',N'Southwest',N'In Progress',N'Reached — Call Back Requested',N'Audit worksheet pending. Call back requested by Thursday.',1,0,DATEADD(day,-2,SYSUTCDATETIME()),DATEADD(day,2,CAST(SYSUTCDATETIME() AS date)),N'AMS',N'Synced',SYSUTCDATETIME(),DATEADD(day,-6,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Sullivan Mfg. LLC',N'Robert Sullivan',N'rjsullivan@email.com',N'(713) 555-0101',N'Renewal — 60 Days',N'High',N'Sarah Kim',N'Maria Santos',N'Gulf Coast',N'In Progress',N'Reached — Completed',N'Renewal discovery completed; schedule coverage review.',1,0,DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(day,1,CAST(SYSUTCDATETIME() AS date)),N'AMS',N'Synced',SYSUTCDATETIME(),DATEADD(day,-7,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Harbor Logistics',N'Chris Navarro',N'cnavarro@harborlog.com',N'(713) 555-0224',N'New Business Follow-Up',N'High',N'Sarah Kim',N'Maria Santos',N'Gulf Coast',N'Open',N'No Answer — No Voicemail',N'Lead came from campaign form. Needs second attempt today.',2,0,DATEADD(day,-1,SYSUTCDATETIME()),CAST(SYSUTCDATETIME() AS date),N'Marketing',N'Synced',SYSUTCDATETIME(),DATEADD(day,-3,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Dallas Roofing LLC',N'Marcus Webb',N'mwebb@dallasroofing.com',N'(214) 555-0242',N'Certificate Expiring',N'Medium',N'Kevin Obi',N'James Park',N'North Texas',N'Open',N'',N'Certificate holder expires soon. Confirm renewal wording.',0,0,NULL,DATEADD(day,3,CAST(SYSUTCDATETIME() AS date)),N'AMS',N'Synced',SYSUTCDATETIME(),DATEADD(day,-1,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Clearwater Dental',N'Dr. Paul Reyes',N'preyes@clearwaterdental.com',N'(832) 555-0490',N'At-Risk / Cancellation',N'Critical',N'',N'Diana Perez',N'Gulf Coast',N'Open',N'No Answer — Voicemail Left',N'At-risk cancellation. Unassigned and overdue.',3,0,DATEADD(day,-5,SYSUTCDATETIME()),DATEADD(day,-2,CAST(SYSUTCDATETIME() AS date)),N'AMS',N'Needs Review',SYSUTCDATETIME(),DATEADD(day,-8,SYSUTCDATETIME()),0),
    (NEWID(),@TenantId,N'Coastal Properties',N'Anne Simmons',N'asimmons@coastalprop.com',N'(361) 555-0732',N'Win-Back',N'Medium',N'Sarah Kim',N'Maria Santos',N'Gulf Coast',N'In Progress',N'Reached — Call Back Requested',N'Former client interested in updated property quote.',1,0,DATEADD(day,-7,SYSUTCDATETIME()),DATEADD(day,3,CAST(SYSUTCDATETIME() AS date)),N'Marketing',N'Synced',SYSUTCDATETIME(),DATEADD(day,-10,SYSUTCDATETIME()),0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, cancellationToken: cancellationToken));
        await cn.ExecuteAsync(new CommandDefinition(seedSql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    [HttpGet("campaigns")]
    public async Task<IActionResult> GetCampaigns([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] string? status = null, [FromQuery] string? type = null, CancellationToken cancellationToken = default)
    {
        await EnsureCampaignDataAsync(tenantId, cancellationToken);
        const string sql = @"
SELECT CampaignId, TenantId, Name, Type, Status, Segment, Goal, Description, Subject, SenderName, ReplyToEmail,
       TemplateName, CtaLabel, SendMode, Timezone, FollowUpDays, SendFollowUp, SuppressRecentContacts,
       SuppressOptOut, AbTestSubject, EndDate, ScheduledDateUtc, LandingPageSlug, LandingPageUrl,
       StartDate, Reached, OpenRate, Conversions, Revenue
FROM Comms.Campaign
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Name LIKE '%' + @SearchTerm + '%' OR Segment LIKE '%' + @SearchTerm + '%' OR Type LIKE '%' + @SearchTerm + '%')
  AND (@Status IS NULL OR @Status = '' OR Status = @Status)
  AND (@Type IS NULL OR @Type = '' OR Type = @Type)
ORDER BY StartDate DESC, Name;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<CommunicationCampaignDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Status = status, Type = type }, cancellationToken: cancellationToken))).AsList();
        return Ok(new PagedResult<CommunicationCampaignDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpPost("campaigns/seed")]
    public async Task<IActionResult> EnsureCampaignSeed([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureCampaignDataAsync(tenantId, cancellationToken);
        await EnsureMarketingDataAsync(tenantId, cancellationToken);
        return NoContent();
    }

    [HttpGet("campaigns/{id:guid}/builder")]
    public async Task<IActionResult> GetCampaignBuilder(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureCampaignDataAsync(tenantId, cancellationToken);
        await EnsureMarketingDataAsync(tenantId, cancellationToken);

        const string campaignSql = @"
SELECT CampaignId, TenantId, Name, Type, Status, Segment, Goal, Description, Subject, SenderName, ReplyToEmail,
       TemplateName, CtaLabel, SendMode, Timezone, FollowUpDays, SendFollowUp, SuppressRecentContacts,
       SuppressOptOut, AbTestSubject, EndDate, ScheduledDateUtc, LandingPageSlug, LandingPageUrl,
       StartDate, Reached, OpenRate, Conversions, Revenue
FROM Comms.Campaign
WHERE CampaignId = @Id AND TenantId = @TenantId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var campaign = await cn.QuerySingleOrDefaultAsync<CommunicationCampaignDto>(new CommandDefinition(campaignSql, new { Id = id, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (campaign is null) return NotFound();

        var data = await GetBuilderDataAsync(cn, tenantId, campaign, cancellationToken);
        return Ok(data);
    }

    [HttpGet("campaigns/builder-workspace")]
    public async Task<IActionResult> GetCampaignBuilderWorkspace([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureCampaignDataAsync(tenantId, cancellationToken);
        await EnsureMarketingDataAsync(tenantId, cancellationToken);

        var campaign = new CommunicationCampaignDto
        {
            CampaignId = Guid.Empty,
            TenantId = tenantId,
            Name = string.Empty,
            Type = "Email",
            Status = "Draft",
            Segment = string.Empty,
            Goal = "Cross-Sell",
            Description = string.Empty,
            Subject = string.Empty,
            SenderName = "AgencyBinder Team",
            ReplyToEmail = "marketing@agencybinder.local",
            TemplateName = "Insurance Benefit Card",
            CtaLabel = "Get a Quote",
            SendMode = "Scheduled",
            Timezone = "Eastern",
            FollowUpDays = 7,
            SendFollowUp = true,
            SuppressRecentContacts = true,
            SuppressOptOut = true,
            StartDate = DateTime.UtcNow.Date.AddDays(7),
            ScheduledDateUtc = DateTime.UtcNow.Date.AddDays(7)
        };

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var data = await GetBuilderDataAsync(cn, tenantId, campaign, cancellationToken);
        return Ok(data);
    }

    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign([FromBody] CommunicationCampaignDto request, CancellationToken cancellationToken)
    {
        await EnsureCampaignDataAsync(request.TenantId, cancellationToken);
        await EnsureMarketingDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Comms.Campaign (CampaignId,TenantId,Name,Type,Status,Segment,Goal,Description,Subject,SenderName,ReplyToEmail,TemplateName,CtaLabel,SendMode,Timezone,FollowUpDays,SendFollowUp,SuppressRecentContacts,SuppressOptOut,AbTestSubject,EndDate,ScheduledDateUtc,LandingPageSlug,LandingPageUrl,StartDate,Reached,OpenRate,Conversions,Revenue,CreatedDateUtc,IsDeleted)
VALUES (@Id,@TenantId,@Name,@Type,@Status,@Segment,@Goal,@Description,@Subject,@SenderName,@ReplyToEmail,@TemplateName,@CtaLabel,@SendMode,@Timezone,@FollowUpDays,@SendFollowUp,@SuppressRecentContacts,@SuppressOptOut,@AbTestSubject,@EndDate,@ScheduledDateUtc,@LandingPageSlug,@LandingPageUrl,@StartDate,@Reached,@OpenRate,@Conversions,@Revenue,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, ToCampaignParams(id, request), cancellationToken: cancellationToken));
        await SyncCampaignManagementRecordsAsync(cn, id, request, cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("campaigns/{id:guid}")]
    public async Task<IActionResult> UpdateCampaign(Guid id, [FromBody] CommunicationCampaignDto request, CancellationToken cancellationToken)
    {
        await EnsureCampaignDataAsync(request.TenantId, cancellationToken);
        await EnsureMarketingDataAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Comms.Campaign
SET Name=@Name, Type=@Type, Status=@Status, Segment=@Segment, Goal=@Goal, Description=@Description,
    Subject=@Subject, SenderName=@SenderName, ReplyToEmail=@ReplyToEmail, TemplateName=@TemplateName,
    CtaLabel=@CtaLabel, SendMode=@SendMode, Timezone=@Timezone, FollowUpDays=@FollowUpDays,
    SendFollowUp=@SendFollowUp, SuppressRecentContacts=@SuppressRecentContacts, SuppressOptOut=@SuppressOptOut,
    AbTestSubject=@AbTestSubject, EndDate=@EndDate, ScheduledDateUtc=@ScheduledDateUtc,
    LandingPageSlug=@LandingPageSlug, LandingPageUrl=@LandingPageUrl, StartDate=@StartDate,
    Reached=@Reached, OpenRate=@OpenRate, Conversions=@Conversions, Revenue=@Revenue, ModifiedDateUtc=SYSUTCDATETIME()
WHERE CampaignId=@Id AND TenantId=@TenantId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, ToCampaignParams(id, request), cancellationToken: cancellationToken));
        if (affected == 0) return NotFound();
        await SyncCampaignManagementRecordsAsync(cn, id, request, cancellationToken);
        return NoContent();
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> GetAppointments([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        await EnsureAppointmentDataAsync(tenantId, cancellationToken);
        const string sql = @"
SELECT AppointmentId, TenantId, AccountName, ContactName, Type, Channel, Status, Duration, Producer, CsrOwner, Branch, Notes, Outcome, OutcomeNotes, FollowUp, SendConfirmation, SendReminder, ScheduledDate, ScheduledTime, ConfirmationStatus, ReminderStatus, SlaStatus, SourceSystem, SyncStatus, LastReminderSentUtc, LastSyncedDateUtc, CreatedDateUtc, ModifiedDateUtc
FROM Comms.Appointment
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR AccountName LIKE '%' + @SearchTerm + '%' OR ContactName LIKE '%' + @SearchTerm + '%' OR Type LIKE '%' + @SearchTerm + '%' OR Producer LIKE '%' + @SearchTerm + '%' OR CsrOwner LIKE '%' + @SearchTerm + '%')
ORDER BY ScheduledDate, ScheduledTime, AccountName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<CommunicationAppointmentDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).AsList();
        return Ok(new PagedResult<CommunicationAppointmentDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpPost("appointments")]
    public async Task<IActionResult> CreateAppointment([FromBody] UpsertCommunicationAppointmentRequest request, CancellationToken cancellationToken)
    {
        await EnsureAppointmentDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Comms.Appointment (AppointmentId,TenantId,AccountName,ContactName,Type,Channel,Status,Duration,Producer,CsrOwner,Branch,Notes,Outcome,OutcomeNotes,FollowUp,SendConfirmation,SendReminder,ScheduledDate,ScheduledTime,ConfirmationStatus,ReminderStatus,SlaStatus,SourceSystem,SyncStatus,LastSyncedDateUtc,CreatedDateUtc,IsDeleted)
VALUES (@AppointmentId,@TenantId,@AccountName,@ContactName,@Type,@Channel,@Status,@Duration,@Producer,@CsrOwner,@Branch,@Notes,N'',N'',N'',@SendConfirmation,@SendReminder,@ScheduledDate,@ScheduledTime,CASE WHEN @SendConfirmation=1 THEN N'Pending' ELSE N'Not Required' END,CASE WHEN @SendReminder=1 THEN N'Scheduled' ELSE N'Not Required' END,@SlaStatus,N'AMS',N'Synced',SYSUTCDATETIME(),SYSUTCDATETIME(),0);
INSERT INTO Comms.AppointmentAuditLog (TenantId,AppointmentId,ActionName,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@AppointmentId,N'Created',N'Appointment scheduled from Tenant Appointment Control Center.',SYSUTCDATETIME(),0);
INSERT INTO Comms.AppointmentProviderSync (TenantId,AppointmentId,ProviderName,SyncStatus,ExternalAppointmentId,LastSyncDateUtc,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@AppointmentId,N'AMS Calendar',N'Synced',CONVERT(nvarchar(160),@AppointmentId),SYSUTCDATETIME(),N'Appointment synchronized to tenant schedule.',SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, ToAppointmentParams(id, request), cancellationToken: cancellationToken));
        return Ok(new { id });
    }

    [HttpPut("appointments/{id:guid}")]
    public async Task<IActionResult> UpdateAppointment(Guid id, [FromBody] UpsertCommunicationAppointmentRequest request, CancellationToken cancellationToken)
    {
        await EnsureAppointmentDataAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Comms.Appointment
SET AccountName=@AccountName, ContactName=@ContactName, Type=@Type, Channel=@Channel, Status=@Status, Duration=@Duration, Producer=@Producer, CsrOwner=@CsrOwner, Branch=@Branch, Notes=@Notes, SendConfirmation=@SendConfirmation, SendReminder=@SendReminder, ScheduledDate=@ScheduledDate, ScheduledTime=@ScheduledTime, ConfirmationStatus=CASE WHEN @SendConfirmation=1 AND ConfirmationStatus='Not Required' THEN N'Pending' WHEN @SendConfirmation=0 THEN N'Not Required' ELSE ConfirmationStatus END, ReminderStatus=CASE WHEN @SendReminder=1 AND ReminderStatus='Not Required' THEN N'Scheduled' WHEN @SendReminder=0 THEN N'Not Required' ELSE ReminderStatus END, SlaStatus=@SlaStatus, SyncStatus=N'Synced', LastSyncedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME()
WHERE AppointmentId=@AppointmentId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Comms.AppointmentAuditLog (TenantId,AppointmentId,ActionName,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@AppointmentId,N'Updated',N'Appointment details updated.',SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, ToAppointmentParams(id, request), cancellationToken: cancellationToken));
        return affected == 0 ? NotFound() : NoContent();
    }

    [HttpPost("appointments/{id:guid}/outcome")]
    public async Task<IActionResult> LogAppointmentOutcome(Guid id, [FromQuery] Guid tenantId, [FromBody] AppointmentOutcomeRequest request, CancellationToken cancellationToken)
    {
        await EnsureAppointmentDataAsync(tenantId, cancellationToken);
        var status = request.Outcome.StartsWith("Completed", StringComparison.OrdinalIgnoreCase) ? "Completed" : request.Outcome.StartsWith("No Answer", StringComparison.OrdinalIgnoreCase) ? "No Answer" : request.Outcome.StartsWith("Rescheduled", StringComparison.OrdinalIgnoreCase) ? "Rescheduled" : "Cancelled";
        const string sql = @"
UPDATE Comms.Appointment SET Status=@Status, Outcome=@Outcome, OutcomeNotes=@Notes, FollowUp=@FollowUp, SlaStatus=CASE WHEN @Status='Completed' THEN N'Met' ELSE N'Closed' END, SyncStatus=N'Synced', LastSyncedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME() WHERE AppointmentId=@AppointmentId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Comms.AppointmentAuditLog (TenantId,AppointmentId,ActionName,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@AppointmentId,N'Outcome Logged',@Outcome,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { AppointmentId = id, TenantId = tenantId, Status = status, request.Outcome, Notes = request.Notes ?? string.Empty, FollowUp = request.FollowUp ?? "None" }, cancellationToken: cancellationToken));
        return affected == 0 ? NotFound() : NoContent();
    }

    [HttpPost("appointments/{id:guid}/status")]
    public async Task<IActionResult> UpdateAppointmentStatus(Guid id, [FromQuery] Guid tenantId, [FromBody] AppointmentStatusRequest request, CancellationToken cancellationToken)
    {
        await EnsureAppointmentDataAsync(tenantId, cancellationToken);
        const string sql = @"
UPDATE Comms.Appointment SET Status=@Status, SlaStatus=CASE WHEN @Status='Cancelled' THEN N'Closed' WHEN @Status='Rescheduled' THEN N'At Risk' WHEN @Status='Completed' THEN N'Met' ELSE N'On Track' END, ConfirmationStatus=CASE WHEN @Status='Cancelled' THEN N'Cancelled' ELSE ConfirmationStatus END, ReminderStatus=CASE WHEN @Status='Cancelled' THEN N'Cancelled' ELSE ReminderStatus END, SyncStatus=N'Synced', LastSyncedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME() WHERE AppointmentId=@AppointmentId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Comms.AppointmentAuditLog (TenantId,AppointmentId,ActionName,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@AppointmentId,@Status,COALESCE(@Reason,N''),SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { AppointmentId = id, TenantId = tenantId, request.Status, request.Reason }, cancellationToken: cancellationToken));
        return affected == 0 ? NotFound() : NoContent();
    }

    [HttpPost("appointments/{id:guid}/reminder")]
    public async Task<IActionResult> SendAppointmentReminder(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureAppointmentDataAsync(tenantId, cancellationToken);
        const string sql = @"
UPDATE Comms.Appointment SET ReminderStatus=N'Sent', LastReminderSentUtc=SYSUTCDATETIME(), SyncStatus=N'Synced', LastSyncedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME() WHERE AppointmentId=@AppointmentId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Comms.AppointmentReminder (TenantId,AppointmentId,Channel,Status,ScheduledDateUtc,SentDateUtc,Message,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@AppointmentId,N'Tenant Preference',N'Sent',SYSUTCDATETIME(),SYSUTCDATETIME(),N'Appointment reminder sent from Tenant Appointment Control Center.',SYSUTCDATETIME(),0);
INSERT INTO Comms.AppointmentAuditLog (TenantId,AppointmentId,ActionName,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@AppointmentId,N'Reminder Sent',N'Manual reminder sent.',SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { AppointmentId = id, TenantId = tenantId }, cancellationToken: cancellationToken));
        return affected == 0 ? NotFound() : NoContent();
    }

    private static object ToCampaignParams(Guid id, CommunicationCampaignDto request)
        => new
        {
            Id = id,
            request.TenantId,
            Name = request.Name.Trim(),
            Type = string.IsNullOrWhiteSpace(request.Type) ? "Email" : request.Type.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status.Trim(),
            Segment = string.IsNullOrWhiteSpace(request.Segment) ? "All Contacts" : request.Segment.Trim(),
            Goal = string.IsNullOrWhiteSpace(request.Goal) ? "Cross-Sell" : request.Goal.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Subject = request.Subject?.Trim() ?? string.Empty,
            SenderName = string.IsNullOrWhiteSpace(request.SenderName) ? "AgencyBinder Team" : request.SenderName.Trim(),
            ReplyToEmail = string.IsNullOrWhiteSpace(request.ReplyToEmail) ? "marketing@agencybinder.local" : request.ReplyToEmail.Trim(),
            TemplateName = string.IsNullOrWhiteSpace(request.TemplateName) ? "Insurance Benefit Card" : request.TemplateName.Trim(),
            CtaLabel = string.IsNullOrWhiteSpace(request.CtaLabel) ? "Get a Quote" : request.CtaLabel.Trim(),
            SendMode = string.IsNullOrWhiteSpace(request.SendMode) ? "Scheduled" : request.SendMode.Trim(),
            Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "Eastern" : request.Timezone.Trim(),
            FollowUpDays = request.FollowUpDays <= 0 ? 7 : request.FollowUpDays,
            request.SendFollowUp,
            request.SuppressRecentContacts,
            request.SuppressOptOut,
            request.AbTestSubject,
            request.EndDate,
            request.ScheduledDateUtc,
            LandingPageSlug = request.LandingPageSlug?.Trim() ?? string.Empty,
            LandingPageUrl = request.LandingPageUrl?.Trim() ?? string.Empty,
            request.StartDate,
            request.Reached,
            request.OpenRate,
            request.Conversions,
            request.Revenue
        };

    private static object ToAppointmentParams(Guid id, UpsertCommunicationAppointmentRequest request)
        => new
        {
            AppointmentId = id,
            request.TenantId,
            AccountName = request.AccountName.Trim(),
            ContactName = request.ContactName.Trim(),
            Type = string.IsNullOrWhiteSpace(request.Type) ? "Coverage Review" : request.Type.Trim(),
            Channel = string.IsNullOrWhiteSpace(request.Channel) ? "Phone Call" : request.Channel.Trim(),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Scheduled" : request.Status.Trim(),
            Duration = string.IsNullOrWhiteSpace(request.Duration) ? "30 min" : request.Duration.Trim(),
            Producer = request.Producer?.Trim() ?? string.Empty,
            CsrOwner = request.CsrOwner?.Trim() ?? string.Empty,
            Branch = request.Branch?.Trim() ?? string.Empty,
            Notes = request.Notes?.Trim() ?? string.Empty,
            request.SendConfirmation,
            request.SendReminder,
            request.ScheduledDate,
            request.ScheduledTime,
            SlaStatus = request.ScheduledDate.HasValue && request.ScheduledDate.Value.Date < DateTime.UtcNow.Date && request.Status == "Scheduled" ? "At Risk" : "On Track"
        };

    private static object ToOutreachParams(Guid id, UpsertCommunicationOutreachRequest request)
        => new
        {
            OutreachContactId = id,
            request.TenantId,
            AccountName = request.AccountName.Trim(),
            ContactName = request.ContactName.Trim(),
            Email = request.Email?.Trim() ?? string.Empty,
            Phone = request.Phone?.Trim() ?? string.Empty,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "General Follow-Up" : request.Reason.Trim(),
            Priority = string.IsNullOrWhiteSpace(request.Priority) ? "Medium" : request.Priority.Trim(),
            AssignedTo = request.AssignedTo?.Trim() ?? string.Empty,
            Producer = request.Producer?.Trim() ?? string.Empty,
            Branch = request.Branch?.Trim() ?? string.Empty,
            Status = request.OptedOut ? "Opted Out" : string.IsNullOrWhiteSpace(request.Status) ? "Open" : request.Status.Trim(),
            Notes = request.Notes?.Trim() ?? string.Empty,
            request.OptedOut,
            request.NextContactDate
        };

    private static async Task<CommunicationCampaignBuilderDataDto> GetBuilderDataAsync(System.Data.IDbConnection cn, Guid tenantId, CommunicationCampaignDto campaign, CancellationToken cancellationToken)
    {
        const string segmentSql = @"SELECT SegmentId,TenantId,Name,Icon,ColorCss,COALESCE(Description,'') AS Description,ContactCount,IsDynamic,COALESCE(Rules,'') AS Rules,UpdatedDateUtc FROM Marketing.Segment WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY UpdatedDateUtc DESC;";
        const string emailSql = @"SELECT EmailBlastId,TenantId,CampaignId,Name,Subject,COALESCE(PreviewText,'') AS PreviewText,AudienceSegment,SenderName,SenderEmail,Status,ScheduledDateUtc,SentDateUtc,RecipientCount,SentCount,OpenCount,ClickCount,BounceCount,UnsubscribeCount,CreatedDateUtc,ModifiedDateUtc FROM Marketing.EmailBlast WHERE TenantId=@TenantId AND IsDeleted=0 AND (CampaignId=@CampaignId OR CampaignId IS NULL) ORDER BY COALESCE(SentDateUtc,ScheduledDateUtc,CreatedDateUtc) DESC;";
        const string landingSql = @"SELECT LandingPageId,TenantId,CampaignId,@CampaignName AS CampaignName,Name,Slug,TemplateName,Status,COALESCE(PublishedUrl,'') AS PublishedUrl,COALESCE(PrimaryCta,'') AS PrimaryCta,ViewCount,ConversionCount,ConversionRate,LastPublishedDateUtc,CreatedDateUtc,ModifiedDateUtc FROM Marketing.LandingPage WHERE TenantId=@TenantId AND IsDeleted=0 AND (CampaignId=@CampaignId OR CampaignId IS NULL) ORDER BY COALESCE(LastPublishedDateUtc,CreatedDateUtc) DESC;";
        var args = new { TenantId = tenantId, CampaignId = campaign.CampaignId, CampaignName = campaign.Name };
        return new CommunicationCampaignBuilderDataDto
        {
            Campaign = campaign,
            Segments = (await cn.QueryAsync<MarketingSegmentDto>(new CommandDefinition(segmentSql, args, cancellationToken: cancellationToken))).AsList(),
            EmailBlasts = (await cn.QueryAsync<MarketingEmailBlastDto>(new CommandDefinition(emailSql, args, cancellationToken: cancellationToken))).AsList(),
            LandingPages = (await cn.QueryAsync<MarketingLandingPageDto>(new CommandDefinition(landingSql, args, cancellationToken: cancellationToken))).AsList()
        };
    }

    private static async Task SyncCampaignManagementRecordsAsync(System.Data.IDbConnection cn, Guid campaignId, CommunicationCampaignDto request, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE Comms.CampaignAudience SET IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME() WHERE CampaignId=@CampaignId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Comms.CampaignAudience (CampaignAudienceId,TenantId,CampaignId,SegmentName,EstimatedContacts,CreatedDateUtc,IsDeleted)
VALUES (NEWID(),@TenantId,@CampaignId,@Segment,@Reached,SYSUTCDATETIME(),0);

UPDATE Comms.CampaignContent SET IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME() WHERE CampaignId=@CampaignId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Comms.CampaignContent (CampaignContentId,TenantId,CampaignId,Channel,Subject,TemplateName,CtaLabel,BodyHtml,CreatedDateUtc,IsDeleted)
VALUES (NEWID(),@TenantId,@CampaignId,@Type,@Subject,@TemplateName,@CtaLabel,@Description,SYSUTCDATETIME(),0);

UPDATE Comms.CampaignAutomation SET IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME() WHERE CampaignId=@CampaignId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Comms.CampaignAutomation (CampaignAutomationId,TenantId,CampaignId,SendMode,Timezone,FollowUpDays,SendFollowUp,SuppressRecentContacts,SuppressOptOut,AbTestSubject,CreatedDateUtc,IsDeleted)
VALUES (NEWID(),@TenantId,@CampaignId,@SendMode,@Timezone,@FollowUpDays,@SendFollowUp,@SuppressRecentContacts,@SuppressOptOut,@AbTestSubject,SYSUTCDATETIME(),0);

IF NOT EXISTS (SELECT 1 FROM Marketing.EmailBlast WHERE TenantId=@TenantId AND CampaignId=@CampaignId AND IsDeleted=0)
BEGIN
    INSERT INTO Marketing.EmailBlast (EmailBlastId,TenantId,CampaignId,Name,Subject,PreviewText,AudienceSegment,SenderName,SenderEmail,Status,ScheduledDateUtc,RecipientCount,SentCount,OpenCount,ClickCount,BounceCount,UnsubscribeCount,CreatedDateUtc,IsDeleted)
    VALUES (NEWID(),@TenantId,@CampaignId,@Name,@Subject,@Description,@Segment,@SenderName,@ReplyToEmail,@Status,@ScheduledDateUtc,@Reached,0,0,0,0,0,SYSUTCDATETIME(),0);
END
ELSE
BEGIN
    UPDATE Marketing.EmailBlast SET Name=@Name, Subject=@Subject, PreviewText=@Description, AudienceSegment=@Segment, SenderName=@SenderName, SenderEmail=@ReplyToEmail, Status=@Status, ScheduledDateUtc=@ScheduledDateUtc, RecipientCount=@Reached, ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND CampaignId=@CampaignId AND IsDeleted=0;
END;

IF @LandingPageSlug <> ''
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Marketing.LandingPage WHERE TenantId=@TenantId AND CampaignId=@CampaignId AND IsDeleted=0)
    BEGIN
        INSERT INTO Marketing.LandingPage (LandingPageId,TenantId,CampaignId,Name,Slug,TemplateName,Status,PublishedUrl,PrimaryCta,ViewCount,ConversionCount,ConversionRate,CreatedDateUtc,IsDeleted)
        VALUES (NEWID(),@TenantId,@CampaignId,@Name,@LandingPageSlug,@TemplateName,CASE WHEN @Status='Active' THEN 'Published' ELSE 'Draft' END,@LandingPageUrl,@CtaLabel,0,0,0,SYSUTCDATETIME(),0);
    END
    ELSE
    BEGIN
        UPDATE Marketing.LandingPage SET Name=@Name, Slug=@LandingPageSlug, TemplateName=@TemplateName, Status=CASE WHEN @Status='Active' THEN 'Published' ELSE 'Draft' END, PublishedUrl=@LandingPageUrl, PrimaryCta=@CtaLabel, ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND CampaignId=@CampaignId AND IsDeleted=0;
    END;
END;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            request.TenantId,
            CampaignId = campaignId,
            Name = request.Name.Trim(),
            request.Type,
            request.Status,
            Segment = request.Segment.Trim(),
            request.Description,
            request.Subject,
            request.SenderName,
            request.ReplyToEmail,
            request.TemplateName,
            request.CtaLabel,
            request.SendMode,
            request.Timezone,
            FollowUpDays = request.FollowUpDays <= 0 ? 7 : request.FollowUpDays,
            request.SendFollowUp,
            request.SuppressRecentContacts,
            request.SuppressOptOut,
            request.AbTestSubject,
            ScheduledDateUtc = request.ScheduledDateUtc ?? request.StartDate,
            request.LandingPageSlug,
            request.LandingPageUrl,
            request.Reached
        }, cancellationToken: cancellationToken));
    }

    [HttpGet("outreach")]
    public async Task<IActionResult> GetOutreach([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        await EnsureOutreachDataAsync(tenantId, cancellationToken);
        const string sql = @"
SELECT OutreachContactId, TenantId, AccountName, ContactName, Email, Phone, Reason, Priority, AssignedTo, Producer, Branch, Status, LastOutcome, Notes, Attempts, OptedOut, LastContactDate, NextContactDate, SourceSystem, SyncStatus, LastSyncedDateUtc, CreatedDateUtc, ModifiedDateUtc
FROM Comms.OutreachContact
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR AccountName LIKE '%' + @SearchTerm + '%' OR ContactName LIKE '%' + @SearchTerm + '%' OR Reason LIKE '%' + @SearchTerm + '%')
ORDER BY CASE Priority WHEN 'Critical' THEN 0 WHEN 'High' THEN 1 WHEN 'Medium' THEN 2 ELSE 3 END, NextContactDate;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<CommunicationOutreachContactDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).AsList();
        return Ok(new PagedResult<CommunicationOutreachContactDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpPost("outreach")]
    public async Task<IActionResult> CreateOutreach([FromBody] UpsertCommunicationOutreachRequest request, CancellationToken cancellationToken)
    {
        await EnsureOutreachDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Comms.OutreachContact (OutreachContactId,TenantId,AccountName,ContactName,Email,Phone,Reason,Priority,AssignedTo,Producer,Branch,Status,LastOutcome,Notes,Attempts,OptedOut,LastContactDate,NextContactDate,SourceSystem,SyncStatus,LastSyncedDateUtc,CreatedDateUtc,IsDeleted)
VALUES (@OutreachContactId,@TenantId,@AccountName,@ContactName,@Email,@Phone,@Reason,@Priority,@AssignedTo,@Producer,@Branch,@Status,N'',@Notes,0,@OptedOut,NULL,@NextContactDate,N'AMS',N'Synced',SYSUTCDATETIME(),SYSUTCDATETIME(),0);
INSERT INTO Comms.OutreachAuditLog (TenantId,OutreachContactId,ActionName,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@OutreachContactId,N'Created',N'Outreach record created from Tenant Outreach Control Center.',SYSUTCDATETIME(),0);
INSERT INTO Comms.OutreachProviderSync (TenantId,OutreachContactId,ProviderName,SyncStatus,ExternalReference,LastSyncDateUtc,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@OutreachContactId,N'AMS Outreach',N'Synced',CONVERT(nvarchar(160),@OutreachContactId),SYSUTCDATETIME(),N'Outreach record synchronized.',SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, ToOutreachParams(id, request), cancellationToken: cancellationToken));
        return Ok(new { id });
    }

    [HttpPut("outreach/{id:guid}")]
    public async Task<IActionResult> UpdateOutreach(Guid id, [FromBody] UpsertCommunicationOutreachRequest request, CancellationToken cancellationToken)
    {
        await EnsureOutreachDataAsync(request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Comms.OutreachContact
SET AccountName=@AccountName, ContactName=@ContactName, Email=@Email, Phone=@Phone, Reason=@Reason, Priority=@Priority, AssignedTo=@AssignedTo, Producer=@Producer, Branch=@Branch, Status=@Status, Notes=@Notes, OptedOut=@OptedOut, NextContactDate=@NextContactDate, SyncStatus=N'Synced', LastSyncedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME()
WHERE OutreachContactId=@OutreachContactId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Comms.OutreachAuditLog (TenantId,OutreachContactId,ActionName,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@OutreachContactId,N'Updated',N'Outreach record updated.',SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, ToOutreachParams(id, request), cancellationToken: cancellationToken));
        return affected == 0 ? NotFound() : NoContent();
    }

    [HttpPost("outreach/{id:guid}/log")]
    public async Task<IActionResult> LogOutreachAttempt(Guid id, [FromQuery] Guid tenantId, [FromBody] OutreachLogAttemptRequest request, CancellationToken cancellationToken)
    {
        await EnsureOutreachDataAsync(tenantId, cancellationToken);
        var status = request.Outcome.Equals("Opted Out", StringComparison.OrdinalIgnoreCase) ? "Opted Out" : request.Outcome.StartsWith("Reached — Completed", StringComparison.OrdinalIgnoreCase) || request.NextAction == "Remove from Queue" ? "Completed" : "In Progress";
        const string sql = @"
UPDATE Comms.OutreachContact
SET Attempts=Attempts+1, LastOutcome=@Outcome, LastContactDate=SYSUTCDATETIME(), NextContactDate=@NextContactDate, Notes=CASE WHEN @Notes='' THEN Notes ELSE @Notes END, Status=@Status, OptedOut=CASE WHEN @Status='Opted Out' THEN 1 ELSE OptedOut END, SyncStatus=N'Synced', LastSyncedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME()
WHERE OutreachContactId=@OutreachContactId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Comms.OutreachAuditLog (TenantId,OutreachContactId,ActionName,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@OutreachContactId,N'Attempt Logged',@Outcome,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { OutreachContactId = id, TenantId = tenantId, request.Outcome, NextContactDate = request.NextContactDate, Notes = request.Notes?.Trim() ?? string.Empty, Status = status }, cancellationToken: cancellationToken));
        return affected == 0 ? NotFound() : NoContent();
    }

    [HttpPost("outreach/assign")]
    public async Task<IActionResult> AssignOutreach([FromQuery] Guid tenantId, [FromBody] OutreachAssignRequest request, CancellationToken cancellationToken)
    {
        await EnsureOutreachDataAsync(tenantId, cancellationToken);
        const string sql = @"
UPDATE Comms.OutreachContact SET AssignedTo=@AssignedTo, SyncStatus=N'Synced', LastSyncedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND OutreachContactId IN @Ids AND IsDeleted=0;
INSERT INTO Comms.OutreachAuditLog (TenantId,OutreachContactId,ActionName,Details,CreatedDateUtc,IsDeleted) SELECT @TenantId, value, N'Assigned', @AssignedTo, SYSUTCDATETIME(), 0 FROM STRING_SPLIT(@IdList, ',');";
        var ids = request.OutreachContactIds?.Where(x => x != Guid.Empty).Distinct().ToArray() ?? [];
        if (ids.Length == 0 || string.IsNullOrWhiteSpace(request.AssignedTo)) return BadRequest();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Ids = ids, IdList = string.Join(',', ids), AssignedTo = request.AssignedTo.Trim() }, cancellationToken: cancellationToken));
        return Ok(new { count = affected });
    }

    [HttpPost("outreach/{id:guid}/status")]
    public async Task<IActionResult> UpdateOutreachStatus(Guid id, [FromQuery] Guid tenantId, [FromBody] OutreachStatusRequest request, CancellationToken cancellationToken)
    {
        await EnsureOutreachDataAsync(tenantId, cancellationToken);
        const string sql = @"
UPDATE Comms.OutreachContact SET Status=@Status, OptedOut=CASE WHEN @Status='Opted Out' THEN 1 ELSE OptedOut END, SyncStatus=N'Synced', LastSyncedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME() WHERE OutreachContactId=@OutreachContactId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Comms.OutreachAuditLog (TenantId,OutreachContactId,ActionName,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@OutreachContactId,@Status,COALESCE(@Reason,N''),SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { OutreachContactId = id, TenantId = tenantId, request.Status, request.Reason }, cancellationToken: cancellationToken));
        return affected == 0 ? NotFound() : NoContent();
    }

    [HttpPost("outreach/batch-sms")]
    public async Task<IActionResult> SendOutreachBatchSms([FromQuery] Guid tenantId, [FromBody] OutreachBatchSmsRequest request, CancellationToken cancellationToken)
    {
        await EnsureOutreachDataAsync(tenantId, cancellationToken);
        if (!request.TcpaConsentConfirmed || string.IsNullOrWhiteSpace(request.Message)) return BadRequest();
        var ids = request.OutreachContactIds?.Where(x => x != Guid.Empty).Distinct().ToArray() ?? [];
        if (ids.Length == 0) return BadRequest();
        const string sql = @"
INSERT INTO Comms.OutreachDelivery (TenantId,OutreachContactId,Channel,TemplateName,Message,Status,SentDateUtc,CreatedDateUtc,IsDeleted)
SELECT @TenantId, OutreachContactId, N'SMS', @TemplateName, @Message, N'Sent', SYSUTCDATETIME(), SYSUTCDATETIME(), 0 FROM Comms.OutreachContact WHERE TenantId=@TenantId AND OutreachContactId IN @Ids AND IsDeleted=0 AND (@HonorOptOut=0 OR OptedOut=0);
UPDATE Comms.OutreachContact SET LastOutcome=N'SMS Sent', LastContactDate=SYSUTCDATETIME(), Attempts=Attempts+1, Status=CASE WHEN Status='Open' THEN N'In Progress' ELSE Status END, SyncStatus=N'Synced', LastSyncedDateUtc=SYSUTCDATETIME(), ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND OutreachContactId IN @Ids AND IsDeleted=0 AND (@HonorOptOut=0 OR OptedOut=0);
INSERT INTO Comms.OutreachAuditLog (TenantId,OutreachContactId,ActionName,Details,CreatedDateUtc,IsDeleted) SELECT @TenantId, OutreachContactId, N'Batch SMS Sent', @TemplateName, SYSUTCDATETIME(), 0 FROM Comms.OutreachContact WHERE TenantId=@TenantId AND OutreachContactId IN @Ids AND IsDeleted=0 AND (@HonorOptOut=0 OR OptedOut=0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Ids = ids, Message = request.Message.Trim(), TemplateName = request.TemplateName?.Trim() ?? string.Empty, request.HonorOptOut }, cancellationToken: cancellationToken));
        return Ok(new { count = affected });
    }

    [HttpDelete("outreach/{id:guid}")]
    public async Task<IActionResult> DeleteOutreach(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureOutreachDataAsync(tenantId, cancellationToken);
        const string sql = @"
UPDATE Comms.OutreachContact SET IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME() WHERE OutreachContactId=@OutreachContactId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Comms.OutreachAuditLog (TenantId,OutreachContactId,ActionName,Details,CreatedDateUtc,IsDeleted) VALUES (@TenantId,@OutreachContactId,N'Deleted',N'Outreach record removed from active queue.',SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { OutreachContactId = id, TenantId = tenantId }, cancellationToken: cancellationToken));
        return affected == 0 ? NotFound() : NoContent();
    }
}
