using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Submissions;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Data;
using System.Text;
using System.Text.Json;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SubmissionRepository : ISubmissionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public SubmissionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string RecalculateSubmissionStatusSql = @"
DECLARE @DerivedSubmissionStatus NVARCHAR(50) = CASE
    WHEN EXISTS (SELECT 1 FROM Submissions.BoundPolicy bp WHERE bp.SubmissionId = @SubmissionId AND bp.TenantId = @TenantId AND bp.IsDeleted = 0) THEN N'Bound'
    WHEN EXISTS (SELECT 1 FROM Submissions.PolicyBindTransaction pbt WHERE pbt.SubmissionId = @SubmissionId AND pbt.TenantId = @TenantId AND pbt.IsDeleted = 0 AND pbt.BindStatusCode IN (N'Pending', N'CarrierReviewing', N'WaitingPayment', N'WaitingDocuments', N'Approved', N'Draft', N'PendingApproval', N'ReadyToBind', N'Submitted', N'Acknowledged', N'MoreInformationRequired', N'Confirmed')) THEN N'Binding'
    WHEN OBJECT_ID(N'Submissions.ClientAcceptance', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM Submissions.ClientAcceptance ca WHERE ca.SubmissionId = @SubmissionId AND ca.TenantId = @TenantId AND ca.IsDeleted = 0 AND ca.StatusCode IN (N'Accepted', N'BindRequested')) THEN N'Customer Accepted'
    WHEN EXISTS (SELECT 1 FROM Submissions.CustomerAuthorization ca WHERE ca.SubmissionId = @SubmissionId AND ca.TenantId = @TenantId AND ca.IsDeleted = 0) THEN N'Customer Accepted'
    WHEN EXISTS (SELECT 1 FROM Submissions.Proposal p WHERE p.SubmissionId = @SubmissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0 AND p.Status = N'Accepted') THEN N'Customer Accepted'
    WHEN EXISTS (SELECT 1 FROM Submissions.Proposal p WHERE p.SubmissionId = @SubmissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0 AND p.Status IN (N'Sent', N'Pending Decision')) THEN N'Presented'
    WHEN EXISTS (SELECT 1 FROM Submissions.Proposal p WHERE p.SubmissionId = @SubmissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0) THEN N'Proposal Prepared'
    WHEN EXISTS (SELECT 1 FROM Submissions.Quote q WHERE q.SubmissionId = @SubmissionId AND q.IsDeleted = 0) THEN N'Quotes Received'
    WHEN EXISTS (SELECT 1 FROM Submissions.QuoteRequest qr WHERE qr.SubmissionId = @SubmissionId AND qr.TenantId = @TenantId AND qr.IsDeleted = 0) THEN N'Marketing'
    ELSE N'Ready for Marketing'
END;

UPDATE Submissions.Submission
SET Status = CASE
        WHEN @DerivedSubmissionStatus = N'Bound' THEN N'Bound'
        WHEN Status IN (N'Lost', N'Cancelled', N'Closed') THEN Status
        ELSE @DerivedSubmissionStatus END,
    QuoteCount = (SELECT COUNT(1) FROM Submissions.Quote WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    MarketCount = (SELECT COUNT(1) FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId
  AND TenantId = @TenantId
  AND IsDeleted = 0;";

    private static async Task EnsureEnterpriseWorkflowSchemaAsync(System.Data.IDbConnection cn, Guid? tenantId, CancellationToken cancellationToken)
    {
        const string readinessActionColumnsSql = @"
IF OBJECT_ID(N'Submissions.SubmissionReadinessRequirement', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'ActionCode') IS NULL
        EXEC(N'ALTER TABLE Submissions.SubmissionReadinessRequirement ADD ActionCode NVARCHAR(50) NULL;');
    IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'ActionLabel') IS NULL
        EXEC(N'ALTER TABLE Submissions.SubmissionReadinessRequirement ADD ActionLabel NVARCHAR(150) NULL;');
END;";

        await cn.ExecuteAsync(new CommandDefinition(readinessActionColumnsSql, cancellationToken: cancellationToken));

        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Submissions') EXEC(N'CREATE SCHEMA Submissions');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Core') EXEC(N'CREATE SCHEMA Core');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Agency') EXEC(N'CREATE SCHEMA Agency');

IF OBJECT_ID(N'Core.CarrierMarketSuggestionPreference', N'U') IS NULL
BEGIN
    CREATE TABLE Core.CarrierMarketSuggestionPreference
    (
        CarrierMarketSuggestionPreferenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Core_CarrierMarketSuggestionPreference PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CarrierId UNIQUEIDENTIFIER NOT NULL,
        LineOfBusiness NVARCHAR(100) NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_SortOrder DEFAULT 500,
        IsActive BIT NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_IsActive DEFAULT 1,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_IsDeleted DEFAULT 0
    );
END;

IF OBJECT_ID(N'Agency.CarrierExternalConnector', N'U') IS NULL
BEGIN
    CREATE TABLE Agency.CarrierExternalConnector
    (
        CarrierExternalConnectorId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Agency_CarrierExternalConnector_Runtime PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CarrierId UNIQUEIDENTIFIER NULL,
        ConnectorCode NVARCHAR(100) NOT NULL,
        ConnectorName NVARCHAR(200) NOT NULL,
        ConnectorTypeCode NVARCHAR(50) NOT NULL,
        ExecutionModeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CarrierExternalConnector_Mode_Runtime DEFAULT N'ExternalConnector',
        EndpointUri NVARCHAR(1000) NULL,
        DefaultChannelCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CarrierExternalConnector_Channel_Runtime DEFAULT N'InternalQueue',
        SupportsDocumentPackage BIT NOT NULL CONSTRAINT DF_CarrierExternalConnector_DocumentPackage_Runtime DEFAULT 1,
        SupportsDeliveryConfirmation BIT NOT NULL CONSTRAINT DF_CarrierExternalConnector_Confirmation_Runtime DEFAULT 1,
        SupportsInboundResponse BIT NOT NULL CONSTRAINT DF_CarrierExternalConnector_Inbound_Runtime DEFAULT 1,
        ConfigurationJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CarrierExternalConnector_Config_Runtime DEFAULT N'{}',
        UiSchemaJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CarrierExternalConnector_Ui_Runtime DEFAULT N'{}',
        IsActive BIT NOT NULL CONSTRAINT DF_CarrierExternalConnector_Active_Runtime DEFAULT 1,
        SortOrder INT NOT NULL CONSTRAINT DF_CarrierExternalConnector_Sort_Runtime DEFAULT 100,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CarrierExternalConnector_Created_Runtime DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_CarrierExternalConnector_IsDeleted_Runtime DEFAULT 0
    );
END;

IF OBJECT_ID(N'Submissions.CarrierTransmission', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.CarrierTransmission
    (
        CarrierTransmissionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_CarrierTransmission_Runtime PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        SubmissionMarketId UNIQUEIDENTIFIER NOT NULL,
        SubmissionMarketDispatchId UNIQUEIDENTIFIER NULL,
        CarrierId UNIQUEIDENTIFIER NOT NULL,
        CarrierExternalConnectorId UNIQUEIDENTIFIER NULL,
        TransmissionTypeCode NVARCHAR(50) NOT NULL,
        ChannelCode NVARCHAR(50) NOT NULL,
        StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CarrierTransmission_Status_Runtime DEFAULT N'Queued',
        Recipient NVARCHAR(500) NULL,
        Subject NVARCHAR(300) NULL,
        EndpointUri NVARCHAR(1000) NULL,
        PayloadJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CarrierTransmission_Payload_Runtime DEFAULT N'{}',
        DocumentPackageJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CarrierTransmission_Docs_Runtime DEFAULT N'[]',
        ExternalReferenceNumber NVARCHAR(120) NULL,
        AttemptCount INT NOT NULL CONSTRAINT DF_CarrierTransmission_Attempts_Runtime DEFAULT 0,
        LastAttemptDateUtc DATETIME2 NULL,
        SentDateUtc DATETIME2 NULL,
        ConfirmedDateUtc DATETIME2 NULL,
        FailedDateUtc DATETIME2 NULL,
        BounceDateUtc DATETIME2 NULL,
        LastError NVARCHAR(2000) NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CarrierTransmission_Created_Runtime DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_CarrierTransmission_IsDeleted_Runtime DEFAULT 0
    );
END;

IF OBJECT_ID(N'Submissions.CarrierTransmissionEvent', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.CarrierTransmissionEvent
    (
        CarrierTransmissionEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_CarrierTransmissionEvent_Runtime PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CarrierTransmissionId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        SubmissionMarketId UNIQUEIDENTIFIER NOT NULL,
        EventCode NVARCHAR(80) NOT NULL,
        EventMessage NVARCHAR(1000) NULL,
        EventPayloadJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CarrierTransmissionEvent_Payload_Runtime DEFAULT N'{}',
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CarrierTransmissionEvent_Created_Runtime DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_CarrierTransmissionEvent_IsDeleted_Runtime DEFAULT 0
    );
END;

IF OBJECT_ID(N'Submissions.CarrierInboundResponse', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.CarrierInboundResponse
    (
        CarrierInboundResponseId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_CarrierInboundResponse_Runtime PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NULL,
        SubmissionMarketId UNIQUEIDENTIFIER NULL,
        CarrierId UNIQUEIDENTIFIER NULL,
        CarrierTransmissionId UNIQUEIDENTIFIER NULL,
        SourceChannelCode NVARCHAR(50) NOT NULL,
        ResponseTypeCode NVARCHAR(50) NOT NULL,
        StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CarrierInboundResponse_Status_Runtime DEFAULT N'Received',
        CarrierReferenceNumber NVARCHAR(120) NULL,
        PayloadJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_CarrierInboundResponse_Payload_Runtime DEFAULT N'{}',
        ReceivedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CarrierInboundResponse_Received_Runtime DEFAULT SYSUTCDATETIME(),
        ProcessedDateUtc DATETIME2 NULL,
        ProcessingError NVARCHAR(2000) NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CarrierInboundResponse_Created_Runtime DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_CarrierInboundResponse_IsDeleted_Runtime DEFAULT 0
    );
END;

IF @TenantId IS NOT NULL AND OBJECT_ID(N'Agency.CarrierExternalConnector', N'U') IS NOT NULL
BEGIN
    INSERT INTO Agency.CarrierExternalConnector
        (CarrierExternalConnectorId, TenantId, CarrierId, ConnectorCode, ConnectorName, ConnectorTypeCode, ExecutionModeCode, EndpointUri, DefaultChannelCode, SupportsDocumentPackage, SupportsDeliveryConfirmation, SupportsInboundResponse, ConfigurationJson, UiSchemaJson, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    SELECT NEWID(), @TenantId, NULL, N'API_RATING_JSON', N'Carrier API Rating Connector', N'RatingApi', N'ExternalConnector', NULL, N'API', 0, 1, 1,
           N'{""deliveryMode"":""jsonApiRating"",""authModes"":[""None"",""ApiKey"",""BearerToken""],""responseContract"":""normalizedQuoteResponse""}',
           N'{""icon"":""bi-speedometer2"",""description"":""Executes DB-backed API rating requests and stores normalized quote responses.""}', 1, 155, SYSUTCDATETIME(), 0
    WHERE NOT EXISTS (SELECT 1 FROM Agency.CarrierExternalConnector existing WHERE existing.TenantId = @TenantId AND existing.CarrierId IS NULL AND existing.ConnectorCode = N'API_RATING_JSON' AND existing.IsDeleted = 0);
END;

IF @TenantId IS NOT NULL AND OBJECT_ID(N'Submissions.ProposalDeliveryProvider', N'U') IS NOT NULL
BEGIN
    INSERT INTO Submissions.ProposalDeliveryProvider
        (ProposalDeliveryProviderId, TenantId, DeliveryMethodCode, ProviderCode, HandlerCode, DisplayName, EndpointUri, SenderAddress, SecretReference, ConfigurationJson, IsConfigured, IsActive, MaxAttempts, RetryDelaySeconds, CreatedDateUtc, IsDeleted)
    SELECT NEWID(), @TenantId, seed.DeliveryMethodCode, seed.ProviderCode, seed.HandlerCode, seed.DisplayName,
           seed.EndpointUri, seed.SenderAddress, seed.SecretReference, seed.ConfigurationJson,
           seed.IsConfigured, 1, seed.MaxAttempts, seed.RetryDelaySeconds, SYSUTCDATETIME(), 0
    FROM (VALUES
        (N'Email', N'TenantSmtp', N'Smtp', N'NetworkSolutions SMTP', N'smtp://netsol-smtp-oxcs.hostingplatform.com:587', N'ams_admin@agencybinder.com', N'AMS_PROPOSAL_SMTP_PASSWORD', N'{' + NCHAR(34) + N'username' + NCHAR(34) + N':' + NCHAR(34) + N'ams_admin@agencybinder.com' + NCHAR(34) + N',' + NCHAR(34) + N'enableSsl' + NCHAR(34) + N':' + NCHAR(34) + N'true' + NCHAR(34) + N'}', CAST(1 AS bit), 5, 300),
        (N'Portal', N'AmsPortal', N'Portal', N'AMS Customer Portal', NULL, NULL, NULL, NULL, CAST(1 AS bit), 3, 60),
        (N'ESignature', N'TenantESignature', N'ESignature', N'Tenant E-Signature', NULL, NULL, NULL, NULL, CAST(0 AS bit), 5, 300),
        (N'InPerson', N'ManualDelivery', N'Manual', N'In-Person / Manual Delivery', NULL, NULL, NULL, NULL, CAST(1 AS bit), 1, 10)
    ) seed(DeliveryMethodCode, ProviderCode, HandlerCode, DisplayName, EndpointUri, SenderAddress, SecretReference, ConfigurationJson, IsConfigured, MaxAttempts, RetryDelaySeconds)
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM Submissions.ProposalDeliveryProvider existing
        WHERE existing.TenantId = @TenantId
          AND existing.DeliveryMethodCode = seed.DeliveryMethodCode
          AND existing.IsDeleted = 0
    );
END;

IF @TenantId IS NOT NULL AND OBJECT_ID(N'Submissions.ProposalDeliveryProvider', N'U') IS NOT NULL
BEGIN
    UPDATE Submissions.ProposalDeliveryProvider
    SET ProviderCode = CASE WHEN ProviderCode IN (N'SMTP', N'TenantSmtp') THEN N'TenantSmtp' ELSE ProviderCode END,
        DisplayName = CASE WHEN DisplayName IN (N'Email (SMTP)', N'Tenant SMTP') THEN N'NetworkSolutions SMTP' ELSE DisplayName END,
        EndpointUri = CASE WHEN EndpointUri IN (N'smtp://mail.agencybinder.com:587', N'mail.agencybinder.com') THEN N'smtp://netsol-smtp-oxcs.hostingplatform.com:587' ELSE COALESCE(NULLIF(EndpointUri, N''), N'smtp://netsol-smtp-oxcs.hostingplatform.com:587') END,
        SenderAddress = COALESCE(NULLIF(SenderAddress, N''), N'ams_admin@agencybinder.com'),
        SecretReference = COALESCE(NULLIF(SecretReference, N''), N'AMS_PROPOSAL_SMTP_PASSWORD'),
        ConfigurationJson = COALESCE(NULLIF(ConfigurationJson, N''), N'{' + NCHAR(34) + N'username' + NCHAR(34) + N':' + NCHAR(34) + N'ams_admin@agencybinder.com' + NCHAR(34) + N',' + NCHAR(34) + N'enableSsl' + NCHAR(34) + N':' + NCHAR(34) + N'true' + NCHAR(34) + N'}'),
        IsConfigured = CASE WHEN COALESCE(NULLIF(EndpointUri, N''), N'smtp://netsol-smtp-oxcs.hostingplatform.com:587') IS NOT NULL
                            AND COALESCE(NULLIF(SenderAddress, N''), N'ams_admin@agencybinder.com') IS NOT NULL
                            AND COALESCE(NULLIF(SecretReference, N''), N'AMS_PROPOSAL_SMTP_PASSWORD') IS NOT NULL THEN 1 ELSE IsConfigured END,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId
      AND DeliveryMethodCode = N'Email'
      AND HandlerCode = N'Smtp'
      AND IsDeleted = 0;
END;

IF @TenantId IS NOT NULL AND OBJECT_ID(N'Agency.CarrierSetting', N'U') IS NOT NULL
BEGIN
    INSERT INTO Agency.CarrierSetting
        (CarrierSettingId, TenantId, CarrierId, SettingCode, SettingName, CategoryCode, ScopeCode, DataTypeCode, SettingValue, DefaultValue, Description, ValidationJson, UiSchemaJson, AppliesToExecutorType, IsRequired, IsSecret, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    SELECT NEWID(), @TenantId, NULL, seed.SettingCode, seed.SettingName, seed.CategoryCode, seed.ScopeCode, seed.DataTypeCode, seed.SettingValue, seed.DefaultValue, seed.Description, seed.ValidationJson, seed.UiSchemaJson, N'ApiRatingConnectorWorkerService', seed.IsRequired, seed.IsSecret, 1, seed.SortOrder, SYSUTCDATETIME(), 0
    FROM (VALUES
        (N'API_RATING_WORKER_ENABLED', N'API Rating Worker Enabled', N'ApiRating', N'Tenant', N'Boolean', N'true', N'true', N'Enables DB-backed API rating connector execution for queued quote requests.', N'{""required"":true}', N'{""control"":""toggle"",""icon"":""bi-play-circle""}', CAST(1 AS bit), CAST(0 AS bit), 300),
        (N'API_RATING_WORKER_POLL_SECONDS', N'API Rating Worker Poll Seconds', N'ApiRating', N'Tenant', N'Number', N'30', N'30', N'Polling interval for API rating connector execution.', N'{""min"":10,""max"":3600}', N'{""control"":""number"",""icon"":""bi-clock""}', CAST(1 AS bit), CAST(0 AS bit), 310),
        (N'API_RATING_WORKER_BATCH_SIZE', N'API Rating Worker Batch Size', N'ApiRating', N'Tenant', N'Number', N'10', N'10', N'Maximum queued API rating transmissions processed per poll.', N'{""min"":1,""max"":100}', N'{""control"":""number"",""icon"":""bi-list-ol""}', CAST(1 AS bit), CAST(0 AS bit), 320)
    ) seed(SettingCode, SettingName, CategoryCode, ScopeCode, DataTypeCode, SettingValue, DefaultValue, Description, ValidationJson, UiSchemaJson, IsRequired, IsSecret, SortOrder)
    WHERE NOT EXISTS (SELECT 1 FROM Agency.CarrierSetting existing WHERE existing.TenantId = @TenantId AND existing.CarrierId IS NULL AND existing.SettingCode = seed.SettingCode AND existing.IsDeleted = 0);
END;

IF OBJECT_ID(N'Core.CarrierMarketSuggestionPreference', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'TenantId') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_TenantId_Ensure DEFAULT '00000000-0000-0000-0000-000000000001';
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'CarrierId') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD CarrierId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_CarrierId_Ensure DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'LineOfBusiness') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD LineOfBusiness NVARCHAR(100) NULL;
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'SortOrder') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD SortOrder INT NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_SortOrder_Ensure DEFAULT 500;
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'IsActive') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD IsActive BIT NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_IsActive_Ensure DEFAULT 1;
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'CreatedDateUtc') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_Created_Ensure DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'CreatedByUserId') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'ModifiedDateUtc') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'ModifiedByUserId') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'IsDeleted') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_IsDeleted_Ensure DEFAULT 0;
END;

IF OBJECT_ID(N'Core.CarrierMarketSuggestionPreference', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Core.CarrierMarketSuggestionPreference') AND name = N'UX_Core_CarrierMarketSuggestionPreference_Default')
        EXEC(N'CREATE UNIQUE INDEX UX_Core_CarrierMarketSuggestionPreference_Default ON Core.CarrierMarketSuggestionPreference(TenantId, CarrierId) WHERE LineOfBusiness IS NULL AND IsDeleted = 0;');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Core.CarrierMarketSuggestionPreference') AND name = N'UX_Core_CarrierMarketSuggestionPreference_Line')
        EXEC(N'CREATE UNIQUE INDEX UX_Core_CarrierMarketSuggestionPreference_Line ON Core.CarrierMarketSuggestionPreference(TenantId, CarrierId, LineOfBusiness) WHERE LineOfBusiness IS NOT NULL AND IsDeleted = 0;');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Core.CarrierMarketSuggestionPreference') AND name = N'IX_Core_CarrierMarketSuggestionPreference_Tenant_Sort')
        EXEC(N'CREATE INDEX IX_Core_CarrierMarketSuggestionPreference_Tenant_Sort ON Core.CarrierMarketSuggestionPreference(TenantId, LineOfBusiness, IsActive, SortOrder, IsDeleted);');
END;

IF OBJECT_ID(N'Submissions.SubmissionIntakeQuestion', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.SubmissionIntakeQuestion
    (
        IntakeQuestionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SubmissionIntakeQuestion PRIMARY KEY DEFAULT NEWID(),
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        QuestionCode NVARCHAR(100) NOT NULL,
        QuestionText NVARCHAR(500) NOT NULL,
        HelpText NVARCHAR(1000) NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_SubmissionIntakeQuestion_IsRequired DEFAULT 1,
        AnswerText NVARCHAR(2000) NULL,
        IsAnswered BIT NOT NULL CONSTRAINT DF_SubmissionIntakeQuestion_IsAnswered DEFAULT 0,
        AnsweredByUserId UNIQUEIDENTIFIER NULL,
        AnsweredDateUtc DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionIntakeQuestion_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc DATETIME2 NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionIntakeQuestion_IsDeleted DEFAULT 0
    );
END;

IF OBJECT_ID(N'Submissions.SubmissionReadinessRequirement', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.SubmissionReadinessRequirement
    (
        ReadinessRequirementId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SubmissionReadinessRequirement PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        LineOfBusiness NVARCHAR(100) NOT NULL,
        RequirementCode NVARCHAR(100) NOT NULL,
        RequirementTypeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_Type_Runtime DEFAULT N'IntakeConfirmation',
        DisplayName NVARCHAR(200) NOT NULL,
        Description NVARCHAR(1000) NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_IsRequired_Runtime DEFAULT 1,
        AllowsWaiver BIT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_AllowsWaiver_Runtime DEFAULT 1,
        RequiresEvidence BIT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_RequiresEvidence_Runtime DEFAULT 0,
        ScoreWeight INT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_ScoreWeight_Runtime DEFAULT 1,
        SortOrder INT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_SortOrder_Runtime DEFAULT 0,
        IsActive BIT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_IsActive_Runtime DEFAULT 1,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_Created_Runtime DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        ActionCode NVARCHAR(50) NULL,
        ActionLabel NVARCHAR(150) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_IsDeleted_Runtime DEFAULT 0
    );
END;

IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'RequirementTypeCode') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD RequirementTypeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_Type_Ensure DEFAULT N'IntakeConfirmation';
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'Description') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD Description NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'AllowsWaiver') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD AllowsWaiver BIT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_AllowsWaiver_Ensure DEFAULT 1;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'RequiresEvidence') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD RequiresEvidence BIT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_RequiresEvidence_Ensure DEFAULT 0;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'ScoreWeight') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD ScoreWeight INT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_ScoreWeight_Ensure DEFAULT 1;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'SortOrder') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD SortOrder INT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_SortOrder_Ensure DEFAULT 0;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'IsActive') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD IsActive BIT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_IsActive_Ensure DEFAULT 1;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'CreatedDateUtc') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_Created_Ensure DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'IsDeleted') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_IsDeleted_Ensure DEFAULT 0;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'CarrierId') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD CarrierId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'StateCode') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD StateCode NVARCHAR(20) NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'ChannelCode') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD ChannelCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'ScopeCode') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD ScopeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_Scope_Ensure DEFAULT N'Submission';
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'EvidencePrompt') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD EvidencePrompt NVARCHAR(500) NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'ApprovalRoleCode') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD ApprovalRoleCode NVARCHAR(100) NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'BlocksSubmit') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD BlocksSubmit BIT NOT NULL CONSTRAINT DF_SubmissionReadinessRequirement_BlocksSubmit_Ensure DEFAULT 1;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'ActionCode') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD ActionCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'ActionLabel') IS NULL ALTER TABLE Submissions.SubmissionReadinessRequirement ADD ActionLabel NVARCHAR(150) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionIntakeQuestion') AND name = N'UX_SubmissionIntakeQuestion_Submission_Code')
    EXEC(N'CREATE UNIQUE INDEX UX_SubmissionIntakeQuestion_Submission_Code ON Submissions.SubmissionIntakeQuestion(SubmissionId, QuestionCode) WHERE IsDeleted = 0;');

IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'ReadinessRequirementId') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD ReadinessRequirementId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'StatusCode') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_SubmissionIntakeQuestion_StatusCode_Ensure DEFAULT N'NeedsReview';
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'StatusReason') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD StatusReason NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'EvidenceDocumentId') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD EvidenceDocumentId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'WaiverReason') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD WaiverReason NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'WaivedByUserId') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD WaivedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'WaivedDateUtc') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD WaivedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'CompletedByUserId') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD CompletedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'CompletedDateUtc') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD CompletedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'ReviewDueDateUtc') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD ReviewDueDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'ScoreWeight') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD ScoreWeight INT NOT NULL CONSTRAINT DF_SubmissionIntakeQuestion_ScoreWeight_Ensure DEFAULT 1;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'SortOrder') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD SortOrder INT NOT NULL CONSTRAINT DF_SubmissionIntakeQuestion_SortOrder_Ensure DEFAULT 0;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'SubmissionMarketId') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD SubmissionMarketId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'CarrierId') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD CarrierId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'ScopeCode') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD ScopeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_SubmissionIntakeQuestion_Scope_Ensure DEFAULT N'Submission';
IF COL_LENGTH(N'Submissions.SubmissionIntakeQuestion', N'BlocksSubmit') IS NULL ALTER TABLE Submissions.SubmissionIntakeQuestion ADD BlocksSubmit BIT NOT NULL CONSTRAINT DF_SubmissionIntakeQuestion_BlocksSubmit_Ensure DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionReadinessRequirement') AND name = N'UX_SubmissionReadinessRequirement_Tenant_Lob_Code')
    EXEC(N'CREATE UNIQUE INDEX UX_SubmissionReadinessRequirement_Tenant_Lob_Code ON Submissions.SubmissionReadinessRequirement(TenantId, LineOfBusiness, RequirementCode) WHERE IsDeleted = 0;');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionIntakeQuestion') AND name = N'IX_SubmissionIntakeQuestion_Requirement')
    EXEC(N'CREATE INDEX IX_SubmissionIntakeQuestion_Requirement ON Submissions.SubmissionIntakeQuestion(TenantId, ReadinessRequirementId, StatusCode, IsDeleted);');

IF OBJECT_ID(N'Submissions.SubmissionReadinessEvidenceDocument', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.SubmissionReadinessEvidenceDocument
    (
        SubmissionReadinessEvidenceDocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SubmissionReadinessEvidenceDocument_Runtime PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        IntakeQuestionId UNIQUEIDENTIFIER NOT NULL,
        ReadinessRequirementId UNIQUEIDENTIFIER NULL,
        SubmissionMarketId UNIQUEIDENTIFIER NULL,
        CarrierId UNIQUEIDENTIFIER NULL,
        DocumentId UNIQUEIDENTIFIER NOT NULL,
        EvidenceRoleCode NVARCHAR(50) NOT NULL CONSTRAINT DF_SubmissionReadinessEvidenceDocument_Role_Runtime DEFAULT N'SupportingEvidence',
        Notes NVARCHAR(1000) NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionReadinessEvidenceDocument_Created_Runtime DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionReadinessEvidenceDocument_IsDeleted_Runtime DEFAULT 0
    );
END;

IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'TenantId') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SubmissionReadinessEvidenceDocument_Tenant_Runtime DEFAULT '00000000-0000-0000-0000-000000000001';
IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'SubmissionId') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD SubmissionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SubmissionReadinessEvidenceDocument_Submission_Runtime DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'IntakeQuestionId') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD IntakeQuestionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SubmissionReadinessEvidenceDocument_Intake_Runtime DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'ReadinessRequirementId') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD ReadinessRequirementId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'SubmissionMarketId') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD SubmissionMarketId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'CarrierId') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD CarrierId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'DocumentId') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD DocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SubmissionReadinessEvidenceDocument_Document_Runtime DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'EvidenceRoleCode') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD EvidenceRoleCode NVARCHAR(50) NOT NULL CONSTRAINT DF_SubmissionReadinessEvidenceDocument_RoleB_Runtime DEFAULT N'SupportingEvidence';
IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'Notes') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD Notes NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'CreatedDateUtc') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionReadinessEvidenceDocument_CreatedB_Runtime DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessEvidenceDocument', N'IsDeleted') IS NULL ALTER TABLE Submissions.SubmissionReadinessEvidenceDocument ADD IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionReadinessEvidenceDocument_IsDeletedB_Runtime DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionReadinessEvidenceDocument') AND name = N'UX_SubmissionReadinessEvidenceDocument_Intake_Document')
    EXEC(N'CREATE UNIQUE INDEX UX_SubmissionReadinessEvidenceDocument_Intake_Document ON Submissions.SubmissionReadinessEvidenceDocument(IntakeQuestionId, DocumentId) WHERE IsDeleted = 0;');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionReadinessEvidenceDocument') AND name = N'IX_SubmissionReadinessEvidenceDocument_Submission')
    EXEC(N'CREATE INDEX IX_SubmissionReadinessEvidenceDocument_Submission ON Submissions.SubmissionReadinessEvidenceDocument(TenantId, SubmissionId, IntakeQuestionId, IsDeleted) INCLUDE (DocumentId, SubmissionMarketId, CarrierId);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionReadinessEvidenceDocument') AND name = N'IX_SubmissionReadinessEvidenceDocument_Document')
    EXEC(N'CREATE INDEX IX_SubmissionReadinessEvidenceDocument_Document ON Submissions.SubmissionReadinessEvidenceDocument(TenantId, DocumentId, IsDeleted);');

IF OBJECT_ID(N'DMS.Document', N'U') IS NOT NULL
BEGIN
    INSERT INTO Submissions.SubmissionReadinessEvidenceDocument
        (SubmissionReadinessEvidenceDocumentId, TenantId, SubmissionId, IntakeQuestionId, ReadinessRequirementId, SubmissionMarketId, CarrierId, DocumentId, EvidenceRoleCode, Notes, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT NEWID(), q.TenantId, q.SubmissionId, q.IntakeQuestionId, q.ReadinessRequirementId, q.SubmissionMarketId, q.CarrierId, q.EvidenceDocumentId, N'SupportingEvidence', N'Migrated from legacy readiness EvidenceDocumentId.', SYSUTCDATETIME(), q.AnsweredByUserId, 0
    FROM Submissions.SubmissionIntakeQuestion q
    INNER JOIN DMS.Document d ON d.DocumentId = q.EvidenceDocumentId AND d.TenantId = q.TenantId AND d.EntityName = N'Submission' AND d.EntityId = q.SubmissionId AND d.IsDeleted = 0
    WHERE q.EvidenceDocumentId IS NOT NULL
      AND q.IsDeleted = 0
      AND NOT EXISTS
      (
          SELECT 1
          FROM Submissions.SubmissionReadinessEvidenceDocument existing
          WHERE existing.IntakeQuestionId = q.IntakeQuestionId
            AND existing.DocumentId = q.EvidenceDocumentId
            AND existing.IsDeleted = 0
      );
END;

IF OBJECT_ID(N'Submissions.SubmissionDocumentRequirement', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.SubmissionDocumentRequirement
    (
        DocumentRequirementId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SubmissionDocumentRequirement PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        LineOfBusiness NVARCHAR(100) NOT NULL,
        CategoryCode NVARCHAR(100) NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_IsRequired DEFAULT 1,
        SortOrder INT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_SortOrder DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_IsDeleted DEFAULT 0
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionDocumentRequirement') AND name = N'UX_SubmissionDocumentRequirement_Tenant_Lob_Code')
    EXEC(N'CREATE UNIQUE INDEX UX_SubmissionDocumentRequirement_Tenant_Lob_Code ON Submissions.SubmissionDocumentRequirement(TenantId, LineOfBusiness, CategoryCode) WHERE IsDeleted = 0;');

IF OBJECT_ID(N'Submissions.SubmissionMarketDocument', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.SubmissionMarketDocument
    (
        SubmissionMarketDocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SubmissionMarketDocument PRIMARY KEY DEFAULT NEWID(),
        SubmissionMarketId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        DocumentId UNIQUEIDENTIFIER NOT NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionMarketDocument_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionMarketDocument_IsDeleted DEFAULT 0
    );
END;

IF COL_LENGTH(N'Submissions.SubmissionMarket', N'ReasonCode') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD ReasonCode NVARCHAR(80) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'Notes') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD Notes NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'NextActionDateUtc') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD NextActionDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'SubmittedDateUtc') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD SubmittedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'SubmittedByUserId') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD SubmittedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'UnderwriterName') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD UnderwriterName NVARCHAR(200) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'UnderwriterEmail') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD UnderwriterEmail NVARCHAR(320) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'UnderwriterPhone') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD UnderwriterPhone NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'DueDateUtc') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD DueDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'RequestedCoverageSummary') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD RequestedCoverageSummary NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'RequestedLimits') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD RequestedLimits NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'SubmissionMethodCode') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD SubmissionMethodCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'FollowUpTaskId') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD FollowUpTaskId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'QuoteRequestScopeCode') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD QuoteRequestScopeCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'RequestedPremium') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD RequestedPremium DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

IF OBJECT_ID(N'Submissions.QuoteRequestHistory', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.QuoteRequestHistory
    (
        QuoteRequestHistoryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_QuoteRequestHistory PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        SubmissionMarketId UNIQUEIDENTIFIER NOT NULL,
        CarrierId UNIQUEIDENTIFIER NOT NULL,
        QuoteRequestActionCode NVARCHAR(50) NOT NULL,
        QuoteRequestReasonCode NVARCHAR(80) NULL,
        QuoteRequestScopeCode NVARCHAR(50) NULL,
        RequestedPremium DECIMAL(18,2) NULL,
        CoverageNotes NVARCHAR(1000) NULL,
        RequestVersion INT NOT NULL CONSTRAINT DF_QuoteRequestHistory_RequestVersion DEFAULT 1,
        StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequestHistory_Status DEFAULT N'Open',
        RequestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_QuoteRequestHistory_RequestedDateUtc DEFAULT SYSUTCDATETIME(),
        RequestedByUserId UNIQUEIDENTIFIER NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_QuoteRequestHistory_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_QuoteRequestHistory_IsDeleted DEFAULT 0
    );
END;

IF OBJECT_ID(N'Submissions.QuoteRequestHistory', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Submissions.QuoteRequestHistory', N'QuoteRequestReasonCode') IS NULL ALTER TABLE Submissions.QuoteRequestHistory ADD QuoteRequestReasonCode NVARCHAR(80) NULL;
    IF COL_LENGTH(N'Submissions.QuoteRequestHistory', N'QuoteRequestMethodCode') IS NULL ALTER TABLE Submissions.QuoteRequestHistory ADD QuoteRequestMethodCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequestHistory_Method_Runtime DEFAULT N'ManualUnderwriter';
    IF COL_LENGTH(N'Submissions.QuoteRequestHistory', N'QuoteRequestScopeCode') IS NULL ALTER TABLE Submissions.QuoteRequestHistory ADD QuoteRequestScopeCode NVARCHAR(50) NULL;
    IF COL_LENGTH(N'Submissions.QuoteRequestHistory', N'RequestedPremium') IS NULL ALTER TABLE Submissions.QuoteRequestHistory ADD RequestedPremium DECIMAL(18,2) NULL;
    IF COL_LENGTH(N'Submissions.QuoteRequestHistory', N'CoverageNotes') IS NULL ALTER TABLE Submissions.QuoteRequestHistory ADD CoverageNotes NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'Submissions.QuoteRequestHistory', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequestHistory ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Submissions.QuoteRequestHistory', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.QuoteRequestHistory ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.QuoteRequestHistory') AND name = N'IX_QuoteRequestHistory_Market_Open')
        EXEC(N'CREATE INDEX IX_QuoteRequestHistory_Market_Open ON Submissions.QuoteRequestHistory(SubmissionMarketId, StatusCode, IsDeleted, RequestedDateUtc DESC);');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.QuoteRequestHistory') AND name = N'IX_QuoteRequestHistory_Submission')
        EXEC(N'CREATE INDEX IX_QuoteRequestHistory_Submission ON Submissions.QuoteRequestHistory(SubmissionId, TenantId, IsDeleted, RequestedDateUtc DESC);');
END;

IF OBJECT_ID(N'Submissions.QuoteRequest', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.QuoteRequest
    (
        QuoteRequestId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_QuoteRequest_Runtime PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        SubmissionMarketId UNIQUEIDENTIFIER NOT NULL,
        CarrierId UNIQUEIDENTIFIER NOT NULL,
        QuoteRequestActionCode NVARCHAR(50) NOT NULL,
        QuoteRequestReasonCode NVARCHAR(80) NULL,
        QuoteRequestMethodCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequest_Method_Runtime DEFAULT N'ManualUnderwriter',
        QuoteRequestScopeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequest_Scope_Runtime DEFAULT N'Package',
        RequestedPremium DECIMAL(18,2) NULL,
        Premium DECIMAL(18,2) NULL,
        CommissionPercent DECIMAL(9,4) NULL,
        QuoteNumber NVARCHAR(80) NULL,
        ExpirationDateUtc DATETIME2 NULL,
        CoverageNotes NVARCHAR(1000) NULL,
        CarrierReferenceNumber NVARCHAR(120) NULL,
        DeliveryMethodCode NVARCHAR(50) NULL,
        AssignedUnderwriterUserId UNIQUEIDENTIFIER NULL,
        AssignedUnderwriterName NVARCHAR(200) NULL,
        AssignedUnderwriterEmail NVARCHAR(320) NULL,
        AssignedUnderwriterPhone NVARCHAR(50) NULL,
        DueDateUtc DATETIME2 NULL,
        RetryCount INT NOT NULL CONSTRAINT DF_QuoteRequest_RetryCount_Runtime DEFAULT 0,
        CorrelationId NVARCHAR(120) NULL,
        DispatchedDateUtc DATETIME2 NULL,
        AcknowledgedDateUtc DATETIME2 NULL,
        ResponseDateUtc DATETIME2 NULL,
        LastAttemptDateUtc DATETIME2 NULL,
        LastError NVARCHAR(2000) NULL,
        RequestVersion INT NOT NULL CONSTRAINT DF_QuoteRequest_RequestVersion_Runtime DEFAULT 1,
        StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequest_Status_Runtime DEFAULT N'PendingDispatch',
        RequestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_QuoteRequest_RequestedDateUtc_Runtime DEFAULT SYSUTCDATETIME(),
        RequestedByUserId UNIQUEIDENTIFIER NULL,
        ClosedDateUtc DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_QuoteRequest_CreatedDateUtc_Runtime DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_QuoteRequest_IsDeleted_Runtime DEFAULT 0
    );
END;

IF COL_LENGTH(N'Submissions.QuoteRequest', N'TenantId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_QuoteRequest_Tenant_Runtime DEFAULT '00000000-0000-0000-0000-000000000001';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'SubmissionId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD SubmissionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_QuoteRequest_Submission_Runtime DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'SubmissionMarketId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD SubmissionMarketId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_QuoteRequest_Market_Runtime DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'CarrierId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD CarrierId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_QuoteRequest_Carrier_Runtime DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'QuoteRequestActionCode') IS NULL ALTER TABLE Submissions.QuoteRequest ADD QuoteRequestActionCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequest_Action_Runtime DEFAULT N'InitialRequest';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'QuoteRequestReasonCode') IS NULL ALTER TABLE Submissions.QuoteRequest ADD QuoteRequestReasonCode NVARCHAR(80) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'QuoteRequestMethodCode') IS NULL ALTER TABLE Submissions.QuoteRequest ADD QuoteRequestMethodCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequest_MethodB_Runtime DEFAULT N'ManualUnderwriter';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'QuoteRequestScopeCode') IS NULL ALTER TABLE Submissions.QuoteRequest ADD QuoteRequestScopeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequest_ScopeB_Runtime DEFAULT N'Package';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'RequestedPremium') IS NULL ALTER TABLE Submissions.QuoteRequest ADD RequestedPremium DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'Premium') IS NULL ALTER TABLE Submissions.QuoteRequest ADD Premium DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'CommissionPercent') IS NULL ALTER TABLE Submissions.QuoteRequest ADD CommissionPercent DECIMAL(9,4) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'QuoteNumber') IS NULL ALTER TABLE Submissions.QuoteRequest ADD QuoteNumber NVARCHAR(80) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'ExpirationDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD ExpirationDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'CoverageNotes') IS NULL ALTER TABLE Submissions.QuoteRequest ADD CoverageNotes NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'CarrierReferenceNumber') IS NULL ALTER TABLE Submissions.QuoteRequest ADD CarrierReferenceNumber NVARCHAR(120) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'DeliveryMethodCode') IS NULL ALTER TABLE Submissions.QuoteRequest ADD DeliveryMethodCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'AssignedUnderwriterUserId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD AssignedUnderwriterUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'AssignedUnderwriterName') IS NULL ALTER TABLE Submissions.QuoteRequest ADD AssignedUnderwriterName NVARCHAR(200) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'AssignedUnderwriterEmail') IS NULL ALTER TABLE Submissions.QuoteRequest ADD AssignedUnderwriterEmail NVARCHAR(320) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'AssignedUnderwriterPhone') IS NULL ALTER TABLE Submissions.QuoteRequest ADD AssignedUnderwriterPhone NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'DueDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD DueDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'RetryCount') IS NULL ALTER TABLE Submissions.QuoteRequest ADD RetryCount INT NOT NULL CONSTRAINT DF_QuoteRequest_RetryCountB_Runtime DEFAULT 0;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'CorrelationId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD CorrelationId NVARCHAR(120) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'DispatchedDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD DispatchedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'AcknowledgedDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD AcknowledgedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'ResponseDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD ResponseDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'LastAttemptDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD LastAttemptDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'LastError') IS NULL ALTER TABLE Submissions.QuoteRequest ADD LastError NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'RequestVersion') IS NULL ALTER TABLE Submissions.QuoteRequest ADD RequestVersion INT NOT NULL CONSTRAINT DF_QuoteRequest_RequestVersionB_Runtime DEFAULT 1;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'StatusCode') IS NULL ALTER TABLE Submissions.QuoteRequest ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequest_StatusB_Runtime DEFAULT N'PendingDispatch';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'RequestedDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD RequestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_QuoteRequest_RequestedDateUtcB_Runtime DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Submissions.QuoteRequest', N'RequestedByUserId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD RequestedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'ClosedDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD ClosedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'CreatedDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_QuoteRequest_CreatedDateUtcB_Runtime DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Submissions.QuoteRequest', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'IsDeleted') IS NULL ALTER TABLE Submissions.QuoteRequest ADD IsDeleted BIT NOT NULL CONSTRAINT DF_QuoteRequest_IsDeletedB_Runtime DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.QuoteRequest') AND name = N'UX_QuoteRequest_Market_Version')
    EXEC(N'CREATE UNIQUE INDEX UX_QuoteRequest_Market_Version ON Submissions.QuoteRequest(SubmissionMarketId, RequestVersion) WHERE IsDeleted = 0;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.QuoteRequest') AND name = N'IX_QuoteRequest_Submission')
    EXEC(N'CREATE INDEX IX_QuoteRequest_Submission ON Submissions.QuoteRequest(TenantId, SubmissionId, IsDeleted, RequestedDateUtc DESC);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.QuoteRequest') AND name = N'IX_QuoteRequest_Market_Status')
    EXEC(N'CREATE INDEX IX_QuoteRequest_Market_Status ON Submissions.QuoteRequest(SubmissionMarketId, StatusCode, IsDeleted, RequestedDateUtc DESC);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.QuoteRequest') AND name = N'IX_QuoteRequest_Correlation')
    EXEC(N'CREATE INDEX IX_QuoteRequest_Correlation ON Submissions.QuoteRequest(TenantId, CorrelationId, IsDeleted) WHERE CorrelationId IS NOT NULL;');

IF OBJECT_ID(N'Submissions.QuoteRequestHistory', N'U') IS NOT NULL
BEGIN
    EXEC(N'
    INSERT INTO Submissions.QuoteRequest
        (QuoteRequestId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, QuoteRequestActionCode, QuoteRequestReasonCode, QuoteRequestMethodCode, QuoteRequestScopeCode,
         RequestedPremium, CoverageNotes, RequestVersion, StatusCode, RequestedDateUtc, RequestedByUserId, ClosedDateUtc, CreatedDateUtc, CreatedByUserId,
         ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    SELECT NEWID(), h.TenantId, h.SubmissionId, h.SubmissionMarketId, h.CarrierId, h.QuoteRequestActionCode, h.QuoteRequestReasonCode,
           COALESCE(NULLIF(h.QuoteRequestMethodCode, N''''), NULLIF(sm.SubmissionMethodCode, N''''), N''ManualUnderwriter''), COALESCE(NULLIF(h.QuoteRequestScopeCode, N''''), N''Package''), h.RequestedPremium, h.CoverageNotes, h.RequestVersion, h.StatusCode,
           h.RequestedDateUtc, h.RequestedByUserId, CASE WHEN h.StatusCode IN (N''Closed'', N''Declined'', N''Received'', N''Expired'', N''No Response'') THEN h.ModifiedDateUtc ELSE NULL END,
           h.CreatedDateUtc, h.CreatedByUserId, h.ModifiedDateUtc, h.ModifiedByUserId, h.IsDeleted
    FROM Submissions.QuoteRequestHistory h
    LEFT JOIN Submissions.SubmissionMarket sm ON sm.SubmissionMarketId = h.SubmissionMarketId AND sm.IsDeleted = 0
    WHERE h.IsDeleted = 0
      AND NOT EXISTS
      (
          SELECT 1
          FROM Submissions.QuoteRequest existing
          WHERE existing.SubmissionMarketId = h.SubmissionMarketId
            AND existing.RequestVersion = h.RequestVersion
            AND existing.IsDeleted = 0
      );
    ');
END;

IF OBJECT_ID(N'Submissions.SubmissionMarketLine', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.SubmissionMarketLine
    (
        SubmissionMarketLineId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_SubmissionMarketLine PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SubmissionMarketId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        SubmissionLineId UNIQUEIDENTIFIER NOT NULL,
        LineOfBusiness NVARCHAR(100) NOT NULL,
        TargetPremium DECIMAL(18,2) NOT NULL CONSTRAINT DF_SubmissionMarketLine_TargetPremium DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionMarketLine_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionMarketLine_IsDeleted DEFAULT 0
    );
END;

IF OBJECT_ID(N'Submissions.SubmissionMarketLine', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionMarketLine') AND name = N'UX_SubmissionMarketLine_Market_Line')
        EXEC(N'CREATE UNIQUE INDEX UX_SubmissionMarketLine_Market_Line ON Submissions.SubmissionMarketLine(SubmissionMarketId, SubmissionLineId) WHERE IsDeleted = 0;');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionMarketLine') AND name = N'IX_SubmissionMarketLine_Submission')
        EXEC(N'CREATE INDEX IX_SubmissionMarketLine_Submission ON Submissions.SubmissionMarketLine(SubmissionId, TenantId, IsDeleted);');
END;

IF OBJECT_ID(N'Submissions.QuoteLine', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.QuoteLine
    (
        QuoteLineId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_QuoteLine PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        QuoteId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        SubmissionLineId UNIQUEIDENTIFIER NULL,
        OpportunityLineId UNIQUEIDENTIFIER NULL,
        LineOfBusiness NVARCHAR(100) NOT NULL,
        QuotedPremium DECIMAL(18,2) NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_QuotedPremium DEFAULT 0,
        Deductible DECIMAL(18,2) NULL,
        [Limit] DECIMAL(18,2) NULL,
        CommissionPercent DECIMAL(9,4) NULL,
        CoverageForms NVARCHAR(2000) NULL,
        Subjectivities NVARCHAR(2000) NULL,
        Exclusions NVARCHAR(2000) NULL,
        PaymentTerms NVARCHAR(200) NULL,
        MinimumEarnedPremium DECIMAL(18,2) NULL,
        TaxesAndFees DECIMAL(18,2) NULL,
        BrokerFee DECIMAL(18,2) NULL,
        TriaIncluded BIT NULL,
        IsBindable BIT NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_IsBindable DEFAULT 0,
        CoverageNotes NVARCHAR(1000) NULL,
        Status NVARCHAR(50) NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_Status DEFAULT N'Quoted',
        SortOrder INT NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_SortOrder DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_IsDeleted DEFAULT 0
    );
END;

IF COL_LENGTH(N'Submissions.QuoteLine', N'SubmissionLineId') IS NULL ALTER TABLE Submissions.QuoteLine ADD SubmissionLineId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'Deductible') IS NULL ALTER TABLE Submissions.QuoteLine ADD Deductible DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'Limit') IS NULL ALTER TABLE Submissions.QuoteLine ADD [Limit] DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'CommissionPercent') IS NULL ALTER TABLE Submissions.QuoteLine ADD CommissionPercent DECIMAL(9,4) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'CoverageForms') IS NULL ALTER TABLE Submissions.QuoteLine ADD CoverageForms NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'Subjectivities') IS NULL ALTER TABLE Submissions.QuoteLine ADD Subjectivities NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'Exclusions') IS NULL ALTER TABLE Submissions.QuoteLine ADD Exclusions NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'PaymentTerms') IS NULL ALTER TABLE Submissions.QuoteLine ADD PaymentTerms NVARCHAR(200) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'MinimumEarnedPremium') IS NULL ALTER TABLE Submissions.QuoteLine ADD MinimumEarnedPremium DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'TaxesAndFees') IS NULL ALTER TABLE Submissions.QuoteLine ADD TaxesAndFees DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'BrokerFee') IS NULL ALTER TABLE Submissions.QuoteLine ADD BrokerFee DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'TriaIncluded') IS NULL ALTER TABLE Submissions.QuoteLine ADD TriaIncluded BIT NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'IsBindable') IS NULL ALTER TABLE Submissions.QuoteLine ADD IsBindable BIT NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_IsBindable DEFAULT 0;
IF COL_LENGTH(N'Submissions.QuoteLine', N'CoverageNotes') IS NULL ALTER TABLE Submissions.QuoteLine ADD CoverageNotes NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'SortOrder') IS NULL ALTER TABLE Submissions.QuoteLine ADD SortOrder INT NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_SortOrder DEFAULT 0;
IF COL_LENGTH(N'Submissions.QuoteLine', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.QuoteLine ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.QuoteLine ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.QuoteLine ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

EXEC(N';WITH DuplicateLines AS
(
    SELECT QuoteLineId,
           ROW_NUMBER() OVER
           (
               PARTITION BY QuoteId, SubmissionLineId
               ORDER BY COALESCE(ModifiedDateUtc, CreatedDateUtc) DESC, CreatedDateUtc DESC, QuoteLineId
           ) AS DuplicateOrder
    FROM Submissions.QuoteLine
    WHERE SubmissionLineId IS NOT NULL
      AND IsDeleted = 0
)
UPDATE ql
SET IsDeleted = 1,
    ModifiedDateUtc = SYSUTCDATETIME()
FROM Submissions.QuoteLine ql
JOIN DuplicateLines duplicate ON duplicate.QuoteLineId = ql.QuoteLineId
WHERE duplicate.DuplicateOrder > 1;');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.QuoteLine') AND name = N'UX_SubmissionsQuoteLine_Quote_SubmissionLine')
    EXEC(N'CREATE UNIQUE INDEX UX_SubmissionsQuoteLine_Quote_SubmissionLine ON Submissions.QuoteLine(QuoteId, SubmissionLineId) WHERE IsDeleted = 0 AND SubmissionLineId IS NOT NULL;');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.QuoteLine') AND name = N'IX_SubmissionsQuoteLine_SubmissionLine')
    EXEC(N'CREATE INDEX IX_SubmissionsQuoteLine_SubmissionLine ON Submissions.QuoteLine(SubmissionLineId, QuoteId, IsDeleted);');

IF COL_LENGTH(N'Submissions.Quote', N'SubmissionMarketId') IS NULL ALTER TABLE Submissions.Quote ADD SubmissionMarketId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'QuoteRequestId') IS NULL ALTER TABLE Submissions.Quote ADD QuoteRequestId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'QuoteRequestDateUtc') IS NULL ALTER TABLE Submissions.Quote ADD QuoteRequestDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Quote', N'QuoteReceivedDateUtc') IS NULL ALTER TABLE Submissions.Quote ADD QuoteReceivedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Quote', N'ResponseVersion') IS NULL ALTER TABLE Submissions.Quote ADD ResponseVersion INT NOT NULL CONSTRAINT DF_Quote_ResponseVersion DEFAULT 1;
IF COL_LENGTH(N'Submissions.Quote', N'ResponseSourceCode') IS NULL ALTER TABLE Submissions.Quote ADD ResponseSourceCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'CarrierReferenceNumber') IS NULL ALTER TABLE Submissions.Quote ADD CarrierReferenceNumber NVARCHAR(100) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'RequestedByUserId') IS NULL ALTER TABLE Submissions.Quote ADD RequestedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'ReceivedByUserId') IS NULL ALTER TABLE Submissions.Quote ADD ReceivedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'EffectiveDate') IS NULL ALTER TABLE Submissions.Quote ADD EffectiveDate DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Quote', N'CoverageForms') IS NULL ALTER TABLE Submissions.Quote ADD CoverageForms NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'IsBindable') IS NULL ALTER TABLE Submissions.Quote ADD IsBindable BIT NOT NULL CONSTRAINT DF_Quote_IsBindable DEFAULT 0;
IF COL_LENGTH(N'Submissions.Quote', N'CommissionPercent') IS NULL ALTER TABLE Submissions.Quote ADD CommissionPercent DECIMAL(9,4) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'Subjectivities') IS NULL ALTER TABLE Submissions.Quote ADD Subjectivities NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'Exclusions') IS NULL ALTER TABLE Submissions.Quote ADD Exclusions NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'CarrierRating') IS NULL ALTER TABLE Submissions.Quote ADD CarrierRating NVARCHAR(80) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'PaymentTerms') IS NULL ALTER TABLE Submissions.Quote ADD PaymentTerms NVARCHAR(200) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'MinimumEarnedPremium') IS NULL ALTER TABLE Submissions.Quote ADD MinimumEarnedPremium DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'TaxesAndFees') IS NULL ALTER TABLE Submissions.Quote ADD TaxesAndFees DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'BrokerFee') IS NULL ALTER TABLE Submissions.Quote ADD BrokerFee DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'TriaIncluded') IS NULL ALTER TABLE Submissions.Quote ADD TriaIncluded BIT NULL;
IF COL_LENGTH(N'Submissions.Quote', N'QuoteDocumentId') IS NULL ALTER TABLE Submissions.Quote ADD QuoteDocumentId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'IsSelected') IS NULL ALTER TABLE Submissions.Quote ADD IsSelected BIT NOT NULL CONSTRAINT DF_Quote_IsSelected DEFAULT 0;
IF COL_LENGTH(N'Submissions.Quote', N'IsRecommended') IS NULL ALTER TABLE Submissions.Quote ADD IsRecommended BIT NOT NULL CONSTRAINT DF_Quote_IsRecommended DEFAULT 0;
IF COL_LENGTH(N'Submissions.Quote', N'ReviewedByUserId') IS NULL ALTER TABLE Submissions.Quote ADD ReviewedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'ReviewedDateUtc') IS NULL ALTER TABLE Submissions.Quote ADD ReviewedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Quote', N'ApprovedForPresentationByUserId') IS NULL ALTER TABLE Submissions.Quote ADD ApprovedForPresentationByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'ApprovedForPresentationDateUtc') IS NULL ALTER TABLE Submissions.Quote ADD ApprovedForPresentationDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Quote', N'PresentationReadinessNotes') IS NULL ALTER TABLE Submissions.Quote ADD PresentationReadinessNotes NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'DisclosureDocumentId') IS NULL ALTER TABLE Submissions.Quote ADD DisclosureDocumentId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'SelectedByUserId') IS NULL ALTER TABLE Submissions.Quote ADD SelectedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'SelectedDateUtc') IS NULL ALTER TABLE Submissions.Quote ADD SelectedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Quote', N'SelectionReason') IS NULL ALTER TABLE Submissions.Quote ADD SelectionReason NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'RecommendationScore') IS NULL ALTER TABLE Submissions.Quote ADD RecommendationScore INT NOT NULL CONSTRAINT DF_Quote_RecommendationScore DEFAULT 0;
IF COL_LENGTH(N'Submissions.Quote', N'RecommendationReason') IS NULL ALTER TABLE Submissions.Quote ADD RecommendationReason NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.Quote ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Quote', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.Quote ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

IF OBJECT_ID(N'Submissions.ProposalReadinessFactor', N'U') IS NOT NULL
BEGIN
    EXEC(N'INSERT INTO Submissions.SubmissionReadinessRequirement
        (ReadinessRequirementId, TenantId, LineOfBusiness, ScopeCode, RequirementCode, RequirementTypeCode, DisplayName, Description, IsRequired, BlocksSubmit, AllowsWaiver, RequiresEvidence, ActionCode, ActionLabel, ScoreWeight, SortOrder, IsActive, CreatedDateUtc, IsDeleted)
    SELECT NEWID(), factor.TenantId, N''All'', N''Proposal'', factor.FactorCode, N''QuoteData'', factor.DisplayName, factor.Instructions, factor.IsRequired, factor.IsRequired, 0, 0, factor.ActionCode, factor.ActionLabel, 10, factor.SortOrder, factor.IsActive, factor.CreatedDateUtc, factor.IsDeleted
    FROM Submissions.ProposalReadinessFactor factor
    WHERE factor.IsDeleted = 0 AND NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReadinessRequirement existing WHERE existing.TenantId = factor.TenantId AND existing.LineOfBusiness = N''All'' AND existing.RequirementCode = factor.FactorCode AND existing.IsDeleted = 0);');
    DROP TABLE Submissions.ProposalReadinessFactor;
END;

INSERT INTO Submissions.SubmissionReadinessRequirement
    (ReadinessRequirementId, TenantId, LineOfBusiness, ScopeCode, RequirementCode, RequirementTypeCode, DisplayName, Description, IsRequired, BlocksSubmit, AllowsWaiver, RequiresEvidence, ActionCode, ActionLabel, ScoreWeight, SortOrder, IsActive, CreatedDateUtc, IsDeleted)
SELECT NEWID(), tenant.TenantId, N'All', N'Proposal', source.FactorCode, N'QuoteData', source.DisplayName, source.Instructions, 1, 1, 0, 0, N'QuoteReview', N'Review Quote Terms', 10, source.SortOrder, 1, SYSUTCDATETIME(), 0
FROM Core.Tenant tenant
CROSS JOIN (VALUES
    (N'ApprovedStatus', N'Approved status', N'Select Approved for Presentation and save the quote.', 10),
    (N'CurrentExpiration', N'Current expiration', N'Enter a future carrier expiration date.', 20),
    (N'PositivePremium', N'Positive premium', N'Enter an annual premium greater than zero.', 30),
    (N'CarrierMarket', N'Carrier market', N'Link the quote to its carrier market.', 40),
    (N'Deductible', N'Deductible', N'Enter the carrier deductible.', 50),
    (N'CoverageLimit', N'Coverage limit', N'Enter the quoted coverage limit.', 60),
    (N'CoverageDetails', N'Coverage details', N'Enter coverage forms or coverage notes.', 70),
    (N'InternalReview', N'Internal review', N'Open Review Quote Terms, verify the information, and save.', 80),
    (N'CarrierQuoteDocument', N'Carrier quote document', N'Select the carrier-issued quote document.', 90)
) source(FactorCode, DisplayName, Instructions, SortOrder)
WHERE tenant.IsDeleted = 0
  AND (@TenantId IS NULL OR tenant.TenantId = @TenantId)
  AND NOT EXISTS
  (
      SELECT 1 FROM Submissions.SubmissionReadinessRequirement existing
      WHERE existing.TenantId = tenant.TenantId AND existing.LineOfBusiness = N'All' AND existing.RequirementCode = source.FactorCode AND existing.IsDeleted = 0
  );

IF OBJECT_ID(N'Submissions.QuoteRevision', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.QuoteRevision
    (
        QuoteRevisionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_QuoteRevision PRIMARY KEY DEFAULT NEWID(),
        QuoteId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        SubmissionMarketId UNIQUEIDENTIFIER NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ResponseVersion INT NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        AnnualPremium DECIMAL(18,2) NOT NULL,
        Deductible DECIMAL(18,2) NULL,
        [Limit] DECIMAL(18,2) NULL,
        CommissionPercent DECIMAL(9,4) NULL,
        TaxesAndFees DECIMAL(18,2) NULL,
        BrokerFee DECIMAL(18,2) NULL,
        MinimumEarnedPremium DECIMAL(18,2) NULL,
        EffectiveDate DATETIME2 NULL,
        ExpiresDateUtc DATETIME2 NOT NULL,
        CoverageForms NVARCHAR(2000) NULL,
        Subjectivities NVARCHAR(2000) NULL,
        Exclusions NVARCHAR(2000) NULL,
        CarrierRating NVARCHAR(80) NULL,
        PaymentTerms NVARCHAR(200) NULL,
        IsBindable BIT NOT NULL CONSTRAINT DF_QuoteRevision_IsBindable DEFAULT 0,
        CoverageNotes NVARCHAR(1000) NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_QuoteRevision_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_QuoteRevision_IsDeleted DEFAULT 0
    );
END;

IF OBJECT_ID(N'Submissions.CustomerAuthorization', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.CustomerAuthorization
    (
        CustomerAuthorizationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_CustomerAuthorization_Runtime PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        QuoteId UNIQUEIDENTIFIER NOT NULL,
        ProposalId UNIQUEIDENTIFIER NULL,
        AuthorizationMethodCode NVARCHAR(50) NOT NULL,
        AuthorizationReference NVARCHAR(200) NULL,
        AuthorizationNotes NVARCHAR(2000) NULL,
        AuthorizedByName NVARCHAR(200) NULL,
        AuthorizedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CustomerAuthorization_Authorized_Runtime DEFAULT SYSUTCDATETIME(),
        DocumentId UNIQUEIDENTIFIER NULL,
        PolicyBindTransactionId UNIQUEIDENTIFIER NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CustomerAuthorization_Created_Runtime DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_CustomerAuthorization_IsDeleted_Runtime DEFAULT 0
    );
END;

IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'TenantId') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CustomerAuthorization_Tenant_Ensure DEFAULT '00000000-0000-0000-0000-000000000001';
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'SubmissionId') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD SubmissionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CustomerAuthorization_Submission_Ensure DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'QuoteId') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD QuoteId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CustomerAuthorization_Quote_Ensure DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'ProposalId') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD ProposalId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'AuthorizationMethodCode') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD AuthorizationMethodCode NVARCHAR(50) NOT NULL CONSTRAINT DF_CustomerAuthorization_Method_Ensure DEFAULT N'WrittenInstruction';
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'AuthorizationReference') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD AuthorizationReference NVARCHAR(200) NULL;
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'AuthorizationNotes') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD AuthorizationNotes NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'AuthorizedByName') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD AuthorizedByName NVARCHAR(200) NULL;
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'AuthorizedDateUtc') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD AuthorizedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CustomerAuthorization_Authorized_Ensure DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'DocumentId') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD DocumentId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'PolicyBindTransactionId') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD PolicyBindTransactionId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'CreatedDateUtc') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_CustomerAuthorization_Created_Ensure DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.CustomerAuthorization', N'IsDeleted') IS NULL ALTER TABLE Submissions.CustomerAuthorization ADD IsDeleted BIT NOT NULL CONSTRAINT DF_CustomerAuthorization_IsDeleted_Ensure DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.CustomerAuthorization') AND name = N'IX_CustomerAuthorization_Submission')
    EXEC(N'CREATE INDEX IX_CustomerAuthorization_Submission ON Submissions.CustomerAuthorization(TenantId, SubmissionId, QuoteId, IsDeleted, AuthorizedDateUtc DESC);');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.CustomerAuthorization') AND name = N'IX_CustomerAuthorization_BindTransaction')
    EXEC(N'CREATE INDEX IX_CustomerAuthorization_BindTransaction ON Submissions.CustomerAuthorization(PolicyBindTransactionId) WHERE IsDeleted = 0 AND PolicyBindTransactionId IS NOT NULL;');

IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'RelatedEntityName') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD RelatedEntityName NVARCHAR(100) NULL;
IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'RelatedEntityId') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD RelatedEntityId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'ActionSource') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD ActionSource NVARCHAR(50) NULL;

IF @TenantId IS NOT NULL AND OBJECT_ID(N'Submissions.SubmissionReferenceOption', N'U') IS NOT NULL
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    SELECT @TenantId, seed.OptionGroup, seed.OptionCode, seed.OptionName, seed.Description, seed.IsDefault, seed.SortOrder
    FROM (VALUES
        (N'QuoteRequestAction', N'InitialRequest', N'Initial quote request', N'First request for quote terms from this market and coverage scope.', CAST(1 AS bit), 10),
        (N'QuoteRequestAction', N'ResendUpdate', N'Resend / update request', N'Updates or resends an open quote request when underwriting context changes.', CAST(0 AS bit), 20),
        (N'QuoteRequestAction', N'RequestRevision', N'Request quote revision', N'Requests revised quote terms after a carrier quote response has been received.', CAST(0 AS bit), 30),
        (N'QuoteRequestReason', N'UpdatedUnderwritingInfo', N'Updated underwriting information', N'Updated application, exposure, loss, or underwriting information changed the request.', CAST(1 AS bit), 10),
        (N'QuoteRequestReason', N'CoverageChange', N'Coverage or limit change', N'Coverage, limit, deductible, or line selection changed.', CAST(0 AS bit), 20),
        (N'QuoteRequestReason', N'PremiumTargetChange', N'Premium target change', N'Target premium or pricing expectation changed.', CAST(0 AS bit), 30),
        (N'QuoteRequestReason', N'CarrierClarification', N'Carrier clarification', N'Carrier requested clarification or additional information.', CAST(0 AS bit), 40),
        (N'QuoteRequestReason', N'MissingInformation', N'Missing information supplied', N'Previously missing information or documents are now available.', CAST(0 AS bit), 50),
        (N'QuoteRequestReason', N'Other', N'Other', N'Other documented business reason.', CAST(0 AS bit), 90),
        (N'QuoteRequestScope', N'Package', N'Package', N'Request quote terms for the full submission package.', CAST(1 AS bit), 10),
        (N'QuoteRequestScope', N'SingleLine', N'Single coverage line', N'Request quote terms for one selected coverage line.', CAST(0 AS bit), 20),
        (N'QuoteRequestMethod', N'ApiRating', N'API Rating', N'Personal-lines or comparative-rater API path where request quote submits and rates in one workflow.', CAST(1 AS bit), 10),
        (N'QuoteRequestMethod', N'MgaPortal', N'MGA Portal', N'MGA, wholesaler, or carrier portal path where AMS tracks portal submission and quote response.', CAST(0 AS bit), 20),
        (N'QuoteRequestMethod', N'Email', N'Email', N'Email path where AMS tracks a quote request sent to the market or underwriter by email.', CAST(0 AS bit), 30),
        (N'QuoteRequestMethod', N'ManualUnderwriter', N'Manual Underwriter', N'Manual commercial underwriting path where an underwriter reviews before quote terms are returned.', CAST(0 AS bit), 40),
        (N'SubmissionMethod', N'ApiRating', N'API Rating', N'Submission and quote request are sent through a carrier or comparative rater API.', CAST(0 AS bit), 5),
        (N'SubmissionMethod', N'MgaPortal', N'MGA Portal', N'Submission package is delivered through an MGA, wholesaler, or carrier portal.', CAST(0 AS bit), 18),
        (N'SubmissionMethod', N'ManualUnderwriter', N'Manual Underwriter', N'Submission package is tracked through manual underwriter review.', CAST(0 AS bit), 45),
        (N'QuoteRequestOpenMarketStatus', N'In Review', N'In Review', N'Market has an open quote request workflow.', CAST(1 AS bit), 10),
        (N'QuoteRequestOpenMarketStatus', N'Submitted', N'Submitted', N'Market submission has been sent and is awaiting quote activity.', CAST(0 AS bit), 20),
        (N'QuoteRequestOpenMarketStatus', N'Awaiting Quote', N'Awaiting Quote', N'Market is awaiting carrier quote terms.', CAST(0 AS bit), 30),
        (N'QuoteRequestOpenMarketStatus', N'Requested', N'Requested', N'Quote request is pending carrier response.', CAST(0 AS bit), 40),
        (N'QuoteRequestStatus', N'Draft', N'Draft', N'Quote request is being prepared and has not been dispatched.', CAST(0 AS bit), 5),
        (N'QuoteRequestStatus', N'ValidationRequired', N'Validation Required', N'Quote request is blocked until required submission information is completed.', CAST(0 AS bit), 8),
        (N'QuoteRequestStatus', N'PendingDispatch', N'Pending Dispatch', N'Quote request was created and is waiting for dispatch.', CAST(1 AS bit), 10),
        (N'QuoteRequestStatus', N'Submitted', N'Submitted', N'Quote request has been submitted to the market.', CAST(0 AS bit), 20),
        (N'QuoteRequestStatus', N'Acknowledged', N'Acknowledged', N'Market acknowledged the quote request.', CAST(0 AS bit), 30),
        (N'QuoteRequestStatus', N'UnderReview', N'Under Review', N'Market is underwriting or reviewing the quote request.', CAST(0 AS bit), 40),
        (N'QuoteRequestStatus', N'MoreInformationRequired', N'More Information Required', N'Market requested more information before quoting.', CAST(0 AS bit), 50),
        (N'QuoteRequestStatus', N'Quoted', N'Quoted', N'Market returned quote terms and a Quote record may exist.', CAST(0 AS bit), 70),
        (N'QuoteRequestStatus', N'Declined', N'Declined', N'Market declined to quote.', CAST(0 AS bit), 80),
        (N'QuoteRequestStatus', N'Expired', N'Expired', N'Quote request expired before receiving market terms.', CAST(0 AS bit), 90),
        (N'QuoteRequestStatus', N'Cancelled', N'Cancelled', N'Quote request was cancelled before response.', CAST(0 AS bit), 100),
        (N'QuoteRequestStatus', N'Failed', N'Failed', N'Quote request dispatch or processing failed.', CAST(0 AS bit), 110),
        (N'QuoteResponseSource', N'ManualEntry', N'Manual Entry', N'Quote response was entered manually by an agency user.', CAST(1 AS bit), 10),
        (N'QuoteResponseSource', N'CarrierPortal', N'Carrier Portal', N'Quote response was copied from a carrier, MGA, or wholesaler portal.', CAST(0 AS bit), 20),
        (N'QuoteResponseSource', N'Email', N'Email', N'Quote response was received through email.', CAST(0 AS bit), 30),
        (N'QuoteResponseSource', N'Api', N'API', N'Quote response was received through carrier or rater API integration.', CAST(0 AS bit), 40),
        (N'SubmissionStatus', N'Draft', N'Draft', N'Submission is being drafted and is not ready for quote requests.', CAST(1 AS bit), 5),
        (N'SubmissionStatus', N'In Progress', N'In Progress', N'Submission risk information is being collected before marketing readiness.', CAST(0 AS bit), 8),
        (N'SubmissionStatus', N'Ready for Marketing', N'Ready for Marketing', N'Submission passed readiness checks and can be marketed.', CAST(0 AS bit), 10),
        (N'SubmissionStatus', N'Marketing', N'Marketing', N'Submission is in active market placement workflow, including quote requests and underwriting.', CAST(0 AS bit), 20),
        (N'SubmissionStatus', N'Quotes Received', N'Quotes Received', N'One or more market quote responses have been received.', CAST(0 AS bit), 40),
        (N'SubmissionStatus', N'Proposal Prepared', N'Proposal Prepared', N'Customer proposal has been prepared from approved quotes.', CAST(0 AS bit), 50),
        (N'SubmissionStatus', N'Presented', N'Presented', N'Proposal has been presented to the customer.', CAST(0 AS bit), 60),
        (N'SubmissionStatus', N'Customer Accepted', N'Customer Accepted', N'Customer accepted a proposal or quote option.', CAST(0 AS bit), 70),
        (N'SubmissionStatus', N'Binding', N'Binding', N'Selected quote is in bind request workflow.', CAST(0 AS bit), 80),
        (N'SubmissionStatus', N'Bound', N'Bound', N'Submission has been bound into policy workflow.', CAST(0 AS bit), 90),
        (N'SubmissionStatus', N'Lost', N'Lost', N'Submission was lost or not placed.', CAST(0 AS bit), 100),
        (N'SubmissionStatus', N'Cancelled', N'Cancelled', N'Submission workflow was cancelled.', CAST(0 AS bit), 110),
        (N'SubmissionStatus', N'Closed', N'Closed', N'Submission workflow was administratively closed.', CAST(0 AS bit), 120),
        (N'QuoteStatus', N'Received', N'Received', N'Carrier quote response has been received and is awaiting internal review.', CAST(1 AS bit), 10),
        (N'QuoteStatus', N'Under Review', N'Under Review', N'Quote is under internal review before customer presentation.', CAST(0 AS bit), 20),
        (N'QuoteStatus', N'Revision Requested', N'Revision Requested', N'Quote requires revised terms from the market.', CAST(0 AS bit), 30),
        (N'QuoteStatus', N'Approved for Presentation', N'Approved for Presentation', N'Quote has been approved for customer presentation.', CAST(0 AS bit), 40),
        (N'QuoteStatus', N'Presented', N'Presented', N'Quote has been included in a customer proposal.', CAST(0 AS bit), 50),
        (N'QuoteStatus', N'Selected', N'Selected', N'Customer selected this quote for binding.', CAST(0 AS bit), 60),
        (N'QuoteStatus', N'Not Selected', N'Not Selected', N'Quote was retained in history but not selected.', CAST(0 AS bit), 70),
        (N'QuoteStatus', N'Expired', N'Expired', N'Quote expired before selection or binding.', CAST(0 AS bit), 80),
        (N'QuoteStatus', N'Superseded', N'Superseded', N'Quote was superseded by a later version or revision.', CAST(0 AS bit), 90),
        (N'QuoteStatus', N'Bound', N'Bound', N'Quote was bound into a policy.', CAST(0 AS bit), 100),
        (N'CustomerAuthorizationMethod', N'SignedProposal', N'Signed Proposal', N'Customer accepted using a signed proposal.', CAST(1 AS bit), 10),
        (N'CustomerAuthorizationMethod', N'EmailApproval', N'Email Approval', N'Customer accepted using written email approval.', CAST(0 AS bit), 20),
        (N'CustomerAuthorizationMethod', N'RecordedCall', N'Recorded Call', N'Customer accepted using a recorded call.', CAST(0 AS bit), 30),
        (N'CustomerAuthorizationMethod', N'ESignature', N'E-Signature', N'Customer accepted using an e-signature workflow.', CAST(0 AS bit), 40),
        (N'CustomerAuthorizationMethod', N'PortalAcceptance', N'Portal Acceptance', N'Customer accepted through a portal workflow.', CAST(0 AS bit), 50),
        (N'CustomerAuthorizationMethod', N'WrittenInstruction', N'Written Instruction', N'Customer accepted using written instruction outside a proposal.', CAST(0 AS bit), 60),
        (N'CustomerAuthorizationMethod', N'Other', N'Other', N'Customer authorization was documented using another agency-approved method.', CAST(0 AS bit), 90),
        (N'BindConfirmationSource', N'Api', N'API', N'Carrier confirmation was received through API integration.', CAST(0 AS bit), 10),
        (N'BindConfirmationSource', N'Webhook', N'Webhook', N'Carrier confirmation was received asynchronously by webhook or polling.', CAST(0 AS bit), 20),
        (N'BindConfirmationSource', N'CarrierPortal', N'Carrier Portal', N'Agency user recorded confirmation from the carrier portal.', CAST(1 AS bit), 30),
        (N'BindConfirmationSource', N'Email', N'Email', N'Carrier confirmation was received by email.', CAST(0 AS bit), 40),
        (N'BindConfirmationSource', N'Phone', N'Phone', N'Carrier confirmation was received verbally by phone and requires documentation.', CAST(0 AS bit), 50),
        (N'BindConfirmationSource', N'BinderDocument', N'Binder Document', N'Carrier binder document confirms coverage is bound.', CAST(0 AS bit), 60),
        (N'BindConfirmationSource', N'Manual', N'Manual', N'Agency user manually recorded authoritative carrier confirmation.', CAST(0 AS bit), 70),
        (N'QuoteRequestBlockedStatus', N'Bound', N'Bound', N'Bound submissions or markets cannot request additional quotes.', CAST(1 AS bit), 10),
        (N'QuoteRequestBlockedStatus', N'Declined', N'Declined', N'Declined submissions or markets are blocked from additional quote requests.', CAST(0 AS bit), 20),
        (N'QuoteRequestBlockedStatus', N'Withdrawn', N'Withdrawn', N'Withdrawn submissions are blocked from quote requests.', CAST(0 AS bit), 30),
        (N'QuoteRequestBlockedStatus', N'Closed', N'Closed', N'Closed submissions or markets are blocked from quote requests.', CAST(0 AS bit), 40),
        (N'QuoteRequestBlockedStatus', N'Lost', N'Lost', N'Lost submissions or markets are blocked from quote requests.', CAST(0 AS bit), 50)
    ) seed(OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM Submissions.SubmissionReferenceOption existing
        WHERE existing.TenantId = @TenantId
          AND existing.OptionGroup = seed.OptionGroup
          AND existing.OptionCode = seed.OptionCode
          AND existing.IsDeleted = 0
    );

    UPDATE Submissions.SubmissionReferenceOption
    SET IsActive = 0,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId
      AND OptionGroup = N'QuoteRequestStatus'
      AND OptionCode IN (N'Open', N'CarrierProcessing', N'Referred', N'Received', N'Withdrawn', N'Closed', N'No Response')
      AND IsDeleted = 0;

    UPDATE Submissions.SubmissionReferenceOption
    SET IsActive = 0,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId
      AND OptionGroup = N'SubmissionStatus'
      AND OptionCode IN (N'New', N'In Review', N'Submitted', N'Quoted', N'Quotes Requested', N'Declined', N'Withdrawn')
      AND IsDeleted = 0;

    IF OBJECT_ID(N'Submissions.Submission', N'U') IS NOT NULL
    BEGIN
        UPDATE Submissions.Submission
        SET Status = CASE Status
                WHEN N'New' THEN N'In Progress'
                WHEN N'In Review' THEN N'Marketing'
                WHEN N'Submitted' THEN N'Marketing'
                WHEN N'Quoted' THEN N'Quotes Received'
                WHEN N'Quotes Requested' THEN N'Marketing'
                WHEN N'Declined' THEN N'Lost'
                WHEN N'Withdrawn' THEN N'Cancelled'
                ELSE Status END,
            ModifiedDateUtc = SYSUTCDATETIME()
        WHERE TenantId = @TenantId
          AND Status IN (N'New', N'In Review', N'Submitted', N'Quoted', N'Quotes Requested', N'Declined', N'Withdrawn')
          AND IsDeleted = 0;
    END;

    IF OBJECT_ID(N'Submissions.QuoteRequest', N'U') IS NOT NULL
    BEGIN
        UPDATE Submissions.QuoteRequest
        SET StatusCode = CASE StatusCode
                WHEN N'Open' THEN N'PendingDispatch'
                WHEN N'CarrierProcessing' THEN N'UnderReview'
                WHEN N'Referred' THEN N'UnderReview'
                WHEN N'Received' THEN N'Quoted'
                WHEN N'No Response' THEN N'Failed'
                WHEN N'Withdrawn' THEN N'Cancelled'
                WHEN N'Closed' THEN N'Cancelled'
                ELSE StatusCode END,
            ClosedDateUtc = CASE WHEN StatusCode IN (N'No Response', N'Withdrawn', N'Closed') THEN COALESCE(ClosedDateUtc, SYSUTCDATETIME()) ELSE ClosedDateUtc END,
            ModifiedDateUtc = SYSUTCDATETIME()
        WHERE TenantId = @TenantId
          AND StatusCode IN (N'Open', N'CarrierProcessing', N'Referred', N'Received', N'No Response', N'Withdrawn', N'Closed')
          AND IsDeleted = 0;
    END;

    IF OBJECT_ID(N'Submissions.QuoteRequestHistory', N'U') IS NOT NULL
    BEGIN
        UPDATE Submissions.QuoteRequestHistory
        SET StatusCode = CASE StatusCode
                WHEN N'Open' THEN N'PendingDispatch'
                WHEN N'CarrierProcessing' THEN N'UnderReview'
                WHEN N'Referred' THEN N'UnderReview'
                WHEN N'Received' THEN N'Quoted'
                WHEN N'No Response' THEN N'Failed'
                WHEN N'Withdrawn' THEN N'Cancelled'
                WHEN N'Closed' THEN N'Cancelled'
                ELSE StatusCode END,
            ModifiedDateUtc = SYSUTCDATETIME()
        WHERE TenantId = @TenantId
          AND StatusCode IN (N'Open', N'CarrierProcessing', N'Referred', N'Received', N'No Response', N'Withdrawn', N'Closed')
          AND IsDeleted = 0;
    END;
END;

IF @TenantId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Submissions.SubmissionDocumentRequirement WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionDocumentRequirement (TenantId, LineOfBusiness, CategoryCode, DisplayName, IsRequired, SortOrder)
    SELECT @TenantId, lob.LineOfBusiness, req.CategoryCode, req.DisplayName, 1, req.SortOrder
    FROM (VALUES (N'Application', N'Application', 10), (N'LossRuns', N'Loss runs', 20), (N'ExposureSchedules', N'Exposure schedules', 30), (N'PriorPolicies', N'Prior policies', 40), (N'Financials', N'Financials', 50), (N'ACORD', N'ACORD forms', 60)) req(CategoryCode, DisplayName, SortOrder)
    CROSS JOIN (SELECT DISTINCT COALESCE(NULLIF(LineOfBusiness, N''), N'General Liability') AS LineOfBusiness FROM Submissions.Submission WHERE TenantId = @TenantId AND IsDeleted = 0) lob;
END;";

        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<BindCommissionEstimateDto> GetBindCommissionEstimateAsync(Guid submissionId, Guid quoteId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var resolved = await ResolveBindCommissionEstimateAsync(cn, submissionId, quoteId, tenantId, null, null, cancellationToken);
        return resolved ?? new BindCommissionEstimateDto
        {
            IsConfigured = false,
            UnavailableReason = "No active commission plan is configured for this producer, business type, and effective date."
        };
    }

    private static async Task<BindCommissionEstimateDto?> ResolveBindCommissionEstimateAsync(
        System.Data.IDbConnection cn,
        Guid submissionId,
        Guid quoteId,
        Guid tenantId,
        decimal? commissionablePremium,
        DateTime? effectiveDate,
        CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @ResolvedEffectiveDate DATE;
DECLARE @ResolvedPremium DECIMAL(18,2);
DECLARE @ProducerUserId UNIQUEIDENTIFIER;
DECLARE @BusinessTypeCode NVARCHAR(50);

SELECT @ResolvedEffectiveDate = COALESCE(@EffectiveDate, s.EffectiveDate),
       @ResolvedPremium = COALESCE(@CommissionablePremium, q.AnnualPremium),
       @ProducerUserId = s.AssignedToUserId,
       @BusinessTypeCode = CASE WHEN EXISTS
       (
           SELECT 1 FROM Renewal.RetentionCase rc
           WHERE rc.TenantId = s.TenantId AND rc.RenewalSubmissionId = s.SubmissionId AND rc.IsDeleted = 0
       ) THEN N'Renewal' ELSE N'NewBusiness' END
FROM Submissions.Submission s
INNER JOIN Submissions.Quote q ON q.SubmissionId = s.SubmissionId AND q.QuoteId = @QuoteId AND q.IsDeleted = 0
WHERE s.SubmissionId = @SubmissionId AND s.TenantId = @TenantId AND s.IsDeleted = 0;

SELECT TOP 1
       CAST(1 AS bit) AS IsConfigured,
       CAST(NULL AS uniqueidentifier) AS CommissionPlanApplicabilityId,
       p.CommissionPlanId,
       p.PlanName AS CommissionPlanName,
       CAST(NULL AS uniqueidentifier) AS CommissionPlanVersionId,
       CAST(NULL AS int) AS PlanVersionNumber,
       COALESCE(payee.PayeeId, payee.CommissionPayeeId) AS CommissionPayeeId,
       split.SplitRuleId AS CommissionSplitRuleId,
       @BusinessTypeCode AS BusinessTypeCode,
       COALESCE(split.OverrideRatePct, CASE WHEN @BusinessTypeCode = N'Renewal' THEN p.RenewalRatePct ELSE p.NewBusinessRatePct END) AS CommissionRatePct,
       COALESCE(split.SplitPct, payee.SplitPercentage) AS CommissionSplitPct,
       @ResolvedPremium AS CommissionablePremium,
       ROUND(@ResolvedPremium * COALESCE(split.OverrideRatePct, CASE WHEN @BusinessTypeCode = N'Renewal' THEN p.RenewalRatePct ELSE p.NewBusinessRatePct END) / 100.0, 2) AS EstimatedGrossCommission,
       ROUND(@ResolvedPremium * COALESCE(split.OverrideRatePct, CASE WHEN @BusinessTypeCode = N'Renewal' THEN p.RenewalRatePct ELSE p.NewBusinessRatePct END) / 100.0 * COALESCE(split.SplitPct, payee.SplitPercentage) / 100.0, 2) AS EstimatedProducerCommission
FROM Commission.CommissionPlan p
INNER JOIN Commission.CommissionPayee payee
    ON payee.CommissionPlanId = p.CommissionPlanId AND payee.TenantId = p.TenantId AND payee.UserId = @ProducerUserId
   AND payee.StatusCode = N'Active' AND payee.EffectiveDate <= @ResolvedEffectiveDate AND payee.IsDeleted = 0
OUTER APPLY
(
    SELECT TOP 1 sr.SplitRuleId, sr.SplitPct, sr.OverrideRatePct
    FROM Commission.CommissionSplitRule sr
    WHERE sr.TenantId = p.TenantId AND sr.CommissionPlanId = p.CommissionPlanId
      AND (sr.PayeeId = COALESCE(payee.PayeeId, payee.CommissionPayeeId) OR sr.PayeeId IS NULL) AND sr.StatusCode = N'Active'
      AND sr.EffectiveStartDate <= @ResolvedEffectiveDate AND (sr.EffectiveEndDate IS NULL OR sr.EffectiveEndDate >= @ResolvedEffectiveDate)
      AND sr.IsDeleted = 0
    ORDER BY CASE WHEN sr.PayeeId = COALESCE(payee.PayeeId, payee.CommissionPayeeId) THEN 0 ELSE 1 END, sr.Priority, sr.EffectiveStartDate DESC
) split
WHERE p.TenantId = @TenantId AND p.StatusCode = N'Active' AND p.IsDeleted = 0
  AND p.EffectiveStartDate <= @ResolvedEffectiveDate AND (p.EffectiveEndDate IS NULL OR p.EffectiveEndDate >= @ResolvedEffectiveDate)
  AND @ResolvedPremium > 0 AND @ProducerUserId IS NOT NULL
ORDER BY p.EffectiveStartDate DESC, p.CreatedDateUtc DESC;";

        return await cn.QuerySingleOrDefaultAsync<BindCommissionEstimateDto>(new CommandDefinition(sql, new
        {
            SubmissionId = submissionId,
            QuoteId = quoteId,
            TenantId = tenantId,
            CommissionablePremium = commissionablePremium,
            EffectiveDate = effectiveDate
        }, cancellationToken: cancellationToken));
    }

    private async Task EnsureDefaultIntakeAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, null, cancellationToken);
        const string sql = @"
DECLARE @LineOfBusiness NVARCHAR(100);

SELECT @LineOfBusiness = s.LineOfBusiness
FROM Submissions.Submission s
WHERE s.SubmissionId = @SubmissionId
  AND s.TenantId = @TenantId
  AND s.IsDeleted = 0;

INSERT INTO Submissions.SubmissionIntakeQuestion
    (IntakeQuestionId, SubmissionId, TenantId, ReadinessRequirementId, QuestionCode, QuestionText, HelpText, IsRequired, StatusCode, ScoreWeight, SortOrder, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @SubmissionId, @TenantId, r.ReadinessRequirementId, r.RequirementCode, r.DisplayName, r.Description, r.IsRequired,
       N'NeedsReview', r.ScoreWeight, r.SortOrder, SYSUTCDATETIME(), 0
FROM Submissions.SubmissionReadinessRequirement r
WHERE r.TenantId = @TenantId
  AND r.LineOfBusiness = @LineOfBusiness
  AND r.ScopeCode = N'Submission'
  AND r.IsActive = 1
  AND r.IsDeleted = 0
  AND NOT EXISTS
  (
      SELECT 1
      FROM Submissions.SubmissionIntakeQuestion q
      WHERE q.SubmissionId = @SubmissionId
        AND q.QuestionCode = r.RequirementCode
        AND q.IsDeleted = 0
  );

UPDATE q
SET ReadinessRequirementId = r.ReadinessRequirementId,
    QuestionText = r.DisplayName,
    HelpText = r.Description,
    IsRequired = r.IsRequired,
    ScoreWeight = r.ScoreWeight,
    SortOrder = r.SortOrder,
    StatusCode = CASE
        WHEN q.IsAnswered = 1 AND q.StatusCode = N'NeedsReview' THEN N'Confirmed'
        WHEN q.IsAnswered = 0 AND q.StatusCode = N'Confirmed' THEN N'NeedsReview'
        ELSE q.StatusCode
    END,
    CompletedByUserId = CASE WHEN q.IsAnswered = 1 THEN COALESCE(q.CompletedByUserId, q.AnsweredByUserId) ELSE q.CompletedByUserId END,
    CompletedDateUtc = CASE WHEN q.IsAnswered = 1 THEN COALESCE(q.CompletedDateUtc, q.AnsweredDateUtc) ELSE q.CompletedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME()
FROM Submissions.SubmissionIntakeQuestion q
INNER JOIN Submissions.SubmissionReadinessRequirement r ON r.TenantId = q.TenantId AND r.LineOfBusiness = @LineOfBusiness AND r.RequirementCode = q.QuestionCode AND r.ScopeCode = N'Submission' AND r.IsDeleted = 0
WHERE q.SubmissionId = @SubmissionId
  AND q.TenantId = @TenantId
  AND q.IsDeleted = 0;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private async Task EnsureMarketReadinessAsync(Guid submissionId, Guid submissionMarketId, Guid tenantId, CancellationToken cancellationToken)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, tenantId, cancellationToken);
        const string sql = @"
DECLARE @LineOfBusiness NVARCHAR(100), @CarrierId UNIQUEIDENTIFIER, @ChannelCode NVARCHAR(50);

SELECT @LineOfBusiness = s.LineOfBusiness,
       @CarrierId = sm.CarrierId,
       @ChannelCode = NULLIF(sm.SubmissionMethodCode, N'')
FROM Submissions.Submission s
INNER JOIN Submissions.SubmissionMarket sm ON sm.SubmissionId = s.SubmissionId AND sm.IsDeleted = 0
WHERE s.SubmissionId = @SubmissionId
  AND s.TenantId = @TenantId
  AND sm.SubmissionMarketId = @SubmissionMarketId
  AND s.IsDeleted = 0;

INSERT INTO Submissions.SubmissionIntakeQuestion
    (IntakeQuestionId, SubmissionId, SubmissionMarketId, TenantId, CarrierId, ReadinessRequirementId, QuestionCode, QuestionText, HelpText, IsRequired, StatusCode, ScoreWeight, SortOrder, ScopeCode, BlocksSubmit, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @SubmissionId, @SubmissionMarketId, @TenantId, @CarrierId, r.ReadinessRequirementId, r.RequirementCode, r.DisplayName, r.Description, r.IsRequired,
       N'NeedsReview', r.ScoreWeight, r.SortOrder, COALESCE(r.ScopeCode, N'Market'), r.BlocksSubmit, SYSUTCDATETIME(), 0
FROM Submissions.SubmissionReadinessRequirement r
WHERE r.TenantId = @TenantId
  AND r.LineOfBusiness = @LineOfBusiness
  AND r.IsActive = 1
  AND r.IsDeleted = 0
  AND (r.CarrierId IS NULL OR r.CarrierId = @CarrierId)
  AND (r.ChannelCode IS NULL OR r.ChannelCode = @ChannelCode)
  AND COALESCE(r.ScopeCode, N'Submission') IN (N'Market', N'Carrier', N'Submission')
  AND NOT EXISTS
  (
      SELECT 1
      FROM Submissions.SubmissionIntakeQuestion q
      WHERE q.SubmissionId = @SubmissionId
        AND ((q.SubmissionMarketId = @SubmissionMarketId) OR (q.SubmissionMarketId IS NULL AND r.CarrierId IS NULL))
        AND q.QuestionCode = r.RequirementCode
        AND q.IsDeleted = 0
  );

UPDATE q
SET ReadinessRequirementId = r.ReadinessRequirementId,
    CarrierId = COALESCE(q.CarrierId, r.CarrierId),
    QuestionText = r.DisplayName,
    HelpText = r.Description,
    IsRequired = r.IsRequired,
    ScoreWeight = r.ScoreWeight,
    SortOrder = r.SortOrder,
    ScopeCode = COALESCE(r.ScopeCode, q.ScopeCode),
    BlocksSubmit = r.BlocksSubmit,
    ModifiedDateUtc = SYSUTCDATETIME()
FROM Submissions.SubmissionIntakeQuestion q
INNER JOIN Submissions.SubmissionReadinessRequirement r ON r.TenantId = q.TenantId AND r.RequirementCode = q.QuestionCode AND r.IsDeleted = 0
WHERE q.SubmissionId = @SubmissionId
  AND q.TenantId = @TenantId
  AND (q.SubmissionMarketId = @SubmissionMarketId OR q.SubmissionMarketId IS NULL)
  AND r.LineOfBusiness = @LineOfBusiness
  AND (r.CarrierId IS NULL OR r.CarrierId = @CarrierId)
  AND q.IsDeleted = 0;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { SubmissionId = submissionId, SubmissionMarketId = submissionMarketId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SubmissionReadinessRequirementDto>> GetReadinessRequirementsAsync(Guid tenantId, string? searchTerm, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, tenantId, cancellationToken);
        const string sql = @"
SELECT r.ReadinessRequirementId, r.TenantId, r.LineOfBusiness, r.CarrierId, c.CarrierName, r.StateCode, r.ChannelCode,
       r.ScopeCode, r.RequirementCode, r.RequirementTypeCode, r.DisplayName, r.Description, r.IsRequired, r.BlocksSubmit,
       r.AllowsWaiver, r.RequiresEvidence, r.EvidencePrompt, r.ApprovalRoleCode, r.ActionCode, r.ActionLabel, r.ScoreWeight, r.SortOrder, r.IsActive, r.CreatedDateUtc
FROM Submissions.SubmissionReadinessRequirement r
LEFT JOIN Core.Carrier c ON c.CarrierId = r.CarrierId AND c.IsDeleted = 0
WHERE r.TenantId = @TenantId
  AND r.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = N'' OR r.DisplayName LIKE N'%' + @SearchTerm + N'%' OR r.RequirementCode LIKE N'%' + @SearchTerm + N'%' OR r.LineOfBusiness LIKE N'%' + @SearchTerm + N'%' OR c.CarrierName LIKE N'%' + @SearchTerm + N'%')
ORDER BY r.LineOfBusiness, c.CarrierName, r.ScopeCode, r.SortOrder, r.DisplayName;";
        return (await cn.QueryAsync<SubmissionReadinessRequirementDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> UpsertReadinessRequirementAsync(Guid? readinessRequirementId, UpsertSubmissionReadinessRequirementRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
DECLARE @Id UNIQUEIDENTIFIER = COALESCE(@ReadinessRequirementId, NEWID());

IF @ReadinessRequirementId IS NULL
BEGIN
    INSERT INTO Submissions.SubmissionReadinessRequirement
        (ReadinessRequirementId, TenantId, LineOfBusiness, CarrierId, StateCode, ChannelCode, ScopeCode, RequirementCode, RequirementTypeCode, DisplayName, Description,
         IsRequired, BlocksSubmit, AllowsWaiver, RequiresEvidence, EvidencePrompt, ApprovalRoleCode, ActionCode, ActionLabel, ScoreWeight, SortOrder, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@Id, @TenantId, @LineOfBusiness, @CarrierId, NULLIF(@StateCode, N''), NULLIF(@ChannelCode, N''), @ScopeCode, @RequirementCode, @RequirementTypeCode, @DisplayName, @Description,
            @IsRequired, @BlocksSubmit, @AllowsWaiver, @RequiresEvidence, @EvidencePrompt, @ApprovalRoleCode, NULLIF(@ActionCode, N''), NULLIF(@ActionLabel, N''), @ScoreWeight, @SortOrder, @IsActive, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END
ELSE
BEGIN
    UPDATE Submissions.SubmissionReadinessRequirement
    SET LineOfBusiness = @LineOfBusiness,
        CarrierId = @CarrierId,
        StateCode = NULLIF(@StateCode, N''),
        ChannelCode = NULLIF(@ChannelCode, N''),
        ScopeCode = @ScopeCode,
        RequirementCode = @RequirementCode,
        RequirementTypeCode = @RequirementTypeCode,
        DisplayName = @DisplayName,
        Description = @Description,
        IsRequired = @IsRequired,
        BlocksSubmit = @BlocksSubmit,
        AllowsWaiver = @AllowsWaiver,
        RequiresEvidence = @RequiresEvidence,
        EvidencePrompt = @EvidencePrompt,
        ApprovalRoleCode = @ApprovalRoleCode,
        ActionCode = NULLIF(@ActionCode, N''),
        ActionLabel = NULLIF(@ActionLabel, N''),
        ScoreWeight = @ScoreWeight,
        SortOrder = @SortOrder,
        IsActive = @IsActive,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ModifiedByUserId
    WHERE ReadinessRequirementId = @Id AND TenantId = @TenantId AND IsDeleted = 0;

    IF @@ROWCOUNT = 0 THROW 52070, 'Readiness requirement was not found.', 1;
END;

SELECT @Id;";
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { ReadinessRequirementId = readinessRequirementId, request.TenantId, request.LineOfBusiness, request.CarrierId, request.StateCode, request.ChannelCode, request.ScopeCode, request.RequirementCode, request.RequirementTypeCode, request.DisplayName, request.Description, request.IsRequired, request.BlocksSubmit, request.AllowsWaiver, request.RequiresEvidence, request.EvidencePrompt, request.ApprovalRoleCode, request.ActionCode, request.ActionLabel, request.ScoreWeight, request.SortOrder, request.IsActive, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteReadinessRequirementAsync(Guid readinessRequirementId, Guid tenantId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, tenantId, cancellationToken);
        const string sql = @"
UPDATE Submissions.SubmissionReadinessRequirement
SET IsDeleted = 1,
    IsActive = 0,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE ReadinessRequirementId = @ReadinessRequirementId AND TenantId = @TenantId AND IsDeleted = 0;

IF @@ROWCOUNT = 0 THROW 52071, 'Readiness requirement was not found.', 1;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ReadinessRequirementId = readinessRequirementId, TenantId = tenantId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    private static async Task RecordOpportunityWorkflowAsync(System.Data.IDbConnection cn, Guid submissionId, Guid tenantId, string stageName, string eventType, string eventTitle, string eventDetail, string relatedEntityName, Guid? relatedEntityId, Guid? userId, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @OpportunityId UNIQUEIDENTIFIER;
SELECT @OpportunityId = OpportunityId FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

IF @OpportunityId IS NOT NULL AND @OpportunityId <> '00000000-0000-0000-0000-000000000000'
BEGIN
    DECLARE @StageId UNIQUEIDENTIFIER = (SELECT TOP 1 OpportunityStageId FROM CRM.OpportunityStage WHERE TenantId = @TenantId AND StageName = @StageName AND IsActive = 1 ORDER BY SortOrder, StageName);

    UPDATE opportunity
    SET StageName = stage.StageName,
        OpportunityStageId = stage.OpportunityStageId,
        ForecastCategoryCode = CASE WHEN @StageName IN (N'Won', N'Bound', N'Closed Won') THEN N'Closed Won' WHEN @StageName IN (N'Lost', N'Declined', N'Closed Lost') THEN N'Closed Lost' ELSE COALESCE(NULLIF(ForecastCategoryCode, N''), N'Pipeline') END,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @UserId
    FROM CRM.Opportunity opportunity
    INNER JOIN CRM.OpportunityStage stage ON stage.OpportunityStageId = @StageId
    WHERE opportunity.OpportunityId = @OpportunityId AND opportunity.TenantId = @TenantId AND opportunity.IsDeleted = 0
      AND COALESCE(opportunity.StageName, N'') NOT IN (N'Closed Won', N'Closed Lost');

    INSERT INTO CRM.OpportunityWorkflowEvent (WorkflowEventId, TenantId, OpportunityId, EventType, EventTitle, EventDetail, RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @OpportunityId, @EventType, @EventTitle, @EventDetail, @RelatedEntityName, @RelatedEntityId, SYSUTCDATETIME(), SYSUTCDATETIME(), @UserId, 0);
END;";

        await cn.ExecuteAsync(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId, StageName = stageName, EventType = eventType, EventTitle = eventTitle, EventDetail = eventDetail, RelatedEntityName = relatedEntityName, RelatedEntityId = relatedEntityId, UserId = userId }, cancellationToken: cancellationToken));
    }

    // ── Submission Register ───────────────────────────────────────────

    private const string SubmissionColumns = @"
        s.SubmissionId, s.TenantId, s.AccountId, a.AccountName, s.OpportunityId, o.OpportunityName,
        s.SubmissionNumber, s.LineOfBusiness, s.Status, s.Priority,
        s.AssignedToUserId, u.FullName AS AssignedToUserName,
        s.EffectiveDate, s.ExpirationDate, s.TargetPremium,
        s.MarketCount, s.QuoteCount, s.CreatedDateUtc, s.ModifiedDateUtc";

    public async Task<PagedResult<SubmissionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT s.SubmissionId,
           s.TenantId,
           s.AccountId,
           a.AccountName,
       primaryContact.ContactId AS PrimaryContactId,
       primaryContact.ContactName AS PrimaryContactName,
       primaryContact.Email AS PrimaryContactEmail,
           s.OpportunityId,
           COALESCE(o.OpportunityName, s.SubmissionNumber) AS OpportunityName,
           s.SubmissionNumber,
           s.LineOfBusiness,
           s.Status,
           s.Priority,
           s.AssignedToUserId,
           COALESCE(u.FullName, u.DisplayName, u.UserName) AS AssignedToUserName,
           s.EffectiveDate,
           s.ExpirationDate,
           s.TargetPremium,
           (SELECT COUNT(1) FROM Submissions.SubmissionMarket sm WHERE sm.SubmissionId = s.SubmissionId AND sm.IsDeleted = 0) AS MarketCount,
           (SELECT COUNT(1) FROM Submissions.Quote q WHERE q.SubmissionId = s.SubmissionId AND q.IsDeleted = 0) AS QuoteCount,
           s.CreatedDateUtc,
           s.ModifiedDateUtc
    FROM   Submissions.Submission s
    JOIN   Client.Account a ON a.AccountId = s.AccountId
OUTER APPLY
(
    SELECT TOP 1 contact.ContactId,
           LTRIM(RTRIM(CONCAT(contact.FirstName, N' ', contact.LastName))) AS ContactName,
           contact.Email
    FROM Client.Contact contact
    WHERE contact.TenantId = s.TenantId
      AND contact.AccountId = s.AccountId
      AND (contact.ContactTypeCode = N'Primary' OR contact.IsKeyContact = 1 OR contact.IsServiceContact = 1 OR contact.IsBillingContact = 1)
      AND contact.IsDeleted = 0
    ORDER BY CASE WHEN contact.ContactTypeCode = N'Primary' THEN 0 ELSE 1 END,
             CASE WHEN contact.IsKeyContact = 1 THEN 0 ELSE 1 END,
             CASE WHEN contact.StatusCode = N'Active' THEN 0 ELSE 1 END,
             COALESCE(contact.ModifiedDateUtc, contact.CreatedDateUtc) DESC,
             contact.ContactId
) primaryContact
    LEFT JOIN CRM.Opportunity o ON o.OpportunityId = s.OpportunityId
    LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
    WHERE  s.TenantId = @TenantId
      AND  s.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = '' OR s.SubmissionNumber LIKE '%' + @SearchTerm + '%' OR s.LineOfBusiness LIKE '%' + @SearchTerm + '%' OR a.AccountName LIKE '%' + @SearchTerm + '%' OR o.OpportunityName LIKE '%' + @SearchTerm + '%')
),
Filtered AS
(
    SELECT *
    FROM Cte
    WHERE (@Status IS NULL OR @Status = '' OR Status = @Status)
      AND (@LineOfBusiness IS NULL OR @LineOfBusiness = '' OR LineOfBusiness = @LineOfBusiness)
)
SELECT * FROM Filtered
ORDER BY CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

;WITH Cte AS
(
    SELECT s.LineOfBusiness, s.Status
    FROM   Submissions.Submission s
    JOIN   Client.Account a ON a.AccountId = s.AccountId
    LEFT JOIN CRM.Opportunity o ON o.OpportunityId = s.OpportunityId
    WHERE  s.TenantId = @TenantId
      AND  s.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = '' OR s.SubmissionNumber LIKE '%' + @SearchTerm + '%' OR s.LineOfBusiness LIKE '%' + @SearchTerm + '%' OR a.AccountName LIKE '%' + @SearchTerm + '%' OR o.OpportunityName LIKE '%' + @SearchTerm + '%')
),
Filtered AS
(
    SELECT *
    FROM Cte
    WHERE (@Status IS NULL OR @Status = '' OR Status = @Status)
      AND (@LineOfBusiness IS NULL OR @LineOfBusiness = '' OR LineOfBusiness = @LineOfBusiness)
)
SELECT COUNT(1) FROM Filtered;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId       = tenantId,
            SearchTerm     = searchTerm,
            Status         = status,
            LineOfBusiness = lineOfBusiness,
            Offset         = (Math.Max(pageNumber, 1) - 1) * pageSize,
            PageSize       = pageSize,
        }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<SubmissionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<SubmissionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<SubmissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT s.SubmissionId,
       s.TenantId,
       s.AccountId,
       a.AccountName,
       primaryContact.ContactId AS PrimaryContactId,
       primaryContact.ContactName AS PrimaryContactName,
       primaryContact.Email AS PrimaryContactEmail,
       s.OpportunityId,
       COALESCE(o.OpportunityName, s.SubmissionNumber) AS OpportunityName,
       s.SubmissionNumber,
       s.LineOfBusiness,
       s.Status,
       s.Priority,
       s.AssignedToUserId,
       COALESCE(u.FullName, u.DisplayName, u.UserName) AS AssignedToUserName,
       s.EffectiveDate,
       s.ExpirationDate,
       s.TargetPremium,
       (SELECT COUNT(1) FROM Submissions.SubmissionMarket sm WHERE sm.SubmissionId = s.SubmissionId AND sm.IsDeleted = 0) AS MarketCount,
       (SELECT COUNT(1) FROM Submissions.Quote q WHERE q.SubmissionId = s.SubmissionId AND q.IsDeleted = 0) AS QuoteCount,
       s.CreatedDateUtc,
       s.ModifiedDateUtc
FROM   Submissions.Submission s
JOIN   Client.Account a ON a.AccountId = s.AccountId
OUTER APPLY
(
    SELECT TOP 1 contact.ContactId,
           LTRIM(RTRIM(CONCAT(contact.FirstName, N' ', contact.LastName))) AS ContactName,
           contact.Email
    FROM Client.Contact contact
    WHERE contact.TenantId = s.TenantId
      AND contact.AccountId = s.AccountId
      AND (contact.ContactTypeCode = N'Primary' OR contact.IsKeyContact = 1 OR contact.IsServiceContact = 1 OR contact.IsBillingContact = 1)
      AND contact.IsDeleted = 0
    ORDER BY CASE WHEN contact.ContactTypeCode = N'Primary' THEN 0 ELSE 1 END,
             CASE WHEN contact.IsKeyContact = 1 THEN 0 ELSE 1 END,
             CASE WHEN contact.StatusCode = N'Active' THEN 0 ELSE 1 END,
             COALESCE(contact.ModifiedDateUtc, contact.CreatedDateUtc) DESC,
             contact.ContactId
) primaryContact
LEFT JOIN CRM.Opportunity o ON o.OpportunityId = s.OpportunityId
LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
WHERE  s.SubmissionId = @Id AND s.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<SubmissionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @PrimaryOpportunityLineId UNIQUEIDENTIFIER;
DECLARE @PrimaryLobId UNIQUEIDENTIFIER;
DECLARE @PrimaryLineOfBusiness NVARCHAR(100);

SELECT TOP 1
    @PrimaryOpportunityLineId = line.OpportunityLineId,
    @PrimaryLobId = line.LobId,
    @PrimaryLineOfBusiness = lob.LobName
FROM CRM.Opportunity opportunity
INNER JOIN CRM.OpportunityLine line
    ON line.TenantId = opportunity.TenantId
   AND line.OpportunityId = opportunity.OpportunityId
   AND line.IsDeleted = 0
INNER JOIN Agency.LineOfBusiness lob
    ON lob.TenantId = line.TenantId
   AND lob.LobId = line.LobId
   AND lob.IsActive = 1
   AND lob.IsDeleted = 0
WHERE opportunity.TenantId = @TenantId
  AND opportunity.OpportunityId = @OpportunityId
  AND opportunity.AccountId = @AccountId
  AND opportunity.IsDeleted = 0
ORDER BY CASE WHEN line.OpportunityLineId = opportunity.PrimaryOpportunityLineId OR line.IsPrimary = 1 THEN 0 ELSE 1 END,
         line.EstPremium DESC,
         line.CreatedDateUtc;

IF @PrimaryOpportunityLineId IS NULL
    THROW 52420, N'The selected opportunity does not have an active database-backed line of business.', 1;

INSERT INTO Submissions.Submission
    (SubmissionId, TenantId, AccountId, OpportunityId, SubmissionNumber, LobId, LineOfBusiness, Status, Priority,
     AssignedToUserId, CsrUserId, EffectiveDate, ExpirationDate, TargetPremium, RiskState, NamedInsured, Description, InternalNotes, IsRush, MarketCount, QuoteCount,
     CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@SubmissionId, @TenantId, @AccountId, @OpportunityId,
     'SUB-' + FORMAT(GETUTCDATE(), 'yyyyMMdd') + '-' + RIGHT('0000' + CAST(NEXT VALUE FOR Submissions.SubmissionSeq AS VARCHAR), 4),
     @PrimaryLobId, @PrimaryLineOfBusiness, 'Draft', @Priority,
      @AssignedToUserId, @CsrUserId, @EffectiveDate, @ExpirationDate, @TargetPremium, @RiskState, @NamedInsured, @Description, @InternalNotes, @IsRush, 0, 0,
     GETUTCDATE(), @CreatedByUserId, 0);

INSERT INTO Submissions.SubmissionLine
    (SubmissionLineId, TenantId, SubmissionId, OpportunityId, OpportunityLineId, LobId, LineOfBusiness, TargetPremium, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), line.TenantId, @SubmissionId, line.OpportunityId, line.OpportunityLineId, line.LobId, lob.LobName,
       CASE WHEN line.OpportunityLineId = @PrimaryOpportunityLineId THEN COALESCE(@TargetPremium, line.EstPremium, 0) ELSE COALESCE(line.EstPremium, 0) END,
       SYSUTCDATETIME(), @CreatedByUserId, 0
FROM CRM.OpportunityLine line
INNER JOIN Agency.LineOfBusiness lob
    ON lob.TenantId = line.TenantId
   AND lob.LobId = line.LobId
   AND lob.IsActive = 1
   AND lob.IsDeleted = 0
WHERE line.TenantId = @TenantId
  AND line.OpportunityId = @OpportunityId
  AND line.IsDeleted = 0;

INSERT INTO CRM.OpportunitySubmissionLine
    (OpportunitySubmissionLineId, TenantId, SubmissionId, OpportunityId, OpportunityLineId, LineOfBusiness, TargetPremium, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), submissionLine.TenantId, submissionLine.SubmissionId, submissionLine.OpportunityId, submissionLine.OpportunityLineId,
       submissionLine.LineOfBusiness, submissionLine.TargetPremium, SYSUTCDATETIME(), @CreatedByUserId, 0
FROM Submissions.SubmissionLine submissionLine
WHERE submissionLine.TenantId = @TenantId
  AND submissionLine.SubmissionId = @SubmissionId
  AND submissionLine.IsDeleted = 0;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = cn.BeginTransaction();
        try
        {
            await cn.ExecuteAsync(new CommandDefinition(sql, new
            {
                SubmissionId = id,
                request.TenantId,
                request.AccountId,
                request.OpportunityId,
                request.Priority,
                request.AssignedToUserId,
                request.CsrUserId,
                request.EffectiveDate,
                request.ExpirationDate,
                request.TargetPremium,
                request.RiskState,
                request.NamedInsured,
                request.Description,
                request.InternalNotes,
                request.IsRush,
                request.CreatedByUserId,
            }, transaction, cancellationToken: cancellationToken));
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        return id;
    }

    public async Task SubmitProposalReviewAsync(Guid proposalId, SubmitProposalReviewRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER, @VersionNumber INT, @ReviewRound INT, @ReviewId UNIQUEIDENTIFIER = NEWID();
SELECT @SubmissionId = SubmissionId, @VersionNumber = VersionNumber FROM Submissions.Proposal WITH (UPDLOCK) WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND GovernanceStatusCode IN (N'Draft', N'ChangesRequired') AND IsDeleted = 0;
IF @SubmissionId IS NULL THROW 52200, 'Only a draft or changes-required proposal may be submitted for review.', 1;
IF NOT EXISTS (SELECT 1 FROM IAM.[User] WHERE UserId = @AssignedReviewerUserId AND TenantId = @TenantId AND IsActive = 1 AND IsDeleted = 0) THROW 52201, 'Assigned reviewer is not an active tenant user.', 1;
IF EXISTS (SELECT 1 FROM Submissions.ProposalReview WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND StatusCode = N'Pending' AND IsDeleted = 0) THROW 52202, 'An active proposal review already exists.', 1;
SET @ReviewRound = ISNULL((SELECT MAX(ReviewRound) FROM Submissions.ProposalReview WHERE ProposalId = @ProposalId AND TenantId = @TenantId), 0) + 1;
INSERT INTO Submissions.ProposalReview (ProposalReviewId, TenantId, SubmissionId, ProposalId, ProposalVersionNumber, ReviewRound, StatusCode, AssignedReviewerUserId, RequestedByUserId, DueDateUtc, DecisionNotes, CreatedByUserId, IsDeleted)
VALUES (@ReviewId, @TenantId, @SubmissionId, @ProposalId, @VersionNumber, @ReviewRound, N'Pending', @AssignedReviewerUserId, @RequestedByUserId, @DueDateUtc, @ReviewNotes, @RequestedByUserId, 0);
UPDATE Submissions.Proposal SET Status = N'Internal Review', GovernanceStatusCode = N'InternalReview', CurrentReviewId = @ReviewId, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @RequestedByUserId WHERE ProposalId = @ProposalId;
INSERT INTO Submissions.ProposalLifecycleEvent (ProposalLifecycleEventId, TenantId, ProposalId, SubmissionId, EventCode, EventDetail, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted) VALUES (NEWID(), @TenantId, @ProposalId, @SubmissionId, N'ReviewRequested', @ReviewNotes, SYSUTCDATETIME(), SYSUTCDATETIME(), @RequestedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ProposalId = proposalId, request.TenantId, request.AssignedReviewerUserId, request.RequestedByUserId, request.DueDateUtc, request.ReviewNotes }, cancellationToken: cancellationToken));
    }

    public async Task DecideProposalReviewAsync(Guid proposalId, DecideProposalReviewRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @ReviewId UNIQUEIDENTIFIER, @SubmissionId UNIQUEIDENTIFIER, @VersionNumber INT, @SnapshotJson NVARCHAR(MAX), @SnapshotHash CHAR(64);
SELECT @ReviewId = review.ProposalReviewId, @SubmissionId = review.SubmissionId, @VersionNumber = review.ProposalVersionNumber
FROM Submissions.ProposalReview review WITH (UPDLOCK) INNER JOIN Submissions.Proposal proposal ON proposal.ProposalId = review.ProposalId AND proposal.CurrentReviewId = review.ProposalReviewId
WHERE review.ProposalId = @ProposalId AND review.TenantId = @TenantId AND review.AssignedReviewerUserId = @DecidedByUserId AND review.StatusCode = N'Pending' AND review.IsDeleted = 0;
IF @ReviewId IS NULL THROW 52203, 'The active review is not assigned to this reviewer.', 1;
IF @DecisionCode = N'Approved' AND NOT EXISTS (SELECT 1 FROM Submissions.ProposalQuote WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 52212, 'Proposal approval requires at least one included bindable quote with persisted bindable coverage lines.', 1;
IF @DecisionCode = N'Approved' AND EXISTS
(
    SELECT 1
    FROM Submissions.ProposalQuote proposalQuote
    INNER JOIN Submissions.Quote quote ON quote.QuoteId = proposalQuote.QuoteId
    WHERE proposalQuote.ProposalId = @ProposalId
      AND proposalQuote.TenantId = @TenantId
      AND proposalQuote.IsDeleted = 0
      AND
      (
          quote.IsDeleted = 1
          OR quote.IsBindable = 0
          OR NOT EXISTS (SELECT 1 FROM Submissions.QuoteLine quoteLine WHERE quoteLine.QuoteId = quote.QuoteId AND quoteLine.TenantId = @TenantId AND quoteLine.IsDeleted = 0)
          OR EXISTS (SELECT 1 FROM Submissions.QuoteLine quoteLine WHERE quoteLine.QuoteId = quote.QuoteId AND quoteLine.TenantId = @TenantId AND quoteLine.IsDeleted = 0 AND quoteLine.IsBindable = 0)
          OR EXISTS
          (
              SELECT 1
              FROM Submissions.SubmissionLine submissionLine
              WHERE submissionLine.SubmissionId = quote.SubmissionId
                AND submissionLine.TenantId = @TenantId
                AND submissionLine.IsDeleted = 0
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM Submissions.QuoteLine quoteLine
                    WHERE quoteLine.QuoteId = quote.QuoteId
                      AND quoteLine.TenantId = @TenantId
                      AND quoteLine.SubmissionLineId = submissionLine.SubmissionLineId
                      AND quoteLine.IsDeleted = 0
                      AND quoteLine.IsBindable = 1
                )
          )
      )
)
    THROW 52212, 'Proposal approval requires every included quote and persisted coverage line to be marked bindable. Open Quote Review and resolve all bindability items before approving.', 1;
UPDATE Submissions.ProposalReview SET StatusCode = @DecisionCode, DecisionNotes = @DecisionNotes, CompletedDateUtc = SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @DecidedByUserId WHERE ProposalReviewId = @ReviewId;
IF @DecisionCode = N'Approved'
BEGIN
    SELECT @SnapshotJson = (SELECT proposal.ProposalId, proposal.SubmissionId, proposal.VersionNumber, proposal.Title, proposal.HtmlContent, proposal.CustomIntroduction,
        JSON_QUERY((SELECT pq.QuoteId, pq.SortOrder, quote.QuoteNumber, quote.AnnualPremium, quote.ExpiresDateUtc, quote.ResponseVersion FROM Submissions.ProposalQuote pq INNER JOIN Submissions.Quote quote ON quote.QuoteId = pq.QuoteId WHERE pq.ProposalId = proposal.ProposalId AND pq.IsDeleted = 0 ORDER BY pq.SortOrder FOR JSON PATH)) AS Quotes
        FROM Submissions.Proposal proposal WHERE proposal.ProposalId = @ProposalId FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);
    SET @SnapshotHash = CONVERT(char(64), HASHBYTES('SHA2_256', @SnapshotJson), 2);
    INSERT INTO Submissions.ProposalApprovedSnapshot (ProposalApprovedSnapshotId, TenantId, SubmissionId, ProposalId, ProposalVersionNumber, SnapshotHash, SnapshotJson, ApprovedDateUtc, ApprovedByUserId) VALUES (NEWID(), @TenantId, @SubmissionId, @ProposalId, @VersionNumber, @SnapshotHash, @SnapshotJson, SYSUTCDATETIME(), @DecidedByUserId);
    UPDATE Submissions.Proposal SET Status = N'Ready to Deliver', GovernanceStatusCode = N'ReadyToDeliver', ApprovedDateUtc = SYSUTCDATETIME(), ApprovedByUserId = @DecidedByUserId, ApprovalVersionNumber = @VersionNumber, ApprovedSnapshotHash = @SnapshotHash, ReadyToDeliverDateUtc = SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @DecidedByUserId WHERE ProposalId = @ProposalId;
END
ELSE UPDATE Submissions.Proposal SET Status = @DecisionCode, GovernanceStatusCode = @DecisionCode, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @DecidedByUserId WHERE ProposalId = @ProposalId;
INSERT INTO Submissions.ProposalLifecycleEvent (ProposalLifecycleEventId, TenantId, ProposalId, SubmissionId, EventCode, EventDetail, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted) VALUES (NEWID(), @TenantId, @ProposalId, @SubmissionId, @DecisionCode, @DecisionNotes, SYSUTCDATETIME(), SYSUTCDATETIME(), @DecidedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ProposalId = proposalId, request.TenantId, request.DecisionCode, request.DecisionNotes, request.DecidedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> UpsertProposalRecipientAsync(Guid proposalId, UpsertProposalRecipientRequest request, CancellationToken cancellationToken = default)
    {
        var id = request.ProposalRecipientId.GetValueOrDefault(Guid.NewGuid());
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM Submissions.Proposal WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND GovernanceStatusCode IN (N'Draft', N'ChangesRequired', N'ReadyToDeliver') AND IsDeleted = 0) THROW 52204, 'Recipients cannot be changed after delivery begins.', 1;
IF @ContactId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Client.Contact contact INNER JOIN Submissions.Submission submission ON submission.AccountId = contact.AccountId AND submission.SubmissionId = (SELECT SubmissionId FROM Submissions.Proposal WHERE ProposalId = @ProposalId) WHERE contact.ContactId = @ContactId AND contact.TenantId = @TenantId AND contact.IsDeleted = 0) THROW 52205, 'Recipient contact does not belong to the submission account.', 1;
IF @IsPrimary = 1 UPDATE Submissions.ProposalRecipient SET IsPrimary = 0, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0;
IF @ContactId IS NOT NULL
BEGIN
    SELECT @RecipientName = COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(contact.FirstName, N' ', contact.LastName))), N''), @RecipientName),
           @RecipientEmail = COALESCE(NULLIF(contact.Email, N''), @RecipientEmail)
    FROM Client.Contact contact
    WHERE contact.ContactId = @ContactId AND contact.TenantId = @TenantId AND contact.IsDeleted = 0;
END;
MERGE Submissions.ProposalRecipient AS target USING (SELECT @ProposalRecipientId AS ProposalRecipientId) AS source ON target.ProposalRecipientId = source.ProposalRecipientId AND target.TenantId = @TenantId
WHEN MATCHED THEN UPDATE SET ContactId=@ContactId, RecipientTypeCode=@RecipientTypeCode, RecipientName=@RecipientName, RecipientEmail=@RecipientEmail, SigningOrder=@SigningOrder, IsPrimary=@IsPrimary, IsSigner=@IsSigner, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@ModifiedByUserId, IsDeleted=0
WHEN NOT MATCHED THEN INSERT (ProposalRecipientId,TenantId,SubmissionId,ProposalId,ContactId,RecipientTypeCode,RecipientName,RecipientEmail,SigningOrder,IsPrimary,IsSigner,CreatedByUserId,IsDeleted) VALUES (@ProposalRecipientId,@TenantId,(SELECT SubmissionId FROM Submissions.Proposal WHERE ProposalId=@ProposalId),@ProposalId,@ContactId,@RecipientTypeCode,@RecipientName,@RecipientEmail,@SigningOrder,@IsPrimary,@IsSigner,@ModifiedByUserId,0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ProposalId = proposalId, ProposalRecipientId = id, request.TenantId, request.ContactId, request.RecipientTypeCode, request.RecipientName, request.RecipientEmail, request.SigningOrder, request.IsPrimary, request.IsSigner, request.ModifiedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task DeleteProposalRecipientAsync(Guid proposalId, Guid recipientId, Guid tenantId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE recipient SET IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@ModifiedByUserId FROM Submissions.ProposalRecipient recipient INNER JOIN Submissions.Proposal proposal ON proposal.ProposalId=recipient.ProposalId WHERE recipient.ProposalRecipientId=@RecipientId AND recipient.ProposalId=@ProposalId AND recipient.TenantId=@TenantId AND proposal.GovernanceStatusCode IN (N'Draft',N'ChangesRequired',N'ReadyToDeliver'); IF @@ROWCOUNT=0 THROW 52206, 'Recipient cannot be removed.', 1;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ProposalId = proposalId, RecipientId = recipientId, TenantId = tenantId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ProposalSlaPolicyDto>> GetProposalSlaPoliciesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<ProposalSlaPolicyDto>(new CommandDefinition("SELECT ProposalSlaPolicyId,TenantId,EventCode,DueAfterMinutes,EscalateAfterMinutes,PriorityCode,AssignedRoleCode,IsActive FROM Submissions.ProposalSlaPolicy WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY EventCode;", new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> UpsertProposalSlaPolicyAsync(UpsertProposalSlaPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"MERGE Submissions.ProposalSlaPolicy AS target USING (SELECT @TenantId TenantId,@EventCode EventCode) source ON target.TenantId=source.TenantId AND target.EventCode=source.EventCode WHEN MATCHED THEN UPDATE SET DueAfterMinutes=@DueAfterMinutes,EscalateAfterMinutes=@EscalateAfterMinutes,PriorityCode=@PriorityCode,AssignedRoleCode=@AssignedRoleCode,IsActive=@IsActive,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHEN NOT MATCHED THEN INSERT (ProposalSlaPolicyId,TenantId,EventCode,DueAfterMinutes,EscalateAfterMinutes,PriorityCode,AssignedRoleCode,IsActive,CreatedByUserId,IsDeleted) VALUES (@Id,@TenantId,@EventCode,@DueAfterMinutes,@EscalateAfterMinutes,@PriorityCode,@AssignedRoleCode,@IsActive,@ModifiedByUserId,0) OUTPUT inserted.ProposalSlaPolicyId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { Id=id, request.TenantId, request.EventCode, request.DueAfterMinutes, request.EscalateAfterMinutes, request.PriorityCode, request.AssignedRoleCode, request.IsActive, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Guid id, UpdateSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.Submission
SET    LineOfBusiness  = @LineOfBusiness,
       Status          = @Status,
       Priority        = @Priority,
       EffectiveDate   = @EffectiveDate,
       ExpirationDate  = @ExpirationDate,
       TargetPremium   = @TargetPremium,
       AssignedToUserId = @AssignedToUserId,
       ModifiedDateUtc = GETUTCDATE()
WHERE  SubmissionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.LineOfBusiness,
            request.Status,
            request.Priority,
            request.EffectiveDate,
            request.ExpirationDate,
            request.TargetPremium,
            request.AssignedToUserId,
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SubmissionReadinessEvidenceDocumentDto>> GetReadinessEvidenceDocumentsAsync(Guid submissionId, Guid intakeQuestionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, tenantId, cancellationToken);
        return await GetEvidenceDocumentsAsync(cn, submissionId, intakeQuestionId, tenantId, cancellationToken);
    }

    public async Task ReplaceReadinessEvidenceDocumentsAsync(Guid submissionId, Guid intakeQuestionId, ReplaceSubmissionReadinessEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);

        var distinctDocumentIds = request.DocumentIds.Distinct().ToArray();
        var documentIdsJson = System.Text.Json.JsonSerializer.Serialize(distinctDocumentIds);
        const string validationSql = @"
DECLARE @SelectedDocuments TABLE (Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);
INSERT INTO @SelectedDocuments (Id)
SELECT DISTINCT TRY_CONVERT(UNIQUEIDENTIFIER, [value])
FROM OPENJSON(@DocumentIdsJson)
WHERE TRY_CONVERT(UNIQUEIDENTIFIER, [value]) IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionIntakeQuestion WHERE IntakeQuestionId = @IntakeQuestionId AND SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 52013, 'Submission readiness requirement was not found.', 1;

IF EXISTS
(
    SELECT 1
    FROM @SelectedDocuments source
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM DMS.Document d
        WHERE d.DocumentId = source.Id
          AND d.TenantId = @TenantId
          AND d.EntityName = N'Submission'
          AND d.EntityId = @SubmissionId
          AND d.IsDeleted = 0
    )
)
    THROW 52014, 'One or more selected evidence documents are not attached to this submission.', 1;";

        using var tx = cn.BeginTransaction();
        await cn.ExecuteAsync(new CommandDefinition(validationSql, new { SubmissionId = submissionId, IntakeQuestionId = intakeQuestionId, request.TenantId, DocumentIdsJson = documentIdsJson }, tx, cancellationToken: cancellationToken));

        const string deleteSql = @"
DECLARE @SelectedDocuments TABLE (Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);
INSERT INTO @SelectedDocuments (Id)
SELECT DISTINCT TRY_CONVERT(UNIQUEIDENTIFIER, [value])
FROM OPENJSON(@DocumentIdsJson)
WHERE TRY_CONVERT(UNIQUEIDENTIFIER, [value]) IS NOT NULL;

UPDATE Submissions.SubmissionReadinessEvidenceDocument
SET IsDeleted = 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE IntakeQuestionId = @IntakeQuestionId
  AND SubmissionId = @SubmissionId
  AND TenantId = @TenantId
  AND IsDeleted = 0
  AND DocumentId NOT IN (SELECT Id FROM @SelectedDocuments);";
        await cn.ExecuteAsync(new CommandDefinition(deleteSql, new { SubmissionId = submissionId, IntakeQuestionId = intakeQuestionId, request.TenantId, request.ModifiedByUserId, DocumentIdsJson = documentIdsJson }, tx, cancellationToken: cancellationToken));

        const string upsertSql = @"
DECLARE @SelectedDocuments TABLE (Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);
INSERT INTO @SelectedDocuments (Id)
SELECT DISTINCT TRY_CONVERT(UNIQUEIDENTIFIER, [value])
FROM OPENJSON(@DocumentIdsJson)
WHERE TRY_CONVERT(UNIQUEIDENTIFIER, [value]) IS NOT NULL;

INSERT INTO Submissions.SubmissionReadinessEvidenceDocument
    (SubmissionReadinessEvidenceDocumentId, TenantId, SubmissionId, IntakeQuestionId, ReadinessRequirementId, SubmissionMarketId, CarrierId, DocumentId, EvidenceRoleCode, Notes, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), q.TenantId, q.SubmissionId, q.IntakeQuestionId, q.ReadinessRequirementId, q.SubmissionMarketId, q.CarrierId, source.Id,
       COALESCE(NULLIF(@EvidenceRoleCode, N''), N'SupportingEvidence'), @Notes, SYSUTCDATETIME(), @ModifiedByUserId, 0
FROM @SelectedDocuments source
INNER JOIN Submissions.SubmissionIntakeQuestion q ON q.IntakeQuestionId = @IntakeQuestionId AND q.SubmissionId = @SubmissionId AND q.TenantId = @TenantId AND q.IsDeleted = 0
WHERE NOT EXISTS
(
    SELECT 1
    FROM Submissions.SubmissionReadinessEvidenceDocument existing
    WHERE existing.IntakeQuestionId = q.IntakeQuestionId
      AND existing.DocumentId = source.Id
      AND existing.IsDeleted = 0
);

UPDATE existing
SET EvidenceRoleCode = COALESCE(NULLIF(@EvidenceRoleCode, N''), existing.EvidenceRoleCode),
    Notes = @Notes,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
FROM Submissions.SubmissionReadinessEvidenceDocument existing
INNER JOIN @SelectedDocuments source ON source.Id = existing.DocumentId
WHERE existing.IntakeQuestionId = @IntakeQuestionId
  AND existing.SubmissionId = @SubmissionId
  AND existing.TenantId = @TenantId
  AND existing.IsDeleted = 0;

UPDATE q
SET EvidenceDocumentId = primaryEvidence.DocumentId,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
FROM Submissions.SubmissionIntakeQuestion q
OUTER APPLY
(
    SELECT TOP 1 ev.DocumentId
    FROM Submissions.SubmissionReadinessEvidenceDocument ev
    INNER JOIN DMS.Document d ON d.DocumentId = ev.DocumentId AND d.IsDeleted = 0
    WHERE ev.IntakeQuestionId = q.IntakeQuestionId
      AND ev.IsDeleted = 0
    ORDER BY ev.CreatedDateUtc, d.CreatedDateUtc DESC
) primaryEvidence
WHERE q.IntakeQuestionId = @IntakeQuestionId
  AND q.SubmissionId = @SubmissionId
  AND q.TenantId = @TenantId
  AND q.IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'ReadinessEvidenceUpdated', CONCAT(N'Readiness evidence document links updated. Count: ', (SELECT COUNT(1) FROM @SelectedDocuments)), SYSUTCDATETIME(), @ModifiedByUserId, N'SubmissionIntakeQuestion', @IntakeQuestionId, N'User', 0);";
        await cn.ExecuteAsync(new CommandDefinition(upsertSql, new { SubmissionId = submissionId, IntakeQuestionId = intakeQuestionId, request.TenantId, EvidenceRoleCode = request.EvidenceRoleCode, request.Notes, request.ModifiedByUserId, DocumentIdsJson = documentIdsJson }, tx, cancellationToken: cancellationToken));
        tx.Commit();
    }

    public async Task AssignAsync(Guid id, AssignSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.Submission
SET    AssignedToUserId = @AssignedToUserId,
       ModifiedDateUtc  = GETUTCDATE()
WHERE  SubmissionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AssignedToUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SubmissionActivityDto>> GetActivitiesAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT al.ActionLogId AS ActivityId,
       al.SubmissionId,
       al.TenantId,
       al.ActionCode,
       CASE al.ActionCode
           WHEN N'Note' THEN N'Note added'
           WHEN N'SubmitToMarket' THEN N'Submitted to market'
           WHEN N'RequestQuote' THEN N'Quote requested'
           WHEN N'Decline' THEN N'Submission declined'
           WHEN N'Copy' THEN N'Submission copied'
           WHEN N'Assign' THEN N'Submission assigned'
           WHEN N'FollowUpTask' THEN N'Follow-up task created'
           WHEN N'DocumentAttached' THEN N'Document attached'
           ELSE al.ActionCode
       END AS Title,
       al.Notes,
       NULL AS CreatedByName,
       al.CreatedDateUtc
FROM Submissions.SubmissionActionLog al
WHERE al.SubmissionId = @SubmissionId
  AND al.IsDeleted = 0
ORDER BY al.CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<SubmissionActivityDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> AddNoteAsync(Guid submissionId, AddSubmissionNoteRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 52010, 'Submission was not found for note creation.', 1;

DECLARE @ActionLogId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (@ActionLogId, @SubmissionId, @TenantId, N'Note', @Notes, SYSUTCDATETIME(), 0);

UPDATE Submissions.Submission
SET ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @CreatedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

SELECT @ActionLogId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { SubmissionId = submissionId, request.TenantId, request.Notes, request.CreatedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT DocumentId, TenantId, DocumentTypeCode, CategoryCode, EntityName, EntityId, FileName, StoragePath, ContentType, FileSizeBytes, VersionNumber, StatusCode, RetentionDate, Description, Tags, UploadedByName, CreatedDateUtc, ModifiedDateUtc
FROM DMS.Document
WHERE TenantId = @TenantId
  AND EntityName = N'Submission'
  AND EntityId = @SubmissionId
  AND IsDeleted = 0
ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<DocumentDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<SubmissionTaskDto>> GetTasksAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT t.TaskItemId,
       t.TenantId,
       t.TaskNumber,
       t.Title,
       t.Description,
       t.TaskTypeCode,
       t.StageCode,
       t.PriorityCode,
       t.StatusCode,
       t.AssignedToUserId,
       COALESCE(u.FullName, u.DisplayName, u.UserName) AS AssignedToUserName,
       t.DueDate,
       t.CreatedDateUtc
FROM OPS.TaskItem t
LEFT JOIN IAM.[User] u ON u.UserId = t.AssignedToUserId
WHERE t.TenantId = @TenantId
  AND t.RelatedEntityName = N'Submission'
  AND t.RelatedEntityId = @SubmissionId
  AND t.IsDeleted = 0
ORDER BY CASE WHEN t.StatusCode IN (N'Completed', N'Closed') THEN 1 ELSE 0 END, t.DueDate ASC, t.CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<SubmissionTaskDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> CreateFollowUpTaskAsync(Guid submissionId, CreateSubmissionFollowUpTaskRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @AccountId UNIQUEIDENTIFIER;
SELECT @AccountId = AccountId
FROM Submissions.Submission
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

IF @AccountId IS NULL
    THROW 52011, 'Submission was not found for follow-up task creation.', 1;

DECLARE @TaskItemId UNIQUEIDENTIFIER = NEWID();
DECLARE @TaskNumber NVARCHAR(50) = CONCAT(N'TASK-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), @TaskItemId), N'-', N''), 6));

INSERT INTO OPS.TaskItem
    (TaskItemId, TenantId, TaskNumber, Title, Description, TaskTypeCode, StageCode, PriorityCode, StatusCode,
     RelatedEntityName, RelatedEntityId, AccountId, AssignedToUserId, DueDate, CompletedDate,
     CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES
    (@TaskItemId, @TenantId, @TaskNumber, @Title, @Description, N'FollowUp', N'Submission', @PriorityCode, N'Open',
     N'Submission', @SubmissionId, @AccountId, @AssignedToUserId, @DueDate, NULL,
     SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'FollowUpTask', CONCAT(N'Follow-up task created: ', @Title), SYSUTCDATETIME(), 0);

UPDATE Submissions.Submission
SET ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @CreatedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

SELECT @TaskItemId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
        {
            SubmissionId = submissionId,
            request.TenantId,
            request.Title,
            request.Description,
            request.PriorityCode,
            request.AssignedToUserId,
            request.DueDate,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SubmissionLineDto>> GetLinesAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT sl.SubmissionLineId,
       sl.TenantId,
       sl.SubmissionId,
       sl.OpportunityId,
       sl.OpportunityLineId,
       sl.LineOfBusiness,
       CAST(NULL AS NVARCHAR(200)) AS Carrier,
       sl.TargetPremium,
       CAST(NULL AS NVARCHAR(50)) AS Priority,
       CAST(CASE WHEN ROW_NUMBER() OVER (ORDER BY sl.TargetPremium DESC, sl.CreatedDateUtc) = 1 THEN 1 ELSE 0 END AS bit) AS IsPrimary,
       s.EffectiveDate AS TargetEffectiveDate
FROM Submissions.SubmissionLine sl
JOIN Submissions.Submission s ON s.SubmissionId = sl.SubmissionId
WHERE sl.SubmissionId = @SubmissionId AND sl.IsDeleted = 0
ORDER BY IsPrimary DESC, sl.LineOfBusiness;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<SubmissionLineDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<SubmissionIntakeQuestionDto>> GetIntakeAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var submission = await GetByIdAsync(submissionId, cancellationToken) ?? throw new InvalidOperationException("Submission was not found for intake.");
        await EnsureDefaultIntakeAsync(submissionId, submission.TenantId, cancellationToken);

        const string sql = @"
SELECT q.IntakeQuestionId,
       q.ReadinessRequirementId,
       q.SubmissionMarketId,
       q.CarrierId,
       q.SubmissionId,
       q.TenantId,
       q.QuestionCode,
       COALESCE(r.RequirementTypeCode, N'IntakeConfirmation') AS RequirementTypeCode,
       COALESCE(q.ScopeCode, r.ScopeCode, N'Submission') AS ScopeCode,
       q.QuestionText,
       COALESCE(q.HelpText, N'') AS HelpText,
       q.IsRequired,
       COALESCE(q.BlocksSubmit, r.BlocksSubmit, CAST(1 AS bit)) AS BlocksSubmit,
       COALESCE(r.AllowsWaiver, CAST(1 AS bit)) AS AllowsWaiver,
       COALESCE(r.RequiresEvidence, CAST(0 AS bit)) AS RequiresEvidence,
       r.EvidencePrompt,
       r.ApprovalRoleCode,
       q.AnswerText,
       q.IsAnswered,
       COALESCE(q.StatusCode, CASE WHEN q.IsAnswered = 1 THEN N'Confirmed' ELSE N'NeedsReview' END) AS StatusCode,
       q.StatusReason,
       q.EvidenceDocumentId,
       q.WaiverReason,
       q.WaivedByUserId,
       q.WaivedDateUtc,
       q.CompletedByUserId,
       q.CompletedDateUtc,
       q.ReviewDueDateUtc,
       q.ScoreWeight,
       q.SortOrder,
       q.AnsweredByUserId,
       q.AnsweredDateUtc
FROM Submissions.SubmissionIntakeQuestion q
LEFT JOIN Submissions.SubmissionReadinessRequirement r ON r.ReadinessRequirementId = q.ReadinessRequirementId AND r.IsDeleted = 0
WHERE q.SubmissionId = @SubmissionId
  AND q.SubmissionMarketId IS NULL
  AND COALESCE(r.ScopeCode, q.ScopeCode, N'Submission') = N'Submission'
  AND q.IsDeleted = 0
ORDER BY q.IsRequired DESC, q.SortOrder, q.QuestionText;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var intake = (await cn.QueryAsync<SubmissionIntakeQuestionDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
        await AttachEvidenceDocumentsAsync(cn, intake, cancellationToken);
        return intake;
    }

    public async Task UpdateIntakeQuestionAsync(Guid submissionId, Guid intakeQuestionId, UpdateSubmissionIntakeQuestionRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
DECLARE @EffectiveStatusCode NVARCHAR(50) = COALESCE(NULLIF(@StatusCode, N''), CASE WHEN @IsAnswered = 1 THEN N'Confirmed' ELSE N'NeedsReview' END);

UPDATE Submissions.SubmissionIntakeQuestion
SET AnswerText = @AnswerText,
    IsAnswered = @IsAnswered,
    StatusCode = @EffectiveStatusCode,
    StatusReason = @StatusReason,
    EvidenceDocumentId = @EvidenceDocumentId,
    WaiverReason = CASE WHEN @EffectiveStatusCode = N'Waived' THEN @WaiverReason ELSE NULL END,
    WaivedByUserId = CASE WHEN @EffectiveStatusCode = N'Waived' THEN @AnsweredByUserId ELSE NULL END,
    WaivedDateUtc = CASE WHEN @EffectiveStatusCode = N'Waived' THEN SYSUTCDATETIME() ELSE NULL END,
    CompletedByUserId = CASE WHEN @EffectiveStatusCode IN (N'Confirmed', N'Waived') THEN @AnsweredByUserId ELSE NULL END,
    CompletedDateUtc = CASE WHEN @EffectiveStatusCode IN (N'Confirmed', N'Waived') THEN SYSUTCDATETIME() ELSE NULL END,
    ReviewDueDateUtc = @ReviewDueDateUtc,
    AnsweredByUserId = @AnsweredByUserId,
    AnsweredDateUtc = CASE WHEN @IsAnswered = 1 THEN SYSUTCDATETIME() ELSE NULL END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @AnsweredByUserId
WHERE IntakeQuestionId = @IntakeQuestionId AND SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

IF @@ROWCOUNT = 0 THROW 52012, 'Submission intake question was not found.', 1;

IF @EvidenceDocumentId IS NOT NULL
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM DMS.Document d
        WHERE d.DocumentId = @EvidenceDocumentId
          AND d.TenantId = @TenantId
          AND d.EntityName = N'Submission'
          AND d.EntityId = @SubmissionId
          AND d.IsDeleted = 0
    )
        THROW 52014, 'Selected evidence document is not attached to this submission.', 1;

    INSERT INTO Submissions.SubmissionReadinessEvidenceDocument
        (SubmissionReadinessEvidenceDocumentId, TenantId, SubmissionId, IntakeQuestionId, ReadinessRequirementId, SubmissionMarketId, CarrierId, DocumentId, EvidenceRoleCode, Notes, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT NEWID(), q.TenantId, q.SubmissionId, q.IntakeQuestionId, q.ReadinessRequirementId, q.SubmissionMarketId, q.CarrierId, @EvidenceDocumentId, N'SupportingEvidence', N'Synced from readiness update.', SYSUTCDATETIME(), @AnsweredByUserId, 0
    FROM Submissions.SubmissionIntakeQuestion q
    WHERE q.IntakeQuestionId = @IntakeQuestionId
      AND q.SubmissionId = @SubmissionId
      AND q.TenantId = @TenantId
      AND q.IsDeleted = 0
      AND NOT EXISTS
      (
          SELECT 1
          FROM Submissions.SubmissionReadinessEvidenceDocument existing
          WHERE existing.IntakeQuestionId = q.IntakeQuestionId
            AND existing.DocumentId = @EvidenceDocumentId
            AND existing.IsDeleted = 0
      );
END;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'ReadinessUpdated', CONCAT(N'Underwriting readiness item updated to ', @EffectiveStatusCode, N'.'), SYSUTCDATETIME(), @AnsweredByUserId, N'SubmissionIntakeQuestion', @IntakeQuestionId, N'User', 0);";
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            SubmissionId = submissionId,
            IntakeQuestionId = intakeQuestionId,
            request.TenantId,
            request.AnswerText,
            request.IsAnswered,
            request.AnsweredByUserId,
            request.StatusCode,
            request.StatusReason,
            request.EvidenceDocumentId,
            request.WaiverReason,
            request.ReviewDueDateUtc
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SubmissionDocumentChecklistDto>> GetDocumentChecklistAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, tenantId, cancellationToken);
        const string sql = @"
DECLARE @LineOfBusiness NVARCHAR(100) = (SELECT TOP 1 LineOfBusiness FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0);

SELECT r.DocumentRequirementId AS ChecklistItemId,
       @SubmissionId AS SubmissionId,
       r.TenantId,
       r.CategoryCode,
       r.DisplayName,
       r.IsRequired,
       CAST(CASE WHEN d.DocumentId IS NULL THEN 0 ELSE 1 END AS bit) AS IsSatisfied,
       d.DocumentId,
       d.FileName,
       d.CreatedDateUtc AS UploadedDateUtc
FROM Submissions.SubmissionDocumentRequirement r
OUTER APPLY (
    SELECT TOP 1 DocumentId, FileName, CreatedDateUtc
    FROM DMS.Document d
    WHERE d.TenantId = @TenantId
      AND d.EntityName = N'Submission'
      AND d.EntityId = @SubmissionId
      AND d.IsDeleted = 0
      AND (d.CategoryCode = r.CategoryCode OR d.DocumentTypeCode = r.CategoryCode OR d.Tags LIKE N'%' + r.DisplayName + N'%')
    ORDER BY d.CreatedDateUtc DESC
) d
WHERE r.TenantId = @TenantId
  AND r.IsDeleted = 0
  AND r.LineOfBusiness = COALESCE(@LineOfBusiness, r.LineOfBusiness)
ORDER BY r.SortOrder, r.DisplayName;";
        return (await cn.QueryAsync<SubmissionDocumentChecklistDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<SubmissionReadinessDto> GetReadinessAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var intake = await GetIntakeAsync(submissionId, cancellationToken);
        var checklist = await GetDocumentChecklistAsync(submissionId, tenantId, cancellationToken);
        static bool IsSatisfied(SubmissionIntakeQuestionDto question) => question.StatusCode.Equals("Waived", StringComparison.OrdinalIgnoreCase)
            || ((question.IsAnswered || question.StatusCode.Equals("Confirmed", StringComparison.OrdinalIgnoreCase)) && (!question.RequiresEvidence || question.EvidenceDocuments.Count > 0));

        var blockingQuestions = intake.Where(q => q.IsRequired && q.BlocksSubmit).ToArray();
        var requiredQuestionWeight = blockingQuestions.Sum(q => Math.Max(q.ScoreWeight, 1));
        var satisfiedQuestionWeight = blockingQuestions.Where(IsSatisfied).Sum(q => Math.Max(q.ScoreWeight, 1));
        var requiredDocumentWeight = checklist.Count(d => d.IsRequired);
        var satisfiedDocumentWeight = checklist.Count(d => d.IsRequired && d.IsSatisfied);
        var totalWeight = requiredQuestionWeight + requiredDocumentWeight;
        var completedWeight = satisfiedQuestionWeight + satisfiedDocumentWeight;
        var blockingReasons = blockingQuestions.Where(q => !IsSatisfied(q)).Select(q => q.RequiresEvidence && q.EvidenceDocuments.Count == 0 && (q.IsAnswered || q.StatusCode.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
                ? $"Missing evidence: {q.QuestionText}"
                : $"Missing intake: {q.QuestionText}")
            .Concat(checklist.Where(d => d.IsRequired && !d.IsSatisfied).Select(d => $"Missing document: {d.DisplayName}"))
            .ToArray();

        return new SubmissionReadinessDto
        {
            SubmissionId = submissionId,
            ReadinessScore = totalWeight == 0 ? 100 : (int)Math.Round((double)completedWeight / totalWeight * 100),
            RequiredQuestionCount = blockingQuestions.Length,
            AnsweredRequiredQuestionCount = blockingQuestions.Count(IsSatisfied),
            WaivedRequiredQuestionCount = blockingQuestions.Count(q => q.StatusCode.Equals("Waived", StringComparison.OrdinalIgnoreCase)),
            RequiredQuestionScoreWeight = requiredQuestionWeight,
            SatisfiedQuestionScoreWeight = satisfiedQuestionWeight,
            RequiredDocumentCount = checklist.Count(d => d.IsRequired),
            SatisfiedRequiredDocumentCount = checklist.Count(d => d.IsRequired && d.IsSatisfied),
            IsReadyForMarketing = blockingReasons.Length == 0,
            BlockingReasons = blockingReasons
        };
    }

    public async Task<SubmissionReadinessDto> GetMarketReadinessAsync(Guid submissionId, Guid submissionMarketId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsureMarketReadinessAsync(submissionId, submissionMarketId, tenantId, cancellationToken);
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = @"
SELECT q.IntakeQuestionId, q.ReadinessRequirementId, q.SubmissionMarketId, q.CarrierId, q.SubmissionId, q.TenantId, q.QuestionCode,
       COALESCE(r.RequirementTypeCode, N'IntakeConfirmation') AS RequirementTypeCode,
       COALESCE(q.ScopeCode, r.ScopeCode, N'Submission') AS ScopeCode,
       q.QuestionText, COALESCE(q.HelpText, N'') AS HelpText, q.IsRequired,
       COALESCE(q.BlocksSubmit, r.BlocksSubmit, CAST(1 AS bit)) AS BlocksSubmit,
       COALESCE(r.AllowsWaiver, CAST(1 AS bit)) AS AllowsWaiver,
       COALESCE(r.RequiresEvidence, CAST(0 AS bit)) AS RequiresEvidence,
       r.EvidencePrompt, r.ApprovalRoleCode, q.AnswerText, q.IsAnswered,
       COALESCE(q.StatusCode, CASE WHEN q.IsAnswered = 1 THEN N'Confirmed' ELSE N'NeedsReview' END) AS StatusCode,
       q.StatusReason, q.EvidenceDocumentId, q.WaiverReason, q.WaivedByUserId, q.WaivedDateUtc,
       q.CompletedByUserId, q.CompletedDateUtc, q.ReviewDueDateUtc, q.ScoreWeight, q.SortOrder, q.AnsweredByUserId, q.AnsweredDateUtc
FROM Submissions.SubmissionIntakeQuestion q
LEFT JOIN Submissions.SubmissionReadinessRequirement r ON r.ReadinessRequirementId = q.ReadinessRequirementId AND r.IsDeleted = 0
WHERE q.SubmissionId = @SubmissionId
  AND (q.SubmissionMarketId = @SubmissionMarketId OR (q.SubmissionMarketId IS NULL AND COALESCE(q.ScopeCode, N'Submission') = N'Submission'))
  AND q.IsDeleted = 0
ORDER BY q.IsRequired DESC, q.ScopeCode, q.SortOrder, q.QuestionText;

SELECT sm.CarrierId, c.CarrierName
FROM Submissions.SubmissionMarket sm
LEFT JOIN Core.Carrier c ON c.CarrierId = sm.CarrierId AND c.IsDeleted = 0
WHERE sm.SubmissionMarketId = @SubmissionMarketId AND sm.SubmissionId = @SubmissionId AND sm.IsDeleted = 0;";
        using var grid = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { SubmissionId = submissionId, SubmissionMarketId = submissionMarketId }, cancellationToken: cancellationToken));
        var intake = (await grid.ReadAsync<SubmissionIntakeQuestionDto>()).AsList();
        await AttachEvidenceDocumentsAsync(cn, intake, cancellationToken);
        var market = await grid.ReadFirstOrDefaultAsync<(Guid CarrierId, string CarrierName)>();
        var checklist = await GetDocumentChecklistAsync(submissionId, tenantId, cancellationToken);
        static bool IsSatisfied(SubmissionIntakeQuestionDto question) => question.StatusCode.Equals("Waived", StringComparison.OrdinalIgnoreCase)
            || ((question.IsAnswered || question.StatusCode.Equals("Confirmed", StringComparison.OrdinalIgnoreCase)) && (!question.RequiresEvidence || question.EvidenceDocuments.Count > 0));
        var blockingQuestions = intake.Where(q => q.IsRequired && q.BlocksSubmit).ToArray();
        var requiredQuestionWeight = blockingQuestions.Sum(q => Math.Max(q.ScoreWeight, 1));
        var satisfiedQuestionWeight = blockingQuestions.Where(IsSatisfied).Sum(q => Math.Max(q.ScoreWeight, 1));
        var requiredDocumentWeight = checklist.Count(d => d.IsRequired);
        var satisfiedDocumentWeight = checklist.Count(d => d.IsRequired && d.IsSatisfied);
        var totalWeight = requiredQuestionWeight + requiredDocumentWeight;
        var completedWeight = satisfiedQuestionWeight + satisfiedDocumentWeight;
        var blockingReasons = blockingQuestions.Where(q => !IsSatisfied(q)).Select(q => q.RequiresEvidence && q.EvidenceDocuments.Count == 0 && (q.IsAnswered || q.StatusCode.Equals("Confirmed", StringComparison.OrdinalIgnoreCase))
                ? $"Missing evidence: {q.QuestionText}"
                : $"Missing readiness: {q.QuestionText}")
            .Concat(checklist.Where(d => d.IsRequired && !d.IsSatisfied).Select(d => $"Missing document: {d.DisplayName}"))
            .ToArray();

        return new SubmissionReadinessDto
        {
            SubmissionId = submissionId,
            SubmissionMarketId = submissionMarketId,
            CarrierId = market.CarrierId == Guid.Empty ? null : market.CarrierId,
            CarrierName = market.CarrierName,
            ReadinessScore = totalWeight == 0 ? 100 : (int)Math.Round((double)completedWeight / totalWeight * 100),
            RequiredQuestionCount = blockingQuestions.Length,
            AnsweredRequiredQuestionCount = blockingQuestions.Count(IsSatisfied),
            WaivedRequiredQuestionCount = blockingQuestions.Count(q => q.StatusCode.Equals("Waived", StringComparison.OrdinalIgnoreCase)),
            RequiredQuestionScoreWeight = requiredQuestionWeight,
            SatisfiedQuestionScoreWeight = satisfiedQuestionWeight,
            RequiredDocumentCount = checklist.Count(d => d.IsRequired),
            SatisfiedRequiredDocumentCount = checklist.Count(d => d.IsRequired && d.IsSatisfied),
            IsReadyForMarketing = blockingReasons.Length == 0,
            BlockingReasons = blockingReasons
        };
    }

    public async Task<SubmissionPackagePreviewDto> GetSubmissionPackagePreviewAsync(Guid submissionId, Guid? submissionMarketId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (submissionMarketId.HasValue)
        {
            await EnsureMarketReadinessAsync(submissionId, submissionMarketId.Value, tenantId, cancellationToken);
        }

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string headerSql = @"
SELECT s.SubmissionId, @SubmissionMarketId AS SubmissionMarketId, sm.CarrierId, c.CarrierName,
       COALESCE(NULLIF(sm.SubmissionMethodCode, N''), latest.ChannelCode, N'Manual') AS ChannelCode,
       CASE COALESCE(NULLIF(sm.SubmissionMethodCode, N''), latest.ChannelCode, N'Manual')
           WHEN N'API' THEN N'Carrier connector/API transmission'
           WHEN N'Email' THEN N'Email package workflow'
           WHEN N'Portal' THEN N'Carrier portal/manual upload workflow'
           ELSE N'Manual package tracking'
       END AS ChannelDescription,
       COALESCE(latest.StatusCode, CASE WHEN sm.SubmittedDateUtc IS NULL THEN N'Not queued' ELSE N'Submitted' END, N'Not queued') AS PackageStatus,
       s.SubmissionNumber, s.AccountName, s.LineOfBusiness, sm.RequestedCoverageSummary, sm.RequestedLimits, sm.RequestedPremium
FROM Submissions.Submission s
LEFT JOIN Submissions.SubmissionMarket sm ON sm.SubmissionMarketId = @SubmissionMarketId AND sm.SubmissionId = s.SubmissionId AND sm.IsDeleted = 0
LEFT JOIN Core.Carrier c ON c.CarrierId = sm.CarrierId AND c.IsDeleted = 0
OUTER APPLY (
    SELECT TOP 1 t.StatusCode, t.ChannelCode
    FROM Submissions.CarrierTransmission t
    WHERE t.SubmissionId = s.SubmissionId
      AND (@SubmissionMarketId IS NULL OR t.SubmissionMarketId = @SubmissionMarketId)
      AND t.IsDeleted = 0
    ORDER BY t.CreatedDateUtc DESC
) latest
WHERE s.SubmissionId = @SubmissionId AND s.TenantId = @TenantId AND s.IsDeleted = 0;";
        var preview = await cn.QueryFirstAsync<SubmissionPackagePreviewDto>(new CommandDefinition(headerSql, new { SubmissionId = submissionId, SubmissionMarketId = submissionMarketId, TenantId = tenantId }, cancellationToken: cancellationToken));

        preview.Lines = await GetPreviewLinesAsync(cn, submissionId, submissionMarketId, cancellationToken);
        preview.Documents = await GetPreviewDocumentsAsync(cn, submissionId, tenantId, cancellationToken);
        preview.ReadinessItems = await GetPreviewReadinessAsync(cn, submissionId, submissionMarketId, cancellationToken);
        preview.Transmissions = submissionMarketId.HasValue ? (await GetMarketsAsync(submissionId, cancellationToken)).FirstOrDefault(m => m.SubmissionMarketId == submissionMarketId.Value)?.Transmissions ?? [] : [];
        return preview;
    }

    private static async Task<IReadOnlyList<SubmissionPackagePreviewLineDto>> GetPreviewLinesAsync(System.Data.IDbConnection cn, Guid submissionId, Guid? submissionMarketId, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT sl.SubmissionLineId, sl.LineOfBusiness, sl.TargetPremium
FROM Submissions.SubmissionLine sl
WHERE sl.SubmissionId = @SubmissionId
  AND sl.IsDeleted = 0
  AND (@SubmissionMarketId IS NULL OR EXISTS (SELECT 1 FROM Submissions.SubmissionMarketLine ml WHERE ml.SubmissionMarketId = @SubmissionMarketId AND ml.SubmissionLineId = sl.SubmissionLineId AND ml.IsDeleted = 0))
ORDER BY sl.LineOfBusiness;";
        return (await cn.QueryAsync<SubmissionPackagePreviewLineDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, SubmissionMarketId = submissionMarketId }, cancellationToken: cancellationToken))).AsList();
    }

    private static async Task<IReadOnlyList<SubmissionPackagePreviewDocumentDto>> GetPreviewDocumentsAsync(System.Data.IDbConnection cn, Guid submissionId, Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT d.DocumentId, d.FileName, d.CategoryCode, d.DocumentTypeCode, d.CreatedDateUtc
FROM DMS.Document d
WHERE d.TenantId = @TenantId
  AND d.EntityName = N'Submission'
  AND d.EntityId = @SubmissionId
  AND d.IsDeleted = 0
ORDER BY d.CreatedDateUtc DESC;";
        return (await cn.QueryAsync<SubmissionPackagePreviewDocumentDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    private static async Task AttachEvidenceDocumentsAsync(System.Data.IDbConnection cn, IReadOnlyList<SubmissionIntakeQuestionDto> intake, CancellationToken cancellationToken)
    {
        if (intake.Count == 0)
        {
            return;
        }

        var submissionId = intake[0].SubmissionId;
        var tenantId = intake[0].TenantId;
        var evidence = await GetEvidenceDocumentsAsync(cn, submissionId, null, tenantId, cancellationToken);
        var byQuestion = evidence.GroupBy(e => e.IntakeQuestionId).ToDictionary(g => g.Key, g => (IReadOnlyList<SubmissionReadinessEvidenceDocumentDto>)g.ToList());
        foreach (var question in intake)
        {
            question.EvidenceDocuments = byQuestion.TryGetValue(question.IntakeQuestionId, out var documents) ? documents : [];
            question.EvidenceDocumentId ??= question.EvidenceDocuments.FirstOrDefault()?.DocumentId;
        }
    }

    private static async Task<IReadOnlyList<SubmissionReadinessEvidenceDocumentDto>> GetEvidenceDocumentsAsync(System.Data.IDbConnection cn, Guid submissionId, Guid? intakeQuestionId, Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT ev.SubmissionReadinessEvidenceDocumentId, ev.TenantId, ev.SubmissionId, ev.IntakeQuestionId, ev.ReadinessRequirementId,
       ev.SubmissionMarketId, ev.CarrierId, ev.DocumentId, ev.EvidenceRoleCode, ev.Notes,
       d.FileName, d.CategoryCode, d.DocumentTypeCode, d.ContentType, d.FileSizeBytes, d.CreatedDateUtc AS DocumentCreatedDateUtc,
       ev.CreatedDateUtc
FROM Submissions.SubmissionReadinessEvidenceDocument ev
INNER JOIN DMS.Document d ON d.DocumentId = ev.DocumentId AND d.TenantId = ev.TenantId AND d.EntityName = N'Submission' AND d.EntityId = ev.SubmissionId AND d.IsDeleted = 0
WHERE ev.SubmissionId = @SubmissionId
  AND ev.TenantId = @TenantId
  AND (@IntakeQuestionId IS NULL OR ev.IntakeQuestionId = @IntakeQuestionId)
  AND ev.IsDeleted = 0
ORDER BY ev.IntakeQuestionId, ev.CreatedDateUtc, d.CreatedDateUtc DESC;";
        return (await cn.QueryAsync<SubmissionReadinessEvidenceDocumentDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, IntakeQuestionId = intakeQuestionId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    private static async Task<IReadOnlyList<SubmissionPackagePreviewReadinessDto>> GetPreviewReadinessAsync(System.Data.IDbConnection cn, Guid submissionId, Guid? submissionMarketId, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT q.IntakeQuestionId, q.QuestionCode AS RequirementCode, q.QuestionText AS DisplayName, COALESCE(q.StatusCode, CASE WHEN q.IsAnswered = 1 THEN N'Confirmed' ELSE N'NeedsReview' END) AS StatusCode,
       q.IsRequired, q.BlocksSubmit, COALESCE(r.RequiresEvidence, CAST(0 AS bit)) AS RequiresEvidence, q.EvidenceDocumentId, d.FileName AS EvidenceFileName, d.CategoryCode AS EvidenceCategoryCode, d.DocumentTypeCode AS EvidenceDocumentTypeCode
FROM Submissions.SubmissionIntakeQuestion q
LEFT JOIN Submissions.SubmissionReadinessRequirement r ON r.ReadinessRequirementId = q.ReadinessRequirementId AND r.IsDeleted = 0
LEFT JOIN DMS.Document d ON d.DocumentId = q.EvidenceDocumentId AND d.TenantId = q.TenantId AND d.EntityName = N'Submission' AND d.EntityId = q.SubmissionId AND d.IsDeleted = 0
WHERE q.SubmissionId = @SubmissionId
  AND (q.SubmissionMarketId IS NULL OR q.SubmissionMarketId = @SubmissionMarketId)
  AND q.IsDeleted = 0
ORDER BY q.IsRequired DESC, q.SortOrder, q.QuestionText;";
        var readiness = (await cn.QueryAsync<SubmissionPackagePreviewReadinessDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, SubmissionMarketId = submissionMarketId }, cancellationToken: cancellationToken))).AsList();
        if (readiness.Count == 0)
        {
            return readiness;
        }

        var tenantId = (await cn.QueryFirstAsync<Guid>(new CommandDefinition("SELECT TOP 1 TenantId FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND IsDeleted = 0", new { SubmissionId = submissionId }, cancellationToken: cancellationToken)));
        var evidence = await GetEvidenceDocumentsAsync(cn, submissionId, null, tenantId, cancellationToken);
        var byQuestion = evidence.GroupBy(e => e.IntakeQuestionId).ToDictionary(g => g.Key, g => (IReadOnlyList<SubmissionReadinessEvidenceDocumentDto>)g.ToList());
        foreach (var item in readiness)
        {
            item.EvidenceDocuments = byQuestion.TryGetValue(item.IntakeQuestionId, out var documents) ? documents : [];
            item.EvidenceDocumentId ??= item.EvidenceDocuments.FirstOrDefault()?.DocumentId;
            item.EvidenceFileName ??= item.EvidenceDocuments.FirstOrDefault()?.FileName;
            item.EvidenceCategoryCode ??= item.EvidenceDocuments.FirstOrDefault()?.CategoryCode;
            item.EvidenceDocumentTypeCode ??= item.EvidenceDocuments.FirstOrDefault()?.DocumentTypeCode;
        }

        return readiness;
    }

    public async Task<IReadOnlyList<SubmissionTaskTemplateDto>> GetTaskTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string seedSql = @"
DECLARE @Templates TABLE (TaskTypeCode NVARCHAR(50), TaskTypeName NVARCHAR(100), Description NVARCHAR(500), SortOrder INT);
INSERT INTO @Templates VALUES
(N'MissingInformation', N'Missing information', N'Collect missing intake or underwriting details.', 110),
(N'CarrierFollowUp', N'Carrier follow-up', N'Follow up with carrier for response or terms.', 120),
(N'QuoteReview', N'Quote review', N'Review received quote terms and compare options.', 130),
(N'ProposalFollowUp', N'Proposal follow-up', N'Follow up with client on delivered proposal.', 140),
(N'BindRequest', N'Bind request', N'Coordinate binding request and subjectivities.', 150),
(N'SubjectivitiesFollowUp', N'Subjectivities follow-up', N'Collect and clear quote subjectivities.', 160),
(N'DocumentCollection', N'Document collection', N'Collect required submission or post-bind documents.', 170);

INSERT INTO OPS.TaskType (TaskTypeId, TenantId, TaskTypeCode, TaskTypeName, Description, SortOrder, IsActive, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @TenantId, t.TaskTypeCode, t.TaskTypeName, t.Description, t.SortOrder, 1, SYSUTCDATETIME(), 0
FROM @Templates t
WHERE NOT EXISTS (SELECT 1 FROM OPS.TaskType x WHERE x.TenantId = @TenantId AND x.TaskTypeCode = t.TaskTypeCode AND x.IsDeleted = 0);

SELECT TaskTypeCode, TaskTypeName AS DisplayName, COALESCE(Description, N'') AS Description,
       CASE WHEN TaskTypeCode IN (N'BindRequest', N'QuoteReview') THEN N'High' ELSE N'Medium' END AS PriorityCode,
       CASE WHEN TaskTypeCode IN (N'CarrierFollowUp', N'ProposalFollowUp') THEN 3 WHEN TaskTypeCode = N'BindRequest' THEN 1 ELSE 5 END AS DefaultDueDays
FROM OPS.TaskType
WHERE TenantId = @TenantId AND IsDeleted = 0 AND TaskTypeCode IN (N'MissingInformation', N'CarrierFollowUp', N'QuoteReview', N'ProposalFollowUp', N'BindRequest', N'SubjectivitiesFollowUp', N'DocumentCollection')
ORDER BY SortOrder, TaskTypeName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<SubmissionTaskTemplateDto>(new CommandDefinition(seedSql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<SubmissionMetricsDto> GetMetricsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, tenantId, cancellationToken);
        const string sql = @"
SELECT COUNT(DISTINCT s.SubmissionId)
FROM Submissions.Submission s
LEFT JOIN Submissions.SubmissionIntakeQuestion q ON q.SubmissionId = s.SubmissionId AND q.IsDeleted = 0 AND q.IsRequired = 1 AND q.IsAnswered = 0
WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.Status NOT IN (N'Bound', N'Lost', N'Cancelled', N'Closed') AND (s.Status IN (N'Draft', N'In Progress') OR q.IntakeQuestionId IS NOT NULL);

SELECT COUNT(1)
FROM Submissions.Submission s
WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.Status IN (N'Ready', N'Ready for Market');

SELECT COUNT(1)
FROM Submissions.SubmissionMarket sm
JOIN Submissions.Submission s ON s.SubmissionId = sm.SubmissionId
WHERE s.TenantId = @TenantId AND sm.IsDeleted = 0 AND sm.Status IN (N'Sent', N'Submitted', N'In Review');

SELECT COUNT(1)
FROM Submissions.Quote q
JOIN Submissions.Submission s ON s.SubmissionId = q.SubmissionId
WHERE s.TenantId = @TenantId AND q.IsDeleted = 0 AND q.Status IN (N'Received', N'Presented', N'Accepted') AND q.ExpiresDateUtc BETWEEN SYSUTCDATETIME() AND DATEADD(day, 14, SYSUTCDATETIME());

SELECT COUNT(1)
FROM Submissions.Proposal p
WHERE p.TenantId = @TenantId AND p.IsDeleted = 0 AND p.Status IN (N'Sent', N'Delivered', N'Pending Decision');

SELECT COUNT(1)
FROM OPS.TaskItem
WHERE TenantId = @TenantId AND IsDeleted = 0 AND TaskTypeCode = N'BindRequest' AND StatusCode NOT IN (N'Completed', N'Closed', N'Done');";

        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return new SubmissionMetricsDto
        {
            PendingIntake = await multi.ReadSingleAsync<int>(),
            ReadyForMarket = await multi.ReadSingleAsync<int>(),
            MarketsAwaitingResponse = await multi.ReadSingleAsync<int>(),
            QuotesExpiringSoon = await multi.ReadSingleAsync<int>(),
            ProposalsPendingDecision = await multi.ReadSingleAsync<int>(),
            BindRequestsPending = await multi.ReadSingleAsync<int>()
        };
    }

    public async Task<IReadOnlyList<PolicyCreationSourceDto>> GetPolicyCreationSourcesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF OBJECT_ID(N'Submissions.PolicyCreationSource', N'U') IS NULL
BEGIN
    SELECT CAST(N'00000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER) AS PolicyCreationSourceId,
           @TenantId AS TenantId,
           N'QuoteBound' AS SourceCode,
           N'Quote Bound' AS SourceName,
           N'Policy is created from an accepted or selected quote.' AS Description,
           CAST(1 AS bit) AS RequiresQuote,
           CAST(1 AS bit) AS RequiresSubmission,
           CAST(1 AS bit) AS RequiresAccount,
           CAST(0 AS bit) AS RequiresReason,
           CAST(0 AS bit) AS RequiresPolicyNumber,
           CAST(0 AS bit) AS AllowsDirectPolicyEntry,
           CAST(0 AS bit) AS IsImportSource,
           CAST(0 AS bit) AS IsConversionSource,
           CAST(1 AS bit) AS IsDefault,
           CAST(1 AS bit) AS IsActive,
           10 AS SortOrder
    UNION ALL
    SELECT CAST(N'00000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER),
           @TenantId,
           N'AlreadyBound',
           N'Already Bound Outside System',
           N'Carrier or broker already bound coverage outside the platform.',
           CAST(0 AS bit),
           CAST(0 AS bit),
           CAST(1 AS bit),
           CAST(1 AS bit),
           CAST(1 AS bit),
           CAST(1 AS bit),
           CAST(0 AS bit),
           CAST(0 AS bit),
           CAST(0 AS bit),
           CAST(1 AS bit),
           20;
    RETURN;
END;

SELECT PolicyCreationSourceId,
       TenantId,
       SourceCode,
       SourceName,
       Description,
       RequiresQuote,
       RequiresSubmission,
       RequiresAccount,
       RequiresReason,
       RequiresPolicyNumber,
       AllowsDirectPolicyEntry,
       IsImportSource,
       IsConversionSource,
       IsDefault,
       IsActive,
       SortOrder
FROM Submissions.PolicyCreationSource
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND IsActive = 1
ORDER BY SortOrder, SourceName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<PolicyCreationSourceDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<PolicyBindStatusDto>> GetPolicyBindStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF OBJECT_ID(N'Submissions.PolicyBindStatus', N'U') IS NULL
BEGIN
    SELECT CAST(N'00000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER) AS PolicyBindStatusId,
           @TenantId AS TenantId,
           N'Bound' AS StatusCode,
           N'Bound' AS StatusName,
           N'Bind transaction created the policy and completed the bind workflow.' AS Description,
           CAST(1 AS bit) AS IsTerminal,
           CAST(1 AS bit) AS CreatesPolicy,
           CAST(0 AS bit) AS IsDefault,
           CAST(1 AS bit) AS IsActive,
           40 AS SortOrder;
    RETURN;
END;

SELECT PolicyBindStatusId,
       TenantId,
       StatusCode,
       StatusName,
       Description,
       IsTerminal,
       CreatesPolicy,
       IsDefault,
       IsActive,
       SortOrder
FROM Submissions.PolicyBindStatus
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND IsActive = 1
ORDER BY SortOrder, StatusName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<PolicyBindStatusDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<BindQueueItemDto>> GetBindQueueAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT q.QuoteId,
       s.SubmissionId,
       s.TenantId,
       s.AccountId,
       a.AccountName,
       s.SubmissionNumber,
       s.LineOfBusiness,
       COALESCE(u.FullName, u.DisplayName, u.UserName, N'') AS ProducerName,
       CAST(s.EffectiveDate AS DATETIME2) AS EffectiveDate,
       CAST(s.ExpirationDate AS DATETIME2) AS ExpirationDate,
       q.CarrierId,
       c.CarrierName,
       q.QuoteNumber,
       q.Status AS QuoteStatus,
       q.AnnualPremium,
       q.Deductible,
       q.[Limit],
       q.CommissionPercent,
       q.Subjectivities,
       q.Exclusions,
       q.CarrierRating,
       q.PaymentTerms,
       q.MinimumEarnedPremium,
       q.TaxesAndFees,
       q.BrokerFee,
       q.TriaIncluded,
       q.QuoteDocumentId,
       q.CoverageNotes,
       q.IsSelected,
       q.IsRecommended,
       q.RecommendationScore,
       q.RecommendationReason,
       q.ExpiresDateUtc AS QuoteExpiresDateUtc,
       latest.PolicyBindTransactionId,
       latest.PolicyId,
       COALESCE(bp.PolicyNumber, latest.PolicyNumber) AS PolicyNumber,
       latest.BindStatusCode,
       COALESCE(pbs.StatusName, latest.BindStatusCode) AS BindStatusName,
       latest.BindReason,
       latest.Notes AS BindNotes,
       latest.CreatedDateUtc AS BindCreatedDateUtc
FROM Submissions.Submission s
INNER JOIN Client.Account a ON a.AccountId = s.AccountId
INNER JOIN Submissions.Quote q ON q.SubmissionId = s.SubmissionId AND q.IsDeleted = 0
INNER JOIN Core.Carrier c ON c.CarrierId = q.CarrierId
LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
OUTER APPLY
(
    SELECT TOP 1 pbt.PolicyBindTransactionId,
           pbt.PolicyId,
           pbt.PolicyNumber,
           pbt.BindStatusCode,
           pbt.BindReason,
           pbt.Notes,
           pbt.CreatedDateUtc
    FROM Submissions.PolicyBindTransaction pbt
    WHERE pbt.TenantId = s.TenantId
      AND pbt.SubmissionId = s.SubmissionId
      AND pbt.QuoteId = q.QuoteId
      AND pbt.IsDeleted = 0
    ORDER BY pbt.CreatedDateUtc DESC, pbt.PolicyBindTransactionId DESC
) latest
LEFT JOIN Submissions.BoundPolicy bp ON bp.PolicyId = latest.PolicyId AND bp.IsDeleted = 0
LEFT JOIN Submissions.PolicyBindStatus pbs ON pbs.TenantId = s.TenantId AND pbs.StatusCode = latest.BindStatusCode AND pbs.IsDeleted = 0
WHERE s.TenantId = @TenantId
  AND s.IsDeleted = 0
  AND (q.IsSelected = 1 OR q.IsRecommended = 1 OR q.AnnualPremium > 0 OR NULLIF(q.Status, N'') IS NOT NULL)
ORDER BY COALESCE(latest.CreatedDateUtc, q.QuoteReceivedDateUtc, q.CreatedDateUtc) DESC,
         s.SubmissionNumber,
         q.IsSelected DESC,
         q.RecommendationScore DESC;
""";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsurePolicyBindTransactionSchemaAsync(cn, tenantId, cancellationToken);
        return (await cn.QueryAsync<BindQueueItemDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<PolicyBindTransactionDto>> GetPolicyBindTransactionsAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF OBJECT_ID(N'Submissions.PolicyBindTransaction', N'U') IS NULL
BEGIN
    SELECT TOP 0
           CAST(NULL AS UNIQUEIDENTIFIER) AS PolicyBindTransactionId,
           CAST(NULL AS UNIQUEIDENTIFIER) AS TenantId,
           CAST(NULL AS UNIQUEIDENTIFIER) AS SubmissionId,
           CAST(N'' AS NVARCHAR(50)) AS SubmissionNumber,
           CAST(NULL AS UNIQUEIDENTIFIER) AS QuoteId,
           CAST(NULL AS NVARCHAR(80)) AS QuoteNumber,
           CAST(NULL AS UNIQUEIDENTIFIER) AS PolicyId,
           CAST(NULL AS NVARCHAR(80)) AS PolicyNumber,
           CAST(NULL AS UNIQUEIDENTIFIER) AS AccountId,
           CAST(N'' AS NVARCHAR(200)) AS AccountName,
           CAST(NULL AS UNIQUEIDENTIFIER) AS CarrierId,
           CAST(N'' AS NVARCHAR(200)) AS CarrierName,
           CAST(N'' AS NVARCHAR(50)) AS PolicySourceCode,
           CAST(N'' AS NVARCHAR(100)) AS PolicySourceName,
           CAST(N'' AS NVARCHAR(50)) AS BindStatusCode,
           CAST(N'' AS NVARCHAR(100)) AS BindStatusName,
           CAST(NULL AS NVARCHAR(500)) AS BindReason,
           CAST(NULL AS NVARCHAR(1000)) AS Notes,
           CAST(0 AS DECIMAL(18,2)) AS AnnualPremium,
           CAST(SYSUTCDATETIME() AS DATETIME2) AS EffectiveDate,
           CAST(SYSUTCDATETIME() AS DATETIME2) AS ExpirationDate,
           CAST(NULL AS UNIQUEIDENTIFIER) AS RequestedByUserId,
           CAST(SYSUTCDATETIME() AS DATETIME2) AS RequestedDateUtc,
           CAST(NULL AS UNIQUEIDENTIFIER) AS ApprovedByUserId,
           CAST(NULL AS DATETIME2) AS ApprovedDateUtc,
           CAST(NULL AS UNIQUEIDENTIFIER) AS BoundByUserId,
           CAST(NULL AS DATETIME2) AS BoundDateUtc,
           CAST(SYSUTCDATETIME() AS DATETIME2) AS CreatedDateUtc;
    RETURN;
END;

SELECT pbt.PolicyBindTransactionId,
       pbt.TenantId,
       pbt.SubmissionId,
       s.SubmissionNumber,
       pbt.QuoteId,
       q.QuoteNumber,
       pbt.PolicyId,
       COALESCE(bp.PolicyNumber, pbt.PolicyNumber) AS PolicyNumber,
       pbt.AccountId,
        COALESCE(a.AccountName, s.SubmissionNumber, N'Account') AS AccountName,
       pbt.CarrierId,
       COALESCE(c.CarrierName, N'Carrier') AS CarrierName,
       pbt.PolicySourceCode,
       COALESCE(pcs.SourceName, pbt.PolicySourceCode) AS PolicySourceName,
       pbt.BindStatusCode,
       COALESCE(pbs.StatusName, pbt.BindStatusCode) AS BindStatusName,
       pbt.BindReason,
       pbt.Notes,
       pbt.AnnualPremium,
       CAST(pbt.EffectiveDate AS DATETIME2) AS EffectiveDate,
       CAST(pbt.ExpirationDate AS DATETIME2) AS ExpirationDate,
       pbt.RequestedEffectiveTime,
       pbt.ConfirmationSourceCode,
       COALESCE(cso.OptionName, pbt.ConfirmationSourceCode) AS ConfirmationSourceName,
       pbt.CarrierReferenceNumber,
       pbt.BinderNumber,
       pbt.FinalPremium,
       pbt.DownPaymentAmount,
       pbt.SubjectivitiesOutstanding,
       pbt.ConfirmationNotes,
       pbt.ConfirmationDocumentId,
       pbt.ConfirmationReceivedFrom,
       pbt.ConfirmationMessageId,
       pbt.UnderwriterContactId,
       pbt.UnderwriterName,
       pbt.UnderwriterCompany,
       pbt.CommissionPlanApplicabilityId,
       pbt.CommissionPlanId,
       pbt.CommissionPlanVersionId,
       pbt.CommissionPayeeId,
       pbt.CommissionSplitRuleId,
       pbt.CommissionBusinessTypeCode,
       pbt.CommissionRatePct,
       pbt.CommissionSplitPct,
       pbt.CommissionablePremium,
       pbt.EstimatedGrossCommission,
       pbt.EstimatedProducerCommission,
       pbt.FollowUpWrittenConfirmationRequired,
       pbt.IntegrationCorrelationId,
       pbt.ExternalTransactionId,
       pbt.ConfirmedManually,
       pbt.ConfirmationCertified,
       pbt.RequestedByUserId,
       pbt.RequestedDateUtc,
       pbt.ApprovedByUserId,
       pbt.ApprovedDateUtc,
       pbt.BoundByUserId,
       pbt.BoundDateUtc,
       pbt.CreatedDateUtc
FROM Submissions.PolicyBindTransaction pbt
INNER JOIN Submissions.Submission s ON s.SubmissionId = pbt.SubmissionId
LEFT JOIN Submissions.Quote q ON q.QuoteId = pbt.QuoteId AND q.IsDeleted = 0
LEFT JOIN Submissions.BoundPolicy bp ON bp.PolicyId = pbt.PolicyId AND bp.IsDeleted = 0
LEFT JOIN Client.Account a ON a.AccountId = pbt.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = pbt.CarrierId
LEFT JOIN Submissions.PolicyCreationSource pcs ON pcs.TenantId = pbt.TenantId AND pcs.SourceCode = pbt.PolicySourceCode AND pcs.IsDeleted = 0
LEFT JOIN Submissions.PolicyBindStatus pbs ON pbs.TenantId = pbt.TenantId AND pbs.StatusCode = pbt.BindStatusCode AND pbs.IsDeleted = 0
LEFT JOIN Submissions.SubmissionReferenceOption cso ON cso.TenantId = pbt.TenantId AND cso.OptionGroup = N'BindConfirmationSource' AND cso.OptionCode = pbt.ConfirmationSourceCode AND cso.IsDeleted = 0
WHERE pbt.SubmissionId = @SubmissionId
  AND pbt.IsDeleted = 0
ORDER BY pbt.CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var tenantId = await cn.ExecuteScalarAsync<Guid?>(new CommandDefinition(@"
SELECT TOP 1 COALESCE(pbt.TenantId, s.TenantId)
FROM Submissions.PolicyBindTransaction pbt
LEFT JOIN Submissions.Submission s ON s.SubmissionId = pbt.SubmissionId AND s.IsDeleted = 0
WHERE pbt.SubmissionId = @SubmissionId
   OR s.SubmissionId = @SubmissionId;", new { SubmissionId = submissionId }, cancellationToken: cancellationToken));
        await EnsurePolicyBindTransactionSchemaAsync(cn, tenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001"), cancellationToken);

        return (await cn.QueryAsync<PolicyBindTransactionDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<PolicyBindTransactionDto?> GetPolicyBindTransactionAsync(Guid policyBindTransactionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 1 pbt.PolicyBindTransactionId,
       pbt.TenantId,
       pbt.SubmissionId,
       s.SubmissionNumber,
       pbt.QuoteId,
       q.QuoteNumber,
       pbt.PolicyId,
       COALESCE(bp.PolicyNumber, pbt.PolicyNumber) AS PolicyNumber,
       pbt.AccountId,
       COALESCE(a.AccountName, s.SubmissionNumber, N'Account') AS AccountName,
       pbt.CarrierId,
       COALESCE(c.CarrierName, N'Carrier') AS CarrierName,
       pbt.PolicySourceCode,
       COALESCE(pcs.SourceName, pbt.PolicySourceCode) AS PolicySourceName,
       pbt.BindStatusCode,
       COALESCE(pbs.StatusName, pbt.BindStatusCode) AS BindStatusName,
       pbt.BindReason,
       pbt.Notes,
       pbt.AnnualPremium,
       CAST(pbt.EffectiveDate AS DATETIME2) AS EffectiveDate,
       CAST(pbt.ExpirationDate AS DATETIME2) AS ExpirationDate,
       pbt.RequestedEffectiveTime,
       pbt.ConfirmationSourceCode,
       COALESCE(cso.OptionName, pbt.ConfirmationSourceCode) AS ConfirmationSourceName,
       pbt.CarrierReferenceNumber,
       pbt.BinderNumber,
       pbt.FinalPremium,
       pbt.DownPaymentAmount,
       pbt.SubjectivitiesOutstanding,
       pbt.ConfirmationNotes,
       pbt.ConfirmationDocumentId,
       pbt.ConfirmationReceivedFrom,
       pbt.ConfirmationMessageId,
       pbt.UnderwriterContactId,
       pbt.UnderwriterName,
       pbt.UnderwriterCompany,
       pbt.CommissionPlanApplicabilityId,
       pbt.CommissionPlanId,
       pbt.CommissionPlanVersionId,
       pbt.CommissionPayeeId,
       pbt.CommissionSplitRuleId,
       pbt.CommissionBusinessTypeCode,
       pbt.CommissionRatePct,
       pbt.CommissionSplitPct,
       pbt.CommissionablePremium,
       pbt.EstimatedGrossCommission,
       pbt.EstimatedProducerCommission,
       pbt.FollowUpWrittenConfirmationRequired,
       pbt.IntegrationCorrelationId,
       pbt.ExternalTransactionId,
       pbt.ConfirmedManually,
       pbt.ConfirmationCertified,
       pbt.RequestedByUserId,
       pbt.RequestedDateUtc,
       pbt.ApprovedByUserId,
       pbt.ApprovedDateUtc,
       pbt.BoundByUserId,
       pbt.BoundDateUtc,
       pbt.CreatedDateUtc
FROM Submissions.PolicyBindTransaction pbt
INNER JOIN Submissions.Submission s ON s.SubmissionId = pbt.SubmissionId
LEFT JOIN Submissions.Quote q ON q.QuoteId = pbt.QuoteId AND q.IsDeleted = 0
LEFT JOIN Submissions.BoundPolicy bp ON bp.PolicyId = pbt.PolicyId AND bp.IsDeleted = 0
LEFT JOIN Client.Account a ON a.AccountId = pbt.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = pbt.CarrierId
LEFT JOIN Submissions.PolicyCreationSource pcs ON pcs.TenantId = pbt.TenantId AND pcs.SourceCode = pbt.PolicySourceCode AND pcs.IsDeleted = 0
LEFT JOIN Submissions.PolicyBindStatus pbs ON pbs.TenantId = pbt.TenantId AND pbs.StatusCode = pbt.BindStatusCode AND pbs.IsDeleted = 0
LEFT JOIN Submissions.SubmissionReferenceOption cso ON cso.TenantId = pbt.TenantId AND cso.OptionGroup = N'BindConfirmationSource' AND cso.OptionCode = pbt.ConfirmationSourceCode AND cso.IsDeleted = 0
WHERE pbt.PolicyBindTransactionId = @PolicyBindTransactionId
  AND pbt.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var tenantId = await cn.ExecuteScalarAsync<Guid?>(new CommandDefinition(@"
SELECT TOP 1 TenantId
FROM Submissions.PolicyBindTransaction
WHERE PolicyBindTransactionId = @PolicyBindTransactionId
  AND IsDeleted = 0;", new { PolicyBindTransactionId = policyBindTransactionId }, cancellationToken: cancellationToken));
        await EnsurePolicyBindTransactionSchemaAsync(cn, tenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001"), cancellationToken);

        return await cn.QuerySingleOrDefaultAsync<PolicyBindTransactionDto>(new CommandDefinition(sql, new { PolicyBindTransactionId = policyBindTransactionId }, cancellationToken: cancellationToken));
    }

    public async Task<BindRequestDetailDto?> GetBindRequestDetailAsync(Guid policyBindTransactionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT pbt.*, s.SubmissionNumber, q.QuoteNumber, a.AccountName, c.CarrierName,
       COALESCE(pcs.SourceName, pbt.PolicySourceCode) AS PolicySourceName,
       COALESCE(pbs.StatusName, pbt.BindStatusCode) AS BindStatusName,
       cso.OptionName AS ConfirmationSourceName,
       bmo.OptionName AS BindingMethodName,
       bao.OptionName AS BindingAuthorityName
FROM Submissions.PolicyBindTransaction pbt
INNER JOIN Submissions.Submission s ON s.SubmissionId = pbt.SubmissionId AND s.TenantId = pbt.TenantId
LEFT JOIN Submissions.Quote q ON q.QuoteId = pbt.QuoteId AND q.IsDeleted = 0
LEFT JOIN Client.Account a ON a.AccountId = pbt.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = pbt.CarrierId
LEFT JOIN Submissions.PolicyCreationSource pcs ON pcs.TenantId = pbt.TenantId AND pcs.SourceCode = pbt.PolicySourceCode AND pcs.IsDeleted = 0
LEFT JOIN Submissions.PolicyBindStatus pbs ON pbs.TenantId = pbt.TenantId AND pbs.StatusCode = pbt.BindStatusCode AND pbs.IsDeleted = 0
LEFT JOIN Submissions.SubmissionReferenceOption cso ON cso.TenantId = pbt.TenantId AND cso.OptionGroup = N'BindConfirmationSource' AND cso.OptionCode = pbt.ConfirmationSourceCode AND cso.IsDeleted = 0
LEFT JOIN Submissions.SubmissionReferenceOption bmo ON bmo.TenantId = pbt.TenantId AND bmo.OptionGroup = N'BindMethod' AND bmo.OptionCode = pbt.BindingMethodCode AND bmo.IsDeleted = 0
LEFT JOIN Submissions.SubmissionReferenceOption bao ON bao.TenantId = pbt.TenantId AND bao.OptionGroup = N'BindingAuthority' AND bao.OptionCode = pbt.BindingAuthorityCode AND bao.IsDeleted = 0
WHERE pbt.PolicyBindTransactionId = @PolicyBindTransactionId AND pbt.TenantId = @TenantId AND pbt.IsDeleted = 0;

SELECT br.* FROM Submissions.BindRequirement br
INNER JOIN Submissions.PolicyBindTransaction pbt ON pbt.PolicyBindTransactionId = @PolicyBindTransactionId AND pbt.TenantId = br.TenantId
INNER JOIN Submissions.Submission s ON s.SubmissionId = pbt.SubmissionId
WHERE br.TenantId = @TenantId AND br.IsActive = 1 AND br.IsDeleted = 0
  AND (br.CarrierId IS NULL OR br.CarrierId = pbt.CarrierId)
  AND (br.LineOfBusiness IS NULL OR br.LineOfBusiness = s.LineOfBusiness)
ORDER BY br.SortOrder, br.RequirementName;

SELECT * FROM Submissions.BindValidationResult WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0 ORDER BY ValidatedDateUtc DESC, RequirementName;
SELECT h.*, COALESCE(u.FullName, u.DisplayName, u.UserName) AS ChangedByName FROM Submissions.BindStatusHistory h LEFT JOIN IAM.[User] u ON u.UserId = h.ChangedByUserId WHERE h.PolicyBindTransactionId = @PolicyBindTransactionId AND h.TenantId = @TenantId AND h.IsDeleted = 0 ORDER BY h.ChangedDateUtc DESC;
SELECT a.*, o.OptionName AS ApprovalReasonName FROM Submissions.BindApproval a LEFT JOIN Submissions.SubmissionReferenceOption o ON o.TenantId = a.TenantId AND o.OptionGroup = N'BindApprovalReason' AND o.OptionCode = a.ApprovalReasonCode AND o.IsDeleted = 0 WHERE a.PolicyBindTransactionId = @PolicyBindTransactionId AND a.TenantId = @TenantId AND a.IsDeleted = 0 ORDER BY a.RequestedDateUtc DESC;
SELECT bd.*, d.FileName, d.CategoryCode AS Category FROM Submissions.BindDocument bd INNER JOIN DMS.Document d ON d.DocumentId = bd.DocumentId AND d.TenantId = bd.TenantId AND d.IsDeleted = 0 WHERE bd.PolicyBindTransactionId = @PolicyBindTransactionId AND bd.TenantId = @TenantId AND bd.IsDeleted = 0 ORDER BY bd.CreatedDateUtc DESC;
SELECT * FROM Submissions.BindCarrierMessage WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0 ORDER BY SentReceivedDateUtc DESC;
SELECT * FROM Submissions.BindPackage WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0 ORDER BY PreparedDateUtc DESC;
SELECT TOP 1 br.*, c.CarrierName FROM Submissions.BinderReview br INNER JOIN Core.Carrier c ON c.CarrierId = br.CarrierId AND c.TenantId = br.TenantId WHERE br.PolicyBindTransactionId = @PolicyBindTransactionId AND br.TenantId = @TenantId AND br.IsDeleted = 0;
SELECT TOP 1 * FROM Submissions.PolicyGenerationRequest WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0 ORDER BY RequestedDateUtc DESC;
SELECT FromStatusCode, ToStatusCode, RequiresValidation, RequiresApproval, RequiresCarrierResponse
FROM Submissions.BindStatusTransition
WHERE TenantId = @TenantId AND FromStatusCode = (SELECT BindStatusCode FROM Submissions.PolicyBindTransaction WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0)
  AND IsActive = 1 AND IsDeleted = 0
ORDER BY ToStatusCode;
SELECT * FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'BindMethod' AND IsActive = 1 AND IsDeleted = 0 ORDER BY SortOrder;
SELECT * FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'BindingAuthority' AND IsActive = 1 AND IsDeleted = 0 ORDER BY SortOrder;
SELECT * FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'BindApprovalReason' AND IsActive = 1 AND IsDeleted = 0 ORDER BY SortOrder;
""";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { PolicyBindTransactionId = policyBindTransactionId, TenantId = tenantId }, cancellationToken: cancellationToken));
        var request = await multi.ReadSingleOrDefaultAsync<PolicyBindTransactionDto>();
        if (request is null) return null;
        var detail = new BindRequestDetailDto
        {
            Request = request,
            Requirements = (await multi.ReadAsync<BindRequirementDto>()).AsList(),
            Validations = (await multi.ReadAsync<BindValidationResultDto>()).AsList(),
            StatusHistory = (await multi.ReadAsync<BindStatusHistoryDto>()).AsList(),
            Approvals = (await multi.ReadAsync<BindApprovalDto>()).AsList(),
            Documents = (await multi.ReadAsync<BindDocumentDto>()).AsList(),
            CarrierMessages = (await multi.ReadAsync<BindCarrierMessageDto>()).AsList(),
            Packages = (await multi.ReadAsync<BindPackageDto>()).AsList(),
            BinderReview = await multi.ReadSingleOrDefaultAsync<BinderReviewDto>(),
            PolicyGeneration = await multi.ReadSingleOrDefaultAsync<PolicyGenerationRequestDto>(),
            AllowedTransitions = (await multi.ReadAsync<BindStatusTransitionDto>()).AsList(),
            BindingMethods = (await multi.ReadAsync<SubmissionReferenceOptionDto>()).AsList(),
            BindingAuthorities = (await multi.ReadAsync<SubmissionReferenceOptionDto>()).AsList(),
            ApprovalReasons = (await multi.ReadAsync<SubmissionReferenceOptionDto>()).AsList()
        };
        detail.BlockingValidationCount = detail.Validations.Count(x => x.IsBlocking && x.StatusCode is not ("Passed" or "Waived"));
        detail.IsReadyToSubmit = detail.Validations.Count > 0 && detail.BlockingValidationCount == 0;
        return detail;
    }

    public async Task<IReadOnlyList<BindValidationResultDto>> ValidateBindRequestAsync(Guid policyBindTransactionId, ValidateBindRequestRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindTransaction WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 52100, 'Bind request was not found.', 1;

UPDATE Submissions.BindValidationResult SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ValidatedByUserId
WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.BindValidationResult
    (BindValidationResultId, TenantId, PolicyBindTransactionId, BindRequirementId, RequirementCode, RequirementName, RequirementTypeCode, StatusCode, IsBlocking, Message, EvidenceDocumentId, ValidatedDateUtc, ValidatedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), br.TenantId, pbt.PolicyBindTransactionId, br.BindRequirementId, br.RequirementCode, br.RequirementName, br.RequirementTypeCode,
       result.StatusCode,
       CAST(CASE WHEN br.BlocksSubmission = 1 AND result.StatusCode NOT IN (N'Passed', N'Waived') THEN 1 ELSE 0 END AS bit),
       result.Message, result.EvidenceDocumentId, SYSUTCDATETIME(), @ValidatedByUserId, SYSUTCDATETIME(), @ValidatedByUserId, 0
FROM Submissions.PolicyBindTransaction pbt
INNER JOIN Submissions.Submission s ON s.SubmissionId = pbt.SubmissionId AND s.TenantId = pbt.TenantId
INNER JOIN Submissions.BindRequirement br ON br.TenantId = pbt.TenantId AND (br.CarrierId IS NULL OR br.CarrierId = pbt.CarrierId) AND (br.LineOfBusiness IS NULL OR br.LineOfBusiness = s.LineOfBusiness) AND br.IsRequired = 1 AND br.IsActive = 1 AND br.IsDeleted = 0
OUTER APPLY
(
    SELECT TOP 1 d.DocumentId
    FROM DMS.Document d
    WHERE d.TenantId = pbt.TenantId AND d.EntityName = N'Submission' AND d.EntityId = pbt.SubmissionId AND d.IsDeleted = 0
      AND (d.CategoryCode = br.DocumentCategoryCode OR EXISTS (SELECT 1 FROM Submissions.BindDocument bd WHERE bd.TenantId = pbt.TenantId AND bd.PolicyBindTransactionId = pbt.PolicyBindTransactionId AND bd.DocumentId = d.DocumentId AND bd.DocumentRoleCode = br.RequirementCode AND bd.IsDeleted = 0))
) evidence
CROSS APPLY
(
    SELECT
      CASE
        WHEN br.RequirementTypeCode = N'Document' THEN CASE WHEN evidence.DocumentId IS NOT NULL THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode = N'ProposalAccepted' THEN CASE WHEN EXISTS (SELECT 1 FROM Submissions.ClientAcceptance ca WHERE ca.TenantId = pbt.TenantId AND ca.SubmissionId = pbt.SubmissionId AND ca.QuoteId = pbt.QuoteId AND ca.StatusCode IN (N'Accepted', N'BindRequested', N'CarrierBound') AND ca.IsDeleted = 0) THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode = N'QuoteBindable' THEN CASE WHEN EXISTS (SELECT 1 FROM Submissions.Quote q WHERE q.QuoteId = pbt.QuoteId AND q.SubmissionId = pbt.SubmissionId AND q.IsBindable = 1 AND q.ExpiresDateUtc > SYSUTCDATETIME() AND q.IsDeleted = 0) THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode = N'EffectiveDateValid' THEN CASE WHEN pbt.EffectiveDate >= CAST(SYSUTCDATETIME() AS date) AND pbt.ExpirationDate > pbt.EffectiveDate THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode = N'UnderwritingComplete' THEN CASE WHEN NOT EXISTS (SELECT 1 FROM Submissions.SubmissionIntakeQuestion iq WHERE iq.SubmissionId = pbt.SubmissionId AND iq.TenantId = pbt.TenantId AND iq.IsRequired = 1 AND iq.IsAnswered = 0 AND iq.IsDeleted = 0) THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode = N'ProducerLicensed' THEN CASE WHEN EXISTS (SELECT 1 FROM Agency.Staff st WHERE st.StaffId = s.AssignedToUserId AND st.TenantId = pbt.TenantId AND NULLIF(st.LicenseNumber, N'') IS NOT NULL AND (st.LicenseExpiryDate IS NULL OR st.LicenseExpiryDate >= pbt.EffectiveDate) AND st.IsActive = 1 AND st.IsDeleted = 0) THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode = N'CarrierAppointment' THEN CASE WHEN EXISTS (SELECT 1 FROM Agency.CarrierAppointment ca WHERE ca.TenantId = pbt.TenantId AND ca.CarrierId = pbt.CarrierId AND (NULLIF(ca.LineOfBusiness, N'') IS NULL OR ca.LineOfBusiness = s.LineOfBusiness) AND ca.AppointmentDate <= pbt.EffectiveDate AND (ca.ExpirationDate IS NULL OR ca.ExpirationDate >= pbt.EffectiveDate) AND ca.IsActive = 1 AND ca.IsDeleted = 0) THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode = N'AgencyAppointment' THEN CASE WHEN EXISTS (SELECT 1 FROM Agency.CarrierAppointment ca WHERE ca.TenantId = pbt.TenantId AND ca.CarrierId = pbt.CarrierId AND ca.AppointmentDate <= pbt.EffectiveDate AND (ca.ExpirationDate IS NULL OR ca.ExpirationDate >= pbt.EffectiveDate) AND ca.IsActive = 1 AND ca.IsDeleted = 0) THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode = N'PremiumCalculated' THEN CASE WHEN pbt.AnnualPremium > 0 THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode = N'TaxesCalculated' THEN CASE WHEN EXISTS (SELECT 1 FROM Submissions.Quote q WHERE q.QuoteId = pbt.QuoteId AND q.TenantId = pbt.TenantId AND q.TaxesAndFees IS NOT NULL AND q.IsDeleted = 0) THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode = N'RequiredNotes' THEN CASE WHEN NULLIF(LTRIM(RTRIM(COALESCE(pbt.ProducerNotes, pbt.Notes))), N'') IS NOT NULL THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode = N'RequiredActivities' THEN CASE WHEN NOT EXISTS (SELECT 1 FROM OPS.TaskItem t WHERE t.TenantId = pbt.TenantId AND t.RelatedEntityName = N'Submission' AND t.RelatedEntityId = pbt.SubmissionId AND t.StatusCode NOT IN (N'Completed', N'Cancelled', N'Closed') AND t.DueDate < CAST(SYSUTCDATETIME() AS date) AND t.IsDeleted = 0) THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode = N'ComplianceClear' THEN CASE WHEN NOT EXISTS (SELECT 1 FROM OPS.TaskItem t WHERE t.TenantId = pbt.TenantId AND t.RelatedEntityName = N'Submission' AND t.RelatedEntityId = pbt.SubmissionId AND (t.TaskTypeCode LIKE N'%Compliance%' OR t.Title LIKE N'%DNC%') AND t.StatusCode NOT IN (N'Completed', N'Cancelled', N'Closed') AND t.IsDeleted = 0) THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode IN (N'OutstandingInspection', N'OutstandingSurvey', N'OutstandingMvr', N'OutstandingClaimsReview') THEN CASE WHEN NOT EXISTS (SELECT 1 FROM OPS.TaskItem t WHERE t.TenantId = pbt.TenantId AND t.RelatedEntityName = N'Submission' AND t.RelatedEntityId = pbt.SubmissionId AND t.StatusCode NOT IN (N'Completed', N'Cancelled', N'Closed') AND t.IsDeleted = 0 AND ((br.RequirementCode = N'OutstandingInspection' AND (t.TaskTypeCode LIKE N'%Inspection%' OR t.Title LIKE N'%Inspection%')) OR (br.RequirementCode = N'OutstandingSurvey' AND (t.TaskTypeCode LIKE N'%Survey%' OR t.Title LIKE N'%Survey%')) OR (br.RequirementCode = N'OutstandingMvr' AND (t.TaskTypeCode LIKE N'%MVR%' OR t.Title LIKE N'%MVR%')) OR (br.RequirementCode = N'OutstandingClaimsReview' AND (t.TaskTypeCode LIKE N'%Claim%' OR t.Title LIKE N'%Claim%')))) THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementCode = N'DownPaymentVerified' AND pbt.PaymentRequired = 0 THEN N'Waived'
        WHEN br.RequirementCode = N'DownPaymentVerified' THEN CASE WHEN pbt.PaymentVerified = 1 AND COALESCE(pbt.DownPaymentAmount, 0) > 0 THEN N'Passed' ELSE N'Failed' END
        WHEN br.RequirementTypeCode = N'Payment' AND pbt.PaymentRequired = 0 THEN N'Waived'
        WHEN br.RequirementTypeCode = N'Payment' AND pbt.PaymentVerified = 1 THEN N'Passed'
        ELSE N'Pending'
      END AS StatusCode,
      CASE
        WHEN br.RequirementTypeCode = N'Document' AND evidence.DocumentId IS NULL THEN CONCAT(br.RequirementName, N' is required before submission.')
        WHEN br.RequirementCode = N'ProducerLicensed' THEN N'The assigned producer license must cover the requested effective date.'
        WHEN br.RequirementCode = N'CarrierAppointment' THEN N'An active carrier appointment must cover this line and effective date.'
        ELSE NULL
      END AS Message,
      evidence.DocumentId AS EvidenceDocumentId
) result
WHERE pbt.PolicyBindTransactionId = @PolicyBindTransactionId AND pbt.TenantId = @TenantId AND pbt.IsDeleted = 0;

SELECT * FROM Submissions.BindValidationResult WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0 ORDER BY IsBlocking DESC, RequirementName;
""";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<BindValidationResultDto>(new CommandDefinition(sql, new { PolicyBindTransactionId = policyBindTransactionId, request.TenantId, request.ValidatedByUserId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task UpdateBindRequestStatusAsync(Guid policyBindTransactionId, UpdateBindRequestStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
DECLARE @OldStatusCode NVARCHAR(50) = (SELECT BindStatusCode FROM Submissions.PolicyBindTransaction WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0);
IF @OldStatusCode IS NULL THROW 52100, 'Bind request was not found.', 1;
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindStatus WHERE TenantId = @TenantId AND StatusCode = @StatusCode AND IsActive = 1 AND IsDeleted = 0) THROW 52101, 'Bind request status is not configured.', 1;
IF NOT EXISTS (SELECT 1 FROM Submissions.BindStatusTransition WHERE TenantId = @TenantId AND FromStatusCode = @OldStatusCode AND ToStatusCode = @StatusCode AND IsActive = 1 AND IsDeleted = 0) THROW 52103, 'The requested bind status transition is not configured.', 1;
UPDATE Submissions.PolicyBindTransaction SET BindStatusCode = @StatusCode,
    PreparedDateUtc = CASE WHEN @StatusCode = N'Ready' THEN COALESCE(PreparedDateUtc, SYSUTCDATETIME()) ELSE PreparedDateUtc END,
    SubmittedDateUtc = CASE WHEN @StatusCode = N'Submitted' THEN COALESCE(SubmittedDateUtc, SYSUTCDATETIME()) ELSE SubmittedDateUtc END,
    ReceivedDateUtc = CASE WHEN @StatusCode = N'Received' THEN COALESCE(ReceivedDateUtc, SYSUTCDATETIME()) ELSE ReceivedDateUtc END,
    ApprovedDateUtc = CASE WHEN @StatusCode = N'Approved' THEN COALESCE(ApprovedDateUtc, SYSUTCDATETIME()) ELSE ApprovedDateUtc END,
    BoundDateUtc = CASE WHEN @StatusCode = N'Bound' THEN COALESCE(BoundDateUtc, SYSUTCDATETIME()) ELSE BoundDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ChangedByUserId
WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0;
INSERT INTO Submissions.BindStatusHistory (BindStatusHistoryId, TenantId, PolicyBindTransactionId, OldStatusCode, NewStatusCode, Comments, IpAddress, DeviceInfo, ChangedDateUtc, ChangedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (NEWID(), @TenantId, @PolicyBindTransactionId, @OldStatusCode, @StatusCode, @Comments, @IpAddress, @DeviceInfo, SYSUTCDATETIME(), @ChangedByUserId, SYSUTCDATETIME(), @ChangedByUserId, 0);
""";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PolicyBindTransactionId = policyBindTransactionId, request.TenantId, request.StatusCode, request.Comments, request.ChangedByUserId, request.IpAddress, request.DeviceInfo }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> RequestBindApprovalAsync(Guid policyBindTransactionId, RequestBindApprovalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindTransaction WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0) THROW 52100, 'Bind request was not found.', 1;
IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'BindApprovalReason' AND OptionCode = @ApprovalReasonCode AND IsActive = 1 AND IsDeleted = 0) THROW 52102, 'Approval reason is not configured.', 1;
DECLARE @OldStatusCode NVARCHAR(50) = (SELECT BindStatusCode FROM Submissions.PolicyBindTransaction WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.BindStatusTransition WHERE TenantId = @TenantId AND FromStatusCode = @OldStatusCode AND ToStatusCode = N'PendingApproval' AND IsActive = 1 AND IsDeleted = 0) THROW 52103, 'This bind request cannot transition to pending approval.', 1;
IF EXISTS (SELECT 1 FROM Submissions.BindApproval WHERE TenantId = @TenantId AND PolicyBindTransactionId = @PolicyBindTransactionId AND StatusCode = N'Pending' AND IsDeleted = 0) THROW 52104, 'A pending bind approval already exists.', 1;
DECLARE @Id UNIQUEIDENTIFIER = NEWID();
INSERT INTO Submissions.BindApproval (BindApprovalId, TenantId, PolicyBindTransactionId, ApprovalReasonCode, StatusCode, RequestedByUserId, RequestedDateUtc, AssignedApproverUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @PolicyBindTransactionId, @ApprovalReasonCode, N'Pending', @RequestedByUserId, SYSUTCDATETIME(), @AssignedApproverUserId, SYSUTCDATETIME(), @RequestedByUserId, 0);
UPDATE Submissions.PolicyBindTransaction SET ApprovalRequired = 1, BindStatusCode = N'PendingApproval', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @RequestedByUserId WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId;
INSERT INTO Submissions.BindStatusHistory (BindStatusHistoryId, TenantId, PolicyBindTransactionId, OldStatusCode, NewStatusCode, Comments, ChangedDateUtc, ChangedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (NEWID(), @TenantId, @PolicyBindTransactionId, @OldStatusCode, N'PendingApproval', N'Bind approval requested.', SYSUTCDATETIME(), @RequestedByUserId, SYSUTCDATETIME(), @RequestedByUserId, 0);
SELECT @Id;
""";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { PolicyBindTransactionId = policyBindTransactionId, request.TenantId, request.ApprovalReasonCode, request.AssignedApproverUserId, request.RequestedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DecideBindApprovalAsync(Guid policyBindTransactionId, Guid bindApprovalId, DecideBindApprovalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
UPDATE Submissions.BindApproval SET StatusCode = @DecisionCode, DecisionByUserId = @DecisionByUserId, DecisionDateUtc = SYSUTCDATETIME(), DecisionNotes = @DecisionNotes, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @DecisionByUserId
WHERE BindApprovalId = @BindApprovalId AND PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND StatusCode = N'Pending' AND IsDeleted = 0;
IF @@ROWCOUNT = 0 THROW 52103, 'Pending bind approval was not found.', 1;
UPDATE Submissions.PolicyBindTransaction SET ApprovedByUserId = CASE WHEN @DecisionCode = N'Approved' THEN @DecisionByUserId ELSE ApprovedByUserId END, ApprovedDateUtc = CASE WHEN @DecisionCode = N'Approved' THEN SYSUTCDATETIME() ELSE ApprovedDateUtc END, BindStatusCode = CASE WHEN @DecisionCode = N'Approved' THEN N'Ready' ELSE N'Draft' END, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @DecisionByUserId
WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0;
INSERT INTO Submissions.BindStatusHistory (BindStatusHistoryId, TenantId, PolicyBindTransactionId, OldStatusCode, NewStatusCode, Comments, ChangedDateUtc, ChangedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (NEWID(), @TenantId, @PolicyBindTransactionId, N'PendingApproval', CASE WHEN @DecisionCode = N'Approved' THEN N'Ready' ELSE N'Draft' END, CONCAT(N'Approval ', LOWER(@DecisionCode), N'. ', COALESCE(@DecisionNotes, N'')), SYSUTCDATETIME(), @DecisionByUserId, SYSUTCDATETIME(), @DecisionByUserId, 0);
""";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PolicyBindTransactionId = policyBindTransactionId, BindApprovalId = bindApprovalId, request.TenantId, request.DecisionCode, request.DecisionNotes, request.DecisionByUserId }, cancellationToken: cancellationToken));
    }

    public async Task RecordBindCarrierResponseAsync(Guid policyBindTransactionId, RecordBindCarrierResponseRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @OldStatusCode NVARCHAR(50) = (SELECT BindStatusCode FROM Submissions.PolicyBindTransaction WITH (UPDLOCK, HOLDLOCK) WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0);
IF @OldStatusCode IS NULL THROW 52100, 'Bind request was not found.', 1;
IF NOT EXISTS (SELECT 1 FROM Submissions.BindStatusTransition WHERE TenantId = @TenantId AND FromStatusCode = @OldStatusCode AND ToStatusCode = @StatusCode AND RequiresCarrierResponse = 1 AND IsActive = 1 AND IsDeleted = 0) THROW 52105, 'The carrier response transition is not configured for the current bind status.', 1;
INSERT INTO Submissions.BindCarrierMessage (BindCarrierMessageId, TenantId, PolicyBindTransactionId, DirectionCode, MessageTypeCode, DeliveryMethodCode, ExternalMessageId, Subject, MessageBody, StatusCode, SentReceivedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (NEWID(), @TenantId, @PolicyBindTransactionId, N'Inbound', @MessageTypeCode, @DeliveryMethodCode, @ExternalMessageId, @Subject, @MessageBody, @StatusCode, SYSUTCDATETIME(), SYSUTCDATETIME(), @RecordedByUserId, 0);
UPDATE Submissions.PolicyBindTransaction SET BindStatusCode = @StatusCode, CarrierReferenceNumber = COALESCE(NULLIF(@CarrierReferenceNumber, N''), CarrierReferenceNumber), BinderNumber = COALESCE(NULLIF(@BinderNumber, N''), BinderNumber), FinalPremium = COALESCE(@FinalPremium, FinalPremium), ConfirmationDocumentId = COALESCE(@ConfirmationDocumentId, ConfirmationDocumentId), ConfirmationSourceCode = COALESCE(NULLIF(@ConfirmationSourceCode, N''), ConfirmationSourceCode), ConfirmationCertified = CASE WHEN @ConfirmationCertified = 1 THEN 1 ELSE ConfirmationCertified END, ConfirmationNotes = COALESCE(NULLIF(@MessageBody, N''), ConfirmationNotes), ReceivedDateUtc = CASE WHEN @StatusCode = N'Received' THEN COALESCE(ReceivedDateUtc, SYSUTCDATETIME()) ELSE ReceivedDateUtc END, ApprovedDateUtc = CASE WHEN @StatusCode = N'Approved' THEN COALESCE(ApprovedDateUtc, SYSUTCDATETIME()) ELSE ApprovedDateUtc END, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @RecordedByUserId
WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId;
INSERT INTO Submissions.BindStatusHistory (BindStatusHistoryId, TenantId, PolicyBindTransactionId, OldStatusCode, NewStatusCode, Comments, ChangedDateUtc, ChangedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (NEWID(), @TenantId, @PolicyBindTransactionId, @OldStatusCode, @StatusCode, @MessageBody, SYSUTCDATETIME(), @RecordedByUserId, SYSUTCDATETIME(), @RecordedByUserId, 0);
COMMIT TRANSACTION;
""";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PolicyBindTransactionId = policyBindTransactionId, request.TenantId, request.StatusCode, request.MessageTypeCode, request.DeliveryMethodCode, request.ExternalMessageId, request.Subject, request.MessageBody, request.CarrierReferenceNumber, request.BinderNumber, request.FinalPremium, request.ConfirmationDocumentId, request.RecordedByUserId, request.ConfirmationSourceCode, request.ConfirmationCertified }, cancellationToken: cancellationToken));
    }

    public async Task<BindPackageDto> PrepareBindPackageAsync(Guid policyBindTransactionId, PrepareBindPackageRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindTransaction WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0) THROW 52100, 'Bind request was not found.', 1;
DECLARE @PackageId UNIQUEIDENTIFIER = NEWID();
DECLARE @PackageNumber NVARCHAR(80) = CONCAT(N'BPK-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), @PackageId), N'-', N''), 8));
INSERT INTO Submissions.BindDocument (BindDocumentId, TenantId, PolicyBindTransactionId, DocumentId, DocumentRoleCode, IsRequiredEvidence, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), pbt.TenantId, pbt.PolicyBindTransactionId, d.DocumentId, COALESCE(NULLIF(d.CategoryCode, N''), N'SupportingDocument'), 0, SYSUTCDATETIME(), @PreparedByUserId, 0
FROM Submissions.PolicyBindTransaction pbt
INNER JOIN DMS.Document d ON d.TenantId = pbt.TenantId AND d.EntityName = N'Submission' AND d.EntityId = pbt.SubmissionId AND d.IsDeleted = 0
WHERE pbt.PolicyBindTransactionId = @PolicyBindTransactionId AND pbt.TenantId = @TenantId AND pbt.IsDeleted = 0
AND NOT EXISTS (SELECT 1 FROM Submissions.BindDocument bd WHERE bd.TenantId = pbt.TenantId AND bd.PolicyBindTransactionId = pbt.PolicyBindTransactionId AND bd.DocumentId = d.DocumentId AND bd.DocumentRoleCode = COALESCE(NULLIF(d.CategoryCode, N''), N'SupportingDocument') AND bd.IsDeleted = 0);
DECLARE @DocumentCount INT = (SELECT COUNT(*) FROM Submissions.BindDocument WHERE TenantId = @TenantId AND PolicyBindTransactionId = @PolicyBindTransactionId AND IsDeleted = 0);
INSERT INTO Submissions.BindPackage (BindPackageId, TenantId, PolicyBindTransactionId, PackageNumber, StatusCode, PreparedDateUtc, PreparedByUserId, DocumentCount, Notes, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@PackageId, @TenantId, @PolicyBindTransactionId, @PackageNumber, N'Prepared', SYSUTCDATETIME(), @PreparedByUserId, @DocumentCount, @Notes, SYSUTCDATETIME(), @PreparedByUserId, 0);
SELECT BindPackageId, PolicyBindTransactionId, PackageNumber, StatusCode, PreparedDateUtc, PreparedByUserId, DocumentCount, Notes FROM Submissions.BindPackage WHERE BindPackageId = @PackageId;
""";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<BindPackageDto>(new CommandDefinition(sql, new { PolicyBindTransactionId = policyBindTransactionId, request.TenantId, request.Notes, request.PreparedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<SubmissionActionResult> SubmitToMarketAsync(Guid id, SubmitSubmissionToMarketRequest request, CancellationToken cancellationToken = default)
    {
        var readiness = await GetReadinessAsync(id, request.TenantId, cancellationToken);
        if (!readiness.IsReadyForMarketing)
            throw new InvalidOperationException("Submission is not ready for marketing: " + string.Join("; ", readiness.BlockingReasons));

        const string sql = @"
DECLARE @CarrierId UNIQUEIDENTIFIER = COALESCE(@CarrierIdIn, (SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CarrierName));
IF @CarrierId IS NULL THROW 52000, 'No carrier is available for this tenant.', 1;

DECLARE @MarketId UNIQUEIDENTIFIER = (SELECT TOP 1 SubmissionMarketId FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND CarrierId = @CarrierId AND IsDeleted = 0);
IF @MarketId IS NULL
BEGIN
    SET @MarketId = NEWID();
    INSERT INTO Submissions.SubmissionMarket (SubmissionMarketId, SubmissionId, CarrierId, Status, AppetiteScore, IsRecommended, DeclineReason, AddedDateUtc, RespondedDateUtc, TenantId, IsDeleted)
    VALUES (@MarketId, @SubmissionId, @CarrierId, N'Submitted', 80, 1, NULL, SYSUTCDATETIME(), NULL, @TenantId, 0);
END
ELSE
BEGIN
    UPDATE Submissions.SubmissionMarket
    SET Status = N'Submitted', DeclineReason = NULL, RespondedDateUtc = NULL, TenantId = COALESCE(TenantId, @TenantId)
    WHERE SubmissionMarketId = @MarketId;
END

UPDATE Submissions.SubmissionMarket
SET SubmittedDateUtc = SYSUTCDATETIME(),
    SubmittedByUserId = NULL,
    Notes = COALESCE(@Notes, Notes),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionMarketId = @MarketId;

INSERT INTO Submissions.SubmissionMarketDocument (SubmissionMarketDocumentId, SubmissionMarketId, SubmissionId, TenantId, DocumentId, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @MarketId, @SubmissionId, @TenantId, d.DocumentId, SYSUTCDATETIME(), 0
FROM DMS.Document d
WHERE d.TenantId = @TenantId AND d.EntityName = N'Submission' AND d.EntityId = @SubmissionId AND d.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Submissions.SubmissionMarketDocument md WHERE md.SubmissionMarketId = @MarketId AND md.DocumentId = d.DocumentId AND md.IsDeleted = 0);

IF OBJECT_ID(N'Submissions.SubmissionMarketDispatch', N'U') IS NOT NULL
BEGIN
    DECLARE @DispatchChannelCode NVARCHAR(50) = N'InternalQueue';
    DECLARE @DispatchRecipient NVARCHAR(500) = NULL;
    DECLARE @DispatchSubjectTemplate NVARCHAR(300) = N'Submission {SubmissionNumber} ready for market review';
    DECLARE @DispatchMaxAttempts INT = 3;

    IF OBJECT_ID(N'Agency.CarrierSetting', N'U') IS NOT NULL
    BEGIN
        SELECT @DispatchChannelCode = COALESCE(NULLIF(marketMethod.SubmissionMethodCode, N''), NULLIF(carrierChannel.SettingValue, N''), NULLIF(carrierChannel.DefaultValue, N''), deliveryChannel.ChannelCode, NULLIF(defaultChannel.SettingValue, N''), NULLIF(defaultChannel.DefaultValue, N''), @DispatchChannelCode),
               @DispatchRecipient = COALESCE(NULLIF(carrierEmail.SettingValue, N''), NULLIF(carrierEmail.DefaultValue, N''), NULLIF(deliveryEmail.SettingValue, N''), NULLIF(deliveryEmail.DefaultValue, N''), NULLIF(carrierPortal.SettingValue, N''), NULLIF(carrierPortal.DefaultValue, N''), NULLIF(deliveryPortal.SettingValue, N''), NULLIF(deliveryPortal.DefaultValue, N''), NULLIF(defaultRecipient.SettingValue, N''), NULLIF(defaultRecipient.DefaultValue, N'')),
               @DispatchSubjectTemplate = COALESCE(NULLIF(deliverySubject.SettingValue, N''), NULLIF(deliverySubject.DefaultValue, N''), NULLIF(subjectTemplate.SettingValue, N''), NULLIF(subjectTemplate.DefaultValue, N''), @DispatchSubjectTemplate),
               @DispatchMaxAttempts = COALESCE(TRY_CONVERT(INT, COALESCE(NULLIF(maxAttempts.SettingValue, N''), NULLIF(maxAttempts.DefaultValue, N''))), @DispatchMaxAttempts)
        FROM (SELECT 1 AS Seed) seed
        OUTER APPLY (SELECT TOP 1 SubmissionMethodCode FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0) marketMethod
        OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = @CarrierId AND SettingCode = N'SUBMIT_TO_MARKET_DEFAULT_CHANNEL' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) carrierChannel
        OUTER APPLY (SELECT TOP 1 ChannelCode = CASE WHEN ISJSON(COALESCE(SettingValue, DefaultValue)) = 1 AND JSON_VALUE(COALESCE(SettingValue, DefaultValue), '$[0]') IS NOT NULL THEN JSON_VALUE(COALESCE(SettingValue, DefaultValue), '$[0]') ELSE COALESCE(NULLIF(SettingValue, N''), NULLIF(DefaultValue, N'')) END FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = @CarrierId AND SettingCode = N'CARRIER_DELIVERY_CHANNEL_PRIORITY' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) deliveryChannel
        OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId IS NULL AND SettingCode = N'SUBMIT_TO_MARKET_DEFAULT_CHANNEL' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) defaultChannel
        OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = @CarrierId AND SettingCode = N'SUBMIT_TO_MARKET_EMAIL' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) carrierEmail
        OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = @CarrierId AND SettingCode = N'CARRIER_DELIVERY_EMAIL_TO' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) deliveryEmail
        OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = @CarrierId AND SettingCode = N'SUBMIT_TO_MARKET_PORTAL_URL' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) carrierPortal
        OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = @CarrierId AND SettingCode = N'CARRIER_DELIVERY_PORTAL_URL' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) deliveryPortal
        OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId IS NULL AND SettingCode = N'SUBMIT_TO_MARKET_DEFAULT_RECIPIENT' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) defaultRecipient
        OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId IS NULL AND SettingCode = N'SUBMIT_TO_MARKET_SUBJECT_TEMPLATE' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) subjectTemplate
        OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = @CarrierId AND SettingCode = N'CARRIER_DELIVERY_EMAIL_SUBJECT_TEMPLATE' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) deliverySubject
        OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId IS NULL AND SettingCode = N'SUBMIT_TO_MARKET_MAX_ATTEMPTS' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) maxAttempts;

        SET @DispatchChannelCode = CASE WHEN @DispatchChannelCode = N'CarrierApi' THEN N'API' ELSE @DispatchChannelCode END;
    END;

    DECLARE @DispatchSubject NVARCHAR(300);
    DECLARE @DispatchPayload NVARCHAR(MAX);

    SELECT @DispatchSubject = LEFT(REPLACE(@DispatchSubjectTemplate, N'{SubmissionNumber}', COALESCE(submission.SubmissionNumber, N'')), 300),
           @DispatchPayload = CONCAT(N'{',
               N'""tenantId"":""', CONVERT(NVARCHAR(36), @TenantId), N'"",',
               N'""submissionId"":""', CONVERT(NVARCHAR(36), @SubmissionId), N'"",',
               N'""submissionMarketId"":""', CONVERT(NVARCHAR(36), @MarketId), N'"",',
               N'""carrierId"":""', CONVERT(NVARCHAR(36), @CarrierId), N'"",',
               N'""submissionNumber"":""', STRING_ESCAPE(COALESCE(submission.SubmissionNumber, N''), 'json'), N'"",',
               N'""lineOfBusiness"":""', STRING_ESCAPE(COALESCE(submission.LineOfBusiness, N''), 'json'), N'"",',
               N'""notes"":""', STRING_ESCAPE(COALESCE(@Notes, N''), 'json'), N'"",',
               N'""documentIds"":', COALESCE(documentPayload.DocumentIdsJson, N'[]'),
            N'}')
    FROM Submissions.Submission submission
    OUTER APPLY
    (
        SELECT CONCAT(N'[', STRING_AGG(CONCAT(N'""', CONVERT(NVARCHAR(36), d.DocumentId), N'""'), N','), N']') AS DocumentIdsJson
        FROM Submissions.SubmissionMarketDocument d
        WHERE d.SubmissionMarketId = @MarketId
          AND d.IsDeleted = 0
    ) documentPayload
    WHERE submission.SubmissionId = @SubmissionId
      AND submission.TenantId = @TenantId
      AND submission.IsDeleted = 0;

    UPDATE existing
    SET DispatchChannelCode = @DispatchChannelCode,
        DispatchStatusCode = N'Pending',
        Recipient = @DispatchRecipient,
        Subject = @DispatchSubject,
        PayloadJson = COALESCE(@DispatchPayload, existing.PayloadJson),
        AttemptCount = 0,
        MaxAttemptCount = @DispatchMaxAttempts,
        NextAttemptDateUtc = SYSUTCDATETIME(),
        LockedDateUtc = NULL,
        LockedBy = NULL,
        LastAttemptDateUtc = NULL,
        CompletedDateUtc = NULL,
        LastError = NULL,
        ModifiedDateUtc = SYSUTCDATETIME()
    FROM Submissions.SubmissionMarketDispatch existing
    WHERE existing.SubmissionMarketId = @MarketId
      AND existing.IsDeleted = 0;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO Submissions.SubmissionMarketDispatch
            (SubmissionMarketDispatchId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, DispatchChannelCode, DispatchStatusCode, Recipient, Subject, PayloadJson, AttemptCount, MaxAttemptCount, NextAttemptDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES (NEWID(), @TenantId, @SubmissionId, @MarketId, @CarrierId, @DispatchChannelCode, N'Pending', @DispatchRecipient, @DispatchSubject, COALESCE(@DispatchPayload, N'{}'), 0, @DispatchMaxAttempts, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, 0);
    END;
END;

UPDATE Submissions.Submission
SET Status = CASE WHEN Status IN (N'Bound', N'Lost', N'Cancelled', N'Closed') THEN Status ELSE N'Marketing' END,
    MarketCount = (SELECT COUNT(1) FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'MarketSubmitted', COALESCE(@Notes, N'Submitted to market.'), SYSUTCDATETIME(), N'SubmissionMarket', @MarketId, N'User', 0);

SELECT @MarketId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        var marketId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { SubmissionId = id, request.TenantId, CarrierIdIn = request.CarrierId, request.Notes }, cancellationToken: cancellationToken));
        await RecordOpportunityWorkflowAsync(cn, id, request.TenantId, "Marketing", "Market Submitted", "Market Submitted", request.Notes ?? "Submission package sent to market.", "SubmissionMarket", marketId, null, cancellationToken);
        return new SubmissionActionResult(marketId, "Submission sent to market.");
    }

    public async Task<SubmissionActionResult> RequestQuoteAsync(Guid id, RequestSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var readiness = await GetReadinessAsync(id, request.TenantId, cancellationToken);
        if (!readiness.IsReadyForMarketing)
        {
            const string validationSql = @"
DECLARE @CarrierId UNIQUEIDENTIFIER = COALESCE(
    (SELECT CarrierId FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND SubmissionId = @SubmissionId AND IsDeleted = 0),
    @CarrierIdIn,
    (SELECT TOP 1 CarrierId FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND IsDeleted = 0 ORDER BY IsRecommended DESC, AddedDateUtc DESC),
    (SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CarrierName));
IF @CarrierId IS NULL THROW 52001, 'No carrier is available for quote request.', 1;

DECLARE @MarketId UNIQUEIDENTIFIER = (SELECT TOP 1 SubmissionMarketId FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND SubmissionId = @SubmissionId AND IsDeleted = 0);
IF @MarketId IS NULL
    SET @MarketId = (SELECT TOP 1 SubmissionMarketId FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND CarrierId = @CarrierId AND IsDeleted = 0 ORDER BY IsRecommended DESC, AddedDateUtc DESC);

IF @MarketId IS NULL
BEGIN
    SET @MarketId = NEWID();
    INSERT INTO Submissions.SubmissionMarket
        (SubmissionMarketId, SubmissionId, CarrierId, Status, AppetiteScore, IsRecommended, AddedDateUtc, IsDeleted, TenantId, Notes)
    VALUES
        (@MarketId, @SubmissionId, @CarrierId, N'Blocked', 0, 0, SYSUTCDATETIME(), 0, @TenantId, N'Quote request validation required.');
END
ELSE
BEGIN
    UPDATE Submissions.SubmissionMarket
    SET Status = N'Blocked',
        Notes = COALESCE(NULLIF(@BlockingReasons, N''), Notes),
        TenantId = COALESCE(TenantId, @TenantId),
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0;
END;

DECLARE @RequestVersion INT = COALESCE((SELECT MAX(RequestVersion) FROM Submissions.QuoteRequest WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0), (SELECT MAX(RequestVersion) FROM Submissions.QuoteRequestHistory WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0), 0) + 1;
DECLARE @QuoteRequestId UNIQUEIDENTIFIER = NEWID();
DECLARE @MethodCode NVARCHAR(50) = COALESCE(NULLIF(@QuoteRequestMethodCode, N''), NULLIF((SELECT TOP 1 SubmissionMethodCode FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0), N''), N'ManualUnderwriter');
DECLARE @ScopeCode NVARCHAR(50) = COALESCE(NULLIF(@QuoteRequestScopeCode, N''), N'Package');

INSERT INTO Submissions.QuoteRequest
    (QuoteRequestId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, QuoteRequestActionCode, QuoteRequestReasonCode, QuoteRequestMethodCode, QuoteRequestScopeCode,
     RequestedPremium, CoverageNotes, DeliveryMethodCode, AssignedUnderwriterName, AssignedUnderwriterEmail, AssignedUnderwriterPhone, DueDateUtc, CorrelationId,
     RequestVersion, StatusCode, RequestedDateUtc, RequestedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@QuoteRequestId, @TenantId, @SubmissionId, @MarketId, @CarrierId, N'InitialRequest', N'MissingInformation', @MethodCode, @ScopeCode,
     @AnnualPremium, @BlockingReasons, @MethodCode,
     (SELECT TOP 1 UnderwriterName FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0),
     (SELECT TOP 1 UnderwriterEmail FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0),
     (SELECT TOP 1 UnderwriterPhone FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0),
     (SELECT TOP 1 DueDateUtc FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0),
     CONCAT(N'QR-', CONVERT(NVARCHAR(36), @QuoteRequestId)), @RequestVersion, N'ValidationRequired', SYSUTCDATETIME(), @RequestedByUserId, SYSUTCDATETIME(), @RequestedByUserId, 0);

INSERT INTO Submissions.QuoteRequestHistory
    (QuoteRequestHistoryId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, QuoteRequestActionCode, QuoteRequestReasonCode, QuoteRequestMethodCode, QuoteRequestScopeCode,
     RequestedPremium, CoverageNotes, RequestVersion, StatusCode, RequestedDateUtc, RequestedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), @TenantId, @SubmissionId, @MarketId, @CarrierId, N'InitialRequest', N'MissingInformation', @MethodCode, @ScopeCode,
     @AnnualPremium, @BlockingReasons, @RequestVersion, N'ValidationRequired', SYSUTCDATETIME(), @RequestedByUserId, SYSUTCDATETIME(), @RequestedByUserId, 0);

UPDATE Submissions.Submission
SET Status = CASE WHEN Status IN (N'Draft', N'In Progress', N'Ready for Marketing', N'Marketing') THEN N'Ready for Marketing' ELSE Status END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @RequestedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'QuoteRequestValidationRequired', @BlockingReasons, SYSUTCDATETIME(), @RequestedByUserId, N'QuoteRequest', @QuoteRequestId, N'User', 0);

SELECT @QuoteRequestId;";

            using var validationCn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await EnsureEnterpriseWorkflowSchemaAsync(validationCn, request.TenantId, cancellationToken);
            var validationQuoteRequestId = await validationCn.ExecuteScalarAsync<Guid>(new CommandDefinition(validationSql, new
            {
                SubmissionId = id,
                request.TenantId,
                request.SubmissionMarketId,
                CarrierIdIn = request.CarrierId,
                request.AnnualPremium,
                request.RequestedByUserId,
                request.QuoteRequestScopeCode,
                request.QuoteRequestMethodCode,
                BlockingReasons = string.Join("; ", readiness.BlockingReasons)
            }, cancellationToken: cancellationToken));
            await RecordOpportunityWorkflowAsync(validationCn, id, request.TenantId, "Ready for Marketing", "Quote Request Validation Required", "Quote Request Validation Required", string.Join("; ", readiness.BlockingReasons), "QuoteRequest", validationQuoteRequestId, request.RequestedByUserId, cancellationToken);
            return new SubmissionActionResult(validationQuoteRequestId, "Quote request validation required: " + string.Join("; ", readiness.BlockingReasons));
        }

        const string sql = @"
DECLARE @CarrierId UNIQUEIDENTIFIER = COALESCE(
    (SELECT CarrierId FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND SubmissionId = @SubmissionId AND IsDeleted = 0),
    @CarrierIdIn,
    (SELECT TOP 1 CarrierId FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND IsDeleted = 0 ORDER BY IsRecommended DESC, AddedDateUtc DESC),
    (SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CarrierName));
IF @CarrierId IS NULL THROW 52001, 'No carrier is available for quote request.', 1;

DECLARE @ScopeCode NVARCHAR(50) = COALESCE(NULLIF(@QuoteRequestScopeCode, N''), N'Package');
DECLARE @RequestedActionCode NVARCHAR(50) = NULLIF(@QuoteRequestActionCode, N'');
DECLARE @RequestedReasonCode NVARCHAR(80) = NULLIF(@QuoteRequestReasonCode, N'');
DECLARE @RequestedMethodCode NVARCHAR(50) = NULLIF(@QuoteRequestMethodCode, N'');

IF NOT EXISTS (SELECT 1 FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 52002, 'Submission was not found for quote request.', 1;

DECLARE @SubmissionStatus NVARCHAR(50) = (SELECT TOP 1 Status FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0);
IF EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'QuoteRequestBlockedStatus' AND IsActive = 1 AND IsDeleted = 0 AND OptionCode = @SubmissionStatus)
    THROW 52023, 'Quote request is blocked because the submission is in a terminal status.', 1;

DECLARE @RequestedLines TABLE
(
    SubmissionLineId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    LineOfBusiness NVARCHAR(100) NOT NULL,
    TargetPremium DECIMAL(18,2) NOT NULL
);

INSERT INTO @RequestedLines (SubmissionLineId, LineOfBusiness, TargetPremium)
SELECT sl.SubmissionLineId, sl.LineOfBusiness, sl.TargetPremium
FROM Submissions.SubmissionLine sl
WHERE sl.SubmissionId = @SubmissionId
  AND sl.TenantId = @TenantId
  AND sl.IsDeleted = 0
  AND EXISTS
  (
      SELECT 1
      FROM STRING_SPLIT(COALESCE(@SubmissionLineIdsCsv, N''), N',') selected
      WHERE TRY_CONVERT(uniqueidentifier, selected.value) = sl.SubmissionLineId
  );

IF NOT EXISTS (SELECT 1 FROM @RequestedLines)
BEGIN
    INSERT INTO @RequestedLines (SubmissionLineId, LineOfBusiness, TargetPremium)
    SELECT sl.SubmissionLineId, sl.LineOfBusiness, sl.TargetPremium
    FROM Submissions.SubmissionLine sl
    WHERE sl.SubmissionId = @SubmissionId
      AND sl.TenantId = @TenantId
      AND sl.IsDeleted = 0;
END;

IF @ScopeCode = N'SingleLine' AND (SELECT COUNT(1) FROM @RequestedLines) > 1
    THROW 52022, 'Single-line quote request must contain exactly one submission line.', 1;

DECLARE @RequestedPremium DECIMAL(18,2) = COALESCE(NULLIF((SELECT SUM(TargetPremium) FROM @RequestedLines), 0), @AnnualPremium, 0);
DECLARE @LineOfBusiness NVARCHAR(100) = (SELECT TOP 1 LineOfBusiness FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0);

DECLARE @MarketId UNIQUEIDENTIFIER = (SELECT TOP 1 SubmissionMarketId FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND SubmissionId = @SubmissionId AND IsDeleted = 0);

IF @MarketId IS NULL
    SET @MarketId = (SELECT TOP 1 SubmissionMarketId FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND CarrierId = @CarrierId AND IsDeleted = 0 ORDER BY IsRecommended DESC, AddedDateUtc DESC);

IF @MarketId IS NULL
BEGIN
    SET @MarketId = NEWID();
    INSERT INTO Submissions.SubmissionMarket
        (SubmissionMarketId, SubmissionId, CarrierId, Status, AppetiteScore, IsRecommended, AddedDateUtc, IsDeleted, TenantId, Notes)
    VALUES
        (@MarketId, @SubmissionId, @CarrierId, N'In Review', 65, 0, SYSUTCDATETIME(), 0, @TenantId, N'Added from current market quote request.');
END;

DECLARE @MarketStatus NVARCHAR(50) = (SELECT TOP 1 Status FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0);
IF EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'QuoteRequestBlockedStatus' AND IsActive = 1 AND IsDeleted = 0 AND OptionCode = @MarketStatus)
    THROW 52024, 'Quote request is blocked because the selected market is in a terminal status.', 1;

DECLARE @MethodCode NVARCHAR(50) = COALESCE(
    @RequestedMethodCode,
    NULLIF((SELECT TOP 1 SubmissionMethodCode FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0), N''),
    CASE WHEN @LineOfBusiness LIKE N'%Auto%' OR @LineOfBusiness LIKE N'%Home%' OR @LineOfBusiness LIKE N'%Rent%' OR @LineOfBusiness LIKE N'%Personal%' THEN N'ApiRating' ELSE N'ManualUnderwriter' END);

SET @MethodCode = CASE
    WHEN @MethodCode IN (N'API', N'CarrierApi') THEN N'ApiRating'
    WHEN @MethodCode = N'Portal' THEN N'MgaPortal'
    WHEN @MethodCode IN (N'Manual', N'InternalQueue', N'Download') THEN N'ManualUnderwriter'
    ELSE @MethodCode END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'QuoteRequestMethod' AND OptionCode = @MethodCode AND IsActive = 1 AND IsDeleted = 0)
    THROW 52033, 'Quote request method is not configured for this tenant.', 1;

DECLARE @TransmissionChannelCode NVARCHAR(50) = CASE @MethodCode
    WHEN N'ApiRating' THEN N'API'
    WHEN N'MgaPortal' THEN N'Portal'
    WHEN N'Email' THEN N'Email'
    ELSE N'InternalQueue' END;

DECLARE @TransmissionStatusCode NVARCHAR(50) = CASE @MethodCode
    WHEN N'ApiRating' THEN N'AwaitingExternalConnector'
    WHEN N'ManualUnderwriter' THEN N'Queued'
    ELSE N'Queued' END;

DECLARE @ActiveQuoteRequestStatuses TABLE (StatusCode NVARCHAR(50) NOT NULL PRIMARY KEY);
INSERT INTO @ActiveQuoteRequestStatuses (StatusCode)
VALUES (N'Open'), (N'PendingDispatch'), (N'Submitted'), (N'Acknowledged'), (N'UnderReview'), (N'MoreInformationRequired'), (N'Referred');

DECLARE @HasPriorRequest BIT = CASE WHEN EXISTS (SELECT 1 FROM Submissions.QuoteRequest WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0) OR EXISTS (SELECT 1 FROM Submissions.QuoteRequestHistory WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0) THEN 1 ELSE 0 END;
DECLARE @HasOpenRequest BIT = CASE WHEN EXISTS (SELECT 1 FROM Submissions.QuoteRequest qr WHERE qr.SubmissionMarketId = @MarketId AND qr.IsDeleted = 0 AND EXISTS (SELECT 1 FROM @ActiveQuoteRequestStatuses active WHERE active.StatusCode = qr.StatusCode)) THEN 1 ELSE 0 END;
DECLARE @HasQuoteResponse BIT = CASE WHEN EXISTS (SELECT 1 FROM Submissions.Quote WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0 AND (QuoteReceivedDateUtc IS NOT NULL OR Status IN (N'Received', N'Presented', N'Accepted', N'Bound', N'Selected'))) THEN 1 ELSE 0 END;

DECLARE @ActionCode NVARCHAR(50) = COALESCE(@RequestedActionCode, CASE WHEN @HasQuoteResponse = 1 THEN N'RequestRevision' WHEN @HasPriorRequest = 1 THEN N'ResendUpdate' ELSE N'InitialRequest' END);
IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'QuoteRequestAction' AND OptionCode = @ActionCode AND IsActive = 1 AND IsDeleted = 0)
    THROW 52025, 'Quote request action is not configured for this tenant.', 1;

IF @ActionCode = N'InitialRequest' AND @HasPriorRequest = 1
    THROW 52026, 'An initial quote request already exists for this market. Use resend/update or request revision.', 1;
IF @ActionCode = N'ResendUpdate' AND @HasPriorRequest = 0
    THROW 52027, 'Cannot resend a quote request before an initial request exists.', 1;
IF @ActionCode = N'RequestRevision' AND @HasQuoteResponse = 0
    THROW 52028, 'Cannot request a revision until a quote response has been received.', 1;
IF @ActionCode = N'ResendUpdate' AND @HasOpenRequest = 0
    THROW 52029, 'Cannot resend or update because there is no open quote request for this market.', 1;
IF @ActionCode <> N'ResendUpdate' AND @HasOpenRequest = 1
    THROW 52030, 'A quote request is already open for this market. Use resend/update instead of creating a duplicate request.', 1;
IF @ActionCode <> N'InitialRequest' AND @RequestedReasonCode IS NULL
    THROW 52031, 'A reason is required when resending or requesting a quote revision.', 1;
IF @RequestedReasonCode IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'QuoteRequestReason' AND OptionCode = @RequestedReasonCode AND IsActive = 1 AND IsDeleted = 0)
    THROW 52032, 'Quote request reason is not configured for this tenant.', 1;

IF @ActionCode = N'RequestRevision'
BEGIN
    UPDATE Submissions.QuoteRequest
    SET StatusCode = N'Closed', ClosedDateUtc = COALESCE(ClosedDateUtc, SYSUTCDATETIME()), ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @RequestedByUserId
    WHERE SubmissionMarketId = @MarketId
      AND IsDeleted = 0
      AND EXISTS (SELECT 1 FROM @ActiveQuoteRequestStatuses active WHERE active.StatusCode = Submissions.QuoteRequest.StatusCode);

    UPDATE Submissions.QuoteRequestHistory
    SET StatusCode = N'Closed', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @RequestedByUserId
    WHERE SubmissionMarketId = @MarketId
      AND StatusCode = N'Open'
      AND IsDeleted = 0;
END;

DECLARE @RequestVersion INT = COALESCE((SELECT MAX(RequestVersion) FROM Submissions.QuoteRequest WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0), (SELECT MAX(RequestVersion) FROM Submissions.QuoteRequestHistory WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0), 0) + 1;
DECLARE @QuoteRequestId UNIQUEIDENTIFIER = NEWID();

UPDATE Submissions.SubmissionMarket
SET Status = CASE WHEN EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'QuoteRequestBlockedStatus' AND OptionCode = Status AND IsActive = 1 AND IsDeleted = 0) THEN Status ELSE N'In Review' END,
    SubmittedDateUtc = COALESCE(SubmittedDateUtc, SYSUTCDATETIME()),
    SubmittedByUserId = COALESCE(SubmittedByUserId, @RequestedByUserId),
    Notes = COALESCE(@CoverageNotes, Notes),
    DueDateUtc = COALESCE(DueDateUtc, DATEADD(day, 14, SYSUTCDATETIME())),
    RequestedCoverageSummary = COALESCE(NULLIF(@CoverageNotes, N''), RequestedCoverageSummary),
    RequestedLimits = COALESCE(RequestedLimits, CONCAT(N'Deductible: ', COALESCE(CONVERT(nvarchar(50), @Deductible), N'Not specified'), N'; Limit: ', COALESCE(CONVERT(nvarchar(50), @Limit), N'Not specified'))),
    SubmissionMethodCode = @MethodCode,
    QuoteRequestScopeCode = @ScopeCode,
    RequestedPremium = @RequestedPremium,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0;

UPDATE Submissions.SubmissionMarketLine
SET IsDeleted = 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @RequestedByUserId
WHERE SubmissionMarketId = @MarketId
  AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionMarketLine
    (SubmissionMarketLineId, TenantId, SubmissionMarketId, SubmissionId, SubmissionLineId, LineOfBusiness, TargetPremium, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @TenantId, @MarketId, @SubmissionId, line.SubmissionLineId, line.LineOfBusiness, line.TargetPremium, SYSUTCDATETIME(), @RequestedByUserId, 0
FROM @RequestedLines line;

UPDATE Submissions.Submission
SET TargetPremium = COALESCE(TargetPremium, @RequestedPremium),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, @ActionCode, COALESCE(@CoverageNotes, N'Quote requested.'), SYSUTCDATETIME(), N'QuoteRequest', @QuoteRequestId, N'User', 0);

INSERT INTO Submissions.QuoteRequest
    (QuoteRequestId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, QuoteRequestActionCode, QuoteRequestReasonCode, QuoteRequestMethodCode, QuoteRequestScopeCode,
     RequestedPremium, CoverageNotes, CarrierReferenceNumber, DeliveryMethodCode, AssignedUnderwriterName, AssignedUnderwriterEmail, AssignedUnderwriterPhone, DueDateUtc, CorrelationId,
     RequestVersion, StatusCode, RequestedDateUtc, RequestedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@QuoteRequestId, @TenantId, @SubmissionId, @MarketId, @CarrierId, @ActionCode, @RequestedReasonCode, @MethodCode, @ScopeCode,
     @RequestedPremium, @CoverageNotes, NULLIF(@CarrierReferenceNumber, N''), @TransmissionChannelCode,
     (SELECT TOP 1 UnderwriterName FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0),
     (SELECT TOP 1 UnderwriterEmail FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0),
     (SELECT TOP 1 UnderwriterPhone FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0),
     (SELECT TOP 1 DueDateUtc FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0),
     CONCAT(N'QR-', CONVERT(NVARCHAR(36), @QuoteRequestId)), @RequestVersion, N'PendingDispatch', SYSUTCDATETIME(), @RequestedByUserId, SYSUTCDATETIME(), @RequestedByUserId, 0);

IF OBJECT_ID(N'Submissions.SubmissionMarketDispatch', N'U') IS NOT NULL
BEGIN
    DECLARE @DispatchId UNIQUEIDENTIFIER = COALESCE(
        (SELECT TOP 1 SubmissionMarketDispatchId
         FROM Submissions.SubmissionMarketDispatch
         WHERE SubmissionMarketId = @MarketId
           AND JSON_VALUE(PayloadJson, '$.quoteRequestId') = CONVERT(NVARCHAR(36), @QuoteRequestId)
           AND IsDeleted = 0),
        NEWID());

    DECLARE @DispatchRecipient NVARCHAR(500) = NULL;
    DECLARE @DispatchEndpointUri NVARCHAR(1000) = NULL;
    SELECT @DispatchRecipient = CASE WHEN @TransmissionChannelCode = N'Email' THEN COALESCE(NULLIF(carrierEmail.SettingValue, N''), NULLIF(carrierEmail.DefaultValue, N'')) WHEN @TransmissionChannelCode = N'Portal' THEN COALESCE(NULLIF(carrierPortal.SettingValue, N''), NULLIF(carrierPortal.DefaultValue, N'')) ELSE NULL END,
           @DispatchEndpointUri = CASE WHEN @TransmissionChannelCode = N'Portal' THEN COALESCE(NULLIF(carrierPortal.SettingValue, N''), NULLIF(carrierPortal.DefaultValue, N'')) ELSE connector.EndpointUri END
    FROM (SELECT 1 AS Seed) seed
    OUTER APPLY (SELECT TOP 1 CarrierExternalConnectorId, EndpointUri FROM Agency.CarrierExternalConnector WHERE TenantId = @TenantId AND (CarrierId = @CarrierId OR CarrierId IS NULL) AND IsActive = 1 AND IsDeleted = 0 AND (DefaultChannelCode = @TransmissionChannelCode OR ConnectorTypeCode = @TransmissionChannelCode OR (@MethodCode = N'ApiRating' AND ConnectorTypeCode IN (N'RatingApi', N'CarrierApi', N'API'))) ORDER BY CASE WHEN CarrierId = @CarrierId THEN 0 ELSE 1 END, SortOrder) connector
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = @CarrierId AND SettingCode IN (N'CARRIER_DELIVERY_EMAIL_TO', N'SUBMIT_TO_MARKET_EMAIL') AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) carrierEmail
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = @CarrierId AND SettingCode IN (N'CARRIER_DELIVERY_PORTAL_URL', N'SUBMIT_TO_MARKET_PORTAL_URL') AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) carrierPortal;

    DECLARE @DispatchSubject NVARCHAR(300) = (SELECT TOP 1 CONCAT(N'Quote request ', @RequestVersion, N' for ', COALESCE(SubmissionNumber, N'')) FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0);
    DECLARE @DispatchPayload NVARCHAR(MAX) = CONCAT(N'{',
        N'""tenantId"":""', CONVERT(NVARCHAR(36), @TenantId), N'"",',
        N'""submissionId"":""', CONVERT(NVARCHAR(36), @SubmissionId), N'"",',
        N'""submissionMarketId"":""', CONVERT(NVARCHAR(36), @MarketId), N'"",',
        N'""carrierId"":""', CONVERT(NVARCHAR(36), @CarrierId), N'"",',
        N'""quoteRequestId"":""', CONVERT(NVARCHAR(36), @QuoteRequestId), N'"",',
        N'""quoteRequestMethodCode"":""', @MethodCode, N'"",',
        N'""quoteRequestActionCode"":""', @ActionCode, N'"",',
        N'""lineOfBusiness"":""', STRING_ESCAPE(COALESCE(@LineOfBusiness, N''), 'json'), N'"",',
        N'""requestedPremium"":', CONVERT(NVARCHAR(50), COALESCE(@RequestedPremium, 0)),
    N'}');

    UPDATE existing
    SET DispatchChannelCode = @TransmissionChannelCode,
        DispatchStatusCode = N'Pending',
        Recipient = @DispatchRecipient,
        Subject = @DispatchSubject,
        PayloadJson = @DispatchPayload,
        AttemptCount = 0,
        MaxAttemptCount = 5,
        NextAttemptDateUtc = SYSUTCDATETIME(),
        LockedDateUtc = NULL,
        LockedBy = NULL,
        LastAttemptDateUtc = NULL,
        CompletedDateUtc = NULL,
        LastError = NULL,
        ModifiedDateUtc = SYSUTCDATETIME()
    FROM Submissions.SubmissionMarketDispatch existing
    WHERE existing.SubmissionMarketDispatchId = @DispatchId
      AND existing.IsDeleted = 0;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO Submissions.SubmissionMarketDispatch
            (SubmissionMarketDispatchId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, DispatchChannelCode, DispatchStatusCode, Recipient, Subject, PayloadJson, AttemptCount, MaxAttemptCount, NextAttemptDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES (@DispatchId, @TenantId, @SubmissionId, @MarketId, @CarrierId, @TransmissionChannelCode, N'Pending', @DispatchRecipient, @DispatchSubject, @DispatchPayload, 0, 5, SYSUTCDATETIME(), SYSUTCDATETIME(), @RequestedByUserId, 0);
    END;
END;

INSERT INTO Submissions.QuoteRequestHistory
    (QuoteRequestHistoryId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, QuoteRequestActionCode, QuoteRequestReasonCode, QuoteRequestMethodCode, QuoteRequestScopeCode, RequestedPremium, CoverageNotes, RequestVersion, StatusCode, RequestedDateUtc, RequestedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), @TenantId, @SubmissionId, @MarketId, @CarrierId, @ActionCode, @RequestedReasonCode, @MethodCode, @ScopeCode, @RequestedPremium, @CoverageNotes, @RequestVersion, N'PendingDispatch', SYSUTCDATETIME(), @RequestedByUserId, SYSUTCDATETIME(), @RequestedByUserId, 0);

SELECT @MarketId AS MarketId, @ActionCode AS ActionCode, @QuoteRequestId AS QuoteRequestId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        var result = await cn.QuerySingleAsync<(Guid MarketId, string ActionCode, Guid QuoteRequestId)>(new CommandDefinition(sql, new { SubmissionId = id, request.TenantId, request.SubmissionMarketId, CarrierIdIn = request.CarrierId, request.AnnualPremium, request.Deductible, request.Limit, request.CoverageNotes, request.RequestedByUserId, request.CarrierReferenceNumber, request.QuoteRequestScopeCode, request.QuoteRequestActionCode, request.QuoteRequestReasonCode, request.QuoteRequestMethodCode, SubmissionLineIdsCsv = string.Join(',', request.SubmissionLineIds ?? []) }, cancellationToken: cancellationToken));
        await cn.ExecuteAsync(new CommandDefinition(RecalculateSubmissionStatusSql, new { SubmissionId = id, request.TenantId }, cancellationToken: cancellationToken));
        var actionTitle = result.ActionCode switch
        {
            "RequestRevision" => "Quote Revision Requested",
            "ResendUpdate" => "Quote Request Updated",
            _ => "Quote Requested"
        };
        await RecordOpportunityWorkflowAsync(cn, id, request.TenantId, "Marketing", actionTitle, actionTitle, request.CoverageNotes ?? "Quote requested from market.", "QuoteRequest", result.QuoteRequestId, null, cancellationToken);
        var message = result.ActionCode switch
        {
            "RequestRevision" => "Quote revision requested from market.",
            "ResendUpdate" => "Quote request updated and resent to market.",
            _ => "Quote requested from market."
        };
        return new SubmissionActionResult(result.QuoteRequestId, message);
    }

    public async Task<SubmissionActionResult> CopyAsync(Guid id, CopySubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NewSubmissionId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Submissions.Submission (SubmissionId, TenantId, AccountId, OpportunityId, SubmissionNumber, LineOfBusiness, Status, Priority, AssignedToUserId, EffectiveDate, ExpirationDate, TargetPremium, MarketCount, QuoteCount, CreatedDateUtc, IsDeleted)
SELECT @NewSubmissionId, TenantId, AccountId, OpportunityId,
       N'SUB-' + CONVERT(NVARCHAR(8), SYSUTCDATETIME(), 112) + N'-' + RIGHT(REPLACE(CONVERT(NVARCHAR(36), @NewSubmissionId), N'-', N''), 6),
       COALESCE(NULLIF(@LineOfBusiness, N''), LineOfBusiness),
       N'In Progress',
       COALESCE(NULLIF(@Priority, N''), Priority),
       AssignedToUserId,
       COALESCE(@EffectiveDate, DATEADD(year, 1, EffectiveDate)),
       DATEADD(year, 1, COALESCE(@EffectiveDate, DATEADD(year, 1, EffectiveDate))),
       TargetPremium,
       0,
       0,
       SYSUTCDATETIME(),
       0
FROM Submissions.Submission
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

IF @@ROWCOUNT = 0 THROW 52002, 'Submission was not found for copy.', 1;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @NewSubmissionId, @TenantId, N'Copy', N'Copied from source submission.', SYSUTCDATETIME(), 0);

SELECT @NewSubmissionId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var newId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { SubmissionId = id, request.TenantId, request.EffectiveDate, request.LineOfBusiness, request.Priority }, cancellationToken: cancellationToken));
        return new SubmissionActionResult(newId, "Submission copied.");
    }

    public async Task<SubmissionActionResult> DeclineAsync(Guid id, DeclineSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.Submission
SET Status = N'Lost', ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

IF @@ROWCOUNT = 0 THROW 52003, 'Submission was not found for decline.', 1;

UPDATE Submissions.SubmissionMarket
SET Status = CASE WHEN Status IN (N'Bound', N'Declined') THEN Status ELSE N'Declined' END,
    DeclineReason = COALESCE(NULLIF(DeclineReason, N''), @Reason),
    RespondedDateUtc = COALESCE(RespondedDateUtc, SYSUTCDATETIME())
WHERE SubmissionId = @SubmissionId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'Decline', @Reason, SYSUTCDATETIME(), 0);

SELECT @SubmissionId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var declinedId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { SubmissionId = id, request.TenantId, request.Reason }, cancellationToken: cancellationToken));
        await RecordOpportunityWorkflowAsync(cn, id, request.TenantId, "Lost", "Submission Declined", "Submission Declined", request.Reason, "Submission", id, null, cancellationToken);
        return new SubmissionActionResult(declinedId, "Submission declined.");
    }

    public async Task<SubmissionActionResult> CreatePolicyAsync(Guid id, CreatePolicyFromSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var submission = await GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Submission was not found for policy creation.");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        await EnsurePolicyCreationSourceSchemaAsync(cn, request.TenantId, cancellationToken);

        var source = await GetPolicyCreationSourceSettingsAsync(cn, request.TenantId, request.PolicySourceCode, cancellationToken);
        var sourceCode = source.SourceCode;
        var sourceReason = Normalize(request.PolicySourceReason);
        var sourceNotes = Normalize(request.PolicySourceNotes);
        var policyNumber = Normalize(request.PolicyNumber);
        var effectiveDate = request.EffectiveDate ?? submission.EffectiveDate;
        var expirationDate = request.ExpirationDate ?? submission.ExpirationDate;
        if (expirationDate <= effectiveDate)
        {
            throw new InvalidOperationException("Policy expiration date must be after the effective date.");
        }

        if (source.RequiresReason && string.IsNullOrWhiteSpace(sourceReason))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a reason.");
        }

        if (source.RequiresPolicyNumber && string.IsNullOrWhiteSpace(policyNumber))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a policy number.");
        }

        QuoteComparisonDto? quote = null;
        if (request.QuoteId.HasValue)
        {
            quote = await GetQuoteByIdAsync(request.QuoteId.Value, request.TenantId, cancellationToken);
            if (quote is null || quote.SubmissionId != id)
            {
                throw new InvalidOperationException("Selected quote was not found for this submission.");
            }
        }
        else if (source.RequiresQuote)
        {
            var quotes = await GetQuoteComparisonAsync(id, request.TenantId, cancellationToken);
            quote = quotes
                .Where(q => q.Status is "Selected" or "Bound")
                .OrderByDescending(q => q.Status == "Selected")
                .ThenByDescending(q => q.IsSelected)
                .ThenByDescending(q => q.AnnualPremium)
                .FirstOrDefault();

            if (quote is null)
            {
                throw new InvalidOperationException("Create Policy with Quote Bound requires a customer-selected quote. Use a non-quote policy source and provide a reason for direct policy creation.");
            }
        }

        var carrierId = request.CarrierId ?? quote?.CarrierId;
        if (!carrierId.HasValue)
        {
            carrierId = await cn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(@"
SELECT TOP 1 CarrierId
FROM Submissions.SubmissionMarket
WHERE SubmissionId = @SubmissionId AND IsDeleted = 0
ORDER BY IsRecommended DESC, AddedDateUtc DESC;", new { SubmissionId = id }, cancellationToken: cancellationToken));
        }

        if (!carrierId.HasValue)
        {
            throw new InvalidOperationException("Create Policy requires a carrier. Select a carrier market or add a market before creating the policy.");
        }

        var annualPremium = request.AnnualPremium ?? quote?.AnnualPremium ?? submission.TargetPremium;
        if (annualPremium is null or <= 0)
        {
            throw new InvalidOperationException("Create Policy requires an annual premium greater than zero.");
        }

        if (source.RequiresQuote && quote is not null)
        {
            if (!quote.IsSelected && quote.Status is not ("Selected" or "Bound"))
            {
                throw new InvalidOperationException("Create Policy with Quote Bound requires a selected quote as customer authorization evidence.");
            }

            if (!quote.IsBindable)
            {
                throw new InvalidOperationException("Create Policy with Quote Bound requires a quote marked as bindable after internal review.");
            }
        }

        var quoteId = quote?.QuoteId ?? Guid.Empty;
        var bindStatusCode = source.RequiresQuote ? "Draft" : "Bound";
        var resultId = await BindPolicyAsync(new BindPolicyRequest(id, quoteId, request.TenantId, submission.AccountId, carrierId.Value, annualPremium.Value, effectiveDate, expirationDate, policyNumber, sourceCode, sourceReason, sourceNotes, BindStatusCode: bindStatusCode, ProposalId: request.ProposalId, CustomerAuthorizationId: request.CustomerAuthorizationId, CustomerAuthorizationMethodCode: request.CustomerAuthorizationMethodCode, CustomerAuthorizationReference: request.CustomerAuthorizationReference, CustomerAuthorizationNotes: request.CustomerAuthorizationNotes, CustomerAuthorizedByName: request.CustomerAuthorizedByName, CustomerAuthorizedDateUtc: request.CustomerAuthorizedDateUtc, CustomerAuthorizationDocumentId: request.CustomerAuthorizationDocumentId), cancellationToken);
        var message = source.RequiresQuote ? "Bind request created from selected quote; policy will be created after carrier confirmation." : $"Policy created using {source.SourceName}.";
        return new SubmissionActionResult(resultId, message);
    }

    private sealed record PolicyCreationSourceSettings(string SourceCode, string SourceName, bool RequiresQuote, bool RequiresSubmission, bool RequiresAccount, bool RequiresReason, bool RequiresPolicyNumber, bool AllowsDirectPolicyEntry, bool IsImportSource, bool IsConversionSource);

    private sealed record PolicyBindStatusSettings(string StatusCode, string StatusName, bool IsTerminal, bool CreatesPolicy);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task EnsurePolicyCreationSourceSchemaAsync(System.Data.IDbConnection connection, Guid tenantId, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'Submissions.BoundPolicy', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Submissions.BoundPolicy', N'PolicySourceCode') IS NULL ALTER TABLE Submissions.BoundPolicy ADD PolicySourceCode NVARCHAR(50) NOT NULL CONSTRAINT DF_BoundPolicy_PolicySourceCode_Runtime DEFAULT N'QuoteBound';
    IF COL_LENGTH(N'Submissions.BoundPolicy', N'PolicySourceReason') IS NULL ALTER TABLE Submissions.BoundPolicy ADD PolicySourceReason NVARCHAR(500) NULL;
    IF COL_LENGTH(N'Submissions.BoundPolicy', N'PolicySourceNotes') IS NULL ALTER TABLE Submissions.BoundPolicy ADD PolicySourceNotes NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'Submissions.BoundPolicy', N'PolicyBindTransactionId') IS NULL ALTER TABLE Submissions.BoundPolicy ADD PolicyBindTransactionId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Submissions.BoundPolicy', N'IssueStatus') IS NULL ALTER TABLE Submissions.BoundPolicy ADD IssueStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_BoundPolicy_IssueStatus_Runtime DEFAULT N'PendingIssue';
    IF COL_LENGTH(N'Submissions.BoundPolicy', N'CoverageStatus') IS NULL ALTER TABLE Submissions.BoundPolicy ADD CoverageStatus NVARCHAR(50) NOT NULL CONSTRAINT DF_BoundPolicy_CoverageStatus_Runtime DEFAULT N'Bound';
    IF COL_LENGTH(N'Submissions.BoundPolicy', N'IssuedDateUtc') IS NULL ALTER TABLE Submissions.BoundPolicy ADD IssuedDateUtc DATETIME2 NULL;
    UPDATE Submissions.BoundPolicy
    SET IssueStatus = CASE WHEN Status IN (N'Issued', N'Active') THEN N'Issued' ELSE COALESCE(NULLIF(IssueStatus, N''), N'PendingIssue') END,
        CoverageStatus = CASE WHEN Status IN (N'Cancelled', N'Expired', N'Non-Renewed') THEN Status ELSE COALESCE(NULLIF(CoverageStatus, N''), N'Bound') END
    WHERE IsDeleted = 0;
END;

IF OBJECT_ID(N'Submissions.PolicyCreationSource', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.PolicyCreationSource
    (
        PolicyCreationSourceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_PolicyCreationSource_Runtime PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SourceCode NVARCHAR(50) NOT NULL,
        SourceName NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        RequiresQuote BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresQuote_Runtime DEFAULT 0,
        RequiresSubmission BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresSubmission_Runtime DEFAULT 0,
        RequiresAccount BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresAccount_Runtime DEFAULT 1,
        RequiresReason BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresReason_Runtime DEFAULT 1,
        RequiresPolicyNumber BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresPolicyNumber_Runtime DEFAULT 1,
        AllowsDirectPolicyEntry BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_AllowsDirect_Runtime DEFAULT 1,
        IsImportSource BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsImport_Runtime DEFAULT 0,
        IsConversionSource BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsConversion_Runtime DEFAULT 0,
        IsDefault BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsDefault_Runtime DEFAULT 0,
        IsActive BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsActive_Runtime DEFAULT 1,
        SortOrder INT NOT NULL CONSTRAINT DF_PolicyCreationSource_SortOrder_Runtime DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCreationSource_Created_Runtime DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsDeleted_Runtime DEFAULT 0
    );
END;

IF COL_LENGTH(N'Submissions.PolicyCreationSource', N'RequiresSubmission') IS NULL ALTER TABLE Submissions.PolicyCreationSource ADD RequiresSubmission BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresSubmission_RuntimeB DEFAULT 0;
IF COL_LENGTH(N'Submissions.PolicyCreationSource', N'RequiresAccount') IS NULL ALTER TABLE Submissions.PolicyCreationSource ADD RequiresAccount BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresAccount_RuntimeB DEFAULT 1;
IF COL_LENGTH(N'Submissions.PolicyCreationSource', N'AllowsDirectPolicyEntry') IS NULL ALTER TABLE Submissions.PolicyCreationSource ADD AllowsDirectPolicyEntry BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_AllowsDirect_RuntimeB DEFAULT 1;
IF COL_LENGTH(N'Submissions.PolicyCreationSource', N'IsImportSource') IS NULL ALTER TABLE Submissions.PolicyCreationSource ADD IsImportSource BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsImport_RuntimeB DEFAULT 0;
IF COL_LENGTH(N'Submissions.PolicyCreationSource', N'IsConversionSource') IS NULL ALTER TABLE Submissions.PolicyCreationSource ADD IsConversionSource BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsConversion_RuntimeB DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyCreationSource WHERE TenantId = @TenantId AND SourceCode = N'QuoteBound' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyCreationSource (TenantId, SourceCode, SourceName, Description, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'QuoteBound', N'Quote Bound', N'Policy is created from an accepted or selected quote.', 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 10, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyCreationSource WHERE TenantId = @TenantId AND SourceCode = N'AlreadyBound' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyCreationSource (TenantId, SourceCode, SourceName, Description, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'AlreadyBound', N'Already Bound Outside System', N'Carrier or broker already bound coverage outside the platform.', 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 20, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyCreationSource WHERE TenantId = @TenantId AND SourceCode = N'ManualEntry' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyCreationSource (TenantId, SourceCode, SourceName, Description, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'ManualEntry', N'Manual Policy Entry', N'Policy is manually entered with required audit reason and policy details.', 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 30, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyCreationSource WHERE TenantId = @TenantId AND SourceCode = N'Imported' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyCreationSource (TenantId, SourceCode, SourceName, Description, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'Imported', N'Imported Policy', N'Policy is imported from a carrier, conversion, or data migration source.', 0, 0, 1, 1, 1, 1, 1, 1, 0, 1, 40, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyCreationSource WHERE TenantId = @TenantId AND SourceCode = N'BOR' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyCreationSource (TenantId, SourceCode, SourceName, Description, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'BOR', N'Broker of Record / Takeover', N'Policy is entered after a broker-of-record or book-of-business takeover.', 0, 0, 1, 1, 1, 1, 0, 1, 0, 1, 50, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyCreationSource WHERE TenantId = @TenantId AND SourceCode = N'RenewalImport' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyCreationSource (TenantId, SourceCode, SourceName, Description, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'RenewalImport', N'Renewal Import', N'Renewal policy was imported from carrier, prior AMS, or external renewal file.', 0, 0, 1, 1, 1, 1, 1, 1, 0, 1, 60, SYSUTCDATETIME(), 0);

UPDATE Submissions.PolicyCreationSource
SET SourceName = N'Renewal Import',
    Description = N'Renewal policy was imported from carrier, prior AMS, or external renewal file.',
    RequiresQuote = 0,
    RequiresSubmission = 0,
    RequiresAccount = 1,
    RequiresReason = 1,
    RequiresPolicyNumber = 1,
    AllowsDirectPolicyEntry = 1,
    IsImportSource = 1,
    IsConversionSource = 1,
    IsDefault = 0,
    IsActive = 1,
    SortOrder = 60,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND SourceCode = N'RenewalImport'
  AND IsDeleted = 0;", new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private static async Task<PolicyCreationSourceSettings> GetPolicyCreationSourceSettingsAsync(System.Data.IDbConnection connection, Guid tenantId, string? sourceCode, CancellationToken cancellationToken)
    {
        var requestedCode = Normalize(sourceCode) ?? "QuoteBound";
        var settings = await connection.QuerySingleOrDefaultAsync<PolicyCreationSourceSettings>(new CommandDefinition(@"
IF OBJECT_ID(N'Submissions.PolicyCreationSource', N'U') IS NULL
BEGIN
    SELECT @SourceCode AS SourceCode, @SourceCode AS SourceName, CAST(CASE WHEN @SourceCode = N'QuoteBound' THEN 1 ELSE 0 END AS bit) AS RequiresQuote, CAST(CASE WHEN @SourceCode = N'QuoteBound' THEN 1 ELSE 0 END AS bit) AS RequiresSubmission, CAST(1 AS bit) AS RequiresAccount, CAST(CASE WHEN @SourceCode = N'QuoteBound' THEN 0 ELSE 1 END AS bit) AS RequiresReason, CAST(CASE WHEN @SourceCode = N'QuoteBound' THEN 0 ELSE 1 END AS bit) AS RequiresPolicyNumber, CAST(CASE WHEN @SourceCode = N'QuoteBound' THEN 0 ELSE 1 END AS bit) AS AllowsDirectPolicyEntry, CAST(0 AS bit) AS IsImportSource, CAST(0 AS bit) AS IsConversionSource;
    RETURN;
END;

SELECT TOP 1 SourceCode, SourceName, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource
FROM Submissions.PolicyCreationSource
WHERE TenantId = @TenantId AND SourceCode = @SourceCode AND IsDeleted = 0 AND IsActive = 1;", new { TenantId = tenantId, SourceCode = requestedCode }, cancellationToken: cancellationToken));

        return settings ?? new PolicyCreationSourceSettings("QuoteBound", "Quote Bound", true, true, true, false, false, false, false, false);
    }

    private static async Task EnsurePolicyBindTransactionSchemaAsync(System.Data.IDbConnection connection, Guid tenantId, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'Submissions.PolicyBindStatus', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.PolicyBindStatus
    (
        PolicyBindStatusId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_PolicyBindStatus_Runtime PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        StatusCode NVARCHAR(50) NOT NULL,
        StatusName NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        IsTerminal BIT NOT NULL CONSTRAINT DF_PolicyBindStatus_IsTerminal_Runtime DEFAULT 0,
        CreatesPolicy BIT NOT NULL CONSTRAINT DF_PolicyBindStatus_CreatesPolicy_Runtime DEFAULT 0,
        IsDefault BIT NOT NULL CONSTRAINT DF_PolicyBindStatus_IsDefault_Runtime DEFAULT 0,
        IsActive BIT NOT NULL CONSTRAINT DF_PolicyBindStatus_IsActive_Runtime DEFAULT 1,
        SortOrder INT NOT NULL CONSTRAINT DF_PolicyBindStatus_SortOrder_Runtime DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyBindStatus_Created_Runtime DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyBindStatus_IsDeleted_Runtime DEFAULT 0
    );
END;

IF OBJECT_ID(N'Submissions.PolicyBindTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.PolicyBindTransaction
    (
        PolicyBindTransactionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_PolicyBindTransaction_Runtime PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        QuoteId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PolicyBindTransaction_QuoteId_Runtime DEFAULT '00000000-0000-0000-0000-000000000000',
        ProposalId UNIQUEIDENTIFIER NULL,
        CustomerAuthorizationId UNIQUEIDENTIFIER NULL,
        PolicyId UNIQUEIDENTIFIER NULL,
        AccountId UNIQUEIDENTIFIER NOT NULL,
        CarrierId UNIQUEIDENTIFIER NOT NULL,
        PolicySourceCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PolicyBindTransaction_Source_Runtime DEFAULT N'QuoteBound',
        BindStatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PolicyBindTransaction_Status_Runtime DEFAULT N'Pending',
        PolicyNumber NVARCHAR(80) NULL,
        AnnualPremium DECIMAL(18,2) NOT NULL,
        EffectiveDate DATE NOT NULL,
        ExpirationDate DATE NOT NULL,
        BindReason NVARCHAR(500) NULL,
        Notes NVARCHAR(1000) NULL,
        RequestedEffectiveTime TIME NULL,
        ConfirmationSourceCode NVARCHAR(50) NULL,
        CarrierReferenceNumber NVARCHAR(120) NULL,
        BinderNumber NVARCHAR(120) NULL,
        FinalPremium DECIMAL(18,2) NULL,
        DownPaymentAmount DECIMAL(18,2) NULL,
        SubjectivitiesOutstanding NVARCHAR(2000) NULL,
        ConfirmationNotes NVARCHAR(2000) NULL,
        ConfirmationDocumentId UNIQUEIDENTIFIER NULL,
        ConfirmationReceivedFrom NVARCHAR(320) NULL,
        ConfirmationMessageId NVARCHAR(200) NULL,
        UnderwriterContactId UNIQUEIDENTIFIER NULL,
        UnderwriterName NVARCHAR(200) NULL,
        UnderwriterCompany NVARCHAR(200) NULL,
        CommissionPlanApplicabilityId UNIQUEIDENTIFIER NULL,
        CommissionPlanId UNIQUEIDENTIFIER NULL,
        CommissionPlanVersionId UNIQUEIDENTIFIER NULL,
        CommissionPayeeId UNIQUEIDENTIFIER NULL,
        CommissionSplitRuleId UNIQUEIDENTIFIER NULL,
        CommissionBusinessTypeCode NVARCHAR(50) NULL,
        CommissionRatePct DECIMAL(9,4) NULL,
        CommissionSplitPct DECIMAL(9,4) NULL,
        CommissionablePremium DECIMAL(18,2) NULL,
        EstimatedGrossCommission DECIMAL(18,2) NULL,
        EstimatedProducerCommission DECIMAL(18,2) NULL,
        FollowUpWrittenConfirmationRequired BIT NOT NULL CONSTRAINT DF_PolicyBindTransaction_FollowUpWritten_Runtime DEFAULT 0,
        IntegrationCorrelationId NVARCHAR(120) NULL,
        ExternalTransactionId NVARCHAR(120) NULL,
        ConfirmedManually BIT NOT NULL CONSTRAINT DF_PolicyBindTransaction_ConfirmedManually_Runtime DEFAULT 0,
        ConfirmationCertified BIT NOT NULL CONSTRAINT DF_PolicyBindTransaction_Certified_Runtime DEFAULT 0,
        RequestedByUserId UNIQUEIDENTIFIER NULL,
        RequestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyBindTransaction_Requested_Runtime DEFAULT SYSUTCDATETIME(),
        ApprovedByUserId UNIQUEIDENTIFIER NULL,
        ApprovedDateUtc DATETIME2 NULL,
        BoundByUserId UNIQUEIDENTIFIER NULL,
        BoundDateUtc DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyBindTransaction_Created_Runtime DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyBindTransaction_IsDeleted_Runtime DEFAULT 0
    );
END;

IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ProposalId') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ProposalId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'CustomerAuthorizationId') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD CustomerAuthorizationId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'RequestedEffectiveTime') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD RequestedEffectiveTime TIME NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ConfirmationSourceCode') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ConfirmationSourceCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'CarrierReferenceNumber') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD CarrierReferenceNumber NVARCHAR(120) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'BinderNumber') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD BinderNumber NVARCHAR(120) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'FinalPremium') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD FinalPremium DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'DownPaymentAmount') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD DownPaymentAmount DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'SubjectivitiesOutstanding') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD SubjectivitiesOutstanding NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ConfirmationNotes') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ConfirmationNotes NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ConfirmationDocumentId') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ConfirmationDocumentId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ConfirmationReceivedFrom') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ConfirmationReceivedFrom NVARCHAR(320) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ConfirmationMessageId') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ConfirmationMessageId NVARCHAR(200) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'UnderwriterContactId') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD UnderwriterContactId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'UnderwriterName') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD UnderwriterName NVARCHAR(200) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'UnderwriterCompany') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD UnderwriterCompany NVARCHAR(200) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'CommissionPlanApplicabilityId') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD CommissionPlanApplicabilityId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'CommissionPlanId') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD CommissionPlanId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'CommissionPlanVersionId') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD CommissionPlanVersionId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'CommissionPayeeId') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD CommissionPayeeId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'CommissionSplitRuleId') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD CommissionSplitRuleId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'CommissionBusinessTypeCode') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD CommissionBusinessTypeCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'CommissionRatePct') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD CommissionRatePct DECIMAL(9,4) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'CommissionSplitPct') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD CommissionSplitPct DECIMAL(9,4) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'CommissionablePremium') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD CommissionablePremium DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'EstimatedGrossCommission') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD EstimatedGrossCommission DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'EstimatedProducerCommission') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD EstimatedProducerCommission DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'FollowUpWrittenConfirmationRequired') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD FollowUpWrittenConfirmationRequired BIT NOT NULL CONSTRAINT DF_PolicyBindTransaction_FollowUpWritten_Ensure DEFAULT 0;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'IntegrationCorrelationId') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD IntegrationCorrelationId NVARCHAR(120) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ExternalTransactionId') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ExternalTransactionId NVARCHAR(120) NULL;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ConfirmedManually') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ConfirmedManually BIT NOT NULL CONSTRAINT DF_PolicyBindTransaction_ConfirmedManually_Ensure DEFAULT 0;
IF COL_LENGTH(N'Submissions.PolicyBindTransaction', N'ConfirmationCertified') IS NULL ALTER TABLE Submissions.PolicyBindTransaction ADD ConfirmationCertified BIT NOT NULL CONSTRAINT DF_PolicyBindTransaction_Certified_Ensure DEFAULT 0;

DECLARE @PolicyBindStatusDefaultName SYSNAME;
SELECT @PolicyBindStatusDefaultName = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'Submissions.PolicyBindTransaction')
  AND c.name = N'BindStatusCode'
  AND dc.definition LIKE N'%Bound%';
IF @PolicyBindStatusDefaultName IS NOT NULL
BEGIN
    DECLARE @DropPolicyBindStatusDefaultSql NVARCHAR(MAX) = N'ALTER TABLE Submissions.PolicyBindTransaction DROP CONSTRAINT ' + QUOTENAME(@PolicyBindStatusDefaultName);
    EXEC sp_executesql @DropPolicyBindStatusDefaultSql;
    ALTER TABLE Submissions.PolicyBindTransaction ADD CONSTRAINT DF_PolicyBindTransaction_Status_Pending_Runtime DEFAULT N'Pending' FOR BindStatusCode;
END;

IF OBJECT_ID(N'Submissions.BoundPolicy', N'U') IS NOT NULL AND COL_LENGTH(N'Submissions.BoundPolicy', N'PolicyBindTransactionId') IS NULL
    ALTER TABLE Submissions.BoundPolicy ADD PolicyBindTransactionId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindStatus WHERE TenantId = @TenantId AND StatusCode = N'Pending' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyBindStatus (TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'Pending', N'Pending', N'Bind request has been captured and is pending carrier submission or review.', 0, 0, 1, 1, 10, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindStatus WHERE TenantId = @TenantId AND StatusCode = N'CarrierReviewing' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyBindStatus (TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'CarrierReviewing', N'Carrier Reviewing', N'Carrier is reviewing the bind request before confirmation.', 0, 0, 0, 1, 20, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindStatus WHERE TenantId = @TenantId AND StatusCode = N'WaitingPayment' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyBindStatus (TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'WaitingPayment', N'Waiting Payment', N'Carrier requires down payment before binding.', 0, 0, 0, 1, 30, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindStatus WHERE TenantId = @TenantId AND StatusCode = N'WaitingDocuments' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyBindStatus (TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'WaitingDocuments', N'Waiting Documents', N'Carrier requires signed application or additional documents before binding.', 0, 0, 0, 1, 40, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindStatus WHERE TenantId = @TenantId AND StatusCode = N'Approved' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyBindStatus (TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'Approved', N'Approved', N'Carrier approved the bind request but policy issuance is not complete.', 0, 0, 0, 1, 50, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindStatus WHERE TenantId = @TenantId AND StatusCode = N'Bound' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyBindStatus (TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'Bound', N'Bound', N'Carrier confirmed coverage is bound; policy creation may proceed.', 1, 1, 0, 1, 60, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindStatus WHERE TenantId = @TenantId AND StatusCode = N'Declined' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyBindStatus (TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'Declined', N'Declined', N'Carrier declined the bind request.', 1, 0, 0, 1, 50, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindStatus WHERE TenantId = @TenantId AND StatusCode = N'Failed' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyBindStatus (TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'Failed', N'Failed', N'Bind request failed validation or carrier processing.', 1, 0, 0, 1, 60, SYSUTCDATETIME(), 0);", new { TenantId = tenantId }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'Submissions.PolicyBindStatus', N'U') IS NOT NULL
BEGIN
    UPDATE Submissions.PolicyBindStatus
    SET StatusName = N'Pending', Description = N'Bind request has been captured and is pending carrier submission or review.', IsTerminal = 0, CreatesPolicy = 0, IsDefault = 1, SortOrder = 10, IsActive = 1, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND StatusCode = N'Pending' AND IsDeleted = 0;
    UPDATE Submissions.PolicyBindStatus
    SET StatusName = N'Carrier Reviewing', Description = N'Carrier is reviewing the bind request before confirmation.', IsTerminal = 0, CreatesPolicy = 0, IsDefault = 0, SortOrder = 20, IsActive = 1, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND StatusCode = N'CarrierReviewing' AND IsDeleted = 0;
    UPDATE Submissions.PolicyBindStatus
    SET StatusName = N'Waiting Payment', Description = N'Carrier requires down payment before binding.', IsTerminal = 0, CreatesPolicy = 0, IsDefault = 0, SortOrder = 30, IsActive = 1, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND StatusCode = N'WaitingPayment' AND IsDeleted = 0;
    UPDATE Submissions.PolicyBindStatus
    SET StatusName = N'Waiting Documents', Description = N'Carrier requires signed application or additional documents before binding.', IsTerminal = 0, CreatesPolicy = 0, IsDefault = 0, SortOrder = 40, IsActive = 1, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND StatusCode = N'WaitingDocuments' AND IsDeleted = 0;
    UPDATE Submissions.PolicyBindStatus
    SET StatusName = N'Approved', Description = N'Carrier approved the bind request but policy issuance is not complete.', IsTerminal = 0, CreatesPolicy = 0, IsDefault = 0, SortOrder = 50, IsActive = 1, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND StatusCode = N'Approved' AND IsDeleted = 0;
    UPDATE Submissions.PolicyBindStatus
    SET StatusName = N'Bound', Description = N'Carrier confirmed coverage is bound; policy creation may proceed.', IsTerminal = 1, CreatesPolicy = 1, IsDefault = 0, SortOrder = 60, IsActive = 1, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND StatusCode = N'Bound' AND IsDeleted = 0;
    UPDATE Submissions.PolicyBindStatus
    SET IsDefault = 0, IsActive = 0, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE TenantId = @TenantId AND StatusCode IN (N'Draft', N'PendingApproval', N'ReadyToBind', N'Submitted', N'Acknowledged', N'MoreInformationRequired', N'Confirmed') AND IsDeleted = 0;
END;", new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    // ── Markets ───────────────────────────────────────────────────────

    private const string MarketColumns = "sm.SubmissionMarketId, sm.SubmissionId, sm.CarrierId, c.CarrierName, sm.Status, sm.AppetiteScore, sm.IsRecommended, sm.DeclineReason, sm.AddedDateUtc, sm.RespondedDateUtc";

    public async Task<IReadOnlyList<SubmissionMarketDto>> GetMarketsAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT sm.SubmissionMarketId, sm.SubmissionId, sm.CarrierId, c.CarrierName,
       sm.Status, sm.AppetiteScore, sm.IsRecommended, sm.DeclineReason, sm.AddedDateUtc, sm.RespondedDateUtc,
       sm.UnderwriterName, sm.UnderwriterEmail, sm.UnderwriterPhone, sm.DueDateUtc,
       sm.RequestedCoverageSummary, sm.RequestedLimits, sm.SubmissionMethodCode, sm.FollowUpTaskId, sm.SubmittedDateUtc,
       sm.QuoteRequestScopeCode, sm.RequestedPremium,
       history.QuoteRequestActionCode AS LatestQuoteRequestActionCode,
       history.QuoteRequestReasonCode AS LatestQuoteRequestReasonCode,
       history.QuoteRequestMethodCode AS LatestQuoteRequestMethodCode,
       history.StatusCode AS LatestQuoteRequestStatusCode,
       COALESCE(history.RequestVersion, 0) AS LatestQuoteRequestVersion,
       history.RequestedDateUtc AS LatestQuoteRequestDateUtc,
       history.QuoteRequestId AS LatestQuoteRequestId,
       history.DeliveryMethodCode AS LatestQuoteRequestDeliveryMethodCode,
       history.AssignedUnderwriterName AS LatestQuoteRequestAssignedUnderwriterName,
       history.AssignedUnderwriterEmail AS LatestQuoteRequestAssignedUnderwriterEmail,
       history.AssignedUnderwriterPhone AS LatestQuoteRequestAssignedUnderwriterPhone,
       history.DueDateUtc AS LatestQuoteRequestDueDateUtc,
       COALESCE(history.RetryCount, 0) AS LatestQuoteRequestRetryCount,
       history.CorrelationId AS LatestQuoteRequestCorrelationId,
       history.CarrierReferenceNumber AS LatestQuoteRequestCarrierReferenceNumber,
       history.ResponseDateUtc AS LatestQuoteRequestResponseDateUtc,
       history.DispatchedDateUtc AS LatestQuoteRequestDispatchedDateUtc,
       history.AcknowledgedDateUtc AS LatestQuoteRequestAcknowledgedDateUtc,
       history.LastAttemptDateUtc AS LatestQuoteRequestLastAttemptDateUtc,
       history.LastError AS LatestQuoteRequestLastError,
       COALESCE(history.AttachmentCount, 0) AS LatestQuoteRequestAttachmentCount,
       CAST(CASE WHEN sm.DueDateUtc IS NOT NULL AND sm.DueDateUtc < SYSUTCDATETIME() AND sm.RespondedDateUtc IS NULL AND sm.Status NOT IN (N'Quoted', N'Declined', N'Bound', N'No Response') THEN 1 ELSE 0 END AS bit) AS IsPastDue,
       CASE WHEN sm.DueDateUtc IS NULL THEN NULL ELSE DATEDIFF(day, SYSUTCDATETIME(), sm.DueDateUtc) END AS DaysUntilDue,
       CASE WHEN sm.DueDateUtc IS NOT NULL AND sm.DueDateUtc < SYSUTCDATETIME() AND sm.RespondedDateUtc IS NULL THEN DATEDIFF(day, sm.DueDateUtc, SYSUTCDATETIME()) ELSE NULL END AS DaysPastDue,
       q.QuoteId AS LatestQuoteId, q.QuoteNumber AS LatestQuoteNumber, q.Status AS LatestQuoteStatus,
       q.QuoteReceivedDateUtc AS LatestQuoteReceivedDateUtc,
       tx.CarrierTransmissionId AS LatestCarrierTransmissionId,
       tx.StatusCode AS LatestTransmissionStatusCode,
       tx.ChannelCode AS LatestTransmissionChannelCode,
       tx.ConnectorName AS LatestTransmissionConnectorName,
       tx.ExternalReferenceNumber AS LatestTransmissionExternalReferenceNumber,
       tx.SentDateUtc AS LatestTransmissionSentDateUtc,
       tx.ConfirmedDateUtc AS LatestTransmissionConfirmedDateUtc,
       tx.FailedDateUtc AS LatestTransmissionFailedDateUtc,
       tx.BounceDateUtc AS LatestTransmissionBounceDateUtc,
       tx.LastError AS LatestTransmissionLastError,
       inbound.StatusCode AS LatestInboundResponseStatusCode,
       inbound.ResponseTypeCode AS LatestInboundResponseTypeCode,
       inbound.ReceivedDateUtc AS LatestInboundResponseReceivedDateUtc
FROM   Submissions.SubmissionMarket sm
JOIN   Core.Carrier                 c  ON c.CarrierId = sm.CarrierId
OUTER APPLY
(
    SELECT TOP 1 h.QuoteRequestId, h.QuoteRequestActionCode, h.QuoteRequestReasonCode, h.QuoteRequestMethodCode, h.StatusCode, h.RequestVersion, h.RequestedDateUtc,
           h.DeliveryMethodCode, h.AssignedUnderwriterName, h.AssignedUnderwriterEmail, h.AssignedUnderwriterPhone, h.DueDateUtc, h.RetryCount,
           h.CorrelationId, h.CarrierReferenceNumber, h.ResponseDateUtc, h.DispatchedDateUtc, h.AcknowledgedDateUtc, h.LastAttemptDateUtc, h.LastError,
           AttachmentCount = CASE WHEN OBJECT_ID(N'DMS.Document', N'U') IS NULL THEN 0 ELSE (SELECT COUNT(1) FROM DMS.Document d WHERE d.TenantId = h.TenantId AND d.EntityName = N'QuoteRequest' AND d.EntityId = h.QuoteRequestId AND d.IsDeleted = 0) END
    FROM Submissions.QuoteRequest h
    WHERE h.SubmissionMarketId = sm.SubmissionMarketId
      AND h.IsDeleted = 0
    ORDER BY h.RequestVersion DESC, h.RequestedDateUtc DESC
) history
OUTER APPLY
(
    SELECT TOP 1 QuoteId, QuoteNumber, Status, QuoteReceivedDateUtc, QuotedDateUtc
    FROM Submissions.Quote q
    WHERE q.SubmissionMarketId = sm.SubmissionMarketId
      AND q.IsDeleted = 0
    ORDER BY q.ResponseVersion DESC, q.QuoteReceivedDateUtc DESC, q.QuotedDateUtc DESC, q.CreatedDateUtc DESC
) q
OUTER APPLY
(
    SELECT TOP 1 t.CarrierTransmissionId, t.StatusCode, t.ChannelCode, connector.ConnectorName, t.ExternalReferenceNumber,
           t.SentDateUtc, t.ConfirmedDateUtc, t.FailedDateUtc, t.BounceDateUtc, t.LastError, t.CreatedDateUtc
    FROM Submissions.CarrierTransmission t
    LEFT JOIN Agency.CarrierExternalConnector connector ON connector.CarrierExternalConnectorId = t.CarrierExternalConnectorId AND connector.IsDeleted = 0
    WHERE t.SubmissionMarketId = sm.SubmissionMarketId
      AND t.IsDeleted = 0
    ORDER BY t.CreatedDateUtc DESC
) tx
OUTER APPLY
(
    SELECT TOP 1 r.StatusCode, r.ResponseTypeCode, r.ReceivedDateUtc
    FROM Submissions.CarrierInboundResponse r
    WHERE r.SubmissionMarketId = sm.SubmissionMarketId
      AND r.IsDeleted = 0
    ORDER BY r.ReceivedDateUtc DESC, r.CreatedDateUtc DESC
) inbound
WHERE  sm.SubmissionId = @SubmissionId AND sm.IsDeleted = 0
ORDER BY sm.AppetiteScore DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, null, cancellationToken);
        var markets = (await cn.QueryAsync<SubmissionMarketDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
        if (markets.Count == 0)
        {
            return markets;
        }

        const string lineSql = @"
SELECT SubmissionMarketLineId, SubmissionMarketId, SubmissionId, SubmissionLineId, LineOfBusiness, TargetPremium
FROM Submissions.SubmissionMarketLine
WHERE SubmissionId = @SubmissionId
  AND IsDeleted = 0
ORDER BY LineOfBusiness;";
        var lines = (await cn.QueryAsync<SubmissionMarketLineDto>(new CommandDefinition(lineSql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
        var lineLookup = lines.ToLookup(l => l.SubmissionMarketId);

        const string quoteRequestSql = @"
SELECT qr.QuoteRequestId, qr.TenantId, qr.SubmissionId, qr.SubmissionMarketId, qr.CarrierId, c.CarrierName,
       qr.QuoteRequestActionCode, qr.QuoteRequestReasonCode, qr.QuoteRequestMethodCode, qr.DeliveryMethodCode, qr.QuoteRequestScopeCode,
       qr.RequestedPremium, qr.Premium, qr.CommissionPercent, qr.QuoteNumber, qr.ExpirationDateUtc, qr.CoverageNotes, qr.CarrierReferenceNumber,
       qr.RequestVersion, qr.StatusCode, qr.RequestedDateUtc, qr.RequestedByUserId, qr.DueDateUtc,
       qr.AssignedUnderwriterName, qr.AssignedUnderwriterEmail, qr.AssignedUnderwriterPhone, qr.RetryCount, qr.CorrelationId,
       qr.DispatchedDateUtc, qr.AcknowledgedDateUtc, qr.ResponseDateUtc, qr.LastAttemptDateUtc, qr.LastError, qr.ClosedDateUtc,
       AttachmentCount = CASE WHEN OBJECT_ID(N'DMS.Document', N'U') IS NULL THEN 0 ELSE (SELECT COUNT(1) FROM DMS.Document d WHERE d.TenantId = qr.TenantId AND d.EntityName = N'QuoteRequest' AND d.EntityId = qr.QuoteRequestId AND d.IsDeleted = 0) END,
       QuoteCount = (SELECT COUNT(1) FROM Submissions.Quote q WHERE q.QuoteRequestId = qr.QuoteRequestId AND q.IsDeleted = 0),
       qr.CreatedDateUtc
FROM Submissions.QuoteRequest qr
JOIN Core.Carrier c ON c.CarrierId = qr.CarrierId
WHERE qr.SubmissionId = @SubmissionId
  AND qr.IsDeleted = 0
ORDER BY qr.SubmissionMarketId, qr.RequestVersion DESC, qr.RequestedDateUtc DESC;";
        var quoteRequests = (await cn.QueryAsync<QuoteRequestDto>(new CommandDefinition(quoteRequestSql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
        var quoteRequestLookup = quoteRequests.ToLookup(q => q.SubmissionMarketId);

        const string transmissionSql = @"
SELECT t.CarrierTransmissionId, t.SubmissionId, t.SubmissionMarketId, t.CarrierId, t.CarrierExternalConnectorId,
       connector.ConnectorName, t.TransmissionTypeCode, t.ChannelCode, t.StatusCode, t.Recipient, t.Subject, t.EndpointUri,
       t.ExternalReferenceNumber, t.AttemptCount, t.LastAttemptDateUtc, t.SentDateUtc, t.ConfirmedDateUtc,
       t.FailedDateUtc, t.BounceDateUtc, t.LastError, t.CreatedDateUtc
FROM Submissions.CarrierTransmission t
LEFT JOIN Agency.CarrierExternalConnector connector ON connector.CarrierExternalConnectorId = t.CarrierExternalConnectorId AND connector.IsDeleted = 0
WHERE t.SubmissionId = @SubmissionId
  AND t.IsDeleted = 0
ORDER BY t.CreatedDateUtc DESC;";
        var transmissions = (await cn.QueryAsync<CarrierTransmissionDto>(new CommandDefinition(transmissionSql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
        var transmissionIds = transmissions.Select(t => t.CarrierTransmissionId).ToArray();
        List<CarrierTransmissionEventDto> events = [];
        if (transmissionIds.Length > 0)
        {
            const string eventSql = @"
SELECT CarrierTransmissionEventId, CarrierTransmissionId, SubmissionId, SubmissionMarketId, EventCode, EventMessage, CreatedDateUtc
FROM Submissions.CarrierTransmissionEvent
WHERE CarrierTransmissionId IN @TransmissionIds
  AND IsDeleted = 0
ORDER BY CreatedDateUtc DESC;";
            events = (await cn.QueryAsync<CarrierTransmissionEventDto>(new CommandDefinition(eventSql, new { TransmissionIds = transmissionIds }, cancellationToken: cancellationToken))).AsList();
        }

        var eventLookup = events.ToLookup(e => e.CarrierTransmissionId);
        foreach (var transmission in transmissions)
        {
            transmission.Events = eventLookup[transmission.CarrierTransmissionId].ToList();
        }

        const string inboundSql = @"
SELECT CarrierInboundResponseId, SubmissionId, SubmissionMarketId, CarrierId, CarrierTransmissionId, SourceChannelCode,
       ResponseTypeCode, StatusCode, CarrierReferenceNumber, ReceivedDateUtc, ProcessedDateUtc, ProcessingError
FROM Submissions.CarrierInboundResponse
WHERE SubmissionId = @SubmissionId
  AND IsDeleted = 0
ORDER BY ReceivedDateUtc DESC, CreatedDateUtc DESC;";
        var inboundResponses = (await cn.QueryAsync<CarrierInboundResponseDto>(new CommandDefinition(inboundSql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
        var transmissionLookup = transmissions.ToLookup(t => t.SubmissionMarketId);
        var inboundLookup = inboundResponses.Where(r => r.SubmissionMarketId.HasValue).ToLookup(r => r.SubmissionMarketId!.Value);
        foreach (var market in markets)
        {
            market.RequestedLines = lineLookup[market.SubmissionMarketId].ToList();
            market.QuoteRequests = quoteRequestLookup[market.SubmissionMarketId].ToList();
            market.Transmissions = transmissionLookup[market.SubmissionMarketId].ToList();
            market.InboundResponses = inboundLookup[market.SubmissionMarketId].ToList();
        }

        return markets;
    }

    public async Task<int> SynchronizeOverdueMarketRequestsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF OBJECT_ID(N'Submissions.SubmissionMarket', N'U') IS NULL
BEGIN
    SELECT 0;
    RETURN;
END;

DECLARE @Updated TABLE (SubmissionMarketId UNIQUEIDENTIFIER NOT NULL, SubmissionId UNIQUEIDENTIFIER NOT NULL, TenantId UNIQUEIDENTIFIER NOT NULL, CarrierId UNIQUEIDENTIFIER NOT NULL, DueDateUtc DATETIME2 NULL);

UPDATE sm
SET Status = N'No Response',
    ReasonCode = COALESCE(ReasonCode, N'Overdue'),
    DeclineReason = COALESCE(DeclineReason, N'Carrier did not respond by the quote request due date.'),
    NextActionDateUtc = COALESCE(NextActionDateUtc, DATEADD(day, 1, SYSUTCDATETIME())),
    ModifiedDateUtc = SYSUTCDATETIME()
OUTPUT inserted.SubmissionMarketId, inserted.SubmissionId, inserted.TenantId, inserted.CarrierId, inserted.DueDateUtc INTO @Updated
FROM Submissions.SubmissionMarket sm
WHERE sm.IsDeleted = 0
  AND sm.DueDateUtc IS NOT NULL
  AND sm.DueDateUtc < SYSUTCDATETIME()
  AND sm.RespondedDateUtc IS NULL
  AND sm.Status IN (N'In Review', N'Sent', N'Awaiting Info');

INSERT INTO Submissions.SubmissionActionLog
    (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
SELECT NEWID(), u.SubmissionId, u.TenantId, N'MarketNoResponse',
       CONCAT(N'Market request marked No Response after due date ', CONVERT(nvarchar(10), u.DueDateUtc, 120), N'.'),
       SYSUTCDATETIME(), N'SubmissionMarket', u.SubmissionMarketId, N'QuoteRequestFollowUpWorker', 0
FROM @Updated u
WHERE NOT EXISTS
(
    SELECT 1
    FROM Submissions.SubmissionActionLog existing
    WHERE existing.RelatedEntityName = N'SubmissionMarket'
      AND existing.RelatedEntityId = u.SubmissionMarketId
      AND existing.ActionCode = N'MarketNoResponse'
      AND existing.IsDeleted = 0
);

SELECT COUNT(1) FROM @Updated;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, null, cancellationToken);
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SubmissionMarketDto>> GetMarketSuggestionsAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @HasAppetiteRule BIT = CASE WHEN OBJECT_ID(N'Core.AppetiteRule', N'U') IS NOT NULL THEN 1 ELSE 0 END;

DECLARE @Appetite TABLE
(
    CarrierId UNIQUEIDENTIFIER NOT NULL,
    LineOfBusiness NVARCHAR(100) NOT NULL,
    AppetiteScore INT NOT NULL
);

IF @HasAppetiteRule = 1
BEGIN
    INSERT INTO @Appetite (CarrierId, LineOfBusiness, AppetiteScore)
    EXEC(N'
        SELECT CarrierId, LineOfBusiness, AppetiteScore
        FROM Core.AppetiteRule
        WHERE IsDeleted = 0;');
END;

;WITH SubmissionContext AS
(
    SELECT SubmissionId, TenantId, LineOfBusiness
    FROM Submissions.Submission
    WHERE SubmissionId = @SubmissionId
      AND IsDeleted = 0
),
CarrierMarkets AS
(
    SELECT c.CarrierId,
           c.CarrierName,
           s.LineOfBusiness,
           COALESCE(MAX(ar.AppetiteScore), 65) AS AppetiteScore,
           CAST(CASE WHEN COALESCE(MAX(ar.AppetiteScore), 65) >= 60 THEN 1 ELSE 0 END AS bit) AS IsRecommended,
           COALESCE(MIN(linePref.SortOrder), MIN(defaultPref.SortOrder), 500) AS SortOrder
    FROM SubmissionContext s
    INNER JOIN Core.Carrier c ON c.TenantId = s.TenantId
        AND c.IsDeleted = 0
        AND c.IsActive = 1
    LEFT JOIN @Appetite ar ON ar.CarrierId = c.CarrierId
        AND ar.LineOfBusiness = s.LineOfBusiness
    LEFT JOIN Core.CarrierMarketSuggestionPreference linePref ON linePref.TenantId = s.TenantId
        AND linePref.CarrierId = c.CarrierId
        AND linePref.LineOfBusiness = s.LineOfBusiness
        AND linePref.IsActive = 1
        AND linePref.IsDeleted = 0
    LEFT JOIN Core.CarrierMarketSuggestionPreference defaultPref ON defaultPref.TenantId = s.TenantId
        AND defaultPref.CarrierId = c.CarrierId
        AND defaultPref.LineOfBusiness IS NULL
        AND defaultPref.IsActive = 1
        AND defaultPref.IsDeleted = 0
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM Submissions.SubmissionMarket existing
        WHERE existing.SubmissionId = s.SubmissionId
          AND existing.CarrierId = c.CarrierId
          AND existing.IsDeleted = 0
    )
    GROUP BY c.CarrierId, c.CarrierName, s.LineOfBusiness
)
SELECT TOP 10 CarrierId,
       CarrierName,
       LineOfBusiness,
       AppetiteScore,
       IsRecommended,
       CAST(NULL AS NVARCHAR(500)) AS DeclineReason,
       SYSUTCDATETIME() AS AddedDateUtc,
       CAST(NULL AS DATETIME2) AS RespondedDateUtc,
       NEWID() AS SubmissionMarketId,
       @SubmissionId AS SubmissionId,
       N'Current Market' AS Status
FROM CarrierMarkets
ORDER BY IsRecommended DESC, AppetiteScore DESC, SortOrder, CarrierName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, null, cancellationToken);
        return (await cn.QueryAsync<SubmissionMarketDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> AddMarketAsync(AddSubmissionMarketRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND IsDeleted = 0);
IF @TenantId IS NULL THROW 52021, 'Submission was not found for market add.', 1;

INSERT INTO Submissions.SubmissionMarket
    (SubmissionMarketId, SubmissionId, CarrierId, Status, AppetiteScore, IsRecommended, AddedDateUtc, TenantId, IsDeleted)
VALUES
    (@SubmissionMarketId, @SubmissionId, @CarrierId, 'Pending', 0, 0, GETUTCDATE(), @TenantId, 0);

UPDATE Submissions.Submission
SET    MarketCount     = MarketCount + 1,
       ModifiedDateUtc = GETUTCDATE()
WHERE  SubmissionId = @SubmissionId;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, null, cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            SubmissionMarketId = id,
            request.SubmissionId,
            request.CarrierId,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateMarketStatusAsync(Guid submissionMarketId, UpdateSubmissionMarketStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.SubmissionMarket
SET    Status           = @Status,
       DeclineReason    = @DeclineReason,
       RespondedDateUtc = GETUTCDATE()
WHERE  SubmissionMarketId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = submissionMarketId, request.Status, request.DeclineReason }, cancellationToken: cancellationToken));
    }

    public async Task UpdateMarketPackageAsync(UpdateSubmissionMarketPackageRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;
SELECT @SubmissionId = SubmissionId FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0;
IF @SubmissionId IS NULL THROW 52013, 'Submission market was not found.', 1;

UPDATE Submissions.SubmissionMarket
SET Status = @Status,
    ReasonCode = @ReasonCode,
    DeclineReason = CASE WHEN @Status IN (N'Declined', N'Blocked') THEN COALESCE(@Notes, DeclineReason) ELSE DeclineReason END,
    Notes = @Notes,
    NextActionDateUtc = @NextActionDateUtc,
    UnderwriterName = @UnderwriterName,
    UnderwriterEmail = @UnderwriterEmail,
    UnderwriterPhone = @UnderwriterPhone,
    DueDateUtc = @DueDateUtc,
    RequestedCoverageSummary = @RequestedCoverageSummary,
    RequestedLimits = @RequestedLimits,
    SubmissionMethodCode = @SubmissionMethodCode,
    FollowUpTaskId = @FollowUpTaskId,
    RespondedDateUtc = CASE WHEN @Status IN (N'Declined', N'Quoted', N'Blocked') THEN SYSUTCDATETIME() ELSE RespondedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE SubmissionMarketId = @SubmissionMarketId;

UPDATE Submissions.SubmissionMarketDocument SET IsDeleted = 1 WHERE SubmissionMarketId = @SubmissionMarketId;

INSERT INTO Submissions.SubmissionMarketDocument (SubmissionMarketDocumentId, SubmissionMarketId, SubmissionId, TenantId, DocumentId, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @SubmissionMarketId, @SubmissionId, @TenantId, value, SYSUTCDATETIME(), @ModifiedByUserId, 0
FROM STRING_SPLIT(@DocumentIdsCsv, N',')
WHERE TRY_CONVERT(uniqueidentifier, value) IS NOT NULL;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, CASE WHEN @Status = N'Declined' THEN N'MarketDeclined' WHEN @Status = N'Blocked' THEN N'MarketBlocked' ELSE N'MarketUpdated' END,
        COALESCE(@Notes, CONCAT(N'Market status updated to ', @Status)), SYSUTCDATETIME(), @ModifiedByUserId, N'SubmissionMarket', @SubmissionMarketId, N'User', 0);";
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            request.TenantId,
            request.SubmissionMarketId,
            request.Status,
            request.ReasonCode,
            request.Notes,
            request.NextActionDateUtc,
            request.UnderwriterName,
            request.UnderwriterEmail,
            request.UnderwriterPhone,
            request.DueDateUtc,
            request.RequestedCoverageSummary,
            request.RequestedLimits,
            request.SubmissionMethodCode,
            request.FollowUpTaskId,
            request.ModifiedByUserId,
            DocumentIdsCsv = string.Join(',', request.DocumentIds ?? [])
        }, cancellationToken: cancellationToken));
    }

    public async Task RemoveMarketAsync(Guid submissionMarketId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.SubmissionMarket SET IsDeleted = 1 WHERE SubmissionMarketId = @Id;

UPDATE Submissions.Submission
SET    MarketCount     = CASE WHEN MarketCount > 0 THEN MarketCount - 1 ELSE 0 END,
       ModifiedDateUtc = GETUTCDATE()
WHERE  SubmissionId = (SELECT SubmissionId FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @Id);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = submissionMarketId }, cancellationToken: cancellationToken));
    }

    // ── Quotes ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<QuoteComparisonDto>> GetQuoteComparisonAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT q.QuoteId, q.SubmissionId, q.SubmissionMarketId, q.CarrierId, c.CarrierName,
       q.QuoteNumber, q.Status, q.AnnualPremium, q.EffectiveDate, q.Deductible, q.Limit, q.CoverageForms,
       q.CommissionPercent, q.Subjectivities, q.Exclusions, q.CarrierRating, q.PaymentTerms,
       q.MinimumEarnedPremium, q.TaxesAndFees, q.BrokerFee, q.TriaIncluded,
       q.IsBindable,
       q.QuoteDocumentId, d.FileName AS QuoteDocumentFileName,
       q.DisclosureDocumentId,
       CAST(CASE WHEN q.ReviewedDateUtc IS NOT NULL OR q.ReviewedByUserId IS NOT NULL OR q.Status IN (N'Under Review', N'Approved for Presentation', N'Presented', N'Selected', N'Bound') THEN 1 ELSE 0 END AS bit) AS IsReviewed,
       q.ReviewedByUserId, q.ReviewedDateUtc, q.ApprovedForPresentationByUserId, q.ApprovedForPresentationDateUtc, q.PresentationReadinessNotes,
       CAST(CASE WHEN q.Status = N'Approved for Presentation'
                   AND q.IsBindable = 1
                  AND q.ExpiresDateUtc > SYSUTCDATETIME()
                  AND q.AnnualPremium > 0
                  AND q.CarrierId IS NOT NULL
                  AND q.Deductible IS NOT NULL
                  AND q.[Limit] IS NOT NULL
                  AND COALESCE(NULLIF(q.CoverageForms, N''), NULLIF(q.CoverageNotes, N'')) IS NOT NULL
                  AND (q.ReviewedDateUtc IS NOT NULL OR q.ReviewedByUserId IS NOT NULL)
                  AND q.QuoteDocumentId IS NOT NULL
                    AND EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = s.TenantId AND ql.IsDeleted = 0)
                    AND NOT EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = s.TenantId AND ql.IsDeleted = 0 AND ql.IsBindable = 0)
                    AND NOT EXISTS
                    (
                        SELECT 1
                        FROM Submissions.SubmissionLine sl
                        WHERE sl.SubmissionId = q.SubmissionId
                          AND sl.TenantId = s.TenantId
                          AND sl.IsDeleted = 0
                          AND NOT EXISTS
                          (
                              SELECT 1
                              FROM Submissions.QuoteLine ql
                              WHERE ql.QuoteId = q.QuoteId
                                AND ql.TenantId = s.TenantId
                                AND ql.SubmissionLineId = sl.SubmissionLineId
                                AND ql.IsDeleted = 0
                                AND ql.IsBindable = 1
                          )
                    )
                 THEN 1 ELSE 0 END AS bit) AS IsProposalReady,
       CASE
           WHEN q.Status <> N'Approved for Presentation' THEN CONCAT(N'Current status: ', COALESCE(NULLIF(q.Status, N''), N'Not set'), N'. Open Review, select Approved for Presentation, and save the quote.')
            WHEN q.IsBindable = 0 THEN N'The quote is not bindable. Open Review Quote Terms and mark every persisted coverage line bindable before creating a proposal.'
             WHEN NOT EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = s.TenantId AND ql.IsDeleted = 0) THEN N'The quote has no persisted coverage lines. Open Review Quote Terms and save the required coverage lines before creating a proposal.'
             WHEN EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = s.TenantId AND ql.IsDeleted = 0 AND ql.IsBindable = 0) THEN N'One or more coverage lines are not bindable. Open Review Quote Terms, mark every eligible line bindable, and save the quote.'
             WHEN EXISTS (SELECT 1 FROM Submissions.SubmissionLine sl WHERE sl.SubmissionId = q.SubmissionId AND sl.TenantId = s.TenantId AND sl.IsDeleted = 0 AND NOT EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = s.TenantId AND ql.SubmissionLineId = sl.SubmissionLineId AND ql.IsDeleted = 0 AND ql.IsBindable = 1)) THEN N'This multi-line submission is not fully covered. Open Review Quote Terms and persist a bindable quote line for every submission line.'
           WHEN q.ExpiresDateUtc IS NULL THEN N'Quote expiration date is missing. Open Review Quote Terms and enter the carrier expiration date.'
           WHEN q.ExpiresDateUtc <= SYSUTCDATETIME() THEN N'The quote expiration date has passed. Open Review and enter a current carrier expiration date.'
           WHEN q.AnnualPremium <= 0 THEN N'Annual premium is missing or zero. Open Review and enter the quoted premium.'
           WHEN q.CarrierId IS NULL THEN N'Carrier market is not linked. Open Review Quote Terms and link the quote to its carrier market.'
           WHEN q.Deductible IS NULL THEN N'Deductible is missing. Open Review and enter the carrier deductible.'
           WHEN q.[Limit] IS NULL THEN N'Coverage limit is missing. Open Review and enter the quoted coverage limit.'
           WHEN COALESCE(NULLIF(q.CoverageForms, N''), NULLIF(q.CoverageNotes, N'')) IS NULL THEN N'Coverage details are missing. Open Review and enter coverage forms or coverage notes.'
           WHEN q.ReviewedDateUtc IS NULL AND q.ReviewedByUserId IS NULL THEN N'Review completion was not recorded. Open Review Quote Terms, verify the information, and save the quote.'
           WHEN q.QuoteDocumentId IS NULL THEN N'Carrier quote document is not linked. Attach or select the carrier quote document before creating the proposal.'
           ELSE NULL
       END AS ProposalReadinessReason,
       q.IsSelected, q.IsRecommended, q.RecommendationScore, q.RecommendationReason,
       q.QuoteRequestDateUtc, q.QuoteReceivedDateUtc, q.ResponseVersion, q.ResponseSourceCode,
       q.CarrierReferenceNumber, q.RequestedByUserId, q.ReceivedByUserId,
       q.CoverageNotes, q.QuotedDateUtc, q.ExpiresDateUtc
FROM   Submissions.Quote q
JOIN   Submissions.Submission s ON s.SubmissionId = q.SubmissionId AND s.TenantId = @TenantId AND s.IsDeleted = 0
JOIN   Core.Carrier      c ON c.CarrierId = q.CarrierId
LEFT JOIN DMS.Document d ON d.DocumentId = q.QuoteDocumentId AND d.IsDeleted = 0
WHERE  q.SubmissionId = @SubmissionId AND q.IsDeleted = 0
ORDER BY q.IsSelected DESC, q.RecommendationScore DESC, q.AnnualPremium ASC;
""";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, null, cancellationToken);
        var quotes = (await cn.QueryAsync<QuoteComparisonDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
        var lines = await GetQuoteLinesAsync(cn, submissionId, null, cancellationToken);
        var factors = await GetProposalReadinessFactorsAsync(cn, submissionId, null, cancellationToken);
        var linesByQuote = lines.GroupBy(line => line.QuoteId).ToDictionary(group => group.Key, group => (IReadOnlyList<SubmissionQuoteLineDto>)group.OrderBy(line => line.SortOrder).ThenBy(line => line.LineOfBusiness).ToList());
        var factorsByQuote = factors.GroupBy(factor => factor.QuoteId).ToDictionary(group => group.Key, group => (IReadOnlyList<ProposalReadinessFactorDto>)group.OrderBy(factor => factor.SortOrder).ToList());
        foreach (var quote in quotes)
        {
            quote.Lines = linesByQuote.GetValueOrDefault(quote.QuoteId) ?? [];
            quote.ProposalReadinessFactors = factorsByQuote.GetValueOrDefault(quote.QuoteId) ?? [];
        }
        return quotes;
    }

    public async Task<QuoteComparisonDto?> GetQuoteByIdAsync(Guid quoteId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT q.QuoteId, q.SubmissionId, q.SubmissionMarketId, q.CarrierId, c.CarrierName,
       q.QuoteNumber, q.Status, q.AnnualPremium, q.EffectiveDate, q.Deductible, q.Limit, q.CoverageForms,
       q.CommissionPercent, q.Subjectivities, q.Exclusions, q.CarrierRating, q.PaymentTerms,
       q.MinimumEarnedPremium, q.TaxesAndFees, q.BrokerFee, q.TriaIncluded,
       q.IsBindable,
       q.QuoteDocumentId, d.FileName AS QuoteDocumentFileName,
       q.DisclosureDocumentId,
       CAST(CASE WHEN q.ReviewedDateUtc IS NOT NULL OR q.ReviewedByUserId IS NOT NULL OR q.Status IN (N'Under Review', N'Approved for Presentation', N'Presented', N'Selected', N'Bound') THEN 1 ELSE 0 END AS bit) AS IsReviewed,
       q.ReviewedByUserId, q.ReviewedDateUtc, q.ApprovedForPresentationByUserId, q.ApprovedForPresentationDateUtc, q.PresentationReadinessNotes,
       CAST(CASE WHEN q.Status = N'Approved for Presentation'
                   AND q.IsBindable = 1
                  AND q.ExpiresDateUtc > SYSUTCDATETIME()
                  AND q.AnnualPremium > 0
                  AND q.CarrierId IS NOT NULL
                  AND q.Deductible IS NOT NULL
                  AND q.[Limit] IS NOT NULL
                  AND COALESCE(NULLIF(q.CoverageForms, N''), NULLIF(q.CoverageNotes, N'')) IS NOT NULL
                  AND (q.ReviewedDateUtc IS NOT NULL OR q.ReviewedByUserId IS NOT NULL)
                  AND q.QuoteDocumentId IS NOT NULL
                    AND EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = s.TenantId AND ql.IsDeleted = 0)
                    AND NOT EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = s.TenantId AND ql.IsDeleted = 0 AND ql.IsBindable = 0)
                    AND NOT EXISTS
                    (
                        SELECT 1
                        FROM Submissions.SubmissionLine sl
                        WHERE sl.SubmissionId = q.SubmissionId
                          AND sl.TenantId = s.TenantId
                          AND sl.IsDeleted = 0
                          AND NOT EXISTS
                          (
                              SELECT 1
                              FROM Submissions.QuoteLine ql
                              WHERE ql.QuoteId = q.QuoteId
                                AND ql.TenantId = s.TenantId
                                AND ql.SubmissionLineId = sl.SubmissionLineId
                                AND ql.IsDeleted = 0
                                AND ql.IsBindable = 1
                          )
                    )
                 THEN 1 ELSE 0 END AS bit) AS IsProposalReady,
       CASE
           WHEN q.Status <> N'Approved for Presentation' THEN CONCAT(N'Current status: ', COALESCE(NULLIF(q.Status, N''), N'Not set'), N'. Open Review, select Approved for Presentation, and save the quote.')
            WHEN q.IsBindable = 0 THEN N'The quote is not bindable. Open Review Quote Terms and mark every persisted coverage line bindable before creating a proposal.'
             WHEN NOT EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = s.TenantId AND ql.IsDeleted = 0) THEN N'The quote has no persisted coverage lines. Open Review Quote Terms and save the required coverage lines before creating a proposal.'
             WHEN EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = s.TenantId AND ql.IsDeleted = 0 AND ql.IsBindable = 0) THEN N'One or more coverage lines are not bindable. Open Review Quote Terms, mark every eligible line bindable, and save the quote.'
             WHEN EXISTS (SELECT 1 FROM Submissions.SubmissionLine sl WHERE sl.SubmissionId = q.SubmissionId AND sl.TenantId = s.TenantId AND sl.IsDeleted = 0 AND NOT EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = s.TenantId AND ql.SubmissionLineId = sl.SubmissionLineId AND ql.IsDeleted = 0 AND ql.IsBindable = 1)) THEN N'This multi-line submission is not fully covered. Open Review Quote Terms and persist a bindable quote line for every submission line.'
           WHEN q.ExpiresDateUtc IS NULL THEN N'Quote expiration date is missing. Open Review Quote Terms and enter the carrier expiration date.'
           WHEN q.ExpiresDateUtc <= SYSUTCDATETIME() THEN N'The quote expiration date has passed. Open Review and enter a current carrier expiration date.'
           WHEN q.AnnualPremium <= 0 THEN N'Annual premium is missing or zero. Open Review and enter the quoted premium.'
           WHEN q.CarrierId IS NULL THEN N'Carrier market is not linked. Open Review Quote Terms and link the quote to its carrier market.'
           WHEN q.Deductible IS NULL THEN N'Deductible is missing. Open Review and enter the carrier deductible.'
           WHEN q.[Limit] IS NULL THEN N'Coverage limit is missing. Open Review and enter the quoted coverage limit.'
           WHEN COALESCE(NULLIF(q.CoverageForms, N''), NULLIF(q.CoverageNotes, N'')) IS NULL THEN N'Coverage details are missing. Open Review and enter coverage forms or coverage notes.'
           WHEN q.ReviewedDateUtc IS NULL AND q.ReviewedByUserId IS NULL THEN N'Review completion was not recorded. Open Review Quote Terms, verify the information, and save the quote.'
           WHEN q.QuoteDocumentId IS NULL THEN N'Carrier quote document is not linked. Attach or select the carrier quote document before creating the proposal.'
           ELSE NULL
       END AS ProposalReadinessReason,
       q.IsSelected, q.IsRecommended, q.RecommendationScore, q.RecommendationReason,
       q.QuoteRequestDateUtc, q.QuoteReceivedDateUtc, q.ResponseVersion, q.ResponseSourceCode,
       q.CarrierReferenceNumber, q.RequestedByUserId, q.ReceivedByUserId,
       q.CoverageNotes, q.QuotedDateUtc, q.ExpiresDateUtc
 FROM   Submissions.Quote q
 JOIN   Submissions.Submission s ON s.SubmissionId = q.SubmissionId AND s.TenantId = @TenantId AND s.IsDeleted = 0
JOIN   Core.Carrier      c ON c.CarrierId = q.CarrierId
LEFT JOIN DMS.Document d ON d.DocumentId = q.QuoteDocumentId AND d.IsDeleted = 0
WHERE  q.QuoteId = @QuoteId AND q.IsDeleted = 0;
""";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, null, cancellationToken);
        var quote = await cn.QuerySingleOrDefaultAsync<QuoteComparisonDto>(new CommandDefinition(sql, new { QuoteId = quoteId, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (quote is not null)
        {
            quote.Lines = await GetQuoteLinesAsync(cn, null, quoteId, cancellationToken);
            quote.ProposalReadinessFactors = await GetProposalReadinessFactorsAsync(cn, quote.SubmissionId, quoteId, cancellationToken);
        }
        return quote;
    }

    private static async Task<IReadOnlyList<ProposalReadinessFactorDto>> GetProposalReadinessFactorsAsync(IDbConnection cn, Guid submissionId, Guid? quoteId, CancellationToken cancellationToken)
    {
        const string sql = """
SELECT factor.ReadinessRequirementId AS ProposalReadinessFactorId, factor.TenantId, quote.QuoteId, factor.RequirementCode AS FactorCode,
       factor.DisplayName, factor.Description AS Instructions, COALESCE(factor.ActionCode, N'QuoteReview') AS ActionCode, COALESCE(factor.ActionLabel, N'Review Quote Terms') AS ActionLabel,
       factor.IsRequired, factor.SortOrder,
       CAST(CASE factor.RequirementCode
           WHEN N'ApprovedStatus' THEN CASE WHEN quote.Status = N'Approved for Presentation' THEN 1 ELSE 0 END
           WHEN N'CurrentExpiration' THEN CASE WHEN quote.ExpiresDateUtc > SYSUTCDATETIME() THEN 1 ELSE 0 END
           WHEN N'PositivePremium' THEN CASE WHEN quote.AnnualPremium > 0 THEN 1 ELSE 0 END
           WHEN N'CarrierMarket' THEN CASE WHEN quote.CarrierId IS NOT NULL THEN 1 ELSE 0 END
           WHEN N'Deductible' THEN CASE WHEN quote.Deductible IS NOT NULL THEN 1 ELSE 0 END
           WHEN N'CoverageLimit' THEN CASE WHEN quote.[Limit] IS NOT NULL THEN 1 ELSE 0 END
           WHEN N'CoverageDetails' THEN CASE WHEN COALESCE(NULLIF(quote.CoverageForms, N''), NULLIF(quote.CoverageNotes, N'')) IS NOT NULL THEN 1 ELSE 0 END
           WHEN N'InternalReview' THEN CASE WHEN quote.ReviewedDateUtc IS NOT NULL OR quote.ReviewedByUserId IS NOT NULL THEN 1 ELSE 0 END
           WHEN N'CarrierQuoteDocument' THEN CASE WHEN quote.QuoteDocumentId IS NOT NULL THEN 1 ELSE 0 END
           ELSE CASE WHEN factor.IsRequired = 0 THEN 1 ELSE 0 END
       END AS bit) AS IsSatisfied
FROM Submissions.Quote quote
JOIN Submissions.Submission submission ON submission.SubmissionId = quote.SubmissionId AND submission.IsDeleted = 0
JOIN Submissions.SubmissionReadinessRequirement factor ON factor.TenantId = submission.TenantId
    AND factor.ScopeCode = N'Proposal'
    AND (factor.LineOfBusiness = N'All' OR factor.LineOfBusiness = submission.LineOfBusiness)
    AND (factor.CarrierId IS NULL OR factor.CarrierId = quote.CarrierId)
    AND factor.IsActive = 1
    AND factor.IsDeleted = 0
WHERE quote.SubmissionId = @SubmissionId
  AND quote.IsDeleted = 0
  AND (@QuoteId IS NULL OR quote.QuoteId = @QuoteId)
ORDER BY quote.QuoteId, factor.SortOrder, factor.DisplayName;
""";
        return (await cn.QueryAsync<ProposalReadinessFactorDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, QuoteId = quoteId }, cancellationToken: cancellationToken))).AsList();
    }

    private static async Task<IReadOnlyList<SubmissionQuoteLineDto>> GetQuoteLinesAsync(IDbConnection cn, Guid? submissionId, Guid? quoteId, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT ql.QuoteLineId, ql.TenantId, ql.QuoteId, ql.SubmissionId, ql.SubmissionLineId, ql.OpportunityLineId,
       ql.LineOfBusiness, ql.Status, ql.QuotedPremium, ql.Deductible, ql.[Limit], ql.CommissionPercent,
       ql.CoverageForms, ql.Subjectivities, ql.Exclusions, ql.PaymentTerms, ql.MinimumEarnedPremium,
       ql.TaxesAndFees, ql.BrokerFee, ql.TriaIncluded, ql.IsBindable, ql.CoverageNotes, ql.SortOrder,
       ql.CreatedDateUtc, ql.ModifiedDateUtc
FROM Submissions.QuoteLine ql
WHERE ql.IsDeleted = 0
  AND (@SubmissionId IS NULL OR ql.SubmissionId = @SubmissionId)
  AND (@QuoteId IS NULL OR ql.QuoteId = @QuoteId)
ORDER BY ql.QuoteId, ql.SortOrder, ql.LineOfBusiness;";
        return (await cn.QueryAsync<SubmissionQuoteLineDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, QuoteId = quoteId }, cancellationToken: cancellationToken))).AsList();
    }

    private static async Task SynchronizeQuoteLinesAsync(
        IDbConnection cn,
        IDbTransaction transaction,
        Guid quoteId,
        Guid submissionId,
        Guid tenantId,
        IReadOnlyList<SubmissionQuoteLineTermRequest>? lines,
        Guid? modifiedByUserId,
        CancellationToken cancellationToken)
    {
        if (lines is null || lines.Count == 0)
            return;

        if (lines.GroupBy(line => line.SubmissionLineId).Any(group => group.Count() > 1))
            throw new InvalidOperationException("A submission line can appear only once in a quote response.");

        const string deactivateSql = @"
UPDATE Submissions.QuoteLine
SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
WHERE QuoteId = @QuoteId
  AND SubmissionId = @SubmissionId
  AND TenantId = @TenantId
  AND IsDeleted = 0
  AND SubmissionLineId NOT IN @SubmissionLineIds;";
        await cn.ExecuteAsync(new CommandDefinition(deactivateSql, new
        {
            QuoteId = quoteId,
            SubmissionId = submissionId,
            TenantId = tenantId,
            SubmissionLineIds = lines.Select(line => line.SubmissionLineId).ToArray(),
            ModifiedByUserId = modifiedByUserId
        }, transaction, cancellationToken: cancellationToken));

        const string upsertSql = @"
IF NOT EXISTS
(
    SELECT 1 FROM Submissions.SubmissionLine
    WHERE SubmissionLineId = @SubmissionLineId AND SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0
)
    THROW 52040, 'Quote line does not belong to the submission.', 1;

DECLARE @OpportunityLineId UNIQUEIDENTIFIER =
(
    SELECT OpportunityLineId FROM Submissions.SubmissionLine
    WHERE SubmissionLineId = @SubmissionLineId AND SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0
);

IF EXISTS (SELECT 1 FROM Submissions.QuoteLine WHERE QuoteId = @QuoteId AND SubmissionLineId = @SubmissionLineId)
BEGIN
    UPDATE Submissions.QuoteLine
    SET OpportunityLineId = @OpportunityLineId,
        LineOfBusiness = @LineOfBusiness,
        QuotedPremium = @QuotedPremium,
        Deductible = @Deductible,
        [Limit] = @Limit,
        CommissionPercent = @CommissionPercent,
        CoverageForms = @CoverageForms,
        Subjectivities = @Subjectivities,
        Exclusions = @Exclusions,
        PaymentTerms = @PaymentTerms,
        MinimumEarnedPremium = @MinimumEarnedPremium,
        TaxesAndFees = @TaxesAndFees,
        BrokerFee = @BrokerFee,
        TriaIncluded = @TriaIncluded,
        IsBindable = @IsBindable,
        CoverageNotes = @CoverageNotes,
        Status = @Status,
        SortOrder = @SortOrder,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ModifiedByUserId,
        IsDeleted = 0
    WHERE QuoteId = @QuoteId AND SubmissionLineId = @SubmissionLineId;
END
ELSE
BEGIN
    INSERT INTO Submissions.QuoteLine
        (QuoteLineId, TenantId, QuoteId, SubmissionId, SubmissionLineId, OpportunityLineId, LineOfBusiness, QuotedPremium,
         Deductible, [Limit], CommissionPercent, CoverageForms, Subjectivities, Exclusions, PaymentTerms, MinimumEarnedPremium,
         TaxesAndFees, BrokerFee, TriaIncluded, IsBindable, CoverageNotes, Status, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @QuoteId, @SubmissionId, @SubmissionLineId, @OpportunityLineId, @LineOfBusiness, @QuotedPremium,
         @Deductible, @Limit, @CommissionPercent, @CoverageForms, @Subjectivities, @Exclusions, @PaymentTerms, @MinimumEarnedPremium,
         @TaxesAndFees, @BrokerFee, @TriaIncluded, @IsBindable, @CoverageNotes, @Status, @SortOrder, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END;";

        foreach (var line in lines)
        {
            await cn.ExecuteAsync(new CommandDefinition(upsertSql, new
            {
                QuoteId = quoteId,
                SubmissionId = submissionId,
                TenantId = tenantId,
                line.SubmissionLineId,
                line.LineOfBusiness,
                line.Status,
                line.QuotedPremium,
                line.Deductible,
                line.Limit,
                line.CommissionPercent,
                line.CoverageForms,
                line.Subjectivities,
                line.Exclusions,
                line.PaymentTerms,
                line.MinimumEarnedPremium,
                line.TaxesAndFees,
                line.BrokerFee,
                line.TriaIncluded,
                line.IsBindable,
                line.CoverageNotes,
                line.SortOrder,
                ModifiedByUserId = modifiedByUserId
            }, transaction, cancellationToken: cancellationToken));
        }

        const string aggregateSql = @"
UPDATE q
SET AnnualPremium = totals.AnnualPremium,
    Status = COALESCE(totals.UnanimousStatus, q.Status),
    Deductible = totals.Deductible,
    [Limit] = totals.[Limit],
    CommissionPercent = totals.CommissionPercent,
    MinimumEarnedPremium = totals.MinimumEarnedPremium,
    TaxesAndFees = totals.TaxesAndFees,
    BrokerFee = totals.BrokerFee,
    TriaIncluded = totals.TriaIncluded,
    IsBindable = totals.IsBindable,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
FROM Submissions.Quote q
CROSS APPLY
(
    SELECT SUM(QuotedPremium) AS AnnualPremium,
           CASE WHEN COUNT(DISTINCT Status) = 1 THEN MAX(Status) END AS UnanimousStatus,
           SUM(Deductible) AS Deductible,
           SUM([Limit]) AS [Limit],
           CASE WHEN SUM(QuotedPremium) > 0 THEN SUM(COALESCE(CommissionPercent, 0) * QuotedPremium) / SUM(QuotedPremium) ELSE AVG(CommissionPercent) END AS CommissionPercent,
           SUM(MinimumEarnedPremium) AS MinimumEarnedPremium,
           SUM(TaxesAndFees) AS TaxesAndFees,
           SUM(BrokerFee) AS BrokerFee,
           CAST(CASE WHEN COUNT(TriaIncluded) = COUNT(1) AND MIN(CONVERT(int, TriaIncluded)) = 1 THEN 1 WHEN COUNT(TriaIncluded) = 0 THEN NULL ELSE 0 END AS bit) AS TriaIncluded,
           CAST(CASE WHEN COUNT(1) > 0 AND MIN(CONVERT(int, IsBindable)) = 1 THEN 1 ELSE 0 END AS bit) AS IsBindable
    FROM Submissions.QuoteLine
    WHERE QuoteId = @QuoteId AND IsDeleted = 0
) totals
WHERE q.QuoteId = @QuoteId AND q.SubmissionId = @SubmissionId AND q.IsDeleted = 0;

UPDATE qr
SET Status = q.Status,
    AnnualPremium = q.AnnualPremium,
    Deductible = q.Deductible,
    [Limit] = q.[Limit],
    CommissionPercent = q.CommissionPercent,
    MinimumEarnedPremium = q.MinimumEarnedPremium,
    TaxesAndFees = q.TaxesAndFees,
    BrokerFee = q.BrokerFee,
    IsBindable = q.IsBindable
FROM Submissions.QuoteRevision qr
JOIN Submissions.Quote q ON q.QuoteId = qr.QuoteId AND q.ResponseVersion = qr.ResponseVersion
WHERE qr.QuoteId = @QuoteId AND qr.IsDeleted = 0;";
        await cn.ExecuteAsync(new CommandDefinition(aggregateSql, new { QuoteId = quoteId, SubmissionId = submissionId, ModifiedByUserId = modifiedByUserId }, transaction, cancellationToken: cancellationToken));
    }

    public async Task<SubmissionActionResult> RecordQuoteResponseAsync(Guid submissionId, RecordSubmissionQuoteResponseRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        using var transaction = cn.BeginTransaction();
        const string sql = @"
DECLARE @CarrierId UNIQUEIDENTIFIER;
DECLARE @QuoteId UNIQUEIDENTIFIER = COALESCE(@QuoteIdIn, NEWID());
DECLARE @ExistingQuoteId UNIQUEIDENTIFIER;
DECLARE @QuoteRequestId UNIQUEIDENTIFIER;
DECLARE @NormalizedStatus NVARCHAR(50) = REPLACE(COALESCE(NULLIF(@Status, N''), N'Received'), N' ', N'');
DECLARE @CreatesQuote BIT = CASE WHEN @NormalizedStatus IN (N'Declined', N'Rejected', N'MoreInformationRequired', N'Referred', N'Failed', N'Expired', N'Withdrawn', N'NoResponse', N'Cancelled') THEN 0 ELSE 1 END;
DECLARE @MappedQuoteRequestStatus NVARCHAR(50) = CASE
    WHEN @NormalizedStatus IN (N'Declined', N'Rejected') THEN N'Declined'
    WHEN @NormalizedStatus = N'MoreInformationRequired' THEN N'MoreInformationRequired'
    WHEN @NormalizedStatus = N'Referred' THEN N'UnderReview'
    WHEN @NormalizedStatus = N'Failed' THEN N'Failed'
    WHEN @NormalizedStatus = N'Expired' THEN N'Expired'
    WHEN @NormalizedStatus IN (N'Withdrawn', N'NoResponse') THEN N'Cancelled'
    WHEN @NormalizedStatus = N'Cancelled' THEN N'Cancelled'
    ELSE N'Quoted'
END;

SELECT @CarrierId = CarrierId
FROM Submissions.SubmissionMarket
WHERE SubmissionMarketId = @SubmissionMarketId
  AND SubmissionId = @SubmissionId
  AND IsDeleted = 0;

IF @CarrierId IS NULL THROW 52017, 'Submission market was not found for quote response.', 1;

SELECT TOP 1 @QuoteRequestId = qr.QuoteRequestId
FROM Submissions.QuoteRequest qr
WHERE qr.SubmissionMarketId = @SubmissionMarketId
  AND qr.SubmissionId = @SubmissionId
  AND qr.TenantId = @TenantId
  AND qr.IsDeleted = 0
ORDER BY qr.RequestVersion DESC, qr.RequestedDateUtc DESC;

IF @QuoteRequestId IS NULL
BEGIN
    SET @QuoteRequestId = NEWID();

    INSERT INTO Submissions.QuoteRequest
        (QuoteRequestId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, QuoteRequestActionCode, QuoteRequestMethodCode, QuoteRequestScopeCode,
         RequestVersion, StatusCode, RequestedDateUtc, RequestedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES
        (@QuoteRequestId, @TenantId, @SubmissionId, @SubmissionMarketId, @CarrierId, N'InitialRequest', COALESCE(NULLIF((SELECT TOP 1 SubmissionMethodCode FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0), N''), N'ManualUnderwriter'), N'Package',
         COALESCE((SELECT MAX(RequestVersion) FROM Submissions.QuoteRequest WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0), 0) + 1,
         N'Submitted', SYSUTCDATETIME(), @ReceivedByUserId, SYSUTCDATETIME(), @ReceivedByUserId, 0);
END;

IF @QuoteIdIn IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Submissions.Quote WHERE QuoteId = @QuoteIdIn AND SubmissionId = @SubmissionId AND SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0)
    THROW 52018, 'Quote response does not belong to the selected market request.', 1;

IF @CreatesQuote = 0
BEGIN
    UPDATE Submissions.QuoteRequest
    SET StatusCode = @MappedQuoteRequestStatus,
        CarrierReferenceNumber = COALESCE(NULLIF(@CarrierReferenceNumber, N''), CarrierReferenceNumber),
        CoverageNotes = COALESCE(NULLIF(@CoverageNotes, N''), CoverageNotes),
    ResponseDateUtc = COALESCE(ResponseDateUtc, SYSUTCDATETIME()),
    LastError = CASE WHEN @MappedQuoteRequestStatus IN (N'Failed', N'Declined') THEN COALESCE(NULLIF(@CoverageNotes, N''), LastError) ELSE LastError END,
        ClosedDateUtc = CASE WHEN @MappedQuoteRequestStatus IN (N'Declined', N'Failed', N'Expired', N'Cancelled') THEN COALESCE(ClosedDateUtc, SYSUTCDATETIME()) ELSE ClosedDateUtc END,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ReceivedByUserId
    WHERE QuoteRequestId = @QuoteRequestId;

    UPDATE Submissions.QuoteRequestHistory
    SET StatusCode = @MappedQuoteRequestStatus,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ReceivedByUserId
    WHERE SubmissionMarketId = @SubmissionMarketId
      AND IsDeleted = 0
      AND StatusCode IN (N'PendingDispatch', N'Submitted', N'Acknowledged', N'UnderReview', N'MoreInformationRequired');

    UPDATE Submissions.SubmissionMarket
    SET Status = CASE
            WHEN @MappedQuoteRequestStatus = N'Declined' THEN N'Declined'
            WHEN @MappedQuoteRequestStatus = N'MoreInformationRequired' THEN N'Need Info'
            WHEN @MappedQuoteRequestStatus = N'UnderReview' THEN N'Under Review'
            WHEN @MappedQuoteRequestStatus IN (N'Failed', N'Expired', N'Cancelled') THEN @MappedQuoteRequestStatus
            ELSE Status
        END,
        RespondedDateUtc = CASE WHEN @MappedQuoteRequestStatus IN (N'Declined', N'Failed', N'Expired', N'Cancelled') THEN COALESCE(RespondedDateUtc, SYSUTCDATETIME()) ELSE RespondedDateUtc END,
        DeclineReason = CASE WHEN @MappedQuoteRequestStatus = N'Declined' THEN COALESCE(NULLIF(@CoverageNotes, N''), DeclineReason) ELSE DeclineReason END,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ReceivedByUserId
    WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0;

    IF @MappedQuoteRequestStatus IN (N'MoreInformationRequired', N'UnderReview')
    BEGIN
        DECLARE @ResponsibleUserId UNIQUEIDENTIFIER = COALESCE(@ReceivedByUserId, (SELECT TOP 1 AssignedToUserId FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0));
        DECLARE @AccountId UNIQUEIDENTIFIER = (SELECT TOP 1 AccountId FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0);
        DECLARE @FollowUpTaskId UNIQUEIDENTIFIER = NEWID();
        DECLARE @FollowUpTitle NVARCHAR(200) = CASE WHEN @MappedQuoteRequestStatus = N'MoreInformationRequired' THEN N'Provide carrier requested information' ELSE N'Follow up on referred quote request' END;
        DECLARE @FollowUpDescription NVARCHAR(2000) = COALESCE(NULLIF(@CoverageNotes, N''), CONCAT(N'Carrier response recorded as ', @MappedQuoteRequestStatus, N'.'));

        IF @MappedQuoteRequestStatus = N'MoreInformationRequired'
           AND NOT EXISTS (SELECT 1 FROM Submissions.SubmissionIntakeQuestion WHERE SubmissionId = @SubmissionId AND QuestionCode = CONCAT(N'MarketInfo-', LEFT(CONVERT(NVARCHAR(36), @QuoteRequestId), 8)) AND IsDeleted = 0)
        BEGIN
            INSERT INTO Submissions.SubmissionIntakeQuestion
                (IntakeQuestionId, SubmissionId, TenantId, QuestionCode, QuestionText, HelpText, IsRequired, AnswerText, IsAnswered, StatusCode, StatusReason, ReviewDueDateUtc, SubmissionMarketId, CarrierId, ScopeCode, BlocksSubmit, CreatedDateUtc, IsDeleted)
            VALUES
                (NEWID(), @SubmissionId, @TenantId, CONCAT(N'MarketInfo-', LEFT(CONVERT(NVARCHAR(36), @QuoteRequestId), 8)), @FollowUpTitle, @FollowUpDescription, 1, NULL, 0, N'NeedsReview', @FollowUpDescription, DATEADD(day, 3, SYSUTCDATETIME()), @SubmissionMarketId, @CarrierId, N'MarketResponse', 1, SYSUTCDATETIME(), 0);
        END;

        IF OBJECT_ID(N'OPS.TaskItem', N'U') IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM OPS.TaskItem WHERE TenantId = @TenantId AND RelatedEntityName = N'QuoteRequest' AND RelatedEntityId = @QuoteRequestId AND TaskTypeCode IN (N'MarketInfoRequest', N'MarketFollowUp') AND IsDeleted = 0)
        BEGIN
            INSERT INTO OPS.TaskItem
                (TaskItemId, TenantId, TaskNumber, Title, Description, TaskTypeCode, StageCode, PriorityCode, StatusCode, RelatedEntityName, RelatedEntityId, AccountId, AssignedToUserId, DueDate, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES
                (@FollowUpTaskId, @TenantId, CONCAT(N'TASK-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), @FollowUpTaskId), N'-', N''), 6)), @FollowUpTitle, @FollowUpDescription,
                 CASE WHEN @MappedQuoteRequestStatus = N'MoreInformationRequired' THEN N'MarketInfoRequest' ELSE N'MarketFollowUp' END, N'Marketing', N'High', N'Open', N'QuoteRequest', @QuoteRequestId, @AccountId, @ResponsibleUserId, DATEADD(day, CASE WHEN @MappedQuoteRequestStatus = N'MoreInformationRequired' THEN 3 ELSE 5 END, CONVERT(date, SYSUTCDATETIME())), SYSUTCDATETIME(), @ReceivedByUserId, 0);

            UPDATE Submissions.SubmissionMarket
            SET FollowUpTaskId = COALESCE(FollowUpTaskId, @FollowUpTaskId),
                NextActionDateUtc = COALESCE(NextActionDateUtc, DATEADD(day, CASE WHEN @MappedQuoteRequestStatus = N'MoreInformationRequired' THEN 3 ELSE 5 END, SYSUTCDATETIME()))
            WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0;
        END;

        IF OBJECT_ID(N'Core.Notification', N'U') IS NOT NULL AND @ResponsibleUserId IS NOT NULL
        BEGIN
            INSERT INTO Core.Notification
                (NotificationId, TenantId, RecipientUserId, ChannelCode, Subject, Body, EntityName, EntityId, StatusCode, IsRead, Priority, Category, DeliveryProvider, DeliveryStatus, PolicyStatus, SyncStatus, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES
                (NEWID(), @TenantId, @ResponsibleUserId, N'InApp', @FollowUpTitle, @FollowUpDescription, N'QuoteRequest', @QuoteRequestId, N'Delivered', 0, N'High', N'Quote Request', N'AMS', N'Delivered', N'Compliant', N'Synced', SYSUTCDATETIME(), @ReceivedByUserId, 0);
        END;
    END;

    INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
    VALUES (NEWID(), @SubmissionId, @TenantId, N'QuoteRequestResponseRecorded', CONCAT(N'Quote request response recorded as ', @MappedQuoteRequestStatus, N'.'), SYSUTCDATETIME(), @ReceivedByUserId, N'QuoteRequest', @QuoteRequestId, N'User', 0);

    SELECT @QuoteRequestId;
    RETURN;
END;

SET @ExistingQuoteId = (SELECT TOP 1 QuoteId FROM Submissions.Quote WHERE QuoteId = @QuoteId AND IsDeleted = 0);

IF @ExistingQuoteId IS NULL
BEGIN
    INSERT INTO Submissions.Quote
        (QuoteId, SubmissionId, SubmissionMarketId, QuoteRequestId, CarrierId, QuoteNumber, Status, AnnualPremium, Deductible, [Limit], CommissionPercent,
         Subjectivities, Exclusions, CarrierRating, PaymentTerms, MinimumEarnedPremium, TaxesAndFees, BrokerFee, TriaIncluded,
         EffectiveDate, CoverageForms, IsBindable, QuoteDocumentId, CoverageNotes, ReviewedByUserId, ReviewedDateUtc, ApprovedForPresentationByUserId, ApprovedForPresentationDateUtc, PresentationReadinessNotes,
         QuotedDateUtc, ExpiresDateUtc, QuoteRequestDateUtc, QuoteReceivedDateUtc, ResponseVersion,
         ResponseSourceCode, CarrierReferenceNumber, ReceivedByUserId, CreatedDateUtc, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (@QuoteId, @SubmissionId, @SubmissionMarketId, @QuoteRequestId, @CarrierId, N'QT-' + CONVERT(NVARCHAR(8), SYSUTCDATETIME(), 112) + N'-' + RIGHT(REPLACE(CONVERT(NVARCHAR(36), @QuoteId), N'-', N''), 6),
         @Status, @AnnualPremium, @Deductible, @Limit, @CommissionPercent, @Subjectivities, @Exclusions, @CarrierRating, @PaymentTerms,
         @MinimumEarnedPremium, @TaxesAndFees, @BrokerFee, @TriaIncluded, @EffectiveDate, @CoverageForms, @IsBindable, @QuoteDocumentId, @CoverageNotes,
         @ReceivedByUserId,
         SYSUTCDATETIME(),
         CASE WHEN @Status = N'Approved for Presentation' THEN @ReceivedByUserId ELSE NULL END,
         CASE WHEN @Status = N'Approved for Presentation' THEN SYSUTCDATETIME() ELSE NULL END,
         CASE WHEN @Status = N'Approved for Presentation' THEN NULLIF(@CoverageNotes, N'') ELSE NULL END,
         SYSUTCDATETIME(), @ExpiresDateUtc,
         (SELECT COALESCE(SubmittedDateUtc, AddedDateUtc) FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId),
         SYSUTCDATETIME(), 1, COALESCE(NULLIF(@ResponseSourceCode, N''), N'ManualEntry'), @CarrierReferenceNumber, @ReceivedByUserId,
         SYSUTCDATETIME(), SYSUTCDATETIME(), @ReceivedByUserId, 0);
END
ELSE
BEGIN
    UPDATE Submissions.Quote
    SET Status = @Status,
        ReviewedByUserId = COALESCE(ReviewedByUserId, @ReceivedByUserId),
        ReviewedDateUtc = COALESCE(ReviewedDateUtc, SYSUTCDATETIME()),
        ApprovedForPresentationByUserId = CASE WHEN @Status = N'Approved for Presentation' THEN COALESCE(ApprovedForPresentationByUserId, @ReceivedByUserId) ELSE ApprovedForPresentationByUserId END,
        ApprovedForPresentationDateUtc = CASE WHEN @Status = N'Approved for Presentation' THEN COALESCE(ApprovedForPresentationDateUtc, SYSUTCDATETIME()) ELSE ApprovedForPresentationDateUtc END,
        PresentationReadinessNotes = CASE WHEN @Status = N'Approved for Presentation' THEN COALESCE(NULLIF(@CoverageNotes, N''), PresentationReadinessNotes) ELSE PresentationReadinessNotes END,
        QuoteRequestId = COALESCE(QuoteRequestId, @QuoteRequestId),
        AnnualPremium = @AnnualPremium,
        Deductible = @Deductible,
        [Limit] = @Limit,
        CommissionPercent = @CommissionPercent,
        Subjectivities = @Subjectivities,
        Exclusions = @Exclusions,
        CarrierRating = @CarrierRating,
        PaymentTerms = @PaymentTerms,
        MinimumEarnedPremium = @MinimumEarnedPremium,
        TaxesAndFees = @TaxesAndFees,
        BrokerFee = @BrokerFee,
        TriaIncluded = @TriaIncluded,
        EffectiveDate = @EffectiveDate,
        CoverageForms = @CoverageForms,
        IsBindable = @IsBindable,
        QuoteDocumentId = @QuoteDocumentId,
        CoverageNotes = @CoverageNotes,
        ExpiresDateUtc = @ExpiresDateUtc,
        QuoteReceivedDateUtc = COALESCE(QuoteReceivedDateUtc, SYSUTCDATETIME()),
        ResponseVersion = ResponseVersion + 1,
        ResponseSourceCode = COALESCE(NULLIF(@ResponseSourceCode, N''), ResponseSourceCode, N'ManualEntry'),
        CarrierReferenceNumber = COALESCE(NULLIF(@CarrierReferenceNumber, N''), CarrierReferenceNumber),
        ReceivedByUserId = COALESCE(@ReceivedByUserId, ReceivedByUserId),
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ReceivedByUserId
    WHERE QuoteId = @QuoteId;
END;

UPDATE Submissions.Quote
SET RecommendationScore = CONVERT(int, ROUND(
        (CASE WHEN NULLIF(AnnualPremium, 0) IS NULL THEN 0 ELSE 35 END) +
        (CASE WHEN CarrierRating IN (N'A++', N'A+', N'A', N'A-') THEN 20 WHEN CarrierRating LIKE N'B%' THEN 10 ELSE 5 END) +
        (CASE WHEN COALESCE(NULLIF(Subjectivities, N''), N'') = N'' THEN 15 ELSE 5 END) +
        (CASE WHEN COALESCE(CommissionPercent, 0) >= 10 THEN 10 ELSE 5 END) +
        (CASE WHEN ExpiresDateUtc > DATEADD(day, 14, SYSUTCDATETIME()) THEN 10 ELSE 2 END) +
        (CASE WHEN COALESCE(TriaIncluded, 0) = 1 THEN 10 ELSE 5 END), 0)),
    RecommendationReason = CONCAT(N'Premium, carrier rating, subjectivity burden, commission, expiration risk, and coverage breadth scored on ', CONVERT(nvarchar(10), SYSUTCDATETIME(), 120), N'.')
WHERE QuoteId = @QuoteId;

INSERT INTO Submissions.QuoteRevision
    (QuoteRevisionId, QuoteId, SubmissionId, SubmissionMarketId, TenantId, ResponseVersion, Status, AnnualPremium, Deductible, [Limit], CommissionPercent,
     TaxesAndFees, BrokerFee, MinimumEarnedPremium, EffectiveDate, ExpiresDateUtc, CoverageForms, Subjectivities, Exclusions, CarrierRating, PaymentTerms,
     IsBindable, CoverageNotes, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), QuoteId, SubmissionId, SubmissionMarketId, @TenantId, ResponseVersion, Status, AnnualPremium, Deductible, [Limit], CommissionPercent,
       TaxesAndFees, BrokerFee, MinimumEarnedPremium, EffectiveDate, ExpiresDateUtc, CoverageForms, Subjectivities, Exclusions, CarrierRating, PaymentTerms,
       IsBindable, CoverageNotes, SYSUTCDATETIME(), @ReceivedByUserId, 0
FROM Submissions.Quote
WHERE QuoteId = @QuoteId
  AND NOT EXISTS (SELECT 1 FROM Submissions.QuoteRevision existing WHERE existing.QuoteId = Submissions.Quote.QuoteId AND existing.ResponseVersion = Submissions.Quote.ResponseVersion AND existing.IsDeleted = 0);

UPDATE Submissions.SubmissionMarket
SET Status = CASE WHEN @Status IN (N'Declined', N'Rejected') THEN N'Declined' ELSE N'Quoted' END,
    RespondedDateUtc = COALESCE(RespondedDateUtc, SYSUTCDATETIME()),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ReceivedByUserId
WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0;

UPDATE Submissions.QuoteRequestHistory
SET StatusCode = @MappedQuoteRequestStatus,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ReceivedByUserId
WHERE SubmissionMarketId = @SubmissionMarketId
  AND StatusCode IN (N'Open', N'PendingDispatch', N'Submitted', N'Acknowledged', N'UnderReview', N'MoreInformationRequired', N'Referred')
  AND IsDeleted = 0;

UPDATE Submissions.QuoteRequest
SET StatusCode = @MappedQuoteRequestStatus,
    Premium = COALESCE(Premium, @AnnualPremium),
    CommissionPercent = COALESCE(CommissionPercent, @CommissionPercent),
    QuoteNumber = COALESCE(QuoteNumber, (SELECT QuoteNumber FROM Submissions.Quote WHERE QuoteId = @QuoteId)),
    ExpirationDateUtc = COALESCE(ExpirationDateUtc, @ExpiresDateUtc),
    CarrierReferenceNumber = COALESCE(NULLIF(@CarrierReferenceNumber, N''), CarrierReferenceNumber),
    ResponseDateUtc = COALESCE(ResponseDateUtc, SYSUTCDATETIME()),
    ClosedDateUtc = COALESCE(ClosedDateUtc, SYSUTCDATETIME()),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ReceivedByUserId
WHERE QuoteRequestId = @QuoteRequestId;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'QuoteResponseRecorded', CONCAT(N'Carrier quote response recorded as ', @Status, N'.'), SYSUTCDATETIME(), @ReceivedByUserId, N'Quote', @QuoteId, N'User', 0);

SELECT @QuoteId;";
        var resultId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
        {
            SubmissionId = submissionId,
            request.TenantId,
            request.SubmissionMarketId,
            QuoteIdIn = request.QuoteId,
            request.Status,
            request.AnnualPremium,
            request.EffectiveDate,
            request.Deductible,
            request.Limit,
            request.CommissionPercent,
            request.CoverageForms,
            request.Subjectivities,
            request.Exclusions,
            request.CarrierRating,
            request.PaymentTerms,
            request.MinimumEarnedPremium,
            request.TaxesAndFees,
            request.BrokerFee,
            request.TriaIncluded,
            request.IsBindable,
            request.QuoteDocumentId,
            request.CoverageNotes,
            request.ExpiresDateUtc,
            request.ResponseSourceCode,
            request.CarrierReferenceNumber,
            request.ReceivedByUserId
        }, transaction, cancellationToken: cancellationToken));
        var normalizedStatus = (request.Status ?? string.Empty).Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        var createsQuote = normalizedStatus is not ("Declined" or "Rejected" or "MoreInformationRequired" or "Referred" or "Failed" or "Expired" or "Withdrawn" or "NoResponse" or "Cancelled");
        if (createsQuote)
            await SynchronizeQuoteLinesAsync(cn, transaction, resultId, submissionId, request.TenantId, request.Lines, request.ReceivedByUserId, cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(RecalculateSubmissionStatusSql, new { SubmissionId = submissionId, request.TenantId }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
        var relatedEntity = createsQuote ? "Quote" : "QuoteRequest";
        var workflowStage = createsQuote ? "Quotes Received" : "Marketing";
        var workflowTitle = createsQuote ? "Quote Response Recorded" : "Quote Request Response Recorded";
        var workflowNotes = request.CoverageNotes ?? (createsQuote ? "Carrier quote response recorded." : $"Carrier response recorded as {request.Status} without creating a quote.");
        await RecordOpportunityWorkflowAsync(cn, submissionId, request.TenantId, workflowStage, workflowTitle, workflowTitle, workflowNotes, relatedEntity, resultId, request.ReceivedByUserId, cancellationToken);
        return new SubmissionActionResult(resultId, createsQuote ? "Carrier quote response recorded." : "Quote request response recorded without creating a quote.");
    }

    public async Task<Guid> RecordCarrierInboundResponseAsync(Guid submissionId, RecordCarrierInboundResponseRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
DECLARE @InboundResponseId UNIQUEIDENTIFIER = NEWID();
DECLARE @SubmissionMarketId UNIQUEIDENTIFIER = @SubmissionMarketIdIn;
DECLARE @CarrierTransmissionId UNIQUEIDENTIFIER = @CarrierTransmissionIdIn;
DECLARE @CarrierId UNIQUEIDENTIFIER = @CarrierIdIn;

IF @CarrierTransmissionId IS NOT NULL
BEGIN
    SELECT TOP 1
        @SubmissionMarketId = COALESCE(@SubmissionMarketId, t.SubmissionMarketId),
        @CarrierId = COALESCE(@CarrierId, t.CarrierId)
    FROM Submissions.CarrierTransmission t
    WHERE t.CarrierTransmissionId = @CarrierTransmissionId
      AND t.SubmissionId = @SubmissionId
      AND t.TenantId = @TenantId
      AND t.IsDeleted = 0;
END;

IF @SubmissionMarketId IS NULL AND @CarrierId IS NOT NULL
BEGIN
    SELECT TOP 1 @SubmissionMarketId = sm.SubmissionMarketId
    FROM Submissions.SubmissionMarket sm
    WHERE sm.SubmissionId = @SubmissionId
      AND sm.TenantId = @TenantId
      AND sm.CarrierId = @CarrierId
      AND sm.IsDeleted = 0
    ORDER BY sm.AddedDateUtc DESC;
END;

IF @SubmissionMarketId IS NULL THROW 52061, 'Carrier inbound response could not be linked to a submission market.', 1;

SELECT @CarrierId = COALESCE(@CarrierId, sm.CarrierId)
FROM Submissions.SubmissionMarket sm
WHERE sm.SubmissionMarketId = @SubmissionMarketId
  AND sm.SubmissionId = @SubmissionId
  AND sm.TenantId = @TenantId
  AND sm.IsDeleted = 0;

IF @CarrierId IS NULL THROW 52062, 'Carrier inbound response market was not found.', 1;

IF @CarrierTransmissionId IS NULL
BEGIN
    SELECT TOP 1 @CarrierTransmissionId = t.CarrierTransmissionId
    FROM Submissions.CarrierTransmission t
    WHERE t.SubmissionMarketId = @SubmissionMarketId
      AND t.IsDeleted = 0
    ORDER BY t.CreatedDateUtc DESC;
END;

INSERT INTO Submissions.CarrierInboundResponse
    (CarrierInboundResponseId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, CarrierTransmissionId,
     SourceChannelCode, ResponseTypeCode, StatusCode, CarrierReferenceNumber, PayloadJson, ReceivedDateUtc, ProcessedDateUtc,
     CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES
    (@InboundResponseId, @TenantId, @SubmissionId, @SubmissionMarketId, @CarrierId, @CarrierTransmissionId,
     @SourceChannelCode, @ResponseTypeCode, @StatusCode, @CarrierReferenceNumber, COALESCE(NULLIF(@PayloadJson, N''), N'{}'), COALESCE(@ReceivedDateUtc, SYSUTCDATETIME()),
     CASE WHEN @StatusCode IN (N'Processed', N'Accepted') THEN SYSUTCDATETIME() ELSE NULL END,
     SYSUTCDATETIME(), @CreatedByUserId, SYSUTCDATETIME(), @CreatedByUserId, 0);

IF @CarrierTransmissionId IS NOT NULL
BEGIN
    UPDATE Submissions.CarrierTransmission
    SET StatusCode = CASE
            WHEN @ResponseTypeCode = N'DeliveryConfirmation' THEN N'Delivered'
            WHEN @ResponseTypeCode IN (N'Bounce', N'Failure') THEN N'Failed'
            WHEN StatusCode = N'AwaitingExternalConnector' THEN N'ResponseReceived'
            ELSE StatusCode END,
        ConfirmedDateUtc = CASE WHEN @ResponseTypeCode = N'DeliveryConfirmation' THEN COALESCE(ConfirmedDateUtc, SYSUTCDATETIME()) ELSE ConfirmedDateUtc END,
        FailedDateUtc = CASE WHEN @ResponseTypeCode IN (N'Bounce', N'Failure') THEN COALESCE(FailedDateUtc, SYSUTCDATETIME()) ELSE FailedDateUtc END,
        BounceDateUtc = CASE WHEN @ResponseTypeCode = N'Bounce' THEN COALESCE(BounceDateUtc, SYSUTCDATETIME()) ELSE BounceDateUtc END,
        LastError = CASE WHEN @ResponseTypeCode IN (N'Bounce', N'Failure') THEN @CarrierReferenceNumber ELSE LastError END,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @CreatedByUserId
    WHERE CarrierTransmissionId = @CarrierTransmissionId
      AND IsDeleted = 0;

    INSERT INTO Submissions.CarrierTransmissionEvent
        (CarrierTransmissionEventId, TenantId, CarrierTransmissionId, SubmissionId, SubmissionMarketId, EventCode, EventMessage, EventPayloadJson, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @CarrierTransmissionId, @SubmissionId, @SubmissionMarketId,
         CONCAT(N'Inbound', @ResponseTypeCode), CONCAT(N'Inbound carrier response recorded with status ', @StatusCode, N'.'), COALESCE(NULLIF(@PayloadJson, N''), N'{}'), SYSUTCDATETIME(), @CreatedByUserId, 0);
END;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'CarrierInboundResponseRecorded', CONCAT(N'Carrier inbound response recorded: ', @ResponseTypeCode, N' / ', @StatusCode, N'.'), SYSUTCDATETIME(), @CreatedByUserId, N'CarrierInboundResponse', @InboundResponseId, N'CarrierConnector', 0);

SELECT @InboundResponseId;";
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
        {
            SubmissionId = submissionId,
            request.TenantId,
            SubmissionMarketIdIn = request.SubmissionMarketId,
            CarrierTransmissionIdIn = request.CarrierTransmissionId,
            CarrierIdIn = request.CarrierId,
            request.SourceChannelCode,
            request.ResponseTypeCode,
            request.StatusCode,
            request.CarrierReferenceNumber,
            request.PayloadJson,
            request.ReceivedDateUtc,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateQuoteAsync(Guid quoteId, UpdateSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        using var transaction = cn.BeginTransaction();
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @CarrierId UNIQUEIDENTIFIER;
DECLARE @MarketId UNIQUEIDENTIFIER;
SELECT @SubmissionId = q.SubmissionId, @CarrierId = q.CarrierId, @MarketId = q.SubmissionMarketId
FROM Submissions.Quote q
JOIN Submissions.Submission s ON s.SubmissionId = q.SubmissionId AND s.TenantId = @TenantId AND s.IsDeleted = 0
WHERE q.QuoteId = @QuoteId AND q.IsDeleted = 0;
IF @SubmissionId IS NULL THROW 52014, 'Quote was not found.', 1;

IF @SubmissionMarketId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND SubmissionId = @SubmissionId AND CarrierId = @CarrierId AND IsDeleted = 0)
        THROW 52016, 'Quote market request does not match this quote.', 1;
    SET @MarketId = @SubmissionMarketId;
END;

IF @MarketId IS NULL
    SET @MarketId = (SELECT TOP 1 SubmissionMarketId FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND CarrierId = @CarrierId AND IsDeleted = 0 ORDER BY IsRecommended DESC, AddedDateUtc DESC);

UPDATE Submissions.Quote
SET SubmissionMarketId = COALESCE(@MarketId, SubmissionMarketId),
    Status = @Status,
    ReviewedByUserId = CASE WHEN @Status IN (N'Under Review', N'Approved for Presentation', N'Presented', N'Selected', N'Bound') THEN COALESCE(ReviewedByUserId, @ReceivedByUserId, @ModifiedByUserId) ELSE ReviewedByUserId END,
    ReviewedDateUtc = CASE WHEN @Status IN (N'Under Review', N'Approved for Presentation', N'Presented', N'Selected', N'Bound') THEN COALESCE(ReviewedDateUtc, SYSUTCDATETIME()) ELSE ReviewedDateUtc END,
    ApprovedForPresentationByUserId = CASE WHEN @Status = N'Approved for Presentation' THEN COALESCE(ApprovedForPresentationByUserId, @ReceivedByUserId, @ModifiedByUserId) ELSE ApprovedForPresentationByUserId END,
    ApprovedForPresentationDateUtc = CASE WHEN @Status = N'Approved for Presentation' THEN COALESCE(ApprovedForPresentationDateUtc, SYSUTCDATETIME()) ELSE ApprovedForPresentationDateUtc END,
    PresentationReadinessNotes = CASE WHEN @Status = N'Approved for Presentation' THEN COALESCE(NULLIF(@CoverageNotes, N''), PresentationReadinessNotes) ELSE PresentationReadinessNotes END,
    AnnualPremium = @AnnualPremium,
    EffectiveDate = @EffectiveDate,
    Deductible = @Deductible,
    [Limit] = @Limit,
    CoverageForms = @CoverageForms,
    CommissionPercent = @CommissionPercent,
    Subjectivities = @Subjectivities,
    Exclusions = @Exclusions,
    CarrierRating = @CarrierRating,
    PaymentTerms = @PaymentTerms,
    MinimumEarnedPremium = @MinimumEarnedPremium,
    TaxesAndFees = @TaxesAndFees,
    BrokerFee = @BrokerFee,
    TriaIncluded = @TriaIncluded,
    IsBindable = @IsBindable,
    QuoteDocumentId = @QuoteDocumentId,
    CoverageNotes = @CoverageNotes,
    ExpiresDateUtc = @ExpiresDateUtc,
    QuoteRequestDateUtc = COALESCE(QuoteRequestDateUtc, (SELECT SubmittedDateUtc FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId), CreatedDateUtc),
    QuoteReceivedDateUtc = CASE WHEN @Status IN (N'Received', N'Under Review', N'Approved for Presentation', N'Presented', N'Bound', N'Selected') THEN COALESCE(QuoteReceivedDateUtc, SYSUTCDATETIME()) ELSE QuoteReceivedDateUtc END,
    ResponseVersion = CASE WHEN @Status IN (N'Revision', N'Revision Requested') THEN ResponseVersion + 1 ELSE ResponseVersion END,
    ResponseSourceCode = COALESCE(NULLIF(@ResponseSourceCode, N''), CASE WHEN @Status IN (N'Received', N'Under Review', N'Approved for Presentation', N'Presented', N'Bound', N'Selected') THEN N'ManualEntry' ELSE ResponseSourceCode END),
    CarrierReferenceNumber = COALESCE(NULLIF(@CarrierReferenceNumber, N''), CarrierReferenceNumber),
    ReceivedByUserId = COALESCE(@ReceivedByUserId, ReceivedByUserId, CASE WHEN @Status IN (N'Received', N'Under Review', N'Approved for Presentation', N'Presented', N'Bound', N'Selected') THEN @ModifiedByUserId ELSE NULL END),
    RecommendationScore = CONVERT(int, ROUND(
        (CASE WHEN NULLIF(@AnnualPremium, 0) IS NULL THEN 0 ELSE 35 END) +
        (CASE WHEN @CarrierRating IN (N'A++', N'A+', N'A', N'A-') THEN 20 WHEN @CarrierRating LIKE N'B%' THEN 10 ELSE 5 END) +
        (CASE WHEN COALESCE(NULLIF(@Subjectivities, N''), N'') = N'' THEN 15 ELSE 5 END) +
        (CASE WHEN COALESCE(@CommissionPercent, 0) >= 10 THEN 10 ELSE 5 END) +
        (CASE WHEN @ExpiresDateUtc > DATEADD(day, 14, SYSUTCDATETIME()) THEN 10 ELSE 2 END) +
        (CASE WHEN COALESCE(@TriaIncluded, 0) = 1 THEN 10 ELSE 5 END), 0)),
    RecommendationReason = CONCAT(N'Premium, carrier rating, subjectivity burden, commission, expiration risk, and coverage breadth scored on ', CONVERT(nvarchar(10), SYSUTCDATETIME(), 120), N'.'),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE QuoteId = @QuoteId;

UPDATE Submissions.SubmissionMarket
SET Status = CASE
        WHEN @Status IN (N'Bound', N'Selected') THEN N'Quoted'
        WHEN @Status IN (N'Received', N'Under Review', N'Approved for Presentation', N'Presented') THEN N'Quoted'
        WHEN @Status IN (N'Declined', N'Rejected') THEN N'Declined'
        WHEN @Status IN (N'Requested', N'Revision', N'Revision Requested') THEN N'In Review'
        ELSE Status
    END,
    RespondedDateUtc = CASE WHEN @Status IN (N'Received', N'Under Review', N'Approved for Presentation', N'Presented', N'Bound', N'Selected', N'Declined', N'Rejected') THEN COALESCE(RespondedDateUtc, SYSUTCDATETIME()) ELSE RespondedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'QuoteUpdated', CONCAT(N'Quote updated to ', @Status, N'.'), SYSUTCDATETIME(), @ModifiedByUserId, N'Quote', @QuoteId, N'User', 0);

SELECT @SubmissionId;";
        var submissionId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { QuoteId = quoteId, request.TenantId, request.SubmissionMarketId, request.Status, request.AnnualPremium, request.EffectiveDate, request.Deductible, request.Limit, request.CoverageForms, request.CommissionPercent, request.Subjectivities, request.Exclusions, request.CarrierRating, request.PaymentTerms, request.MinimumEarnedPremium, request.TaxesAndFees, request.BrokerFee, request.TriaIncluded, request.IsBindable, request.QuoteDocumentId, request.CoverageNotes, request.ExpiresDateUtc, request.ModifiedByUserId, request.ResponseSourceCode, request.CarrierReferenceNumber, request.ReceivedByUserId }, transaction, cancellationToken: cancellationToken));
        await SynchronizeQuoteLinesAsync(cn, transaction, quoteId, submissionId, request.TenantId, request.Lines, request.ModifiedByUserId, cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(RecalculateSubmissionStatusSql, new { SubmissionId = submissionId, request.TenantId }, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
    }

    public async Task SelectQuoteAsync(Guid submissionId, SelectSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM Submissions.Quote WHERE QuoteId = @QuoteId AND SubmissionId = @SubmissionId AND IsDeleted = 0)
    THROW 52015, 'Quote was not found for selection.', 1;

IF NOT EXISTS (SELECT 1 FROM Submissions.Quote WHERE QuoteId = @QuoteId AND SubmissionId = @SubmissionId AND IsDeleted = 0 AND Status IN (N'Presented', N'Approved for Presentation', N'Selected', N'Bound'))
    THROW 52034, 'Quote must be approved for presentation or presented before it can be selected for binding.', 1;

DECLARE @SelectedMarketId UNIQUEIDENTIFIER = (SELECT SubmissionMarketId FROM Submissions.Quote WHERE QuoteId = @QuoteId AND SubmissionId = @SubmissionId AND IsDeleted = 0);

UPDATE Submissions.Quote
SET IsSelected = 0,
    IsRecommended = 0,
    Status = CASE WHEN Status = N'Bound' THEN Status ELSE N'Not Selected' END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND QuoteId <> @QuoteId AND IsDeleted = 0;

UPDATE Submissions.Quote
SET IsSelected = 1,
    IsRecommended = @IsRecommended,
    Status = CASE WHEN Status = N'Bound' THEN Status ELSE N'Selected' END,
    QuoteReceivedDateUtc = COALESCE(QuoteReceivedDateUtc, SYSUTCDATETIME()),
    ResponseSourceCode = COALESCE(NULLIF(ResponseSourceCode, N''), N'ManualEntry'),
    SelectedByUserId = @SelectedByUserId,
    SelectedDateUtc = SYSUTCDATETIME(),
    SelectionReason = @Reason,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE QuoteId = @QuoteId;

UPDATE Submissions.SubmissionMarket
SET Status = CASE WHEN Status = N'Bound' THEN Status ELSE N'Quoted' END,
    RespondedDateUtc = COALESCE(RespondedDateUtc, SYSUTCDATETIME()),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @SelectedByUserId
WHERE SubmissionMarketId = @SelectedMarketId AND IsDeleted = 0;

UPDATE sm
SET Status = CASE WHEN sm.Status IN (N'Bound', N'Declined') THEN sm.Status ELSE N'Quoted' END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @SelectedByUserId
FROM Submissions.SubmissionMarket sm
INNER JOIN Submissions.Quote q ON q.SubmissionMarketId = sm.SubmissionMarketId AND q.SubmissionId = @SubmissionId AND q.QuoteId <> @QuoteId AND q.IsDeleted = 0
WHERE sm.IsDeleted = 0;

UPDATE Submissions.Submission
SET Status = N'Customer Accepted', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @SelectedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'QuoteSelected', @Reason, SYSUTCDATETIME(), @SelectedByUserId, N'Quote', @QuoteId, N'User', 0);";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { SubmissionId = submissionId, request.TenantId, request.QuoteId, request.IsRecommended, request.Reason, request.SelectedByUserId }, cancellationToken: cancellationToken));
    }

    // ── Proposals ─────────────────────────────────────────────────────

    public async Task<ProposalDto?> GetProposalByIdAsync(Guid proposalId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT ProposalId, SubmissionId, TenantId, Title, Status, GovernanceStatusCode, VersionNumber, PdfUrl, HtmlContent, CustomIntroduction,
       DeliveryMethod, Recipient, DeliveryStatus, LastDeliveryDispatchId, SentDateUtc, PresentedDateUtc, ClientDecision, DecisionNotes, DecisionDateUtc,
       CreatedDateUtc, GeneratedDateUtc, ApprovedDateUtc, ApprovedByUserId, ApprovalVersionNumber, ApprovedSnapshotHash, ReadyToDeliverDateUtc, DeliveryConfirmedDateUtc
FROM   Submissions.Proposal
WHERE  ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0;

SELECT q.QuoteId, q.QuoteNumber, c.CarrierName, q.AnnualPremium, q.Deductible, q.[Limit], q.CoverageNotes,
       q.TaxesAndFees, q.BrokerFee, q.MinimumEarnedPremium, q.PaymentTerms, q.TriaIncluded, q.IsBindable,
       q.CarrierRating, q.EffectiveDate, q.ExpiresDateUtc, q.IsSelected, pq.SortOrder
FROM Submissions.ProposalQuote pq
INNER JOIN Submissions.Quote q ON q.QuoteId = pq.QuoteId AND q.IsDeleted = 0
INNER JOIN Core.Carrier c ON c.CarrierId = q.CarrierId AND c.IsDeleted = 0
WHERE pq.ProposalId = @ProposalId AND pq.TenantId = @TenantId AND pq.IsDeleted = 0
ORDER BY pq.SortOrder;

SELECT line.QuoteLineId, line.QuoteId, line.SubmissionLineId, line.LineOfBusiness, line.Status, line.QuotedPremium, line.Deductible, line.[Limit],
       line.CommissionPercent, line.CoverageForms, line.Subjectivities, line.Exclusions, line.PaymentTerms,
       line.MinimumEarnedPremium, line.TaxesAndFees, line.BrokerFee, line.TriaIncluded, line.IsBindable,
       line.CoverageNotes, line.SortOrder
FROM Submissions.QuoteLine line
INNER JOIN Submissions.ProposalQuote pq
  ON pq.QuoteId = line.QuoteId AND pq.ProposalId = @ProposalId AND pq.TenantId = @TenantId AND pq.IsDeleted = 0
WHERE line.TenantId = @TenantId AND line.IsDeleted = 0
ORDER BY pq.SortOrder, line.SortOrder, line.LineOfBusiness;

SELECT ProposalLifecycleEventId, EventCode, EventDetail, EventDateUtc
FROM Submissions.ProposalLifecycleEvent
WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0
ORDER BY EventDateUtc DESC;

SELECT d.ProposalDeliveryDispatchId, d.TenantId, d.SubmissionId, d.ProposalId, d.DeliveryMethodCode,
       COALESCE(provider.DisplayName, d.DeliveryMethodCode) AS ProviderName, d.Recipient, d.StatusCode,
       d.ProposalVersionNumber, d.AttemptCount, d.MaxAttempts, d.NextAttemptDateUtc, d.CompletedDateUtc, d.ExternalDeliveryId,
       d.FirstViewedDateUtc, d.LastViewedDateUtc, d.DownloadedDateUtc, d.SignedDateUtc, d.DeclinedDateUtc, d.ExpiredDateUtc, d.BouncedDateUtc, d.CancelledDateUtc,
       d.ErrorCode, d.ErrorMessage, d.CreatedDateUtc,
       CAST(CASE WHEN d.StatusCode IN (N'Configuration Required', N'Failed') THEN 1 ELSE 0 END AS bit) AS CanRetry
FROM Submissions.ProposalDeliveryDispatch d
LEFT JOIN Submissions.ProposalDeliveryProvider provider
  ON provider.ProposalDeliveryProviderId = d.ProposalDeliveryProviderId AND provider.IsDeleted = 0
WHERE d.ProposalId = @ProposalId AND d.TenantId = @TenantId AND d.IsDeleted = 0
ORDER BY d.CreatedDateUtc DESC;

SELECT TOP 1 review.ProposalReviewId, review.ProposalId, review.ProposalVersionNumber, review.ReviewRound, review.StatusCode,
       review.AssignedReviewerUserId, COALESCE(reviewer.FullName, reviewer.DisplayName, reviewer.UserName) AS AssignedReviewerName,
       review.RequestedDateUtc, review.DueDateUtc, review.CompletedDateUtc, review.DecisionNotes
FROM Submissions.ProposalReview review
LEFT JOIN IAM.[User] reviewer ON reviewer.UserId = review.AssignedReviewerUserId
WHERE review.ProposalId = @ProposalId AND review.TenantId = @TenantId AND review.IsDeleted = 0
ORDER BY review.ReviewRound DESC;

SELECT recipient.ProposalRecipientId,
       recipient.ProposalId,
       recipient.ContactId,
       recipient.RecipientTypeCode,
       COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(contact.FirstName, N' ', contact.LastName))), N''), recipient.RecipientName) AS RecipientName,
       COALESCE(NULLIF(contact.Email, N''), recipient.RecipientEmail) AS RecipientEmail,
       recipient.SigningOrder,
       recipient.IsPrimary,
       recipient.IsSigner
FROM Submissions.ProposalRecipient recipient
LEFT JOIN Client.Contact contact
  ON contact.ContactId = recipient.ContactId
 AND contact.TenantId = recipient.TenantId
 AND contact.IsDeleted = 0
WHERE recipient.ProposalId = @ProposalId AND recipient.TenantId = @TenantId AND recipient.IsDeleted = 0
ORDER BY recipient.SigningOrder, RecipientName;

SELECT ProposalESignEnvelopeId, ProposalId, ProposalVersionNumber, ProposalDeliveryDispatchId, ESignRequestId, ProviderCode,
       ExternalEnvelopeId, StatusCode, SentDateUtc, DeliveredDateUtc, FirstViewedDateUtc, CompletedDateUtc, SignedDocumentId, CertificateDocumentId
FROM Submissions.ProposalESignEnvelope WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { ProposalId = proposalId, TenantId = tenantId }, cancellationToken: cancellationToken));
        var proposal = await multi.ReadSingleOrDefaultAsync<ProposalDto>();
        if (proposal is null) return null;
        var quotes = (await multi.ReadAsync<ProposalQuoteDto>()).AsList();
        var lines = (await multi.ReadAsync<ProposalQuoteLineDto>()).AsList();
        foreach (var quote in quotes)
        {
            quote.Lines = lines.Where(x => x.QuoteId == quote.QuoteId).ToList();
        }
        proposal.Quotes = quotes;
        proposal.Events = (await multi.ReadAsync<ProposalLifecycleEventDto>()).AsList();
        proposal.Deliveries = (await multi.ReadAsync<ProposalDeliveryDispatchDto>()).AsList();
        proposal.CurrentReview = await multi.ReadSingleOrDefaultAsync<ProposalReviewDto>();
        proposal.Recipients = (await multi.ReadAsync<ProposalRecipientDto>()).AsList();
        proposal.ESignEnvelopes = (await multi.ReadAsync<ProposalESignEnvelopeDto>()).AsList();
        return proposal;
    }

    public async Task<IReadOnlyList<ProposalWorkflowDto>> GetProposalsAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT p.ProposalId,
       p.SubmissionId,
       p.TenantId,
       p.Title,
       p.Status, p.GovernanceStatusCode,
       p.VersionNumber,
       p.DeliveryMethod,
       p.Recipient,
       p.DeliveryStatus,
       p.LastDeliveryDispatchId,
       latestDelivery.ProposalVersionNumber AS LatestDeliveryVersionNumber,
       latestDelivery.StatusCode AS LatestDeliveryStatus,
       COALESCE(latestDelivery.ModifiedDateUtc, latestDelivery.CompletedDateUtc, latestDelivery.CreatedDateUtc) AS LatestDeliveryDateUtc,
       p.SentDateUtc, p.PresentedDateUtc, p.ApprovedDateUtc, p.DeliveryConfirmedDateUtc, p.CurrentReviewId,
       p.ClientDecision,
       p.DecisionNotes,
       p.DecisionDateUtc,
       p.DocumentId,
       d.FileName AS DocumentFileName
FROM Submissions.Proposal p
LEFT JOIN DMS.Document d ON d.DocumentId = p.DocumentId AND d.IsDeleted = 0
 OUTER APPLY
 (
     SELECT TOP (1) dispatch.ProposalVersionNumber,
            dispatch.StatusCode,
            dispatch.CompletedDateUtc,
            dispatch.CreatedDateUtc,
            dispatch.ModifiedDateUtc
     FROM Submissions.ProposalDeliveryDispatch dispatch
     WHERE dispatch.ProposalId = p.ProposalId
       AND dispatch.TenantId = p.TenantId
       AND dispatch.ProposalVersionNumber = p.VersionNumber
       AND dispatch.IsDeleted = 0
     ORDER BY COALESCE(dispatch.ModifiedDateUtc, dispatch.CreatedDateUtc) DESC,
              dispatch.CreatedDateUtc DESC
 ) latestDelivery
WHERE p.SubmissionId = @SubmissionId AND p.TenantId = @TenantId AND p.IsDeleted = 0
 ORDER BY COALESCE(p.ModifiedDateUtc, p.CreatedDateUtc) DESC, p.VersionNumber DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, tenantId, cancellationToken);
        return (await cn.QueryAsync<ProposalWorkflowDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<ProposalWorkflowLaunchDto> GetProposalWorkflowLaunchAsync(Guid opportunityId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE OpportunityId = @OpportunityId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 52041, 'Opportunity was not found for proposal workflow.', 1;

;WITH SubmissionReadiness AS
(
    SELECT s.SubmissionId,
           s.ModifiedDateUtc,
           s.CreatedDateUtc,
           COALESCE(SUM(readiness.IsProposalReady), 0) AS ProposalReadyQuoteCount
    FROM Submissions.Submission s
    LEFT JOIN Submissions.Quote q ON q.SubmissionId = s.SubmissionId AND q.TenantId = s.TenantId AND q.IsDeleted = 0
    OUTER APPLY
    (
        SELECT CASE WHEN q.Status = N'Approved for Presentation'
                          AND q.ExpiresDateUtc > SYSUTCDATETIME()
                          AND q.AnnualPremium > 0
                          AND q.CarrierId IS NOT NULL
                          AND q.Deductible IS NOT NULL
                          AND q.[Limit] IS NOT NULL
                          AND COALESCE(NULLIF(q.CoverageForms, N''), NULLIF(q.CoverageNotes, N'')) IS NOT NULL
                          AND (q.ReviewedDateUtc IS NOT NULL OR q.ReviewedByUserId IS NOT NULL)
                          AND q.QuoteDocumentId IS NOT NULL
                          AND q.IsBindable = 1
                          AND EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = @TenantId AND ql.IsDeleted = 0)
                          AND NOT EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = @TenantId AND ql.IsDeleted = 0 AND ql.IsBindable = 0)
                          AND NOT EXISTS
                          (
                              SELECT 1
                              FROM Submissions.SubmissionLine sl
                              WHERE sl.SubmissionId = s.SubmissionId
                                AND sl.TenantId = @TenantId
                                AND sl.IsDeleted = 0
                                AND NOT EXISTS
                                (
                                    SELECT 1
                                    FROM Submissions.QuoteLine ql
                                    WHERE ql.QuoteId = q.QuoteId
                                      AND ql.TenantId = @TenantId
                                      AND ql.SubmissionLineId = sl.SubmissionLineId
                                      AND ql.IsDeleted = 0
                                      AND ql.IsBindable = 1
                                )
                          )
                    THEN 1 ELSE 0 END AS IsProposalReady
    ) readiness
    WHERE s.OpportunityId = @OpportunityId AND s.TenantId = @TenantId AND s.IsDeleted = 0
    GROUP BY s.SubmissionId, s.ModifiedDateUtc, s.CreatedDateUtc
), SelectedSubmission AS
(
    SELECT TOP 1 SubmissionId, ProposalReadyQuoteCount
    FROM SubmissionReadiness
    ORDER BY CASE WHEN ProposalReadyQuoteCount > 0 THEN 0 ELSE 1 END,
             COALESCE(ModifiedDateUtc, CreatedDateUtc) DESC,
             CreatedDateUtc DESC
)
SELECT @OpportunityId AS OpportunityId,
       @TenantId AS TenantId,
       selected.SubmissionId,
       CAST(CASE WHEN selected.SubmissionId IS NULL THEN 0 ELSE 1 END AS bit) AS HasSubmission,
       CAST(CASE WHEN COALESCE(selected.ProposalReadyQuoteCount, 0) > 0 THEN 1 ELSE 0 END AS bit) AS HasProposalReadyQuotes,
       COALESCE(selected.ProposalReadyQuoteCount, 0) AS ProposalReadyQuoteCount,
       CASE WHEN selected.SubmissionId IS NULL THEN N'CreateSubmission'
            WHEN selected.ProposalReadyQuoteCount > 0 THEN N'OpenProposalWorkflow'
            ELSE N'OpenSubmission' END AS NextActionCode,
       CASE WHEN selected.SubmissionId IS NULL THEN N'Create a submission before preparing a proposal.'
            WHEN selected.ProposalReadyQuoteCount > 0 THEN N'Proposal-ready quotes are available.'
            ELSE N'Complete quote review and approval before preparing a proposal.' END AS Message
FROM (VALUES (1)) seed(Value)
LEFT JOIN SelectedSubmission selected ON 1 = 1;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<ProposalWorkflowLaunchDto>(new CommandDefinition(sql, new { OpportunityId = opportunityId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ProposalWorkflowOptionDto>> GetProposalWorkflowOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT optionRow.ProposalWorkflowOptionId, optionRow.OptionGroupCode, optionRow.OptionCode, optionRow.DisplayName,
       optionRow.Description, optionRow.IsDefault, optionRow.SortOrder,
       CAST(COALESCE(provider.IsConfigured, 1) AS bit) AS IsProviderConfigured,
       provider.DisplayName AS ProviderName
FROM Submissions.ProposalWorkflowOption optionRow
LEFT JOIN Submissions.ProposalDeliveryProvider provider
  ON optionRow.OptionGroupCode = N'DeliveryMethod'
 AND provider.TenantId = optionRow.TenantId
 AND provider.DeliveryMethodCode = optionRow.OptionCode
 AND provider.IsActive = 1
 AND provider.IsDeleted = 0
WHERE optionRow.TenantId = @TenantId AND optionRow.IsActive = 1 AND optionRow.IsDeleted = 0
ORDER BY optionRow.OptionGroupCode, optionRow.SortOrder, optionRow.DisplayName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<ProposalWorkflowOptionDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> GenerateProposalAsync(GenerateProposalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @QuoteScope TABLE (QuoteId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, SortOrder INT NOT NULL);

INSERT INTO @QuoteScope (QuoteId, SortOrder)
SELECT q.QuoteId, ROW_NUMBER() OVER (ORDER BY q.IsSelected DESC, q.IsRecommended DESC, q.RecommendationScore DESC, q.AnnualPremium ASC)
FROM Submissions.Quote q
WHERE q.SubmissionId = @SubmissionId
  AND q.IsDeleted = 0
  AND q.Status = N'Approved for Presentation'
  AND q.IsBindable = 1
  AND q.ExpiresDateUtc > SYSUTCDATETIME()
  AND q.AnnualPremium > 0
  AND q.CarrierId IS NOT NULL
  AND q.Deductible IS NOT NULL
  AND q.[Limit] IS NOT NULL
  AND COALESCE(NULLIF(q.CoverageForms, N''), NULLIF(q.CoverageNotes, N'')) IS NOT NULL
  AND (q.ReviewedDateUtc IS NOT NULL OR q.ReviewedByUserId IS NOT NULL)
  AND q.QuoteDocumentId IS NOT NULL
  AND EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = @TenantId AND ql.IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = @TenantId AND ql.IsDeleted = 0 AND ql.IsBindable = 0)
  AND NOT EXISTS
  (
      SELECT 1
      FROM Submissions.SubmissionLine sl
      WHERE sl.SubmissionId = q.SubmissionId
        AND sl.TenantId = @TenantId
        AND sl.IsDeleted = 0
        AND NOT EXISTS
        (
            SELECT 1
            FROM Submissions.QuoteLine ql
            WHERE ql.QuoteId = q.QuoteId
              AND ql.TenantId = @TenantId
              AND ql.SubmissionLineId = sl.SubmissionLineId
              AND ql.IsDeleted = 0
              AND ql.IsBindable = 1
        )
  )
  AND EXISTS (SELECT 1 FROM STRING_SPLIT(@QuoteIdsCsv, N',') s WHERE TRY_CONVERT(uniqueidentifier, s.value) = q.QuoteId);

IF NOT EXISTS (SELECT 1 FROM @QuoteScope) AND COALESCE(NULLIF(@QuoteIdsCsv, N''), N'') <> N''
    THROW 52035, 'Proposal can only include non-expired, reviewed, bindable quotes approved for presentation with persisted bindable coverage lines and complete premium, coverage, deductible, carrier, and document data.', 1;

IF NOT EXISTS (SELECT 1 FROM @QuoteScope)
BEGIN
    INSERT INTO @QuoteScope (QuoteId, SortOrder)
    SELECT q.QuoteId, ROW_NUMBER() OVER (ORDER BY q.IsSelected DESC, q.IsRecommended DESC, q.RecommendationScore DESC, q.AnnualPremium ASC)
    FROM Submissions.Quote q
    WHERE q.SubmissionId = @SubmissionId
      AND q.IsDeleted = 0
      AND q.Status = N'Approved for Presentation'
      AND q.IsBindable = 1
      AND q.ExpiresDateUtc > SYSUTCDATETIME()
      AND q.AnnualPremium > 0
      AND q.CarrierId IS NOT NULL
      AND q.Deductible IS NOT NULL
      AND q.[Limit] IS NOT NULL
      AND COALESCE(NULLIF(q.CoverageForms, N''), NULLIF(q.CoverageNotes, N'')) IS NOT NULL
      AND (q.ReviewedDateUtc IS NOT NULL OR q.ReviewedByUserId IS NOT NULL)
      AND q.QuoteDocumentId IS NOT NULL
      AND EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = @TenantId AND ql.IsDeleted = 0)
      AND NOT EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = @TenantId AND ql.IsDeleted = 0 AND ql.IsBindable = 0)
      AND NOT EXISTS
      (
          SELECT 1
          FROM Submissions.SubmissionLine sl
          WHERE sl.SubmissionId = q.SubmissionId
            AND sl.TenantId = @TenantId
            AND sl.IsDeleted = 0
            AND NOT EXISTS
            (
                SELECT 1
                FROM Submissions.QuoteLine ql
                WHERE ql.QuoteId = q.QuoteId
                  AND ql.TenantId = @TenantId
                  AND ql.SubmissionLineId = sl.SubmissionLineId
                  AND ql.IsDeleted = 0
                  AND ql.IsBindable = 1
            )
      );
END;

IF NOT EXISTS (SELECT 1 FROM @QuoteScope)
    THROW 52036, 'Proposal requires at least one non-expired, reviewed, bindable quote with persisted bindable coverage lines that is approved for presentation.', 1;

DECLARE @QuoteRows NVARCHAR(MAX);
SELECT @QuoteRows = STRING_AGG(CONCAT(N'<tr><td>', c.CarrierName, N'</td><td>', q.QuoteNumber, N'</td><td>', FORMAT(q.AnnualPremium, N'C'), N'</td><td>', COALESCE(q.CarrierRating, N''), N'</td><td>', CONVERT(nvarchar(20), q.ExpiresDateUtc, 101), N'</td><td>', COALESCE(q.CoverageNotes, N''), N'</td></tr>'), N'')
FROM @QuoteScope qs
JOIN Submissions.Quote q ON q.QuoteId = qs.QuoteId
JOIN Core.Carrier c ON c.CarrierId = q.CarrierId;

DECLARE @Html NVARCHAR(MAX) = CONCAT(
    N'<html><body><h1>', @Title, N'</h1>',
    CASE WHEN NULLIF(@CustomIntroduction, N'') IS NULL THEN N'' ELSE CONCAT(N'<p>', @CustomIntroduction, N'</p>') END,
    N'<p>Prepared proposal package for selected submission quote options.</p>',
    N'<table><thead><tr><th>Carrier</th><th>Quote</th><th>Annual Premium</th><th>Rating</th><th>Expires</th><th>Coverage Notes</th></tr></thead><tbody>',
    COALESCE(@QuoteRows, N'<tr><td colspan=""6"">No quote options were available.</td></tr>'),
    N'</tbody></table></body></html>');

DECLARE @VersionNumber INT = ISNULL((SELECT MAX(VersionNumber) FROM Submissions.Proposal WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0), 0) + 1;

INSERT INTO Submissions.Proposal
    (ProposalId, SubmissionId, TenantId, Title, Status, GovernanceStatusCode, VersionNumber, PdfUrl, HtmlContent, CustomIntroduction, CreatedDateUtc, CreatedByUserId, GeneratedDateUtc, IsDeleted)
VALUES
    (@ProposalId, @SubmissionId, @TenantId, @Title, N'Draft', N'Draft', @VersionNumber, CONCAT(N'dms://proposal/', CONVERT(nvarchar(36), @ProposalId)), @Html, @CustomIntroduction, SYSUTCDATETIME(), @GeneratedByUserId, SYSUTCDATETIME(), 0);

INSERT INTO Submissions.ProposalQuote (ProposalQuoteId, ProposalId, QuoteId, SubmissionId, TenantId, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @ProposalId, QuoteId, @SubmissionId, @TenantId, SortOrder, SYSUTCDATETIME(), @GeneratedByUserId, 0
FROM @QuoteScope;

INSERT INTO Submissions.ProposalLifecycleEvent
    (ProposalLifecycleEventId, TenantId, ProposalId, SubmissionId, EventCode, EventDetail, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), @TenantId, @ProposalId, @SubmissionId, N'Generated', CONCAT(@Title, N' version ', @VersionNumber, N' generated.'), SYSUTCDATETIME(), SYSUTCDATETIME(), @GeneratedByUserId, 0);

UPDATE Submissions.Submission
SET Status = N'Proposal Prepared',
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @GeneratedByUserId
WHERE SubmissionId = @SubmissionId
  AND TenantId = @TenantId
  AND IsDeleted = 0
  AND Status NOT IN (N'Presented', N'Customer Accepted', N'Binding', N'Bound', N'Lost', N'Cancelled', N'Closed');

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'ProposalGenerated', CONCAT(@Title, N' (', (SELECT COUNT(1) FROM @QuoteScope), N' quote option(s)).'), SYSUTCDATETIME(), N'Proposal', @ProposalId, N'User', 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        using var tx = cn.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            await cn.ExecuteAsync(new CommandDefinition(sql, new
            {
                ProposalId = id,
                request.SubmissionId,
                request.TenantId,
                request.Title,
                request.CustomIntroduction,
                request.GeneratedByUserId,
                QuoteIdsCsv = string.Join(',', request.QuoteIds ?? []),
            }, tx, cancellationToken: cancellationToken));
            await cn.ExecuteAsync(new CommandDefinition(RecalculateSubmissionStatusSql, new { request.SubmissionId, request.TenantId }, tx, cancellationToken: cancellationToken));
            tx.Commit();
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            tx.Rollback();
            throw new InvalidOperationException("A proposal version was generated concurrently. Reload the workflow and try again.", exception);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
        await RecordOpportunityWorkflowAsync(cn, request.SubmissionId, request.TenantId, "Proposal", "Proposal Generated", "Proposal Generated", request.Title, "Proposal", id, null, cancellationToken);
        return id;
    }

    public async Task<ProposalDeliveryDispatchDto> DeliverProposalAsync(Guid proposalId, ProposalDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @DispatchId UNIQUEIDENTIFIER = NEWID();
DECLARE @ProviderId UNIQUEIDENTIFIER;
DECLARE @ProviderName NVARCHAR(150);
DECLARE @IsConfigured BIT = 0;
DECLARE @MaxAttempts INT = 5;
DECLARE @DispatchStatus NVARCHAR(50);

DECLARE @ProposalVersionNumber INT;
SELECT @SubmissionId = SubmissionId, @ProposalVersionNumber = VersionNumber
FROM Submissions.Proposal
WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0 AND GovernanceStatusCode = N'ReadyToDeliver' AND ApprovalVersionNumber = VersionNumber AND ApprovedSnapshotHash IS NOT NULL;

IF @SubmissionId IS NULL THROW 52016, 'Proposal was not found for delivery.', 1;
IF NOT EXISTS (SELECT 1 FROM Submissions.ProposalQuote WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 52212, 'Proposal delivery requires at least one included bindable quote with persisted bindable coverage lines.', 1;
IF EXISTS
(
    SELECT 1
    FROM Submissions.ProposalQuote proposalQuote
    INNER JOIN Submissions.Quote quote ON quote.QuoteId = proposalQuote.QuoteId
    WHERE proposalQuote.ProposalId = @ProposalId
      AND proposalQuote.TenantId = @TenantId
      AND proposalQuote.IsDeleted = 0
      AND
      (
          quote.IsDeleted = 1
          OR quote.IsBindable = 0
          OR NOT EXISTS (SELECT 1 FROM Submissions.QuoteLine quoteLine WHERE quoteLine.QuoteId = quote.QuoteId AND quoteLine.TenantId = @TenantId AND quoteLine.IsDeleted = 0)
          OR EXISTS (SELECT 1 FROM Submissions.QuoteLine quoteLine WHERE quoteLine.QuoteId = quote.QuoteId AND quoteLine.TenantId = @TenantId AND quoteLine.IsDeleted = 0 AND quoteLine.IsBindable = 0)
          OR EXISTS
          (
              SELECT 1
              FROM Submissions.SubmissionLine submissionLine
              WHERE submissionLine.SubmissionId = quote.SubmissionId
                AND submissionLine.TenantId = @TenantId
                AND submissionLine.IsDeleted = 0
                AND NOT EXISTS
                (
                    SELECT 1
                    FROM Submissions.QuoteLine quoteLine
                    WHERE quoteLine.QuoteId = quote.QuoteId
                      AND quoteLine.TenantId = @TenantId
                      AND quoteLine.SubmissionLineId = submissionLine.SubmissionLineId
                      AND quoteLine.IsDeleted = 0
                      AND quoteLine.IsBindable = 1
                )
          )
      )
)
    THROW 52212, 'Proposal delivery requires every included quote and persisted coverage line to remain bindable. Open Quote Review and resolve all bindability items before delivery.', 1;
IF NOT EXISTS (SELECT 1 FROM Submissions.ProposalRecipient WHERE ProposalId=@ProposalId AND TenantId=@TenantId AND IsPrimary=1 AND IsDeleted=0) THROW 52207, 'Proposal requires a primary recipient before delivery.', 1;
IF NOT EXISTS (SELECT 1 FROM Submissions.ProposalRecipient WHERE ProposalId=@ProposalId AND TenantId=@TenantId AND IsPrimary=1 AND IsDeleted=0 AND LOWER(RecipientEmail)=LOWER(@Recipient)) THROW 52211, 'Delivery recipient must match the persisted primary proposal recipient.', 1;
IF NOT EXISTS (SELECT 1 FROM Submissions.ProposalApprovedSnapshot WHERE ProposalId=@ProposalId AND TenantId=@TenantId AND ProposalVersionNumber=@ProposalVersionNumber) THROW 52208, 'Approved proposal snapshot is missing.', 1;

SELECT @ProviderId = ProposalDeliveryProviderId,
       @ProviderName = DisplayName,
       @IsConfigured = IsConfigured,
       @MaxAttempts = MaxAttempts
FROM Submissions.ProposalDeliveryProvider
WHERE TenantId = @TenantId
  AND DeliveryMethodCode = @DeliveryMethod
  AND IsActive = 1
  AND IsDeleted = 0;

IF @ProviderId IS NULL THROW 52042, 'The selected proposal delivery method is not configured for this tenant.', 1;

IF EXISTS
(
    SELECT 1 FROM Submissions.ProposalDeliveryDispatch
    WHERE TenantId = @TenantId AND ProposalId = @ProposalId AND IsDeleted = 0
      AND StatusCode IN (N'Queued', N'Processing', N'Configuration Required', N'Sent')
)
    THROW 52043, 'An active proposal delivery already exists.', 1;

SET @DispatchStatus = CASE WHEN @IsConfigured = 1 THEN N'Queued' ELSE N'Configuration Required' END;

INSERT INTO Submissions.ProposalDeliveryDispatch
    (ProposalDeliveryDispatchId, TenantId, SubmissionId, ProposalId, ProposalDeliveryProviderId,
     ProposalVersionNumber, DeliveryMethodCode, Recipient, StatusCode, AttemptCount, MaxAttempts, NextAttemptDateUtc,
     ErrorCode, ErrorMessage, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@DispatchId, @TenantId, @SubmissionId, @ProposalId, @ProviderId,
     @ProposalVersionNumber, @DeliveryMethod, @Recipient, @DispatchStatus, 0, @MaxAttempts,
     CASE WHEN @DispatchStatus = N'Queued' THEN SYSUTCDATETIME() ELSE NULL END,
     CASE WHEN @DispatchStatus = N'Configuration Required' THEN N'PROVIDER_NOT_CONFIGURED' ELSE NULL END,
     CASE WHEN @DispatchStatus = N'Configuration Required' THEN CONCAT(@ProviderName, N' requires tenant configuration before delivery.') ELSE NULL END,
     SYSUTCDATETIME(), @SentByUserId, 0);

UPDATE Submissions.Proposal
SET DeliveryMethod = @DeliveryMethod,
    Recipient = @Recipient,
    DeliveryStatus = @DispatchStatus,
    LastDeliveryDispatchId = @DispatchId,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @SentByUserId
WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.ProposalLifecycleEvent
    (ProposalLifecycleEventId, TenantId, ProposalId, SubmissionId, EventCode, EventDetail, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), @TenantId, @ProposalId, @SubmissionId,
     CASE WHEN @DispatchStatus = N'Queued' THEN N'DeliveryQueued' ELSE N'DeliveryConfigurationRequired' END,
     CASE WHEN @DispatchStatus = N'Queued'
          THEN CONCAT(N'Proposal delivery queued through ', @ProviderName, N' to ', @Recipient, N'.')
          ELSE CONCAT(@ProviderName, N' requires tenant configuration before delivery to ', @Recipient, N'.') END,
     SYSUTCDATETIME(), SYSUTCDATETIME(), @SentByUserId, 0);

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'ProposalDeliveryQueued', CONCAT(@DispatchStatus, N': ', @DeliveryMethod, N' to ', @Recipient), SYSUTCDATETIME(), @SentByUserId, N'ProposalDeliveryDispatch', @DispatchId, N'User', 0);

SELECT d.ProposalDeliveryDispatchId, d.TenantId, d.SubmissionId, d.ProposalId, d.DeliveryMethodCode,
       @ProviderName AS ProviderName, d.Recipient, d.StatusCode, d.AttemptCount, d.MaxAttempts,
       d.ProposalVersionNumber, d.NextAttemptDateUtc, d.CompletedDateUtc, d.ExternalDeliveryId, d.ErrorCode, d.ErrorMessage,
       d.CreatedDateUtc, CAST(CASE WHEN d.StatusCode = N'Configuration Required' THEN 1 ELSE 0 END AS bit) AS CanRetry
FROM Submissions.ProposalDeliveryDispatch d
WHERE d.ProposalDeliveryDispatchId = @DispatchId;";
        return await cn.QuerySingleAsync<ProposalDeliveryDispatchDto>(new CommandDefinition(sql, new { ProposalId = proposalId, request.TenantId, request.DeliveryMethod, request.Recipient, request.SentByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ProposalDeliveryDispatchDto>> GetProposalDeliveriesAsync(Guid proposalId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT d.ProposalDeliveryDispatchId, d.TenantId, d.SubmissionId, d.ProposalId, d.DeliveryMethodCode,
       COALESCE(provider.DisplayName, d.DeliveryMethodCode) AS ProviderName, d.Recipient, d.StatusCode,
       d.ProposalVersionNumber, d.AttemptCount, d.MaxAttempts, d.NextAttemptDateUtc, d.CompletedDateUtc, d.ExternalDeliveryId,
       d.FirstViewedDateUtc, d.LastViewedDateUtc, d.DownloadedDateUtc, d.SignedDateUtc, d.DeclinedDateUtc, d.ExpiredDateUtc, d.BouncedDateUtc, d.CancelledDateUtc,
       d.ErrorCode, d.ErrorMessage, d.CreatedDateUtc,
       CAST(CASE WHEN d.StatusCode IN (N'Configuration Required', N'Failed') THEN 1 ELSE 0 END AS bit) AS CanRetry
FROM Submissions.ProposalDeliveryDispatch d
LEFT JOIN Submissions.ProposalDeliveryProvider provider
  ON provider.ProposalDeliveryProviderId = d.ProposalDeliveryProviderId AND provider.IsDeleted = 0
WHERE d.ProposalId = @ProposalId AND d.TenantId = @TenantId AND d.IsDeleted = 0
ORDER BY d.CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<ProposalDeliveryDispatchDto>(new CommandDefinition(sql, new { ProposalId = proposalId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<ProposalDeliveryMonitorDto>> GetProposalDeliveryMonitorAsync(Guid tenantId, string? status, string? searchTerm, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP (250)
       d.ProposalDeliveryDispatchId,
       d.TenantId,
       d.SubmissionId,
       d.ProposalId,
       d.DeliveryMethodCode,
       COALESCE(provider.DisplayName, d.DeliveryMethodCode) AS ProviderName,
       d.Recipient,
       d.StatusCode,
       d.ProposalVersionNumber,
       d.AttemptCount,
       d.MaxAttempts,
       d.NextAttemptDateUtc,
       d.CompletedDateUtc,
       d.ExternalDeliveryId,
       d.FirstViewedDateUtc,
       d.LastViewedDateUtc,
       d.DownloadedDateUtc,
       d.SignedDateUtc,
       d.DeclinedDateUtc,
       d.ExpiredDateUtc,
       d.BouncedDateUtc,
       d.CancelledDateUtc,
       d.ErrorCode,
       d.ErrorMessage,
       d.CreatedDateUtc,
       CAST(CASE WHEN d.StatusCode IN (N'Configuration Required', N'Failed') THEN 1 ELSE 0 END AS bit) AS CanRetry,
       p.Title AS ProposalTitle,
       s.SubmissionNumber,
       s.Status AS SubmissionStatus,
       account.AccountName,
       opportunity.OpportunityName,
       COALESCE(producer.FullName, producer.DisplayName, producer.UserName) AS AssignedProducerName,
       provider.HandlerCode AS DeliveryHandlerCode,
       provider.SenderAddress,
       CAST(COALESCE(provider.IsConfigured, 0) AS bit) AS ProviderIsConfigured,
       CAST(COALESCE(provider.IsActive, 0) AS bit) AS ProviderIsActive
FROM Submissions.ProposalDeliveryDispatch d
INNER JOIN Submissions.Proposal p
  ON p.ProposalId = d.ProposalId
 AND p.TenantId = d.TenantId
 AND p.IsDeleted = 0
INNER JOIN Submissions.Submission s
  ON s.SubmissionId = d.SubmissionId
 AND s.TenantId = d.TenantId
 AND s.IsDeleted = 0
LEFT JOIN Client.Account account
  ON account.AccountId = s.AccountId
 AND account.IsDeleted = 0
LEFT JOIN CRM.Opportunity opportunity
  ON opportunity.OpportunityId = s.OpportunityId
 AND opportunity.IsDeleted = 0
LEFT JOIN IAM.[User] producer
  ON producer.UserId = s.AssignedToUserId
LEFT JOIN Submissions.ProposalDeliveryProvider provider
  ON provider.ProposalDeliveryProviderId = d.ProposalDeliveryProviderId
 AND provider.IsDeleted = 0
WHERE d.TenantId = @TenantId
  AND d.IsDeleted = 0
  AND (@Status IS NULL OR @Status = N'' OR d.StatusCode = @Status)
  AND (@SearchTerm IS NULL OR @SearchTerm = N''
       OR d.Recipient LIKE N'%' + @SearchTerm + N'%'
       OR d.StatusCode LIKE N'%' + @SearchTerm + N'%'
       OR d.DeliveryMethodCode LIKE N'%' + @SearchTerm + N'%'
       OR COALESCE(provider.DisplayName, d.DeliveryMethodCode) LIKE N'%' + @SearchTerm + N'%'
       OR p.Title LIKE N'%' + @SearchTerm + N'%'
       OR s.SubmissionNumber LIKE N'%' + @SearchTerm + N'%'
       OR account.AccountName LIKE N'%' + @SearchTerm + N'%'
       OR opportunity.OpportunityName LIKE N'%' + @SearchTerm + N'%')
ORDER BY d.CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<ProposalDeliveryMonitorDto>(new CommandDefinition(sql, new { TenantId = tenantId, Status = status, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<ProposalDeliveryDispatchDto> RetryProposalDeliveryAsync(Guid dispatchId, RetryProposalDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @ProviderName NVARCHAR(150);
DECLARE @IsConfigured BIT;

SELECT @ProviderName = provider.DisplayName, @IsConfigured = provider.IsConfigured
FROM Submissions.ProposalDeliveryDispatch dispatch
INNER JOIN Submissions.ProposalDeliveryProvider provider
  ON provider.ProposalDeliveryProviderId = dispatch.ProposalDeliveryProviderId
 AND provider.TenantId = dispatch.TenantId
 AND provider.IsActive = 1
 AND provider.IsDeleted = 0
WHERE dispatch.ProposalDeliveryDispatchId = @DispatchId
  AND dispatch.TenantId = @TenantId
  AND dispatch.StatusCode IN (N'Configuration Required', N'Failed')
  AND dispatch.IsDeleted = 0;

IF @ProviderName IS NULL THROW 52044, 'Proposal delivery is not eligible for retry.', 1;

UPDATE Submissions.ProposalDeliveryDispatch
SET StatusCode = CASE WHEN @IsConfigured = 1 THEN N'Queued' ELSE N'Configuration Required' END,
    NextAttemptDateUtc = CASE WHEN @IsConfigured = 1 THEN SYSUTCDATETIME() ELSE NULL END,
    ClaimedDateUtc = NULL,
    ClaimedBy = NULL,
    ErrorCode = CASE WHEN @IsConfigured = 1 THEN NULL ELSE N'PROVIDER_NOT_CONFIGURED' END,
    ErrorMessage = CASE WHEN @IsConfigured = 1 THEN NULL ELSE CONCAT(@ProviderName, N' requires tenant configuration before delivery.') END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @RequestedByUserId
WHERE ProposalDeliveryDispatchId = @DispatchId AND TenantId = @TenantId AND IsDeleted = 0;

UPDATE proposal
SET DeliveryStatus = dispatch.StatusCode,
    LastDeliveryDispatchId = dispatch.ProposalDeliveryDispatchId,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @RequestedByUserId
FROM Submissions.Proposal proposal
INNER JOIN Submissions.ProposalDeliveryDispatch dispatch ON dispatch.ProposalId = proposal.ProposalId
WHERE dispatch.ProposalDeliveryDispatchId = @DispatchId AND proposal.TenantId = @TenantId;

INSERT INTO Submissions.ProposalLifecycleEvent
    (ProposalLifecycleEventId, TenantId, ProposalId, SubmissionId, EventCode, EventDetail, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), TenantId, ProposalId, SubmissionId, N'DeliveryRetryRequested',
       CASE WHEN @IsConfigured = 1 THEN CONCAT(N'Delivery retry queued through ', @ProviderName, N'.') ELSE CONCAT(@ProviderName, N' remains unconfigured.') END,
       SYSUTCDATETIME(), SYSUTCDATETIME(), @RequestedByUserId, 0
FROM Submissions.ProposalDeliveryDispatch
WHERE ProposalDeliveryDispatchId = @DispatchId AND TenantId = @TenantId AND IsDeleted = 0;

SELECT d.ProposalDeliveryDispatchId, d.TenantId, d.SubmissionId, d.ProposalId, d.DeliveryMethodCode,
       @ProviderName AS ProviderName, d.Recipient, d.StatusCode, d.AttemptCount, d.MaxAttempts,
       d.ProposalVersionNumber, d.NextAttemptDateUtc, d.CompletedDateUtc, d.ExternalDeliveryId,
       d.FirstViewedDateUtc, d.LastViewedDateUtc, d.DownloadedDateUtc, d.SignedDateUtc, d.DeclinedDateUtc, d.ExpiredDateUtc, d.BouncedDateUtc, d.CancelledDateUtc,
       d.ErrorCode, d.ErrorMessage,
       d.CreatedDateUtc, CAST(CASE WHEN d.StatusCode IN (N'Configuration Required', N'Failed') THEN 1 ELSE 0 END AS bit) AS CanRetry
FROM Submissions.ProposalDeliveryDispatch d
WHERE d.ProposalDeliveryDispatchId = @DispatchId AND d.TenantId = @TenantId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<ProposalDeliveryDispatchDto>(new CommandDefinition(sql, new { DispatchId = dispatchId, request.TenantId, request.RequestedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<ProposalDeliveryDispatchDto> UpdateProposalDeliveryRecipientAsync(Guid dispatchId, UpdateProposalDeliveryRecipientRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @ProposalId UNIQUEIDENTIFIER;
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @ProviderName NVARCHAR(150);

SELECT @ProposalId = dispatch.ProposalId,
       @SubmissionId = dispatch.SubmissionId,
       @ProviderName = COALESCE(provider.DisplayName, dispatch.DeliveryMethodCode)
FROM Submissions.ProposalDeliveryDispatch dispatch
LEFT JOIN Submissions.ProposalDeliveryProvider provider
  ON provider.ProposalDeliveryProviderId = dispatch.ProposalDeliveryProviderId
 AND provider.IsDeleted = 0
WHERE dispatch.ProposalDeliveryDispatchId = @DispatchId
  AND dispatch.TenantId = @TenantId
  AND dispatch.IsDeleted = 0
  AND dispatch.StatusCode IN (N'Queued', N'Configuration Required', N'Failed');

IF @ProposalId IS NULL THROW 52231, 'Only queued, configuration-required, or failed proposal deliveries can have recipient edited.', 1;

UPDATE Submissions.ProposalDeliveryDispatch
SET Recipient = @Recipient,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE ProposalDeliveryDispatchId = @DispatchId AND TenantId = @TenantId AND IsDeleted = 0;

UPDATE proposal
SET Recipient = @Recipient,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
FROM Submissions.Proposal proposal
WHERE proposal.ProposalId = @ProposalId
  AND proposal.TenantId = @TenantId
  AND proposal.LastDeliveryDispatchId = @DispatchId
  AND proposal.IsDeleted = 0;

INSERT INTO Submissions.ProposalLifecycleEvent
    (ProposalLifecycleEventId, TenantId, ProposalId, SubmissionId, EventCode, EventDetail, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), @TenantId, @ProposalId, @SubmissionId, N'DeliveryRecipientUpdated',
     CONCAT(N'Proposal delivery recipient updated to ', @Recipient, CASE WHEN NULLIF(@ChangeReason, N'') IS NULL THEN N'.' ELSE CONCAT(N'. Reason: ', @ChangeReason) END),
     SYSUTCDATETIME(), SYSUTCDATETIME(), @ModifiedByUserId, 0);

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'ProposalDeliveryRecipientUpdated', CONCAT(@ProviderName, N' recipient updated to ', @Recipient), SYSUTCDATETIME(), @ModifiedByUserId, N'ProposalDeliveryDispatch', @DispatchId, N'User', 0);

SELECT d.ProposalDeliveryDispatchId, d.TenantId, d.SubmissionId, d.ProposalId, d.DeliveryMethodCode,
       @ProviderName AS ProviderName, d.Recipient, d.StatusCode, d.AttemptCount, d.MaxAttempts,
       d.ProposalVersionNumber, d.NextAttemptDateUtc, d.CompletedDateUtc, d.ExternalDeliveryId,
       d.FirstViewedDateUtc, d.LastViewedDateUtc, d.DownloadedDateUtc, d.SignedDateUtc, d.DeclinedDateUtc, d.ExpiredDateUtc, d.BouncedDateUtc, d.CancelledDateUtc,
       d.ErrorCode, d.ErrorMessage,
       d.CreatedDateUtc, CAST(CASE WHEN d.StatusCode IN (N'Configuration Required', N'Failed') THEN 1 ELSE 0 END AS bit) AS CanRetry
FROM Submissions.ProposalDeliveryDispatch d
WHERE d.ProposalDeliveryDispatchId = @DispatchId AND d.TenantId = @TenantId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<ProposalDeliveryDispatchDto>(new CommandDefinition(sql, new { DispatchId = dispatchId, request.TenantId, request.Recipient, request.ChangeReason, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<ProposalDeliveryDispatchDto> ResendProposalDeliveryAsync(Guid dispatchId, ResendProposalDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NewDispatchId UNIQUEIDENTIFIER = NEWID();
DECLARE @ProposalId UNIQUEIDENTIFIER;
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @ProviderId UNIQUEIDENTIFIER;
DECLARE @ProviderName NVARCHAR(150);
DECLARE @DeliveryMethod NVARCHAR(50);
DECLARE @Recipient NVARCHAR(320);
DECLARE @ProposalVersionNumber INT;
DECLARE @MaxAttempts INT;
DECLARE @IsConfigured BIT;
DECLARE @DispatchStatus NVARCHAR(50);
DECLARE @OriginalStatus NVARCHAR(50);

SELECT @ProposalId = dispatch.ProposalId,
       @SubmissionId = dispatch.SubmissionId,
       @ProviderId = dispatch.ProposalDeliveryProviderId,
       @ProviderName = COALESCE(provider.DisplayName, dispatch.DeliveryMethodCode),
       @DeliveryMethod = dispatch.DeliveryMethodCode,
       @Recipient = COALESCE(NULLIF(@RequestedRecipient, N''), dispatch.Recipient),
       @ProposalVersionNumber = dispatch.ProposalVersionNumber,
       @MaxAttempts = COALESCE(provider.MaxAttempts, dispatch.MaxAttempts, 5),
       @IsConfigured = COALESCE(provider.IsConfigured, 0),
       @OriginalStatus = dispatch.StatusCode
FROM Submissions.ProposalDeliveryDispatch dispatch
LEFT JOIN Submissions.ProposalDeliveryProvider provider
  ON provider.ProposalDeliveryProviderId = dispatch.ProposalDeliveryProviderId
 AND provider.TenantId = dispatch.TenantId
 AND provider.IsActive = 1
 AND provider.IsDeleted = 0
WHERE dispatch.ProposalDeliveryDispatchId = @DispatchId
  AND dispatch.TenantId = @TenantId
  AND dispatch.IsDeleted = 0;

IF @ProposalId IS NULL THROW 52232, 'Proposal delivery was not found for resend.', 1;

IF EXISTS
(
    SELECT 1 FROM Submissions.ProposalDeliveryDispatch
    WHERE TenantId = @TenantId AND ProposalId = @ProposalId AND IsDeleted = 0
      AND StatusCode IN (N'Queued', N'Processing', N'Configuration Required', N'Sent')
      AND ProposalDeliveryDispatchId <> @DispatchId
)
    THROW 52233, 'Another active proposal delivery already exists for this proposal.', 1;

SET @DispatchStatus = CASE WHEN @IsConfigured = 1 THEN N'Queued' ELSE N'Configuration Required' END;

IF @OriginalStatus IN (N'Queued', N'Processing', N'Configuration Required', N'Sent')
BEGIN
    UPDATE Submissions.ProposalDeliveryDispatch
    SET StatusCode = N'Cancelled',
        CancelledDateUtc = COALESCE(CancelledDateUtc, SYSUTCDATETIME()),
        ErrorMessage = COALESCE(NULLIF(ErrorMessage, N''), N'Replaced by resend request.'),
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @RequestedByUserId
    WHERE ProposalDeliveryDispatchId = @DispatchId
      AND TenantId = @TenantId
      AND IsDeleted = 0;
END;

INSERT INTO Submissions.ProposalDeliveryDispatch
    (ProposalDeliveryDispatchId, TenantId, SubmissionId, ProposalId, ProposalDeliveryProviderId,
     ProposalVersionNumber, DeliveryMethodCode, Recipient, StatusCode, AttemptCount, MaxAttempts, NextAttemptDateUtc,
     ErrorCode, ErrorMessage, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@NewDispatchId, @TenantId, @SubmissionId, @ProposalId, @ProviderId,
     @ProposalVersionNumber, @DeliveryMethod, @Recipient, @DispatchStatus, 0, @MaxAttempts,
     CASE WHEN @DispatchStatus = N'Queued' THEN SYSUTCDATETIME() ELSE NULL END,
     CASE WHEN @DispatchStatus = N'Configuration Required' THEN N'PROVIDER_NOT_CONFIGURED' ELSE NULL END,
     CASE WHEN @DispatchStatus = N'Configuration Required' THEN CONCAT(@ProviderName, N' requires tenant configuration before resend.') ELSE NULL END,
     SYSUTCDATETIME(), @RequestedByUserId, 0);

UPDATE Submissions.Proposal
SET DeliveryMethod = @DeliveryMethod,
    Recipient = @Recipient,
    DeliveryStatus = @DispatchStatus,
    LastDeliveryDispatchId = @NewDispatchId,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @RequestedByUserId
WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.ProposalLifecycleEvent
    (ProposalLifecycleEventId, TenantId, ProposalId, SubmissionId, EventCode, EventDetail, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), @TenantId, @ProposalId, @SubmissionId, N'DeliveryResendQueued',
     CONCAT(N'Proposal delivery resend queued through ', @ProviderName, N' to ', @Recipient, CASE WHEN NULLIF(@Reason, N'') IS NULL THEN N'.' ELSE CONCAT(N'. Reason: ', @Reason) END),
     SYSUTCDATETIME(), SYSUTCDATETIME(), @RequestedByUserId, 0);

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'ProposalDeliveryResent', CONCAT(@DispatchStatus, N': ', @DeliveryMethod, N' resend to ', @Recipient), SYSUTCDATETIME(), @RequestedByUserId, N'ProposalDeliveryDispatch', @NewDispatchId, N'User', 0);

SELECT d.ProposalDeliveryDispatchId, d.TenantId, d.SubmissionId, d.ProposalId, d.DeliveryMethodCode,
       @ProviderName AS ProviderName, d.Recipient, d.StatusCode, d.AttemptCount, d.MaxAttempts,
       d.ProposalVersionNumber, d.NextAttemptDateUtc, d.CompletedDateUtc, d.ExternalDeliveryId,
       d.FirstViewedDateUtc, d.LastViewedDateUtc, d.DownloadedDateUtc, d.SignedDateUtc, d.DeclinedDateUtc, d.ExpiredDateUtc, d.BouncedDateUtc, d.CancelledDateUtc,
       d.ErrorCode, d.ErrorMessage,
       d.CreatedDateUtc, CAST(CASE WHEN d.StatusCode IN (N'Configuration Required', N'Failed') THEN 1 ELSE 0 END AS bit) AS CanRetry
FROM Submissions.ProposalDeliveryDispatch d
WHERE d.ProposalDeliveryDispatchId = @NewDispatchId AND d.TenantId = @TenantId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<ProposalDeliveryDispatchDto>(new CommandDefinition(sql, new { DispatchId = dispatchId, request.TenantId, RequestedRecipient = request.Recipient, request.Reason, request.RequestedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteProposalDeliveryAsync(Guid dispatchId, DeleteProposalDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @ProposalId UNIQUEIDENTIFIER;
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @IsLast BIT = 0;

SELECT @ProposalId = dispatch.ProposalId,
       @SubmissionId = dispatch.SubmissionId,
       @IsLast = CASE WHEN proposal.LastDeliveryDispatchId = dispatch.ProposalDeliveryDispatchId THEN 1 ELSE 0 END
FROM Submissions.ProposalDeliveryDispatch dispatch
INNER JOIN Submissions.Proposal proposal
  ON proposal.ProposalId = dispatch.ProposalId
 AND proposal.TenantId = dispatch.TenantId
 AND proposal.IsDeleted = 0
WHERE dispatch.ProposalDeliveryDispatchId = @DispatchId
  AND dispatch.TenantId = @TenantId
  AND dispatch.IsDeleted = 0;

IF @ProposalId IS NULL THROW 52234, 'Proposal delivery was not found for removal.', 1;

UPDATE Submissions.ProposalDeliveryDispatch
SET IsDeleted = 1,
    StatusCode = CASE WHEN StatusCode IN (N'Queued', N'Processing', N'Configuration Required', N'Failed') THEN N'Cancelled' ELSE StatusCode END,
    CancelledDateUtc = COALESCE(CancelledDateUtc, SYSUTCDATETIME()),
    ErrorMessage = CASE WHEN StatusCode IN (N'Queued', N'Processing', N'Configuration Required', N'Failed') THEN @Reason ELSE ErrorMessage END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @DeletedByUserId
WHERE ProposalDeliveryDispatchId = @DispatchId AND TenantId = @TenantId AND IsDeleted = 0;

IF @IsLast = 1
BEGIN
    UPDATE Submissions.Proposal
    SET DeliveryStatus = NULL,
        LastDeliveryDispatchId = NULL,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @DeletedByUserId
    WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0;
END;

INSERT INTO Submissions.ProposalLifecycleEvent
    (ProposalLifecycleEventId, TenantId, ProposalId, SubmissionId, EventCode, EventDetail, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), @TenantId, @ProposalId, @SubmissionId, N'DeliveryRemoved', CONCAT(N'Proposal delivery removed from monitor. Reason: ', @Reason), SYSUTCDATETIME(), SYSUTCDATETIME(), @DeletedByUserId, 0);

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'ProposalDeliveryRemoved', @Reason, SYSUTCDATETIME(), @DeletedByUserId, N'ProposalDeliveryDispatch', @DispatchId, N'User', 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { DispatchId = dispatchId, request.TenantId, request.Reason, request.DeletedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ProposalDeliveryProviderDto>> GetProposalDeliveryProvidersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT ProposalDeliveryProviderId, TenantId, DeliveryMethodCode, ProviderCode, HandlerCode, DisplayName,
       EndpointUri, SenderAddress, SecretReference, ConfigurationJson, IsConfigured, IsActive,
       MaxAttempts, RetryDelaySeconds, ModifiedDateUtc
FROM Submissions.ProposalDeliveryProvider
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY DeliveryMethodCode, DisplayName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, tenantId, cancellationToken);
        return (await cn.QueryAsync<ProposalDeliveryProviderDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task UpdateProposalDeliveryProviderAsync(Guid providerId, UpdateProposalDeliveryProviderRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @HandlerCode NVARCHAR(50);
SELECT @HandlerCode = HandlerCode
FROM Submissions.ProposalDeliveryProvider
WHERE ProposalDeliveryProviderId = @ProviderId AND TenantId = @TenantId AND IsDeleted = 0;

IF @HandlerCode IS NULL THROW 52045, 'Proposal delivery provider was not found.', 1;
IF @ConfigurationJson IS NOT NULL AND ISJSON(@ConfigurationJson) = 0 THROW 52046, 'Provider configuration must be valid JSON.', 1;

DECLARE @EffectiveEndpointUri NVARCHAR(1000) = COALESCE(NULLIF(@EndpointUri, N''), CASE WHEN @HandlerCode = N'Smtp' THEN N'smtp://netsol-smtp-oxcs.hostingplatform.com:587' END);
IF @HandlerCode = N'Smtp' AND @EffectiveEndpointUri IS NOT NULL AND @EffectiveEndpointUri NOT LIKE N'%://%'
    SET @EffectiveEndpointUri = CONCAT(N'smtp://', @EffectiveEndpointUri);
IF @HandlerCode = N'Smtp' AND @EffectiveEndpointUri IS NOT NULL AND @EffectiveEndpointUri LIKE N'smtp://%' AND @EffectiveEndpointUri NOT LIKE N'%:[0-9]%'
    SET @EffectiveEndpointUri = CONCAT(@EffectiveEndpointUri, N':587');
DECLARE @EffectiveSenderAddress NVARCHAR(320) = COALESCE(NULLIF(@SenderAddress, N''), CASE WHEN @HandlerCode = N'Smtp' THEN N'ams_admin@agencybinder.com' END);
DECLARE @EffectiveSecretReference NVARCHAR(500) = COALESCE(NULLIF(@SecretReference, N''), CASE WHEN @HandlerCode = N'Smtp' THEN N'AMS_PROPOSAL_SMTP_PASSWORD' END);

IF @IsConfigured = 1 AND @HandlerCode = N'Smtp' AND (@EffectiveEndpointUri IS NULL OR @EffectiveEndpointUri NOT LIKE N'smtp://%' OR @EffectiveSenderAddress IS NULL OR @EffectiveSecretReference IS NULL)
    THROW 52047, 'Configured SMTP delivery requires an smtp:// endpoint, sender address, and secret reference.', 1;
IF @IsConfigured = 1 AND @HandlerCode = N'ESignature' AND (NULLIF(@EndpointUri, N'') IS NULL OR @EndpointUri NOT LIKE N'https://%' OR NULLIF(@SecretReference, N'') IS NULL)
    THROW 52048, 'Configured e-signature delivery requires an HTTPS endpoint and secret reference.', 1;

UPDATE Submissions.ProposalDeliveryProvider
SET EndpointUri = @EffectiveEndpointUri,
    SenderAddress = @EffectiveSenderAddress,
    SecretReference = @EffectiveSecretReference,
    ConfigurationJson = NULLIF(@ConfigurationJson, N''),
    IsConfigured = @IsConfigured,
    IsActive = @IsActive,
    MaxAttempts = @MaxAttempts,
    RetryDelaySeconds = @RetryDelaySeconds,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE ProposalDeliveryProviderId = @ProviderId AND TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ProviderId = providerId,
            request.TenantId,
            request.EndpointUri,
            request.SenderAddress,
            request.SecretReference,
            request.ConfigurationJson,
            request.IsConfigured,
            request.IsActive,
            request.MaxAttempts,
            request.RetryDelaySeconds,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task PresentProposalAsync(Guid proposalId, ProposalPresentationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;

SELECT @SubmissionId = SubmissionId
FROM Submissions.Proposal
WHERE ProposalId = @ProposalId
  AND TenantId = @TenantId
  AND IsDeleted = 0
  AND GovernanceStatusCode IN (N'Delivered', N'Presented')
  AND DeliveryConfirmedDateUtc IS NOT NULL;

IF @SubmissionId IS NULL THROW 52038, 'Proposal must be delivered before it can be presented.', 1;

IF NOT EXISTS (SELECT 1 FROM Submissions.ProposalQuote WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 52212, 'Proposal presentation requires at least one included bindable quote with persisted bindable coverage lines.', 1;
IF EXISTS
(
    SELECT 1
    FROM Submissions.ProposalQuote proposalQuote
    INNER JOIN Submissions.Quote quote ON quote.QuoteId = proposalQuote.QuoteId
    WHERE proposalQuote.ProposalId = @ProposalId
      AND proposalQuote.TenantId = @TenantId
      AND proposalQuote.IsDeleted = 0
      AND
      (
          quote.IsDeleted = 1
          OR quote.IsBindable = 0
          OR NOT EXISTS (SELECT 1 FROM Submissions.QuoteLine quoteLine WHERE quoteLine.QuoteId = quote.QuoteId AND quoteLine.TenantId = @TenantId AND quoteLine.IsDeleted = 0)
          OR EXISTS (SELECT 1 FROM Submissions.QuoteLine quoteLine WHERE quoteLine.QuoteId = quote.QuoteId AND quoteLine.TenantId = @TenantId AND quoteLine.IsDeleted = 0 AND quoteLine.IsBindable = 0)
      )
)
    THROW 52212, 'Proposal presentation requires every included quote and persisted coverage line to remain bindable. Open Quote Review and resolve all bindability items before presentation.', 1;

UPDATE Submissions.Proposal
SET Status = N'Presented',
    GovernanceStatusCode = N'Presented',
    PresentedDateUtc = SYSUTCDATETIME(),
    PresentedByUserId = @PresentedByUserId,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @PresentedByUserId
WHERE ProposalId = @ProposalId
  AND TenantId = @TenantId
  AND IsDeleted = 0
  AND GovernanceStatusCode IN (N'Delivered', N'Presented')
  AND DeliveryConfirmedDateUtc IS NOT NULL;

UPDATE q
SET Status = CASE WHEN q.Status IN (N'Bound', N'Selected') THEN q.Status ELSE N'Presented' END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @PresentedByUserId
FROM Submissions.Quote q
INNER JOIN Submissions.ProposalQuote pq ON pq.QuoteId = q.QuoteId AND pq.ProposalId = @ProposalId AND pq.IsDeleted = 0
WHERE q.IsDeleted = 0;

UPDATE Submissions.Submission
SET Status = N'Presented', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @PresentedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0
  AND Status NOT IN (N'Customer Accepted', N'Binding', N'Bound', N'Lost', N'Cancelled', N'Closed');

INSERT INTO Submissions.ProposalLifecycleEvent
    (ProposalLifecycleEventId, TenantId, ProposalId, SubmissionId, EventCode, EventDetail, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), @TenantId, @ProposalId, @SubmissionId, N'Presented', @PresentationNotes, SYSUTCDATETIME(), SYSUTCDATETIME(), @PresentedByUserId, 0);

INSERT INTO Submissions.SubmissionActionLog
    (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES
    (NEWID(), @SubmissionId, @TenantId, N'ProposalPresented', @PresentationNotes, SYSUTCDATETIME(), @PresentedByUserId, N'Proposal', @ProposalId, N'User', 0);

SELECT @SubmissionId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var submissionId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { ProposalId = proposalId, request.TenantId, request.PresentationNotes, request.PresentedByUserId }, cancellationToken: cancellationToken));
        await RecordOpportunityWorkflowAsync(cn, submissionId, request.TenantId, "Proposal", "Proposal Presented", "Proposal Presented", request.PresentationNotes ?? string.Empty, "Proposal", proposalId, request.PresentedByUserId, cancellationToken);
    }

    public async Task<Guid> ProcessProposalProviderCallbackAsync(ProposalProviderCallbackRequest request, CancellationToken cancellationToken = default)
    {
        const string providerSql = @"SELECT TOP 1 ProposalDeliveryProviderId, SecretReference FROM Submissions.ProposalDeliveryProvider WHERE TenantId=@TenantId AND ProviderCode=@ProviderCode AND IsConfigured=1 AND IsActive=1 AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var provider = await cn.QuerySingleOrDefaultAsync<ProposalCallbackProvider>(new CommandDefinition(providerSql, new { request.TenantId, request.ProviderCode }, cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException("Proposal callback provider is not configured for this tenant.");
        var secret = string.IsNullOrWhiteSpace(provider.SecretReference) ? null : Environment.GetEnvironmentVariable(provider.SecretReference);
        if (string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("Proposal callback verification secret is unavailable.");
        var signedContent = string.Join("\n", request.TenantId.ToString("D"), request.ProviderCode, request.ProviderEventId, request.ExternalEnvelopeId ?? string.Empty, request.EventTypeCode, request.StatusCode, request.PayloadJson, request.SignedDocumentId?.ToString("D") ?? string.Empty, request.CertificateDocumentId?.ToString("D") ?? string.Empty);
        var expectedSignature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedContent))).ToLowerInvariant();
        var suppliedSignature = request.SignatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase) ? request.SignatureHeader[7..] : request.SignatureHeader;
        var signatureValid = suppliedSignature.Length == expectedSignature.Length && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(suppliedSignature.ToLowerInvariant()), Encoding.ASCII.GetBytes(expectedSignature));
        if (!signatureValid) throw new UnauthorizedAccessException("Proposal callback signature verification failed.");

        const string sql = @"
DECLARE @ExistingId UNIQUEIDENTIFIER=(SELECT ProposalProviderCallbackId FROM Submissions.ProposalProviderCallback WHERE TenantId=@TenantId AND ProviderCode=@ProviderCode AND ProviderEventId=@ProviderEventId);
IF @ExistingId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Submissions.ProposalProviderCallback WHERE ProposalProviderCallbackId=@ExistingId AND PayloadHash=@PayloadHash AND ISNULL(ExternalEnvelopeId,N'')=ISNULL(@ExternalEnvelopeId,N'') AND NormalizedStatusCode=@StatusCode)
        THROW 52212, 'Provider event identifier was reused with conflicting callback content.', 1;
    SELECT @ExistingId; RETURN;
END;
IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId=@TenantId AND OptionGroup=N'ProposalCallbackStatus' AND OptionCode=@StatusCode AND IsActive=1 AND IsDeleted=0) THROW 52209, 'Callback status is not configured for this tenant.', 1;
DECLARE @EnvelopeId UNIQUEIDENTIFIER,@DispatchId UNIQUEIDENTIFIER,@ProposalId UNIQUEIDENTIFIER,@SubmissionId UNIQUEIDENTIFIER,@CallbackId UNIQUEIDENTIFIER=NEWID();
SELECT @EnvelopeId=ProposalESignEnvelopeId,@DispatchId=ProposalDeliveryDispatchId,@ProposalId=ProposalId,@SubmissionId=SubmissionId FROM Submissions.ProposalESignEnvelope WHERE TenantId=@TenantId AND ProviderCode=@ProviderCode AND ExternalEnvelopeId=@ExternalEnvelopeId AND IsDeleted=0;
IF @DispatchId IS NULL SELECT TOP 1 @DispatchId=ProposalDeliveryDispatchId,@ProposalId=ProposalId,@SubmissionId=SubmissionId FROM Submissions.ProposalDeliveryDispatch WHERE TenantId=@TenantId AND ExternalDeliveryId=@ExternalEnvelopeId AND IsDeleted=0;
IF @DispatchId IS NULL THROW 52210, 'Callback could not be correlated to a proposal delivery.', 1;
INSERT INTO Submissions.ProposalProviderCallback (ProposalProviderCallbackId,TenantId,ProposalDeliveryProviderId,ProposalDeliveryDispatchId,ProposalESignEnvelopeId,ProviderCode,ProviderEventId,ExternalEnvelopeId,EventTypeCode,NormalizedStatusCode,SignatureHeader,PayloadJson,PayloadHash,IsSignatureValid,IsProcessed,ReceivedDateUtc)
VALUES (@CallbackId,@TenantId,@ProviderId,@DispatchId,@EnvelopeId,@ProviderCode,@ProviderEventId,@ExternalEnvelopeId,@EventTypeCode,@StatusCode,@SignatureHeader,@PayloadJson,@PayloadHash,1,0,SYSUTCDATETIME());
DECLARE @CurrentDispatchStatus NVARCHAR(50)=(SELECT StatusCode FROM Submissions.ProposalDeliveryDispatch WHERE ProposalDeliveryDispatchId=@DispatchId AND TenantId=@TenantId);
DECLARE @EffectiveStatus NVARCHAR(50)=CASE
 WHEN @CurrentDispatchStatus=N'Signed' THEN N'Signed'
 WHEN @CurrentDispatchStatus IN(N'Declined',N'Expired',N'Bounced',N'Cancelled',N'Failed') THEN @CurrentDispatchStatus
 WHEN @CurrentDispatchStatus IN(N'Delivered',N'Viewed',N'Downloaded') AND @StatusCode=N'Sent' THEN @CurrentDispatchStatus
 ELSE @StatusCode END;
UPDATE Submissions.ProposalDeliveryDispatch SET StatusCode=@EffectiveStatus, CompletedDateUtc=CASE WHEN @StatusCode IN(N'Delivered',N'Signed',N'Declined',N'Expired',N'Bounced',N'Cancelled',N'Failed') THEN COALESCE(CompletedDateUtc,SYSUTCDATETIME()) ELSE CompletedDateUtc END,
 FirstViewedDateUtc=CASE WHEN @StatusCode=N'Viewed' THEN COALESCE(FirstViewedDateUtc,SYSUTCDATETIME()) ELSE FirstViewedDateUtc END, LastViewedDateUtc=CASE WHEN @StatusCode=N'Viewed' THEN SYSUTCDATETIME() ELSE LastViewedDateUtc END,
 DownloadedDateUtc=CASE WHEN @StatusCode=N'Downloaded' THEN SYSUTCDATETIME() ELSE DownloadedDateUtc END, SignedDateUtc=CASE WHEN @StatusCode=N'Signed' THEN SYSUTCDATETIME() ELSE SignedDateUtc END,
 DeclinedDateUtc=CASE WHEN @StatusCode=N'Declined' THEN SYSUTCDATETIME() ELSE DeclinedDateUtc END, ExpiredDateUtc=CASE WHEN @StatusCode=N'Expired' THEN SYSUTCDATETIME() ELSE ExpiredDateUtc END,
 BouncedDateUtc=CASE WHEN @StatusCode=N'Bounced' THEN SYSUTCDATETIME() ELSE BouncedDateUtc END, CancelledDateUtc=CASE WHEN @StatusCode=N'Cancelled' THEN SYSUTCDATETIME() ELSE CancelledDateUtc END, ModifiedDateUtc=SYSUTCDATETIME()
WHERE ProposalDeliveryDispatchId=@DispatchId AND TenantId=@TenantId;
IF @EnvelopeId IS NOT NULL UPDATE Submissions.ProposalESignEnvelope SET StatusCode=@EffectiveStatus, LastProviderEventId=@ProviderEventId, SentDateUtc=CASE WHEN @StatusCode=N'Sent' THEN COALESCE(SentDateUtc,SYSUTCDATETIME()) ELSE SentDateUtc END, DeliveredDateUtc=CASE WHEN @StatusCode=N'Delivered' THEN COALESCE(DeliveredDateUtc,SYSUTCDATETIME()) ELSE DeliveredDateUtc END, FirstViewedDateUtc=CASE WHEN @StatusCode=N'Viewed' THEN COALESCE(FirstViewedDateUtc,SYSUTCDATETIME()) ELSE FirstViewedDateUtc END, CompletedDateUtc=CASE WHEN @StatusCode=N'Signed' THEN SYSUTCDATETIME() ELSE CompletedDateUtc END, DeclinedDateUtc=CASE WHEN @StatusCode=N'Declined' THEN SYSUTCDATETIME() ELSE DeclinedDateUtc END, ExpiredDateUtc=CASE WHEN @StatusCode=N'Expired' THEN SYSUTCDATETIME() ELSE ExpiredDateUtc END, VoidedDateUtc=CASE WHEN @StatusCode=N'Cancelled' THEN SYSUTCDATETIME() ELSE VoidedDateUtc END, SignedDocumentId=COALESCE(@SignedDocumentId,SignedDocumentId), CertificateDocumentId=COALESCE(@CertificateDocumentId,CertificateDocumentId), ModifiedDateUtc=SYSUTCDATETIME() WHERE ProposalESignEnvelopeId=@EnvelopeId;
UPDATE Submissions.Proposal SET DeliveryStatus=@EffectiveStatus, Status=CASE WHEN @StatusCode IN(N'Delivered',N'Viewed',N'Downloaded',N'Signed') AND PresentedDateUtc IS NULL THEN N'Delivered' ELSE Status END, GovernanceStatusCode=CASE WHEN @StatusCode IN(N'Delivered',N'Viewed',N'Downloaded',N'Signed') AND PresentedDateUtc IS NULL THEN N'Delivered' ELSE GovernanceStatusCode END, DeliveryConfirmedDateUtc=CASE WHEN @StatusCode IN(N'Delivered',N'Viewed',N'Downloaded',N'Signed') THEN COALESCE(DeliveryConfirmedDateUtc,SYSUTCDATETIME()) ELSE DeliveryConfirmedDateUtc END, ModifiedDateUtc=SYSUTCDATETIME() WHERE ProposalId=@ProposalId AND TenantId=@TenantId;
INSERT INTO Submissions.ProposalLifecycleEvent (ProposalLifecycleEventId,TenantId,ProposalId,SubmissionId,EventCode,EventDetail,EventDateUtc,CreatedDateUtc,IsDeleted) VALUES (NEWID(),@TenantId,@ProposalId,@SubmissionId,@StatusCode,CONCAT(@ProviderCode,N' event ',@ProviderEventId),SYSUTCDATETIME(),SYSUTCDATETIME(),0);
UPDATE Submissions.ProposalProviderCallback SET IsProcessed=1,ProcessedDateUtc=SYSUTCDATETIME() WHERE ProposalProviderCallbackId=@CallbackId;
SELECT @CallbackId;";
        try
        {
            return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { ProviderId = provider.ProposalDeliveryProviderId, request.TenantId, request.ProviderCode, request.ProviderEventId, request.ExternalEnvelopeId, request.EventTypeCode, request.StatusCode, request.SignatureHeader, request.PayloadJson, PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.PayloadJson))), request.SignedDocumentId, request.CertificateDocumentId }, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 52209 or 52210 or 52212)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
    }

    private sealed class ProposalCallbackProvider
    {
        public Guid ProposalDeliveryProviderId { get; set; }
        public string? SecretReference { get; set; }
    }

    public async Task<ProposalBindContinuationDto> GetProposalBindContinuationAsync(Guid proposalId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT p.ProposalId,
       p.SubmissionId,
       p.TenantId,
       CAST(CASE WHEN p.Status = N'Accepted' AND selectedQuote.QuoteId IS NOT NULL AND authorization.CustomerAuthorizationId IS NOT NULL AND acceptance.ClientAcceptanceId IS NOT NULL AND activeBind.PolicyBindTransactionId IS NULL AND selectedQuote.IsBindable = 1 AND selectedQuote.ExpiresDateUtc > SYSUTCDATETIME() AND acceptance.QuoteFingerprint = selectedQuote.CurrentFingerprint THEN 1 ELSE 0 END AS bit) AS CanRequestBind,
       selectedQuote.QuoteId AS SelectedQuoteId,
       authorization.CustomerAuthorizationId,
       CASE WHEN p.Status <> N'Accepted' THEN N'The customer must accept the presented proposal before requesting bind.'
            WHEN selectedQuote.QuoteId IS NULL THEN N'Customer acceptance must identify a proposal quote.'
            WHEN authorization.CustomerAuthorizationId IS NULL THEN N'Customer authorization must be recorded before requesting bind.'
             WHEN acceptance.ClientAcceptanceId IS NULL THEN N'A compliant client acceptance record is required before requesting bind.'
             WHEN activeBind.PolicyBindTransactionId IS NOT NULL THEN CONCAT(N'A bind request already exists in ', COALESCE(activeBind.BindStatusName, activeBind.BindStatusCode), N'. Continue the existing bind workflow.')
             WHEN selectedQuote.IsBindable = 0 THEN N'The accepted quote is no longer bindable.'
             WHEN selectedQuote.ExpiresDateUtc <= SYSUTCDATETIME() THEN N'The accepted quote has expired.'
             WHEN acceptance.QuoteFingerprint <> selectedQuote.CurrentFingerprint THEN N'Quote terms changed after acceptance; client reconfirmation is required.'
            ELSE NULL END AS BlockingReason
FROM Submissions.Proposal p
OUTER APPLY
(
    SELECT TOP 1 q.QuoteId, q.IsBindable, q.ExpiresDateUtc,
           CONVERT(char(64), HASHBYTES('SHA2_256', CONCAT(q.QuoteId, N'|', q.ResponseVersion, N'|', q.AnnualPremium, N'|', q.ExpiresDateUtc, N'|', q.IsBindable, N'|',
               COALESCE((SELECT STRING_AGG(CONCAT(ql.QuoteLineId, N':', ql.QuotedPremium, N':', COALESCE(ql.[Limit], 0), N':', COALESCE(ql.Deductible, 0), N':', COALESCE(ql.TriaIncluded, 0), N':', COALESCE(ql.ModifiedDateUtc, ql.CreatedDateUtc)), N'|') WITHIN GROUP (ORDER BY ql.SortOrder, ql.QuoteLineId) FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.IsDeleted = 0), N''))), 2) AS CurrentFingerprint
    FROM Submissions.ProposalQuote pq
    INNER JOIN Submissions.Quote q ON q.QuoteId = pq.QuoteId AND q.SubmissionId = p.SubmissionId AND q.IsSelected = 1 AND q.IsDeleted = 0
    WHERE pq.ProposalId = p.ProposalId AND pq.TenantId = p.TenantId AND pq.IsDeleted = 0
    ORDER BY pq.SortOrder
) selectedQuote
OUTER APPLY
(
    SELECT TOP 1 ca.CustomerAuthorizationId
    FROM Submissions.CustomerAuthorization ca
    WHERE ca.TenantId = p.TenantId AND ca.SubmissionId = p.SubmissionId AND ca.ProposalId = p.ProposalId
      AND ca.QuoteId = selectedQuote.QuoteId AND ca.IsDeleted = 0
    ORDER BY ca.AuthorizedDateUtc DESC
) authorization
OUTER APPLY
(
    SELECT TOP 1 ca.ClientAcceptanceId, ca.QuoteFingerprint
    FROM Submissions.ClientAcceptance ca
    WHERE ca.TenantId = p.TenantId AND ca.SubmissionId = p.SubmissionId AND ca.ProposalId = p.ProposalId
      AND ca.QuoteId = selectedQuote.QuoteId AND ca.CustomerAuthorizationId = authorization.CustomerAuthorizationId
      AND ca.StatusCode = N'Accepted' AND ca.PolicyBindTransactionId IS NULL AND ca.IsDeleted = 0
    ORDER BY ca.CreatedDateUtc DESC
) acceptance
OUTER APPLY
(
    SELECT TOP 1 pbt.PolicyBindTransactionId, pbt.BindStatusCode, COALESCE(pbs.StatusName, pbt.BindStatusCode) AS BindStatusName
    FROM Submissions.PolicyBindTransaction pbt
    LEFT JOIN Submissions.PolicyBindStatus pbs
      ON pbs.TenantId = pbt.TenantId AND pbs.StatusCode = pbt.BindStatusCode AND pbs.IsActive = 1 AND pbs.IsDeleted = 0
    WHERE pbt.TenantId = p.TenantId AND pbt.SubmissionId = p.SubmissionId AND pbt.QuoteId = selectedQuote.QuoteId
      AND pbt.IsDeleted = 0 AND (pbs.PolicyBindStatusId IS NULL OR pbs.IsTerminal = 0)
    ORDER BY pbt.CreatedDateUtc DESC, pbt.PolicyBindTransactionId DESC
) activeBind
WHERE p.ProposalId = @ProposalId AND p.TenantId = @TenantId AND p.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var result = await cn.QuerySingleOrDefaultAsync<ProposalBindContinuationDto>(new CommandDefinition(sql, new { ProposalId = proposalId, TenantId = tenantId }, cancellationToken: cancellationToken));
        return result ?? throw new InvalidOperationException("Proposal was not found for bind continuation.");
    }

    public async Task<ClientAcceptanceReadinessDto> GetClientAcceptanceReadinessAsync(Guid proposalId, Guid? quoteId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 1 p.ProposalId, p.SubmissionId, p.TenantId, p.VersionNumber AS ProposalVersionNumber,
       q.QuoteId AS SelectedQuoteId,
       CONVERT(char(64), HASHBYTES('SHA2_256', CONCAT(q.QuoteId, N'|', q.ResponseVersion, N'|', q.AnnualPremium, N'|', q.ExpiresDateUtc, N'|', q.IsBindable, N'|',
           COALESCE((SELECT STRING_AGG(CONCAT(ql.QuoteLineId, N':', ql.QuotedPremium, N':', COALESCE(ql.[Limit], 0), N':', COALESCE(ql.Deductible, 0), N':', COALESCE(ql.TriaIncluded, 0), N':', COALESCE(ql.ModifiedDateUtc, ql.CreatedDateUtc)), N'|') WITHIN GROUP (ORDER BY ql.SortOrder, ql.QuoteLineId) FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.IsDeleted = 0), N''))), 2) AS QuoteFingerprint,
       CAST(CASE WHEN p.PresentedDateUtc IS NOT NULL AND p.DeliveryConfirmedDateUtc IS NOT NULL THEN 1 ELSE 0 END AS bit) AS IsProposalDelivered,
       CAST(CASE WHEN p.VersionNumber = (SELECT MAX(p2.VersionNumber) FROM Submissions.Proposal p2 WHERE p2.TenantId = p.TenantId AND p2.SubmissionId = p.SubmissionId AND p2.IsDeleted = 0) THEN 1 ELSE 0 END AS bit) AS IsProposalCurrent,
       CAST(CASE WHEN pq.ProposalQuoteId IS NOT NULL THEN 1 ELSE 0 END AS bit) AS IsQuoteInProposal,
       CAST(CASE WHEN q.Status NOT IN (N'Declined', N'Expired', N'Withdrawn', N'Not Selected') THEN 1 ELSE 0 END AS bit) AS IsQuoteActive,
       CAST(CASE WHEN q.ExpiresDateUtc > SYSUTCDATETIME() THEN 1 ELSE 0 END AS bit) AS IsQuoteUnexpired,
       q.IsBindable AS IsQuoteBindable,
       CAST(CASE WHEN EXISTS (SELECT 1 FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.TenantId = p.TenantId AND ql.SubmissionLineId IS NOT NULL AND ql.IsDeleted = 0) THEN 1 ELSE 0 END AS bit) AS HasCoverageLines,
       CAST(CASE WHEN NOT EXISTS
       (
           SELECT 1
           FROM Submissions.SubmissionLine sl
           WHERE sl.SubmissionId = p.SubmissionId
             AND sl.TenantId = p.TenantId
             AND sl.IsDeleted = 0
             AND NOT EXISTS
             (
                 SELECT 1
                 FROM Submissions.QuoteLine ql
                 WHERE ql.QuoteId = q.QuoteId
                   AND ql.TenantId = p.TenantId
                   AND ql.SubmissionLineId = sl.SubmissionLineId
                   AND ql.IsDeleted = 0
                   AND ql.IsBindable = 1
             )
       ) THEN 1 ELSE 0 END AS bit) AS HasCompleteBindableCoverage
FROM Submissions.Proposal p
LEFT JOIN Submissions.ProposalQuote pq ON pq.ProposalId = p.ProposalId AND pq.TenantId = p.TenantId AND pq.IsDeleted = 0 AND (@QuoteId IS NULL OR pq.QuoteId = @QuoteId)
LEFT JOIN Submissions.Quote q ON q.QuoteId = pq.QuoteId AND q.SubmissionId = p.SubmissionId AND q.IsDeleted = 0
WHERE p.ProposalId = @ProposalId AND p.TenantId = @TenantId AND p.IsDeleted = 0
ORDER BY CASE WHEN q.IsSelected = 1 THEN 0 ELSE 1 END, pq.SortOrder;

SELECT q.QuoteId, q.QuoteNumber, c.CarrierName, q.AnnualPremium, q.Deductible, q.[Limit], q.CoverageNotes, q.IsSelected, pq.SortOrder
FROM Submissions.ProposalQuote pq
INNER JOIN Submissions.Quote q ON q.QuoteId = pq.QuoteId AND q.SubmissionId = pq.SubmissionId AND q.IsDeleted = 0
INNER JOIN Core.Carrier c ON c.CarrierId = q.CarrierId
WHERE pq.ProposalId = @ProposalId AND pq.TenantId = @TenantId AND pq.IsDeleted = 0
ORDER BY pq.SortOrder;

SELECT ql.QuoteLineId, ql.TenantId, ql.QuoteId, ql.SubmissionId, ql.SubmissionLineId, ql.OpportunityLineId,
       ql.LineOfBusiness, ql.Status, ql.QuotedPremium, ql.Deductible, ql.[Limit], ql.CommissionPercent,
       ql.CoverageForms, ql.Subjectivities, ql.Exclusions, ql.PaymentTerms, ql.MinimumEarnedPremium,
       ql.TaxesAndFees, ql.BrokerFee, ql.TriaIncluded, ql.IsBindable, ql.CoverageNotes, ql.SortOrder,
       ql.CreatedDateUtc, ql.ModifiedDateUtc
FROM Submissions.QuoteLine ql
WHERE ql.QuoteId = COALESCE(@QuoteId, (SELECT TOP 1 pq.QuoteId FROM Submissions.ProposalQuote pq INNER JOIN Submissions.Quote q ON q.QuoteId = pq.QuoteId AND q.IsDeleted = 0 WHERE pq.ProposalId = @ProposalId AND pq.TenantId = @TenantId AND pq.IsDeleted = 0 ORDER BY CASE WHEN q.IsSelected = 1 THEN 0 ELSE 1 END, pq.SortOrder))
  AND ql.TenantId = @TenantId AND ql.IsDeleted = 0
ORDER BY ql.SortOrder, ql.LineOfBusiness;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var grid = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { ProposalId = proposalId, QuoteId = quoteId, TenantId = tenantId }, cancellationToken: cancellationToken));
        var readiness = await grid.ReadSingleOrDefaultAsync<ClientAcceptanceReadinessDto>() ?? throw new InvalidOperationException("Proposal or proposal quote was not found for client acceptance.");
        readiness.Quotes = (await grid.ReadAsync<ProposalQuoteDto>()).AsList();
        readiness.QuoteLines = (await grid.ReadAsync<SubmissionQuoteLineDto>()).AsList();
        var blockers = new List<string>();
        if (!readiness.IsProposalDelivered) blockers.Add("The proposal must have confirmed delivery and be explicitly presented before recording a client decision.");
        if (!readiness.IsProposalCurrent) blockers.Add("A newer proposal version exists; client acceptance must use the current version.");
        if (!readiness.IsQuoteInProposal) blockers.Add("The selected quote is not part of this proposal version.");
        if (!readiness.IsQuoteActive) blockers.Add("The selected quote is no longer active.");
        if (!readiness.IsQuoteUnexpired) blockers.Add("The selected quote has expired.");
        if (!readiness.IsQuoteBindable) blockers.Add("The selected quote is not marked bindable.");
        if (!readiness.HasCoverageLines) blockers.Add("The selected quote has no persisted coverage lines.");
        if (!readiness.HasCompleteBindableCoverage) blockers.Add("Every active submission line must have a corresponding persisted bindable quote line before client acceptance.");
        readiness.BlockingReasons = blockers;
        readiness.CanAccept = blockers.Count == 0;
        return readiness;
    }

    public async Task<IReadOnlyList<ClientAcceptanceDto>> GetClientAcceptancesAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT ca.ClientAcceptanceId, ca.TenantId, ca.AccountId, ca.SubmissionId, ca.ProposalId, ca.ProposalVersionNumber,
       ca.QuoteId, ca.QuoteNumber, ca.QuoteFingerprint, ca.DecisionCode, ca.StatusCode, ca.DecisionNotes,
       ca.AuthorizationMethodCode, ca.AuthorizationReference, ca.AuthorizationDocumentId, ca.ESignRequestId,
       ca.AuthorizedByName, ca.AuthorizedByTitle, ca.AuthorityBasisCode, ca.AuthorizedDateUtc, ca.SignerEmail,
       ca.SignerIpAddress, ca.UserAgent, ca.CustomerAuthorizationId, ca.PolicyBindTransactionId,
       ca.IdempotencyKey, ca.VersionNumber, ca.CreatedDateUtc, ca.CreatedByUserId
FROM Submissions.ClientAcceptance ca
WHERE ca.SubmissionId = @SubmissionId AND ca.TenantId = @TenantId AND ca.IsDeleted = 0
ORDER BY ca.CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<ClientAcceptanceDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<ClientAcceptanceDto?> GetClientAcceptanceByIdAsync(Guid clientAcceptanceId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT ca.ClientAcceptanceId, ca.TenantId, ca.AccountId, ca.SubmissionId, ca.ProposalId, ca.ProposalVersionNumber,
       ca.QuoteId, ca.QuoteNumber, ca.QuoteFingerprint, ca.DecisionCode, ca.StatusCode, ca.DecisionNotes,
       ca.AuthorizationMethodCode, ca.AuthorizationReference, ca.AuthorizationDocumentId, ca.ESignRequestId,
       ca.AuthorizedByName, ca.AuthorizedByTitle, ca.AuthorityBasisCode, ca.AuthorizedDateUtc, ca.SignerEmail,
       ca.SignerIpAddress, ca.UserAgent, ca.CustomerAuthorizationId, ca.PolicyBindTransactionId,
       ca.IdempotencyKey, ca.VersionNumber, ca.CreatedDateUtc, ca.CreatedByUserId
FROM Submissions.ClientAcceptance ca WHERE ca.ClientAcceptanceId = @ClientAcceptanceId AND ca.TenantId = @TenantId AND ca.IsDeleted = 0;
SELECT e.ClientAcceptanceCoverageElectionId, e.ClientAcceptanceId, e.QuoteLineId, e.SubmissionLineId, e.LineOfBusiness,
       e.ElectionCode, e.QuotedPremium, e.[Limit], e.Deductible, e.CoverageForms, e.Subjectivities, e.Exclusions,
       e.PaymentTerms, e.TriaIncluded, e.ElectionNotes, e.SortOrder
FROM Submissions.ClientAcceptanceCoverageElection e WHERE e.ClientAcceptanceId = @ClientAcceptanceId AND e.TenantId = @TenantId AND e.IsDeleted = 0 ORDER BY e.SortOrder;
SELECT c.ClientAcceptanceConsentId, c.ClientAcceptanceId, c.ConsentCode, c.ConsentVersion, c.IsAccepted, c.AttestedDateUtc, c.EvidenceDocumentId
FROM Submissions.ClientAcceptanceConsent c WHERE c.ClientAcceptanceId = @ClientAcceptanceId AND c.TenantId = @TenantId AND c.IsDeleted = 0 ORDER BY c.ConsentCode;
SELECT a.ClientAcceptanceAuditEventId, a.ClientAcceptanceId, a.EventCode, a.EventDetail, a.DataJson, a.EventDateUtc, a.ActorUserId
FROM Submissions.ClientAcceptanceAuditEvent a WHERE a.ClientAcceptanceId = @ClientAcceptanceId AND a.TenantId = @TenantId ORDER BY a.EventDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var grid = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { ClientAcceptanceId = clientAcceptanceId, TenantId = tenantId }, cancellationToken: cancellationToken));
        var item = await grid.ReadSingleOrDefaultAsync<ClientAcceptanceDto>();
        if (item is null) return null;
        item.CoverageElections = (await grid.ReadAsync<ClientAcceptanceCoverageElectionDto>()).AsList();
        item.Consents = (await grid.ReadAsync<ClientAcceptanceConsentDto>()).AsList();
        item.AuditEvents = (await grid.ReadAsync<ClientAcceptanceAuditEventDto>()).AsList();
        return item;
    }

    public async Task<Guid> RecordClientAcceptanceAsync(RecordClientAcceptanceRequest request, CancellationToken cancellationToken = default)
    {
        var readiness = await GetClientAcceptanceReadinessAsync(request.ProposalId, request.QuoteId, request.TenantId, cancellationToken);
        var isAccepted = string.Equals(request.DecisionCode, "Accepted", StringComparison.OrdinalIgnoreCase);
        if (isAccepted && !readiness.CanAccept) throw new InvalidOperationException(string.Join(" ", readiness.BlockingReasons));
        if (readiness.ProposalVersionNumber != request.ProposalVersionNumber) throw new InvalidOperationException("Proposal version changed; reload client acceptance before continuing.");
        if (!string.Equals(readiness.QuoteFingerprint, request.QuoteFingerprint, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Quote terms changed; reload and reconfirm all coverage elections.");
        if (isAccepted && request.CoverageElections.Count != readiness.QuoteLines.Count) throw new InvalidOperationException("Every persisted quote coverage line requires an explicit election.");

        const string sql = @"
DECLARE @ExistingId UNIQUEIDENTIFIER = (SELECT ClientAcceptanceId FROM Submissions.ClientAcceptance WHERE TenantId = @TenantId AND IdempotencyKey = @IdempotencyKey);
IF @ExistingId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Submissions.ClientAcceptance WHERE ClientAcceptanceId = @ExistingId AND ProposalId = @ProposalId AND QuoteId = @QuoteId AND ProposalVersionNumber = @ProposalVersionNumber AND DecisionCode = @DecisionCode AND QuoteFingerprint = @QuoteFingerprint)
        THROW 52113, 'The idempotency key was already used for a different client acceptance command.', 1;
    SELECT @ExistingId; RETURN;
END;
IF EXISTS (SELECT 1 FROM Submissions.ClientAcceptance WHERE TenantId = @TenantId AND ProposalId = @ProposalId AND IsDeleted = 0 AND StatusCode IN (N'Accepted', N'BindRequested', N'CarrierBound'))
    THROW 52110, 'An active acceptance already exists for this proposal.', 1;
DECLARE @SubmissionId UNIQUEIDENTIFIER, @AccountId UNIQUEIDENTIFIER, @QuoteNumber NVARCHAR(100), @CustomerAuthorizationId UNIQUEIDENTIFIER = NULL;
SELECT @SubmissionId = p.SubmissionId, @AccountId = s.AccountId FROM Submissions.Proposal p INNER JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.TenantId = p.TenantId AND s.IsDeleted = 0 WHERE p.ProposalId = @ProposalId AND p.TenantId = @TenantId AND p.VersionNumber = @ProposalVersionNumber AND p.IsDeleted = 0;
SELECT @QuoteNumber = q.QuoteNumber FROM Submissions.Quote q INNER JOIN Submissions.ProposalQuote pq ON pq.QuoteId = q.QuoteId AND pq.ProposalId = @ProposalId AND pq.IsDeleted = 0 WHERE q.QuoteId = @QuoteId AND q.SubmissionId = @SubmissionId AND q.IsDeleted = 0;
IF @SubmissionId IS NULL OR @QuoteNumber IS NULL THROW 52111, 'Proposal or selected quote changed before acceptance was recorded.', 1;
IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'ClientAcceptanceDecision' AND OptionCode = @DecisionCode AND IsActive = 1 AND IsDeleted = 0)
    THROW 52119, 'The selected client decision is not active for this tenant.', 1;
IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'CustomerAuthorizationMethod' AND OptionCode = @AuthorizationMethodCode AND IsActive = 1 AND IsDeleted = 0)
    THROW 52120, 'The selected authorization method is not active for this tenant.', 1;
IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'AuthorityBasis' AND OptionCode = @AuthorityBasisCode AND IsActive = 1 AND IsDeleted = 0)
    THROW 52121, 'The selected authority basis is not active for this tenant.', 1;
IF @AuthorizationDocumentId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DMS.Document WHERE DocumentId = @AuthorizationDocumentId AND TenantId = @TenantId AND EntityName = N'Submission' AND EntityId = @SubmissionId AND IsDeleted = 0)
    THROW 52122, 'The authorization evidence document does not belong to this submission.', 1;
IF EXISTS (
    SELECT 1 FROM OPENJSON(@ConsentsJson) WITH (EvidenceDocumentId UNIQUEIDENTIFIER) j
    LEFT JOIN DMS.Document d ON d.DocumentId = j.EvidenceDocumentId AND d.TenantId = @TenantId AND d.EntityName = N'Submission' AND d.EntityId = @SubmissionId AND d.IsDeleted = 0
    WHERE j.EvidenceDocumentId IS NOT NULL AND d.DocumentId IS NULL)
    THROW 52123, 'A consent evidence document does not belong to this submission.', 1;
IF @DecisionCode = N'Accepted'
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM Submissions.Proposal p
        INNER JOIN Submissions.ProposalQuote pq ON pq.ProposalId = p.ProposalId AND pq.QuoteId = @QuoteId AND pq.TenantId = @TenantId AND pq.IsDeleted = 0
        INNER JOIN Submissions.Quote q ON q.QuoteId = pq.QuoteId AND q.SubmissionId = p.SubmissionId AND q.IsDeleted = 0
        WHERE p.ProposalId = @ProposalId AND p.TenantId = @TenantId AND p.VersionNumber = @ProposalVersionNumber AND p.IsDeleted = 0
          AND p.DeliveryConfirmedDateUtc IS NOT NULL AND p.PresentedDateUtc IS NOT NULL
          AND p.VersionNumber = (SELECT MAX(p2.VersionNumber) FROM Submissions.Proposal p2 WHERE p2.TenantId = p.TenantId AND p2.SubmissionId = p.SubmissionId AND p2.IsDeleted = 0)
          AND q.Status NOT IN (N'Declined', N'Expired', N'Withdrawn', N'Not Selected') AND q.ExpiresDateUtc > SYSUTCDATETIME() AND q.IsBindable = 1
          AND CONVERT(char(64), HASHBYTES('SHA2_256', CONCAT(q.QuoteId, N'|', q.ResponseVersion, N'|', q.AnnualPremium, N'|', q.ExpiresDateUtc, N'|', q.IsBindable, N'|',
              COALESCE((SELECT STRING_AGG(CONCAT(ql.QuoteLineId, N':', ql.QuotedPremium, N':', COALESCE(ql.[Limit], 0), N':', COALESCE(ql.Deductible, 0), N':', COALESCE(ql.TriaIncluded, 0), N':', COALESCE(ql.ModifiedDateUtc, ql.CreatedDateUtc)), N'|') WITHIN GROUP (ORDER BY ql.SortOrder, ql.QuoteLineId) FROM Submissions.QuoteLine ql WHERE ql.QuoteId = q.QuoteId AND ql.IsDeleted = 0), N''))), 2) = @QuoteFingerprint)
        THROW 52118, 'Proposal or quote eligibility changed; reload client acceptance before continuing.', 1;
    IF (SELECT COUNT(1) FROM OPENJSON(@ElectionsJson)) <> @ExpectedElectionCount
        OR (SELECT COUNT(DISTINCT QuoteLineId) FROM OPENJSON(@ElectionsJson) WITH (QuoteLineId UNIQUEIDENTIFIER)) <> @ExpectedElectionCount
        OR (SELECT COUNT(1) FROM Submissions.QuoteLine WHERE QuoteId = @QuoteId AND TenantId = @TenantId AND SubmissionLineId IS NOT NULL AND IsDeleted = 0) <> @ExpectedElectionCount
        THROW 52114, 'Coverage elections contain duplicate or invalid rows.', 1;
    IF EXISTS (
        SELECT 1 FROM OPENJSON(@ElectionsJson) WITH (QuoteLineId UNIQUEIDENTIFIER, ElectionCode NVARCHAR(50)) j
        LEFT JOIN Submissions.QuoteLine ql ON ql.QuoteLineId = j.QuoteLineId AND ql.QuoteId = @QuoteId AND ql.TenantId = @TenantId AND ql.IsDeleted = 0
        WHERE ql.QuoteLineId IS NULL OR j.ElectionCode NOT IN (N'Accepted', N'Rejected', N'OptionalAccepted', N'OptionalRejected'))
        THROW 52115, 'Coverage elections no longer match the selected quote.', 1;
    IF (SELECT COUNT(1) FROM OPENJSON(@ConsentsJson)) <> (SELECT COUNT(1) FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = N'ClientAcceptanceConsent' AND IsActive = 1 AND IsDeleted = 0)
        OR (SELECT COUNT(DISTINCT ConsentCode) FROM OPENJSON(@ConsentsJson) WITH (ConsentCode NVARCHAR(100))) <> (SELECT COUNT(1) FROM OPENJSON(@ConsentsJson))
        OR EXISTS (
            SELECT 1 FROM OPENJSON(@ConsentsJson) WITH (ConsentCode NVARCHAR(100), ConsentVersion NVARCHAR(50), IsAccepted BIT) j
            LEFT JOIN Submissions.SubmissionReferenceOption o ON o.TenantId = @TenantId AND o.OptionGroup = N'ClientAcceptanceConsent' AND o.OptionCode = j.ConsentCode AND o.IsActive = 1 AND o.IsDeleted = 0
            WHERE o.SubmissionReferenceOptionId IS NULL OR j.IsAccepted = 0 OR NULLIF(LTRIM(RTRIM(j.ConsentVersion)), N'') IS NULL)
        THROW 52116, 'All currently configured client acceptance consents must be explicitly attested.', 1;
    SET @CustomerAuthorizationId = NEWID();
    INSERT INTO Submissions.CustomerAuthorization (CustomerAuthorizationId, TenantId, SubmissionId, QuoteId, ProposalId, AuthorizationMethodCode, AuthorizationReference, AuthorizationNotes, AuthorizedByName, AuthorizedDateUtc, DocumentId, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@CustomerAuthorizationId, @TenantId, @SubmissionId, @QuoteId, @ProposalId, @AuthorizationMethodCode, @AuthorizationReference, @DecisionNotes, @AuthorizedByName, @AuthorizedDateUtc, @AuthorizationDocumentId, SYSUTCDATETIME(), @RecordedByUserId, 0);
END;
INSERT INTO Submissions.ClientAcceptance (ClientAcceptanceId, TenantId, AccountId, SubmissionId, ProposalId, ProposalVersionNumber, QuoteId, QuoteNumber, QuoteFingerprint, DecisionCode, StatusCode, DecisionNotes, AuthorizationMethodCode, AuthorizationReference, AuthorizationDocumentId, ESignRequestId, AuthorizedByName, AuthorizedByTitle, AuthorityBasisCode, AuthorizedDateUtc, SignerEmail, SignerIpAddress, UserAgent, CustomerAuthorizationId, IdempotencyKey, VersionNumber, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@ClientAcceptanceId, @TenantId, @AccountId, @SubmissionId, @ProposalId, @ProposalVersionNumber, @QuoteId, @QuoteNumber, @QuoteFingerprint, @DecisionCode, @DecisionCode, @DecisionNotes, @AuthorizationMethodCode, @AuthorizationReference, @AuthorizationDocumentId, @ESignRequestId, @AuthorizedByName, @AuthorizedByTitle, @AuthorityBasisCode, @AuthorizedDateUtc, @SignerEmail, @SignerIpAddress, @UserAgent, @CustomerAuthorizationId, @IdempotencyKey, 1, SYSUTCDATETIME(), @RecordedByUserId, 0);
INSERT INTO Submissions.ClientAcceptanceCoverageElection (ClientAcceptanceCoverageElectionId, TenantId, ClientAcceptanceId, QuoteLineId, SubmissionLineId, LineOfBusiness, ElectionCode, QuotedPremium, [Limit], Deductible, CoverageForms, Subjectivities, Exclusions, PaymentTerms, TriaIncluded, ElectionNotes, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @TenantId, @ClientAcceptanceId, ql.QuoteLineId, ql.SubmissionLineId, ql.LineOfBusiness, j.ElectionCode, ql.QuotedPremium, ql.[Limit], ql.Deductible, ql.CoverageForms, ql.Subjectivities, ql.Exclusions, ql.PaymentTerms, ql.TriaIncluded, j.ElectionNotes, ql.SortOrder, SYSUTCDATETIME(), @RecordedByUserId, 0
FROM OPENJSON(@ElectionsJson) WITH (QuoteLineId UNIQUEIDENTIFIER, ElectionCode NVARCHAR(50), ElectionNotes NVARCHAR(1000)) j INNER JOIN Submissions.QuoteLine ql ON ql.QuoteLineId = j.QuoteLineId AND ql.QuoteId = @QuoteId AND ql.TenantId = @TenantId AND ql.IsDeleted = 0;
IF @DecisionCode = N'Accepted' AND @@ROWCOUNT <> @ExpectedElectionCount THROW 52117, 'Not all coverage elections could be persisted.', 1;
INSERT INTO Submissions.ClientAcceptanceConsent (ClientAcceptanceConsentId, TenantId, ClientAcceptanceId, ConsentCode, ConsentVersion, IsAccepted, AttestedDateUtc, EvidenceDocumentId, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @TenantId, @ClientAcceptanceId, ConsentCode, ConsentVersion, IsAccepted, @AuthorizedDateUtc, EvidenceDocumentId, SYSUTCDATETIME(), @RecordedByUserId, 0 FROM OPENJSON(@ConsentsJson) WITH (ConsentCode NVARCHAR(100), ConsentVersion NVARCHAR(50), IsAccepted BIT, EvidenceDocumentId UNIQUEIDENTIFIER);
IF @DecisionCode = N'Accepted'
BEGIN
    UPDATE Submissions.Quote SET Status = CASE WHEN QuoteId = @QuoteId THEN N'Selected' ELSE N'Not Selected' END, IsSelected = CASE WHEN QuoteId = @QuoteId THEN 1 ELSE 0 END, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @RecordedByUserId WHERE SubmissionId = @SubmissionId AND IsDeleted = 0;
    UPDATE Submissions.Proposal SET Status = N'Accepted', GovernanceStatusCode = N'Accepted', ClientDecision = N'Accepted', DecisionNotes = @DecisionNotes, DecisionDateUtc = SYSUTCDATETIME(), DecidedByUserId = @RecordedByUserId, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @RecordedByUserId WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0;
END ELSE UPDATE Submissions.Proposal SET Status = CASE @DecisionCode WHEN N'Declined' THEN N'Declined' ELSE N'Pending Decision' END, GovernanceStatusCode = CASE @DecisionCode WHEN N'Declined' THEN N'Declined' ELSE N'Presented' END, ClientDecision = @DecisionCode, DecisionNotes = @DecisionNotes, DecisionDateUtc = SYSUTCDATETIME(), DecidedByUserId = @RecordedByUserId, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @RecordedByUserId WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0;
INSERT INTO Submissions.ClientAcceptanceAuditEvent (ClientAcceptanceAuditEventId, TenantId, ClientAcceptanceId, EventCode, EventDetail, DataJson, EventDateUtc, ActorUserId) VALUES (NEWID(), @TenantId, @ClientAcceptanceId, N'DecisionRecorded', @DecisionNotes, @AuditJson, SYSUTCDATETIME(), @RecordedByUserId);
INSERT INTO Submissions.ProposalLifecycleEvent (ProposalLifecycleEventId, TenantId, ProposalId, SubmissionId, EventCode, EventDetail, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted) VALUES (NEWID(), @TenantId, @ProposalId, @SubmissionId, @DecisionCode, @DecisionNotes, SYSUTCDATETIME(), SYSUTCDATETIME(), @RecordedByUserId, 0);
INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted) VALUES (NEWID(), @SubmissionId, @TenantId, N'ClientAcceptance', CONCAT(@DecisionCode, N'. ', COALESCE(@DecisionNotes, N'')), SYSUTCDATETIME(), @RecordedByUserId, N'ClientAcceptance', @ClientAcceptanceId, N'User', 0);
IF @DecisionCode = N'Accepted' AND OBJECT_ID(N'OPS.TaskItem', N'U') IS NOT NULL
    INSERT INTO OPS.TaskItem (TaskItemId, TenantId, TaskNumber, Title, Description, TaskTypeCode, StageCode, PriorityCode, StatusCode, RelatedEntityName, RelatedEntityId, AccountId, AssignedToUserId, DueDate, CreatedDateUtc, CreatedByUserId, IsDeleted) SELECT NEWID(), @TenantId, CONCAT(N'TASK-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', RIGHT(REPLACE(CONVERT(nvarchar(36), @ClientAcceptanceId), N'-', N''), 6)), N'Review accepted coverage and request bind', N'Client acceptance is complete. Verify carrier conditions and initiate bind.', N'BindFollowUp', N'Submission', N'High', N'Open', N'ClientAcceptance', @ClientAcceptanceId, @AccountId, s.AssignedToUserId, DATEADD(day, 1, CAST(SYSUTCDATETIME() AS date)), SYSUTCDATETIME(), @RecordedByUserId, 0 FROM Submissions.Submission s WHERE s.SubmissionId = @SubmissionId AND s.TenantId = @TenantId;
IF OBJECT_ID(N'Core.Notification', N'U') IS NOT NULL
    INSERT INTO Core.Notification (NotificationId, TenantId, RecipientUserId, ChannelCode, Subject, Body, EntityName, EntityId, StatusCode, IsRead, Priority, Category, DeliveryProvider, DeliveryStatus, PolicyStatus, SyncStatus, CreatedDateUtc, CreatedByUserId, IsDeleted) SELECT NEWID(), @TenantId, s.AssignedToUserId, N'InApp', CONCAT(N'Client decision: ', @DecisionCode), CONCAT(N'Proposal ', @ProposalVersionNumber, N' decision recorded for quote ', @QuoteNumber, N'.'), N'ClientAcceptance', @ClientAcceptanceId, N'Queued', 0, CASE WHEN @DecisionCode = N'Accepted' THEN N'High' ELSE N'Normal' END, N'Submissions', N'AMS', N'Queued', N'Compliant', N'Synced', SYSUTCDATETIME(), @RecordedByUserId, 0 FROM Submissions.Submission s WHERE s.SubmissionId = @SubmissionId AND s.TenantId = @TenantId AND s.AssignedToUserId IS NOT NULL;
SELECT @ClientAcceptanceId;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = cn.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var result = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
            {
                ClientAcceptanceId = id,
                request.TenantId,
                request.ProposalId,
                request.ProposalVersionNumber,
                request.QuoteId,
                request.QuoteFingerprint,
                request.DecisionCode,
                request.DecisionNotes,
                request.AuthorizationMethodCode,
                request.AuthorizationReference,
                request.AuthorizationDocumentId,
                request.ESignRequestId,
                request.AuthorizedByName,
                request.AuthorizedByTitle,
                request.AuthorityBasisCode,
                request.AuthorizedDateUtc,
                request.SignerEmail,
                request.SignerIpAddress,
                request.UserAgent,
                request.IdempotencyKey,
                request.RecordedByUserId,
                ExpectedElectionCount = readiness.QuoteLines.Count,
                ElectionsJson = JsonSerializer.Serialize(request.CoverageElections),
                ConsentsJson = JsonSerializer.Serialize(request.Consents),
                AuditJson = JsonSerializer.Serialize(new { request.ProposalVersionNumber, request.QuoteId, request.QuoteFingerprint, request.AuthorizationMethodCode, ElectionCount = request.CoverageElections.Count, ConsentCount = request.Consents.Count })
            }, tx, cancellationToken: cancellationToken));
            await cn.ExecuteAsync(new CommandDefinition(RecalculateSubmissionStatusSql, new { SubmissionId = readiness.SubmissionId, request.TenantId }, tx, cancellationToken: cancellationToken));
            tx.Commit();
            return result;
        }
        catch (SqlException exception) when (exception.Number is >= 52110 and <= 52123)
        {
            tx.Rollback();
            throw new InvalidOperationException(exception.Message, exception);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task WithdrawClientAcceptanceAsync(Guid clientAcceptanceId, WithdrawClientAcceptanceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER, @ProposalId UNIQUEIDENTIFIER, @CustomerAuthorizationId UNIQUEIDENTIFIER;
UPDATE Submissions.ClientAcceptance
SET StatusCode = N'Withdrawn', VersionNumber = VersionNumber + 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @WithdrawnByUserId,
    @SubmissionId = SubmissionId, @ProposalId = ProposalId, @CustomerAuthorizationId = CustomerAuthorizationId
WHERE ClientAcceptanceId = @ClientAcceptanceId AND TenantId = @TenantId AND IsDeleted = 0 AND VersionNumber = @ExpectedVersionNumber AND StatusCode = N'Accepted' AND PolicyBindTransactionId IS NULL;
IF @SubmissionId IS NULL THROW 52112, 'Acceptance changed, is already linked to bind, or cannot be withdrawn.', 1;
UPDATE Submissions.CustomerAuthorization SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @WithdrawnByUserId WHERE CustomerAuthorizationId = @CustomerAuthorizationId AND TenantId = @TenantId AND IsDeleted = 0;
UPDATE Submissions.Proposal SET Status = N'Presented', GovernanceStatusCode = N'Presented', ClientDecision = N'Deferred', DecisionNotes = @Reason, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @WithdrawnByUserId WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0;
INSERT INTO Submissions.ClientAcceptanceAuditEvent (ClientAcceptanceAuditEventId, TenantId, ClientAcceptanceId, EventCode, EventDetail, EventDateUtc, ActorUserId) VALUES (NEWID(), @TenantId, @ClientAcceptanceId, N'AcceptanceWithdrawn', @Reason, SYSUTCDATETIME(), @WithdrawnByUserId);
INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted) VALUES (NEWID(), @SubmissionId, @TenantId, N'ClientAcceptanceWithdrawn', @Reason, SYSUTCDATETIME(), @WithdrawnByUserId, N'ClientAcceptance', @ClientAcceptanceId, N'User', 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = cn.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var submissionId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql + "\nSELECT @SubmissionId;", new { ClientAcceptanceId = clientAcceptanceId, request.TenantId, request.ExpectedVersionNumber, request.Reason, request.WithdrawnByUserId }, tx, cancellationToken: cancellationToken));
            await cn.ExecuteAsync(new CommandDefinition(RecalculateSubmissionStatusSql, new { SubmissionId = submissionId, request.TenantId }, tx, cancellationToken: cancellationToken));
            tx.Commit();
        }
        catch (SqlException exception) when (exception.Number == 52112)
        {
            tx.Rollback();
            throw new InvalidOperationException(exception.Message, exception);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ── Appetite ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AppetiteMatchDto>> SearchAppetiteAsync(AppetiteSearchRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT c.CarrierId, c.CarrierName, ar.LineOfBusiness,
       ar.AppetiteScore AS MatchScore,
       CASE
           WHEN ar.AppetiteScore >= 80 THEN 'Strong'
           WHEN ar.AppetiteScore >= 60 THEN 'Moderate'
           ELSE 'Weak'
       END AS MatchLevel,
       NULL AS Notes
FROM   Core.AppetiteRule ar
JOIN   Core.Carrier      c ON c.CarrierId = ar.CarrierId AND c.IsDeleted = 0
WHERE  ar.TenantId      = @TenantId
  AND  ar.IsDeleted     = 0
  AND  ar.LineOfBusiness = @LineOfBusiness
  AND  (@State IS NULL OR @State = '' OR ar.AllowedStates LIKE '%' + @State + '%' OR ar.AllowedStates IS NULL)
ORDER BY ar.AppetiteScore DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await cn.QueryAsync<AppetiteMatchDto>(new CommandDefinition(sql, new
        {
            request.TenantId,
            request.LineOfBusiness,
            request.State,
        }, cancellationToken: cancellationToken))).AsList();
        return rows;
    }

    // ── Bind & Issue ──────────────────────────────────────────────────

    public async Task<PagedResult<PolicyRegisterDto>> SearchPoliciesAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT p.PolicyId,
           p.SubmissionId,
           p.QuoteId,
           p.TenantId,
           p.AccountId,
           COALESCE(a.AccountName, s.SubmissionNumber, p.PolicyNumber) AS AccountName,
           N'Commercial' AS AccountType,
           p.CarrierId,
           COALESCE(c.CarrierName, N'Bound Carrier') AS CarrierName,
           p.PolicyNumber,
            COALESCE(NULLIF(p.IssueStatus, N''), CASE WHEN p.Status = N'Bound' THEN N'PendingIssue' ELSE p.Status END) AS Status,
            COALESCE(NULLIF(p.IssueStatus, N''), CASE WHEN p.Status = N'Bound' THEN N'PendingIssue' ELSE p.Status END) AS IssueStatus,
            COALESCE(NULLIF(p.CoverageStatus, N''), CASE WHEN p.Status = N'Bound' THEN N'Bound' ELSE p.Status END) AS CoverageStatus,
           COALESCE(NULLIF(s.LineOfBusiness, N''), N'General Liability') AS LineOfBusiness,
           COALESCE(NULLIF(s.Priority, N''), N'Normal') AS Priority,
           p.AnnualPremium,
           p.AnnualPremium AS WrittenPremium,
           p.EffectiveDate,
           p.ExpirationDate,
           p.BoundDateUtc,
              p.IssuedDateUtc,
             COALESCE(NULLIF(p.PolicySourceCode, N''), N'ManualEntry') AS PolicySourceCode,
             COALESCE(pcs.SourceName, p.PolicySourceCode, N'Manual Entry') AS PolicySourceName,
             p.PolicySourceReason,
             p.PolicySourceNotes,
             p.PolicyBindTransactionId,
             COALESCE(pbt.BindStatusCode, N'Bound') AS BindStatusCode,
             COALESCE(pbs.StatusName, pbt.BindStatusCode, N'Bound') AS BindStatusName,
           s.AssignedToUserId,
           COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS AssignedToUserName,
           COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS ProducerName,
           COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS CsrName,
           N'HQ' AS Branch,
           (SELECT COUNT(1) FROM Compliance.PolicyDocument d WHERE d.TenantId = p.TenantId AND d.IsDeleted = 0 AND d.PolicyCode = p.PolicyNumber) AS DocumentCount,
           (SELECT COUNT(1) FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0) AS ActivityCount,
           (SELECT COUNT(1) FROM Policy.PolicyEndorsement e WHERE e.TenantId = p.TenantId AND e.PolicyNumber = p.PolicyNumber AND e.IsDeleted = 0) AS EndorsementCount,
            COALESCE(NULLIF(lastRenewal.Notes, N''), CASE
                WHEN DATEDIFF(day, SYSUTCDATETIME(), p.ExpirationDate) BETWEEN 0 AND 90 THEN N'Pre-Renewal'
                WHEN p.ExpirationDate < SYSUTCDATETIME() THEN N'Expired'
                ELSE N'Not Started'
            END) AS RenewalStage,
            COALESCE(lastAction.Notes, pbt.Notes, CONCAT(N'Policy bound ', CONVERT(nvarchar(10), p.BoundDateUtc, 101), CASE WHEN s.SubmissionNumber IS NULL THEN N' from account policy intake' ELSE CONCAT(N' from submission ', s.SubmissionNumber) END)) AS LastAction
    FROM   Submissions.BoundPolicy p
    LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
    LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
    LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
    LEFT JOIN Submissions.PolicyBindTransaction pbt ON pbt.PolicyBindTransactionId = p.PolicyBindTransactionId AND pbt.IsDeleted = 0
    LEFT JOIN Submissions.PolicyCreationSource pcs ON pcs.TenantId = p.TenantId AND pcs.SourceCode = p.PolicySourceCode AND pcs.IsDeleted = 0
    LEFT JOIN Submissions.PolicyBindStatus pbs ON pbs.TenantId = p.TenantId AND pbs.StatusCode = pbt.BindStatusCode AND pbs.IsDeleted = 0
    LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
    OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 ORDER BY al.CreatedDateUtc DESC) lastAction
    OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 AND al.ActionCode = N'RenewalStage' ORDER BY al.CreatedDateUtc DESC) lastRenewal
    WHERE  p.TenantId = @TenantId
      AND  p.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = N'' OR p.PolicyNumber LIKE N'%' + @SearchTerm + N'%' OR a.AccountName LIKE N'%' + @SearchTerm + N'%' OR c.CarrierName LIKE N'%' + @SearchTerm + N'%' OR s.LineOfBusiness LIKE N'%' + @SearchTerm + N'%')
),
Filtered AS
(
    SELECT *
    FROM Cte
    WHERE (@Status IS NULL OR @Status = N'' OR Status = @Status)
      AND (@LineOfBusiness IS NULL OR @LineOfBusiness = N'' OR LineOfBusiness = @LineOfBusiness)
)
SELECT * FROM Filtered
ORDER BY BoundDateUtc DESC, ExpirationDate ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

;WITH Cte AS
(
    SELECT COALESCE(NULLIF(p.IssueStatus, N''), CASE WHEN p.Status = N'Bound' THEN N'PendingIssue' ELSE p.Status END) AS Status,
           COALESCE(NULLIF(s.LineOfBusiness, N''), N'General Liability') AS LineOfBusiness
    FROM   Submissions.BoundPolicy p
    LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
    LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
    LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
    WHERE  p.TenantId = @TenantId
      AND  p.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = N'' OR p.PolicyNumber LIKE N'%' + @SearchTerm + N'%' OR a.AccountName LIKE N'%' + @SearchTerm + N'%' OR c.CarrierName LIKE N'%' + @SearchTerm + N'%' OR s.LineOfBusiness LIKE N'%' + @SearchTerm + N'%')
),
Filtered AS
(
    SELECT *
    FROM Cte
    WHERE (@Status IS NULL OR @Status = N'' OR Status = @Status)
      AND (@LineOfBusiness IS NULL OR @LineOfBusiness = N'' OR LineOfBusiness = @LineOfBusiness)
)
SELECT COUNT(1) FROM Filtered;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId       = tenantId,
            SearchTerm     = searchTerm,
            Status         = status,
            LineOfBusiness = lineOfBusiness,
            Offset         = (Math.Max(pageNumber, 1) - 1) * pageSize,
            PageSize       = pageSize,
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<PolicyRegisterDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PolicyRegisterDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<PolicyRegisterDto?> GetPolicyByIdAsync(Guid policyId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 1 p.PolicyId,
       p.SubmissionId,
       p.QuoteId,
       p.TenantId,
       p.AccountId,
       COALESCE(a.AccountName, s.SubmissionNumber, p.PolicyNumber) AS AccountName,
       N'Commercial' AS AccountType,
       p.CarrierId,
       COALESCE(c.CarrierName, N'Bound Carrier') AS CarrierName,
       p.PolicyNumber,
       COALESCE(NULLIF(p.IssueStatus, N''), CASE WHEN p.Status = N'Bound' THEN N'PendingIssue' ELSE p.Status END) AS Status,
       COALESCE(NULLIF(p.IssueStatus, N''), CASE WHEN p.Status = N'Bound' THEN N'PendingIssue' ELSE p.Status END) AS IssueStatus,
       COALESCE(NULLIF(p.CoverageStatus, N''), CASE WHEN p.Status = N'Bound' THEN N'Bound' ELSE p.Status END) AS CoverageStatus,
       COALESCE(NULLIF(s.LineOfBusiness, N''), N'General Liability') AS LineOfBusiness,
       COALESCE(NULLIF(s.Priority, N''), N'Normal') AS Priority,
       p.AnnualPremium,
       p.AnnualPremium AS WrittenPremium,
       p.EffectiveDate,
       p.ExpirationDate,
       p.BoundDateUtc,
        p.IssuedDateUtc,
        COALESCE(NULLIF(p.PolicySourceCode, N''), N'ManualEntry') AS PolicySourceCode,
        COALESCE(pcs.SourceName, p.PolicySourceCode, N'Manual Entry') AS PolicySourceName,
        p.PolicySourceReason,
        p.PolicySourceNotes,
        p.PolicyBindTransactionId,
        COALESCE(pbt.BindStatusCode, N'Bound') AS BindStatusCode,
        COALESCE(pbs.StatusName, pbt.BindStatusCode, N'Bound') AS BindStatusName,
       s.AssignedToUserId,
       COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS AssignedToUserName,
       COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS ProducerName,
       COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS CsrName,
       N'HQ' AS Branch,
       (SELECT COUNT(1) FROM Compliance.PolicyDocument d WHERE d.TenantId = p.TenantId AND d.IsDeleted = 0 AND d.PolicyCode = p.PolicyNumber) AS DocumentCount,
       (SELECT COUNT(1) FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0) AS ActivityCount,
       (SELECT COUNT(1) FROM Policy.PolicyEndorsement e WHERE e.TenantId = p.TenantId AND e.PolicyNumber = p.PolicyNumber AND e.IsDeleted = 0) AS EndorsementCount,
       COALESCE(NULLIF(lastRenewal.Notes, N''), CASE
           WHEN DATEDIFF(day, SYSUTCDATETIME(), p.ExpirationDate) BETWEEN 0 AND 90 THEN N'Pre-Renewal'
           WHEN p.ExpirationDate < SYSUTCDATETIME() THEN N'Expired'
           ELSE N'Not Started'
       END) AS RenewalStage,
       COALESCE(lastAction.Notes, pbt.Notes, CONCAT(N'Policy bound ', CONVERT(nvarchar(10), p.BoundDateUtc, 101), CASE WHEN s.SubmissionNumber IS NULL THEN N' from account policy intake' ELSE CONCAT(N' from submission ', s.SubmissionNumber) END)) AS LastAction
FROM Submissions.BoundPolicy p
LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
LEFT JOIN Submissions.PolicyBindTransaction pbt ON pbt.PolicyBindTransactionId = p.PolicyBindTransactionId AND pbt.IsDeleted = 0
LEFT JOIN Submissions.PolicyCreationSource pcs ON pcs.TenantId = p.TenantId AND pcs.SourceCode = p.PolicySourceCode AND pcs.IsDeleted = 0
LEFT JOIN Submissions.PolicyBindStatus pbs ON pbs.TenantId = p.TenantId AND pbs.StatusCode = pbt.BindStatusCode AND pbs.IsDeleted = 0
LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 ORDER BY al.CreatedDateUtc DESC) lastAction
OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 AND al.ActionCode = N'RenewalStage' ORDER BY al.CreatedDateUtc DESC) lastRenewal
WHERE p.PolicyId = @PolicyId AND p.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PolicyRegisterDto>(new CommandDefinition(sql, new { PolicyId = policyId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreatePolicyRegisterAsync(UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsurePolicyCreationSourceSchemaAsync(cn, request.TenantId, cancellationToken);
        await EnsurePolicyBindTransactionSchemaAsync(cn, request.TenantId, cancellationToken);

        var source = await GetPolicyCreationSourceSettingsAsync(cn, request.TenantId, request.PolicySourceCode, cancellationToken);
        var sourceReason = Normalize(request.PolicySourceReason);
        var sourceNotes = Normalize(request.Notes);

        if (source.RequiresQuote)
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires quote-bound submission workflow. Use Create Policy from a submission quote.");
        }

        if (source.RequiresSubmission && (!request.SubmissionId.HasValue || request.SubmissionId.Value == Guid.Empty))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a parent submission.");
        }

        if (source.RequiresAccount && request.AccountId == Guid.Empty)
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires an account.");
        }

        if (source.RequiresReason && string.IsNullOrWhiteSpace(sourceReason))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a reason.");
        }

        if (source.RequiresPolicyNumber && string.IsNullOrWhiteSpace(request.PolicyNumber))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a policy number.");
        }

        if (request.ExpirationDate <= request.EffectiveDate)
        {
            throw new InvalidOperationException("Policy expiration date must be after the effective date.");
        }

        if (request.AnnualPremium <= 0)
        {
            throw new InvalidOperationException("Policy annual premium must be greater than zero.");
        }

        const string carrierSql = @"
DECLARE @CarrierId UNIQUEIDENTIFIER = (SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND CarrierName = @CarrierName AND IsDeleted = 0 ORDER BY CreatedDateUtc);
IF @CarrierId IS NULL
BEGIN
    SET @CarrierId = NEWID();
    INSERT INTO Core.Carrier (CarrierId, TenantId, CarrierCode, CarrierName, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@CarrierId, @TenantId, LEFT(REPLACE(UPPER(@CarrierName), N' ', N''), 50), @CarrierName, 1, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END;
SELECT @CarrierId;";
        var carrierId = request.CarrierId ?? await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(carrierSql, new
        {
            request.TenantId,
            request.CarrierName,
            request.ModifiedByUserId,
        }, cancellationToken: cancellationToken));

        var id = await BindPolicyAsync(new BindPolicyRequest(
            SubmissionId: request.SubmissionId,
            QuoteId: request.QuoteId,
            TenantId: request.TenantId,
            AccountId: request.AccountId,
            CarrierId: carrierId,
            AnnualPremium: request.AnnualPremium,
            EffectiveDate: request.EffectiveDate,
            ExpirationDate: request.ExpirationDate,
            PolicyNumber: request.PolicyNumber,
            PolicySourceCode: source.SourceCode,
            PolicySourceReason: sourceReason ?? "Policy created from policy register.",
            PolicySourceNotes: sourceNotes,
            RequestedByUserId: request.ModifiedByUserId,
            ApprovedByUserId: request.ModifiedByUserId,
            BoundByUserId: request.ModifiedByUserId,
            BindStatusCode: "Bound",
            ConfirmationSourceCode: "Manual",
            CarrierReferenceNumber: request.PolicyNumber,
            FinalPremium: request.AnnualPremium,
            ConfirmationNotes: sourceNotes ?? sourceReason ?? "Carrier-issued policy record entered in AgencyBinder.",
            ConfirmedManually: true,
            ConfirmationCertified: true), cancellationToken);

        return id;
    }

    public async Task UpdatePolicyRegisterAsync(Guid policyId, UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @CarrierId UNIQUEIDENTIFIER = (SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND CarrierName = @CarrierName AND IsDeleted = 0 ORDER BY CreatedDateUtc);
IF @CarrierId IS NULL
BEGIN
    SET @CarrierId = NEWID();
    INSERT INTO Core.Carrier (CarrierId, TenantId, CarrierCode, CarrierName, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@CarrierId, @TenantId, LEFT(REPLACE(UPPER(@CarrierName), N' ', N''), 50), @CarrierName, 1, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END;

UPDATE Submissions.BoundPolicy
SET PolicyNumber = @PolicyNumber,
    CarrierId = @CarrierId,
    Status = CASE WHEN @Status = N'Active' THEN N'Bound' ELSE @Status END,
    IssueStatus = CASE WHEN @Status IN (N'Issued', N'Active') THEN N'Issued' WHEN @Status = N'PendingIssue' THEN N'PendingIssue' ELSE @Status END,
    CoverageStatus = CASE WHEN @Status IN (N'Issued', N'Active') THEN N'Active' WHEN @Status = N'PendingIssue' THEN N'Bound' ELSE @Status END,
    IssuedDateUtc = CASE WHEN @Status IN (N'Issued', N'Active') THEN COALESCE(IssuedDateUtc, SYSUTCDATETIME()) ELSE IssuedDateUtc END,
    AnnualPremium = @AnnualPremium,
    EffectiveDate = @EffectiveDate,
    ExpirationDate = @ExpirationDate,
    @SubmissionId = SubmissionId
WHERE PolicyId = @PolicyId AND TenantId = @TenantId AND IsDeleted = 0;

UPDATE Submissions.Submission
SET AccountId = @AccountId,
    LineOfBusiness = @LineOfBusiness,
    Status = CASE WHEN @Status = N'Active' THEN N'Bound' ELSE @Status END,
    EffectiveDate = @EffectiveDate,
    ExpirationDate = @ExpirationDate,
    TargetPremium = NULLIF(@AnnualPremium, 0),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'PolicyUpdated', CONCAT(N'Policy edited from register. ', COALESCE(@Notes, N'')), SYSUTCDATETIME(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PolicyId = policyId,
            request.TenantId,
            request.AccountId,
            request.PolicyNumber,
            request.CarrierName,
            request.LineOfBusiness,
            request.Status,
            request.EffectiveDate,
            request.ExpirationDate,
            request.AnnualPremium,
            request.Notes,
            request.ModifiedByUserId,
        }, cancellationToken: cancellationToken));
    }

    public async Task<SubmissionActionResult> ExecutePolicyRegisterActionAsync(Guid policyId, PolicyRegisterActionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @QuoteId UNIQUEIDENTIFIER;
DECLARE @AccountId UNIQUEIDENTIFIER;
DECLARE @CarrierId UNIQUEIDENTIFIER;
DECLARE @PolicyNumber NVARCHAR(50);
DECLARE @AccountName NVARCHAR(200);
DECLARE @LineOfBusiness NVARCHAR(100);
DECLARE @CarrierName NVARCHAR(200);
DECLARE @AnnualPremium DECIMAL(18,2);
DECLARE @EffectiveDate DATETIME2;
DECLARE @ExpirationDate DATETIME2;

SELECT @SubmissionId = p.SubmissionId,
       @QuoteId = p.QuoteId,
       @AccountId = p.AccountId,
       @CarrierId = p.CarrierId,
       @PolicyNumber = p.PolicyNumber,
       @AccountName = COALESCE(a.AccountName, p.PolicyNumber),
       @LineOfBusiness = COALESCE(NULLIF(s.LineOfBusiness, N''), N'General Liability'),
       @CarrierName = COALESCE(c.CarrierName, N'Carrier'),
       @AnnualPremium = p.AnnualPremium,
       @EffectiveDate = p.EffectiveDate,
       @ExpirationDate = p.ExpirationDate
FROM Submissions.BoundPolicy p
LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
WHERE p.PolicyId = @PolicyId AND p.TenantId = @TenantId AND p.IsDeleted = 0;

IF @PolicyNumber IS NULL THROW 51000, 'Policy was not found.', 1;

DECLARE @ActionCode NVARCHAR(80) = REPLACE(@Action, N' ', N'');
DECLARE @Message NVARCHAR(500) = CONCAT(@Action, N' completed for ', @PolicyNumber, N'.');

IF @Action = N'Cancel Policy'
BEGIN
    UPDATE Submissions.BoundPolicy SET Status = N'Cancelled' WHERE PolicyId = @PolicyId AND TenantId = @TenantId AND IsDeleted = 0;
    UPDATE Submissions.Submission SET Status = N'Cancelled', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;
    INSERT INTO Policy.PolicyCancellation (CancellationId, TenantId, PolicyId, AccountId, CancellationNumber, PolicyNumber, AccountName, LineOfBusiness, Carrier, CancellationReason, CancellationType, RequestType, RequestDateUtc, EffectiveDate, CancellationDate, ReturnPremium, PremiumDue, Status, Priority, RequestedByName, AssignedToName, Notes, WorkflowStage, DueDate, IsUrgent, IsArchived, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @PolicyId, @AccountId, CONCAT(N'CAN-', FORMAT(SYSUTCDATETIME(), N'yyyy'), N'-', FORMAT(ISNULL((SELECT COUNT(1) + 1 FROM Policy.PolicyCancellation WHERE TenantId = @TenantId), 1), N'0000')), @PolicyNumber, @AccountName, @LineOfBusiness, @CarrierName, COALESCE(NULLIF(@Notes, N''), N'Policy cancelled from register'), N'Pro-Rata', N'Cancellation', SYSUTCDATETIME(), COALESCE(@ActionDate, SYSUTCDATETIME()), COALESCE(@ActionDate, SYSUTCDATETIME()), 0, 0, N'Pending', N'Normal', N'Current User', N'Current User', @Notes, N'Cancellation Intake', DATEADD(day, 7, SYSUTCDATETIME()), 0, 0, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END
ELSE IF @Action = N'Renew'
BEGIN
    DECLARE @RenewalPolicyId UNIQUEIDENTIFIER = NEWID();
    DECLARE @RenewalEffective DATETIME2 = COALESCE(@ActionDate, @ExpirationDate);
    DECLARE @RenewalPremium DECIMAL(18,2) = COALESCE(NULLIF(@Premium, 0), @AnnualPremium);

    IF @SubmissionId IS NULL
    BEGIN
        DECLARE @RenewalSourceCode NVARCHAR(50) = N'RenewalImport';
        DECLARE @RenewalPolicyNumber NVARCHAR(80) = CONCAT(@PolicyNumber, N'-REN-', FORMAT(GETUTCDATE(), 'yyMMdd'));
        DECLARE @RenewalReason NVARCHAR(500) = LEFT(COALESCE(NULLIF(@Notes, N''), CONCAT(N'Direct account-origin renewal created from ', @PolicyNumber, N'.')), 500);
        DECLARE @RenewalBindTransactionId UNIQUEIDENTIFIER = NEWID();

        IF NOT EXISTS
        (
            SELECT 1
            FROM Submissions.PolicyCreationSource
            WHERE TenantId = @TenantId
              AND SourceCode = @RenewalSourceCode
              AND RequiresQuote = 0
              AND RequiresSubmission = 0
              AND AllowsDirectPolicyEntry = 1
              AND IsActive = 1
              AND IsDeleted = 0
        )
            THROW 51000, 'Direct policy renewal source configuration is missing or inactive.', 1;

        INSERT INTO Submissions.PolicyBindTransaction
            (PolicyBindTransactionId, TenantId, SubmissionId, QuoteId, PolicyId, AccountId, CarrierId,
             PolicySourceCode, BindStatusCode, PolicyNumber, AnnualPremium, EffectiveDate, ExpirationDate,
             BindReason, Notes, RequestedByUserId, RequestedDateUtc, ApprovedByUserId, ApprovedDateUtc,
             BoundByUserId, BoundDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
            (@RenewalBindTransactionId, @TenantId, NULL, NULL, @RenewalPolicyId, @AccountId, @CarrierId,
             @RenewalSourceCode, N'Bound', @RenewalPolicyNumber, @RenewalPremium, @RenewalEffective, DATEADD(year, 1, @RenewalEffective),
             @RenewalReason, @Notes, @ModifiedByUserId, SYSUTCDATETIME(), @ModifiedByUserId, SYSUTCDATETIME(),
             @ModifiedByUserId, SYSUTCDATETIME(), SYSUTCDATETIME(), @ModifiedByUserId, 0);

        INSERT INTO Submissions.BoundPolicy
            (PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId, PolicyNumber, Status, AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc, PolicySourceCode, PolicySourceReason, PolicySourceNotes, PolicyBindTransactionId, IsDeleted)
        VALUES
            (@RenewalPolicyId, NULL, NULL, @TenantId, @AccountId, @CarrierId, @RenewalPolicyNumber, N'Bound', @RenewalPremium, @RenewalEffective, DATEADD(year, 1, @RenewalEffective), SYSUTCDATETIME(), @RenewalSourceCode, @RenewalReason, @Notes, @RenewalBindTransactionId, 0);
    END
    ELSE
    BEGIN
        DECLARE @RenewalRequestMarketId UNIQUEIDENTIFIER = NULL;
        DECLARE @RenewalQuoteRequestId UNIQUEIDENTIFIER = NEWID();

        SELECT TOP 1 @RenewalRequestMarketId = SubmissionMarketId
        FROM Submissions.SubmissionMarket WITH (UPDLOCK, HOLDLOCK)
        WHERE SubmissionId = @SubmissionId
          AND CarrierId = @CarrierId
          AND IsDeleted = 0
        ORDER BY AddedDateUtc DESC;

        IF @RenewalRequestMarketId IS NULL
        BEGIN
            SET @RenewalRequestMarketId = NEWID();
            INSERT INTO Submissions.SubmissionMarket
                (SubmissionMarketId, SubmissionId, CarrierId, Status, AppetiteScore, IsRecommended, AddedDateUtc, IsDeleted, TenantId, Notes)
            VALUES
                (@RenewalRequestMarketId, @SubmissionId, @CarrierId, N'Awaiting Response', 65, 1, SYSUTCDATETIME(), 0, @TenantId, N'Renewal quote request created from policy action.');
        END;

        INSERT INTO Submissions.QuoteRequest
            (QuoteRequestId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, QuoteRequestActionCode, QuoteRequestReasonCode, QuoteRequestMethodCode, QuoteRequestScopeCode,
             RequestedPremium, CoverageNotes, DeliveryMethodCode, AssignedUnderwriterName, AssignedUnderwriterEmail, AssignedUnderwriterPhone, DueDateUtc, CorrelationId,
             RequestVersion, StatusCode, RequestedDateUtc, RequestedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
            (@RenewalQuoteRequestId, @TenantId, @SubmissionId, @RenewalRequestMarketId, @CarrierId, N'InitialRequest', N'RenewalUpdate', N'ManualUnderwriter', N'Package',
             @RenewalPremium, COALESCE(NULLIF(@Notes, N''), CONCAT(N'Renewal quote requested from policy ', @PolicyNumber, N'.')),
             N'InternalQueue',
             (SELECT TOP 1 UnderwriterName FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @RenewalRequestMarketId AND IsDeleted = 0),
             (SELECT TOP 1 UnderwriterEmail FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @RenewalRequestMarketId AND IsDeleted = 0),
             (SELECT TOP 1 UnderwriterPhone FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @RenewalRequestMarketId AND IsDeleted = 0),
             (SELECT TOP 1 DueDateUtc FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @RenewalRequestMarketId AND IsDeleted = 0),
             CONCAT(N'QR-', CONVERT(NVARCHAR(36), @RenewalQuoteRequestId)),
             COALESCE((SELECT MAX(RequestVersion) FROM Submissions.QuoteRequest WHERE SubmissionMarketId = @RenewalRequestMarketId AND IsDeleted = 0), 0) + 1,
             N'PendingDispatch', SYSUTCDATETIME(), @ModifiedByUserId, SYSUTCDATETIME(), @ModifiedByUserId, 0);

        INSERT INTO Submissions.QuoteRequestHistory
            (QuoteRequestHistoryId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, QuoteRequestActionCode, QuoteRequestReasonCode, QuoteRequestMethodCode, QuoteRequestScopeCode,
             RequestedPremium, CoverageNotes, RequestVersion, StatusCode, RequestedDateUtc, RequestedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
            (NEWID(), @TenantId, @SubmissionId, @RenewalRequestMarketId, @CarrierId, N'InitialRequest', N'RenewalUpdate', N'ManualUnderwriter', N'Package',
             @RenewalPremium, COALESCE(NULLIF(@Notes, N''), CONCAT(N'Renewal quote requested from policy ', @PolicyNumber, N'.')),
             (SELECT RequestVersion FROM Submissions.QuoteRequest WHERE QuoteRequestId = @RenewalQuoteRequestId), N'PendingDispatch', SYSUTCDATETIME(), @ModifiedByUserId, SYSUTCDATETIME(), @ModifiedByUserId, 0);

        INSERT INTO Submissions.BoundPolicy (PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId, PolicyNumber, Status, AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc, IsDeleted)
        VALUES (@RenewalPolicyId, @SubmissionId, NULL, @TenantId, @AccountId, @CarrierId, CONCAT(@PolicyNumber, N'-REN-', FORMAT(GETUTCDATE(), 'yyMMdd')), N'Pending', @RenewalPremium, @RenewalEffective, DATEADD(year, 1, @RenewalEffective), NULL, 0);
    END

    SET @Message = CONCAT(N'Renewal policy created for ', @PolicyNumber, N'.');
END
ELSE IF @Action = N'Endorse'
BEGIN
    INSERT INTO Policy.PolicyEndorsement (EndorsementId, TenantId, PolicyId, AccountId, EndorsementNumber, PolicyNumber, AccountName, LineOfBusiness, Carrier, EndorsementType, Description, EffectiveDate, RequestedDateUtc, PremiumDelta, Status, Priority, RequestedByName, AssignedToName, WorkflowStage, IsUrgent, IsArchived, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @PolicyId, @AccountId, CONCAT(N'END-', FORMAT(SYSUTCDATETIME(), N'yyyy'), N'-', FORMAT(ISNULL((SELECT COUNT(1) + 1 FROM Policy.PolicyEndorsement WHERE TenantId = @TenantId), 1), N'0000')), @PolicyNumber, @AccountName, @LineOfBusiness, @CarrierName, N'Change Endorsement', COALESCE(NULLIF(@Notes, N''), N'Policy endorsement requested from register'), COALESCE(@ActionDate, SYSUTCDATETIME()), COALESCE(@ActionDate, SYSUTCDATETIME()), COALESCE(@Premium, 0), N'Pending', N'Normal', N'Current User', N'Current User', N'Intake', 0, 0, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END
ELSE IF @Action = N'Add Document'
BEGIN
    INSERT INTO Compliance.PolicyDocument (PolicyDocumentId, TenantId, PolicyCode, PolicyTitle, PolicyTypeCode, Version, EffectiveDateUtc, IsActive, StatusCode, Description, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @PolicyNumber, COALESCE(NULLIF(@DocumentTitle, N''), CONCAT(N'Policy Document - ', @PolicyNumber)), N'Policy', N'1.0', COALESCE(@ActionDate, SYSUTCDATETIME()), 1, N'Published', @Notes, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @SubmissionId, @TenantId, @ActionCode, COALESCE(NULLIF(@Notes, N''), @Message), SYSUTCDATETIME(), 0
WHERE @SubmissionId IS NOT NULL;

SELECT @Message;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsurePolicyCreationSourceSchemaAsync(cn, request.TenantId, cancellationToken);
        await EnsurePolicyBindTransactionSchemaAsync(cn, request.TenantId, cancellationToken);
        var message = await cn.QuerySingleAsync<string>(new CommandDefinition(sql, new
        {
            PolicyId = policyId,
            request.TenantId,
            request.Action,
            ActionDate = request.EffectiveDate,
            request.Premium,
            request.DocumentTitle,
            request.Notes,
            request.ModifiedByUserId,
        }, cancellationToken: cancellationToken));
        return new SubmissionActionResult(policyId, message);
    }

    public async Task<PolicyBindDto?> GetPolicyBySubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId,
       PolicyNumber,
       COALESCE(NULLIF(IssueStatus, N''), CASE WHEN Status = N'Bound' THEN N'PendingIssue' ELSE Status END) AS Status,
       COALESCE(NULLIF(IssueStatus, N''), CASE WHEN Status = N'Bound' THEN N'PendingIssue' ELSE Status END) AS IssueStatus,
       COALESCE(NULLIF(CoverageStatus, N''), CASE WHEN Status = N'Bound' THEN N'Bound' ELSE Status END) AS CoverageStatus,
       AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc, IssuedDateUtc
FROM   Submissions.BoundPolicy
WHERE  SubmissionId = @SubmissionId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PolicyBindDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> BindPolicyAsync(BindPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var submissionId = request.SubmissionId is { } sid && sid != Guid.Empty ? sid : (Guid?)null;
        var quoteId = request.QuoteId is { } qid && qid != Guid.Empty ? qid : (Guid?)null;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        await EnsurePolicyCreationSourceSchemaAsync(cn, request.TenantId, cancellationToken);
        await EnsurePolicyBindTransactionSchemaAsync(cn, request.TenantId, cancellationToken);

        var source = await GetPolicyCreationSourceSettingsAsync(cn, request.TenantId, request.PolicySourceCode, cancellationToken);
        if (source.RequiresAccount && request.AccountId == Guid.Empty)
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires an account.");
        }

        if (source.RequiresSubmission && !submissionId.HasValue)
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a parent submission.");
        }

        if (source.RequiresQuote && !quoteId.HasValue)
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a quote.");
        }

        if (source.RequiresReason && string.IsNullOrWhiteSpace(request.PolicySourceReason))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a reason.");
        }

        if (source.RequiresPolicyNumber && string.IsNullOrWhiteSpace(request.PolicyNumber))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a policy number.");
        }

        if (request.ExpirationDate <= request.EffectiveDate)
        {
            throw new InvalidOperationException("Policy expiration date must be after the effective date.");
        }

        if (request.AnnualPremium <= 0)
        {
            throw new InvalidOperationException("Policy annual premium must be greater than zero.");
        }

        if (request.DownPaymentAmount < 0 || request.DownPaymentAmount > (request.FinalPremium ?? request.AnnualPremium))
        {
            throw new InvalidOperationException("Down payment must be between zero and the final premium.");
        }

        string? underwriterName = null;
        string? underwriterCompany = null;
        if (request.UnderwriterContactId.HasValue)
        {
            var underwriter = await cn.QuerySingleOrDefaultAsync<(string ContactName, string CarrierName)>(new CommandDefinition(@"
SELECT cc.ContactName, c.CarrierName
FROM Agency.CarrierContact cc
INNER JOIN Core.Carrier c
    ON c.CarrierId = cc.CarrierId
   AND c.TenantId = cc.TenantId
   AND c.IsActive = 1
   AND c.IsDeleted = 0
WHERE cc.CarrierContactId = @UnderwriterContactId
  AND cc.TenantId = @TenantId
  AND cc.CarrierId = @CarrierId
  AND cc.IsActive = 1
  AND cc.IsDeleted = 0;", new { request.UnderwriterContactId, request.TenantId, request.CarrierId }, cancellationToken: cancellationToken));

            if (string.IsNullOrWhiteSpace(underwriter.ContactName))
            {
                throw new InvalidOperationException("The selected underwriter contact is inactive or does not belong to this tenant and carrier.");
            }

            underwriterName = underwriter.ContactName;
            underwriterCompany = underwriter.CarrierName;
        }

        BindCommissionEstimateDto? commissionEstimate = null;
        if (submissionId.HasValue && quoteId.HasValue)
        {
            commissionEstimate = await ResolveBindCommissionEstimateAsync(cn, submissionId.Value, quoteId.Value, request.TenantId, request.FinalPremium ?? request.AnnualPremium, request.EffectiveDate, cancellationToken);
            if (request.CommissionPlanApplicabilityId.HasValue || request.CommissionPlanVersionId.HasValue || request.CommissionPayeeId.HasValue || request.CommissionSplitRuleId.HasValue)
            {
                if (commissionEstimate is null
                    || request.CommissionPlanApplicabilityId != commissionEstimate.CommissionPlanApplicabilityId
                    || request.CommissionPlanVersionId != commissionEstimate.CommissionPlanVersionId
                    || request.CommissionPayeeId != commissionEstimate.CommissionPayeeId
                    || request.CommissionSplitRuleId != commissionEstimate.CommissionSplitRuleId)
                {
                    throw new InvalidOperationException("Commission configuration changed after the estimate was loaded. Review the refreshed estimate before submitting the bind request.");
                }
            }
        }

        if (source.RequiresQuote)
        {
            var quoteIsBindable = await cn.ExecuteScalarAsync<bool>(new CommandDefinition(@"
SELECT CAST(CASE WHEN EXISTS
(
    SELECT 1
    FROM Submissions.Quote q
    INNER JOIN Submissions.Submission s ON s.SubmissionId = q.SubmissionId AND s.TenantId = @TenantId AND s.IsDeleted = 0
    WHERE q.SubmissionId = @SubmissionId
      AND q.QuoteId = @QuoteId
      AND q.IsDeleted = 0
      AND q.IsBindable = 1
       AND q.IsSelected = 1
      AND q.ExpiresDateUtc > SYSUTCDATETIME()
       AND q.Status = N'Selected'
) THEN 1 ELSE 0 END AS bit);", new { request.TenantId, SubmissionId = submissionId, QuoteId = quoteId }, cancellationToken: cancellationToken));

            if (!quoteIsBindable)
            {
                throw new InvalidOperationException("A bind request requires the submission's selected, non-expired, bindable quote.");
            }

            if (!request.CustomerAuthorizationId.HasValue && string.IsNullOrWhiteSpace(request.CustomerAuthorizationMethodCode))
            {
                throw new InvalidOperationException("Binding requires documented customer authorization. Select an authorization method or link an existing authorization record.");
            }

            if (request.ProposalId.HasValue)
            {
                var compliantAcceptance = await cn.ExecuteScalarAsync<bool>(new CommandDefinition(@"
SELECT CAST(CASE WHEN EXISTS
(
    SELECT 1 FROM Submissions.ClientAcceptance ca
    WHERE ca.TenantId = @TenantId AND ca.SubmissionId = @SubmissionId AND ca.ProposalId = @ProposalId
      AND ca.QuoteId = @QuoteId AND ca.CustomerAuthorizationId = @CustomerAuthorizationId
      AND ca.StatusCode = N'Accepted' AND ca.IsDeleted = 0
) THEN 1 ELSE 0 END AS bit);", new { request.TenantId, SubmissionId = submissionId, request.ProposalId, QuoteId = quoteId, request.CustomerAuthorizationId }, cancellationToken: cancellationToken));
                if (!compliantAcceptance) throw new InvalidOperationException("Proposal-based binding requires the active compliant client acceptance and matching authorization record.");
            }
        }

        var bindStatus = await cn.QuerySingleOrDefaultAsync<PolicyBindStatusSettings>(new CommandDefinition(@"
SELECT TOP 1 StatusCode, StatusName, IsTerminal, CreatesPolicy
FROM Submissions.PolicyBindStatus
WHERE TenantId = @TenantId
  AND StatusCode = @StatusCode
  AND IsDeleted = 0
  AND IsActive = 1;", new { request.TenantId, StatusCode = request.BindStatusCode }, cancellationToken: cancellationToken));

        if (bindStatus is null)
        {
            throw new InvalidOperationException($"Policy bind status '{request.BindStatusCode}' is not configured for this tenant.");
        }

        if (bindStatus.CreatesPolicy)
        {
            if (string.IsNullOrWhiteSpace(request.ConfirmationSourceCode))
            {
                throw new InvalidOperationException("Carrier confirmation source is required before creating a policy from a bind request.");
            }

            if (!request.ConfirmationCertified)
            {
                throw new InvalidOperationException("Carrier confirmation must be certified before the bind request can create a policy.");
            }

            if (string.IsNullOrWhiteSpace(request.CarrierReferenceNumber) && string.IsNullOrWhiteSpace(request.BinderNumber) && string.IsNullOrWhiteSpace(request.PolicyNumber))
            {
                throw new InvalidOperationException("Carrier confirmation requires a carrier reference number, binder number, or policy number.");
            }

            var sourceExists = await cn.ExecuteScalarAsync<bool>(new CommandDefinition(@"
SELECT CAST(CASE WHEN EXISTS
(
    SELECT 1
    FROM Submissions.SubmissionReferenceOption
    WHERE TenantId = @TenantId
      AND OptionGroup = N'BindConfirmationSource'
      AND OptionCode = @ConfirmationSourceCode
      AND IsActive = 1
      AND IsDeleted = 0
) THEN 1 ELSE 0 END AS bit);", new { request.TenantId, request.ConfirmationSourceCode }, cancellationToken: cancellationToken));

            if (!sourceExists)
            {
                throw new InvalidOperationException($"Bind confirmation source '{request.ConfirmationSourceCode}' is not configured for this tenant.");
            }
        }

        const string bindRequestSql = @"
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

DECLARE @PolicyBindTransactionId UNIQUEIDENTIFIER = NEWID();
DECLARE @RequestedDateUtc DATETIME2 = SYSUTCDATETIME();
DECLARE @CustomerAuthorizationId UNIQUEIDENTIFIER = @CustomerAuthorizationIdIn;

IF @SubmissionId IS NOT NULL
   AND @QuoteId IS NOT NULL
   AND EXISTS
   (
       SELECT 1
       FROM Submissions.PolicyBindTransaction pbt WITH (UPDLOCK, HOLDLOCK)
       LEFT JOIN Submissions.PolicyBindStatus pbs
         ON pbs.TenantId = pbt.TenantId
        AND pbs.StatusCode = pbt.BindStatusCode
        AND pbs.IsActive = 1
        AND pbs.IsDeleted = 0
       WHERE pbt.TenantId = @TenantId
         AND pbt.SubmissionId = @SubmissionId
         AND pbt.QuoteId = @QuoteId
         AND pbt.IsDeleted = 0
         AND (pbs.PolicyBindStatusId IS NULL OR pbs.IsTerminal = 0)
   )
    THROW 52072, 'An active bind request already exists for the selected submission quote. Continue the existing bind workflow.', 1;

IF @QuoteId IS NOT NULL AND @QuoteId <> '00000000-0000-0000-0000-000000000000'
BEGIN
    IF @CustomerAuthorizationId IS NULL
    BEGIN
        SET @CustomerAuthorizationId = NEWID();
        INSERT INTO Submissions.CustomerAuthorization
            (CustomerAuthorizationId, TenantId, SubmissionId, QuoteId, ProposalId, AuthorizationMethodCode, AuthorizationReference, AuthorizationNotes, AuthorizedByName, AuthorizedDateUtc, DocumentId, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
            (@CustomerAuthorizationId, @TenantId, @SubmissionId, @QuoteId, @ProposalId, @CustomerAuthorizationMethodCode, @CustomerAuthorizationReference, @CustomerAuthorizationNotes, @CustomerAuthorizedByName, COALESCE(@CustomerAuthorizedDateUtc, @RequestedDateUtc), @CustomerAuthorizationDocumentId, @RequestedDateUtc, @RequestedByUserId, 0);
    END
    ELSE IF NOT EXISTS (SELECT 1 FROM Submissions.CustomerAuthorization WHERE CustomerAuthorizationId = @CustomerAuthorizationId AND TenantId = @TenantId AND SubmissionId = @SubmissionId AND QuoteId = @QuoteId AND IsDeleted = 0)
        THROW 52071, 'Customer authorization does not match the selected submission quote.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM Submissions.ClientAcceptance WITH (UPDLOCK, HOLDLOCK)
        WHERE TenantId = @TenantId
          AND SubmissionId = @SubmissionId
          AND QuoteId = @QuoteId
          AND ProposalId = @ProposalId
          AND CustomerAuthorizationId = @CustomerAuthorizationId
          AND ClientAcceptanceId = @ClientAcceptanceId
          AND StatusCode = N'Accepted'
          AND PolicyBindTransactionId IS NULL
          AND IsDeleted = 0
    )
        THROW 52073, 'The accepted quote authorization is no longer eligible for a new bind request. Continue the existing bind workflow or record a new customer acceptance.', 1;
END;

INSERT INTO Submissions.PolicyBindTransaction
    (PolicyBindTransactionId, TenantId, SubmissionId, QuoteId, ProposalId, CustomerAuthorizationId, ClientAcceptanceId, PolicyId, AccountId, CarrierId,
     PolicySourceCode, BindStatusCode, PolicyNumber, AnnualPremium, EffectiveDate, ExpirationDate,
     BindingAuthorityCode, BindingMethodCode, ProducerNotes, CarrierInstructions, SpecialConditions, ApprovalRequired, PaymentRequired, PaymentVerified, ResponseDueDateUtc,
     BindReason, Notes, RequestedEffectiveTime, ConfirmationSourceCode, CarrierReferenceNumber, BinderNumber, FinalPremium,
     DownPaymentAmount, SubjectivitiesOutstanding, ConfirmationNotes, ConfirmationDocumentId, ConfirmationReceivedFrom,
     ConfirmationMessageId, UnderwriterContactId, UnderwriterName, UnderwriterCompany,
     CommissionPlanApplicabilityId, CommissionPlanId, CommissionPlanVersionId, CommissionPayeeId, CommissionSplitRuleId,
     CommissionBusinessTypeCode, CommissionRatePct, CommissionSplitPct, CommissionablePremium, EstimatedGrossCommission, EstimatedProducerCommission,
     FollowUpWrittenConfirmationRequired, IntegrationCorrelationId,
     ExternalTransactionId, ConfirmedManually, ConfirmationCertified, RequestedByUserId, RequestedDateUtc, ApprovedByUserId, ApprovedDateUtc,
     BoundByUserId, BoundDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@PolicyBindTransactionId, @TenantId, COALESCE(@SubmissionId, '00000000-0000-0000-0000-000000000000'), COALESCE(@QuoteId, '00000000-0000-0000-0000-000000000000'), @ProposalId, @CustomerAuthorizationId, @ClientAcceptanceId, NULL, @AccountId, @CarrierId,
     @PolicySourceCode, @BindStatusCode, NULLIF(@PolicyNumber, N''), @AnnualPremium, @EffectiveDate, @ExpirationDate,
     @BindingAuthorityCode, @BindingMethodCode, @ProducerNotes, @CarrierInstructions, @SpecialConditions, @ApprovalRequired, @PaymentRequired, @PaymentVerified, @ResponseDueDateUtc,
     @PolicySourceReason, @PolicySourceNotes, @RequestedEffectiveTime, @ConfirmationSourceCode, @CarrierReferenceNumber, @BinderNumber, @FinalPremium,
     @DownPaymentAmount, @SubjectivitiesOutstanding, @ConfirmationNotes, @ConfirmationDocumentId, @ConfirmationReceivedFrom,
     @ConfirmationMessageId, @UnderwriterContactId, @UnderwriterName, @UnderwriterCompany,
     @CommissionPlanApplicabilityId, @CommissionPlanId, @CommissionPlanVersionId, @CommissionPayeeId, @CommissionSplitRuleId,
     @CommissionBusinessTypeCode, @CommissionRatePct, @CommissionSplitPct, @CommissionablePremium, @EstimatedGrossCommission, @EstimatedProducerCommission,
     @FollowUpWrittenConfirmationRequired, @IntegrationCorrelationId,
     @ExternalTransactionId, @ConfirmedManually, @ConfirmationCertified, @RequestedByUserId, @RequestedDateUtc, @ApprovedByUserId,
     CASE WHEN @ApprovedByUserId IS NULL THEN NULL ELSE @RequestedDateUtc END,
     CASE WHEN @CreatesPolicy = 1 THEN @BoundByUserId ELSE NULL END, CASE WHEN @CreatesPolicy = 1 THEN @RequestedDateUtc ELSE NULL END, @RequestedDateUtc, @RequestedByUserId, 0);

INSERT INTO Submissions.BindStatusHistory (BindStatusHistoryId, TenantId, PolicyBindTransactionId, OldStatusCode, NewStatusCode, Comments, ChangedDateUtc, ChangedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES (NEWID(), @TenantId, @PolicyBindTransactionId, NULL, @BindStatusCode, COALESCE(NULLIF(@ProducerNotes, N''), N'Bind request created.'), @RequestedDateUtc, @RequestedByUserId, @RequestedDateUtc, @RequestedByUserId, 0);

UPDATE Submissions.ClientAcceptance
SET PolicyBindTransactionId = @PolicyBindTransactionId,
    StatusCode = CASE WHEN @CreatesPolicy = 1 THEN N'CarrierBound' ELSE N'BindRequested' END,
    VersionNumber = VersionNumber + 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = COALESCE(@BoundByUserId, @RequestedByUserId)
WHERE ClientAcceptanceId = @ClientAcceptanceId
  AND TenantId = @TenantId
  AND SubmissionId = @SubmissionId
  AND ProposalId = @ProposalId
  AND QuoteId = @QuoteId
  AND CustomerAuthorizationId = @CustomerAuthorizationId
  AND StatusCode = N'Accepted'
  AND PolicyBindTransactionId IS NULL
  AND IsDeleted = 0;
IF @@ROWCOUNT <> 1
    THROW 52073, 'The accepted quote authorization was already used or changed before bind creation completed.', 1;

INSERT INTO Submissions.ClientAcceptanceAuditEvent (ClientAcceptanceAuditEventId, TenantId, ClientAcceptanceId, EventCode, EventDetail, EventDateUtc, ActorUserId)
SELECT NEWID(), @TenantId, ClientAcceptanceId, CASE WHEN @CreatesPolicy = 1 THEN N'CarrierBound' ELSE N'BindRequested' END,
       CASE WHEN @CreatesPolicy = 1 THEN N'Carrier confirmation certified; policy creation authorized.' ELSE N'Bind request initiated; coverage is not yet carrier bound.' END,
       SYSUTCDATETIME(), COALESCE(@BoundByUserId, @RequestedByUserId)
FROM Submissions.ClientAcceptance
WHERE TenantId = @TenantId AND PolicyBindTransactionId = @PolicyBindTransactionId AND IsDeleted = 0;

UPDATE Submissions.CustomerAuthorization
SET PolicyBindTransactionId = COALESCE(PolicyBindTransactionId, @PolicyBindTransactionId),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @RequestedByUserId
WHERE CustomerAuthorizationId = @CustomerAuthorizationId
  AND IsDeleted = 0;

UPDATE Submissions.Submission
SET Status = N'Binding',
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @RequestedByUserId
WHERE SubmissionId = @SubmissionId
  AND TenantId = @TenantId
  AND IsDeleted = 0
  AND @SubmissionId IS NOT NULL
  AND Status NOT IN (N'Bound', N'Lost', N'Cancelled', N'Closed');

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
SELECT NEWID(), @SubmissionId, @TenantId, CASE WHEN @CreatesPolicy = 1 THEN N'CarrierConfirmationRecorded' ELSE N'BindRequestRecorded' END, CONCAT(CASE WHEN @CreatesPolicy = 1 THEN N'Carrier confirmation recorded with status ' ELSE N'Bind request recorded with status ' END, @BindStatusCode, N'. ', COALESCE(@PolicySourceReason, N''), CASE WHEN NULLIF(@PolicySourceNotes, N'') IS NULL THEN N'' ELSE CONCAT(N' Notes: ', @PolicySourceNotes) END), SYSUTCDATETIME(), N'PolicyBindTransaction', @PolicyBindTransactionId, N'User', 0
WHERE @SubmissionId IS NOT NULL;

COMMIT TRANSACTION;

SELECT @PolicyBindTransactionId;";

        var transactionId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(bindRequestSql, new
        {
            SubmissionId = submissionId,
            QuoteId = quoteId,
            request.TenantId,
            request.AccountId,
            request.CarrierId,
            request.AnnualPremium,
            request.EffectiveDate,
            request.ExpirationDate,
            request.PolicyNumber,
            PolicySourceCode = source.SourceCode,
            request.PolicySourceReason,
            request.PolicySourceNotes,
            request.RequestedByUserId,
            request.ApprovedByUserId,
            request.BoundByUserId,
            request.BindStatusCode,
            request.ProposalId,
            CustomerAuthorizationIdIn = request.CustomerAuthorizationId,
            request.CustomerAuthorizationMethodCode,
            request.CustomerAuthorizationReference,
            request.CustomerAuthorizationNotes,
            request.CustomerAuthorizedByName,
            request.CustomerAuthorizedDateUtc,
            request.CustomerAuthorizationDocumentId,
            request.RequestedEffectiveTime,
            request.ConfirmationSourceCode,
            request.CarrierReferenceNumber,
            request.BinderNumber,
            request.FinalPremium,
            request.DownPaymentAmount,
            request.SubjectivitiesOutstanding,
            request.ConfirmationNotes,
            request.ConfirmationDocumentId,
            request.ConfirmationReceivedFrom,
            request.ConfirmationMessageId,
            request.UnderwriterContactId,
            UnderwriterName = underwriterName,
            UnderwriterCompany = underwriterCompany,
            CommissionPlanApplicabilityId = commissionEstimate?.CommissionPlanApplicabilityId,
            CommissionPlanId = commissionEstimate?.CommissionPlanId,
            CommissionPlanVersionId = commissionEstimate?.CommissionPlanVersionId,
            CommissionPayeeId = commissionEstimate?.CommissionPayeeId,
            CommissionSplitRuleId = commissionEstimate?.CommissionSplitRuleId,
            CommissionBusinessTypeCode = commissionEstimate?.BusinessTypeCode,
            CommissionRatePct = commissionEstimate?.CommissionRatePct,
            CommissionSplitPct = commissionEstimate?.CommissionSplitPct,
            CommissionablePremium = commissionEstimate?.CommissionablePremium,
            EstimatedGrossCommission = commissionEstimate?.EstimatedGrossCommission,
            EstimatedProducerCommission = commissionEstimate?.EstimatedProducerCommission,
            request.FollowUpWrittenConfirmationRequired,
            request.IntegrationCorrelationId,
            request.ExternalTransactionId,
            request.ConfirmedManually,
            request.ConfirmationCertified,
            request.ClientAcceptanceId,
            request.BindingAuthorityCode,
            request.BindingMethodCode,
            request.ProducerNotes,
            request.CarrierInstructions,
            request.SpecialConditions,
            request.ApprovalRequired,
            request.PaymentRequired,
            request.PaymentVerified,
            request.ResponseDueDateUtc,
            CreatesPolicy = bindStatus.CreatesPolicy,
        }, cancellationToken: cancellationToken));

        if (submissionId.HasValue)
        {
            await cn.ExecuteAsync(new CommandDefinition(RecalculateSubmissionStatusSql, new { SubmissionId = submissionId.Value, request.TenantId }, cancellationToken: cancellationToken));
            var workflowTitle = bindStatus.CreatesPolicy ? "Carrier Confirmation Recorded" : "Bind Request Recorded";
            var workflowMessage = bindStatus.CreatesPolicy
                ? $"Carrier confirmation recorded with status {bindStatus.StatusName}; policy creation has been requested from the Policy Service."
                : $"Bind request recorded with status {bindStatus.StatusName}; policy was not created because carrier confirmation is not complete.";
            await RecordOpportunityWorkflowAsync(cn, submissionId.Value, request.TenantId, "Binding", workflowTitle, workflowTitle, workflowMessage, "PolicyBindTransaction", transactionId, request.RequestedByUserId, cancellationToken);
        }

        return transactionId;
    }
}

public sealed class SubmissionReferenceOptionRepository : ISubmissionReferenceOptionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SubmissionReferenceOptionRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<SubmissionReferenceOptionDto>> GetAllAsync(Guid tenantId, string? optionGroup = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureReferenceDataAsync(connection, tenantId, cancellationToken);

        const string sql = @"
SELECT SubmissionReferenceOptionId, TenantId, OptionGroup, OptionCode, OptionName, Description,
       IsDefault, IsActive, SortOrder, CreatedDateUtc
FROM Submissions.SubmissionReferenceOption
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@OptionGroup IS NULL OR @OptionGroup = '' OR OptionGroup = @OptionGroup)
ORDER BY OptionGroup, SortOrder, OptionName;";

        var items = await connection.QueryAsync<SubmissionReferenceOptionDto>(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            OptionGroup = optionGroup,
        }, cancellationToken: cancellationToken));
        return items.AsList();
    }

    private static async Task EnsureReferenceDataAsync(System.Data.IDbConnection connection, Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Submissions') EXEC('CREATE SCHEMA Submissions');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Submissions.SubmissionReferenceOption'))
CREATE TABLE Submissions.SubmissionReferenceOption (
    SubmissionReferenceOptionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId                    UNIQUEIDENTIFIER NOT NULL,
    OptionGroup                 NVARCHAR(50)     NOT NULL,
    OptionCode                  NVARCHAR(100)    NOT NULL,
    OptionName                  NVARCHAR(150)    NOT NULL,
    Description                 NVARCHAR(500)    NULL,
    IsDefault                   BIT              NOT NULL DEFAULT 0,
    IsActive                    BIT              NOT NULL DEFAULT 1,
    SortOrder                   INT              NOT NULL DEFAULT 0,
    CreatedDateUtc              DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc             DATETIME2        NULL,
    IsDeleted                   BIT              NOT NULL DEFAULT 0,
    CONSTRAINT UQ_SubmissionReferenceOption_Tenant_Group_Code UNIQUE (TenantId, OptionGroup, OptionCode)
);

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'SubmissionStatus' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'SubmissionStatus', 'New', 'New', 'New submission intake record.', 1, 10),
        (@TenantId, 'SubmissionStatus', 'In Review', 'In Review', 'Submission is in underwriting or carrier review.', 0, 20),
        (@TenantId, 'SubmissionStatus', 'Quoted', 'Quoted', 'Submission has one or more quotes.', 0, 30),
        (@TenantId, 'SubmissionStatus', 'Bound', 'Bound', 'Submission has been bound into policy workflow.', 0, 40),
        (@TenantId, 'SubmissionStatus', 'Declined', 'Declined', 'Submission was declined by underwriting or market.', 0, 80),
        (@TenantId, 'SubmissionStatus', 'Withdrawn', 'Withdrawn', 'Submission was withdrawn by client or producer.', 0, 90);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'LineOfBusiness' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'LineOfBusiness', 'General Liability', 'General Liability', 'Commercial general liability placement.', 1, 10),
        (@TenantId, 'LineOfBusiness', 'Commercial Property', 'Commercial Property', 'Commercial property placement.', 0, 20),
        (@TenantId, 'LineOfBusiness', 'Commercial Auto', 'Commercial Auto', 'Commercial automobile placement.', 0, 30),
        (@TenantId, 'LineOfBusiness', 'Workers Comp', 'Workers Comp', 'Workers compensation placement.', 0, 40),
        (@TenantId, 'LineOfBusiness', 'Umbrella / Excess', 'Umbrella / Excess', 'Umbrella or excess liability placement.', 0, 50),
        (@TenantId, 'LineOfBusiness', 'Professional Liability', 'Professional Liability', 'Professional liability placement.', 0, 60),
        (@TenantId, 'LineOfBusiness', 'Home / Dwelling', 'Home / Dwelling', 'Personal home or dwelling placement.', 0, 70),
        (@TenantId, 'LineOfBusiness', 'Personal Auto', 'Personal Auto', 'Personal automobile placement.', 0, 80);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'ApplicationStatus' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'ApplicationStatus', 'Draft', 'Draft', 'Application package is being drafted.', 1, 10),
        (@TenantId, 'ApplicationStatus', 'Submitted', 'Submitted', 'Application has been submitted.', 0, 20),
        (@TenantId, 'ApplicationStatus', 'Under Review', 'Under Review', 'Application is under review.', 0, 30),
        (@TenantId, 'ApplicationStatus', 'Requirements Pending', 'Requirements Pending', 'Additional requirements are pending.', 0, 40),
        (@TenantId, 'ApplicationStatus', 'Approved', 'Approved', 'Application is approved for quote workflow.', 0, 50),
        (@TenantId, 'ApplicationStatus', 'Rejected', 'Rejected', 'Application was rejected.', 0, 90);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'QuoteStatus' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'QuoteStatus', 'Pending', 'Pending', 'Quote is pending market response.', 1, 10),
        (@TenantId, 'QuoteStatus', 'Accepted', 'Accepted', 'Quote has been accepted or presented.', 0, 20),
        (@TenantId, 'QuoteStatus', 'Declined', 'Declined', 'Quote has been declined.', 0, 80),
        (@TenantId, 'QuoteStatus', 'Expired', 'Expired', 'Quote has expired.', 0, 90);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'MarketStatus' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'MarketStatus', 'Draft', 'Draft', 'Market request is being prepared.', 1, 10),
        (@TenantId, 'MarketStatus', 'Sent', 'Sent', 'Market request has been sent.', 0, 20),
        (@TenantId, 'MarketStatus', 'In Review', 'In Review', 'Carrier is reviewing the request.', 0, 30),
        (@TenantId, 'MarketStatus', 'Awaiting Info', 'Awaiting Info', 'Carrier requested additional information.', 0, 40),
        (@TenantId, 'MarketStatus', 'Declined', 'Declined', 'Carrier declined the request.', 0, 70),
        (@TenantId, 'MarketStatus', 'Quoted', 'Quoted', 'Carrier provided quote terms.', 0, 80),
        (@TenantId, 'MarketStatus', 'No Response', 'No Response', 'Carrier has not responded by the due date.', 0, 90);
END;

INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
SELECT @TenantId, 'QuoteStatus', v.Code, v.Name, v.Description, 0, v.SortOrder
FROM (VALUES
    ('Received', 'Received', 'Carrier quote response has been received.', 30),
    ('Under Review', 'Under Review', 'Quote is under internal review before customer presentation.', 35),
    ('Revision Requested', 'Revision Requested', 'Quote requires revised terms from the market.', 38),
    ('Approved for Presentation', 'Approved for Presentation', 'Quote has been approved for customer presentation.', 39),
    ('Proposed', 'Proposed', 'Quote was proposed to the client.', 40),
    ('Presented', 'Presented', 'Quote has been included in a customer proposal.', 45),
    ('Selected', 'Selected', 'Quote was selected for proposal or bind.', 50),
    ('Not Selected', 'Not Selected', 'Quote was retained in history but not selected.', 55),
    ('Bound', 'Bound', 'Quote was bound into a policy.', 60),
    ('Superseded', 'Superseded', 'Quote was superseded by a later version or revision.', 65),
    ('Lost', 'Lost', 'Quote was lost or not selected.', 70)
) v(Code, Name, Description, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption existing WHERE existing.TenantId = @TenantId AND existing.OptionGroup = 'QuoteStatus' AND existing.OptionCode = v.Code AND existing.IsDeleted = 0);

INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
SELECT @TenantId, 'MarketStatus', v.Code, v.Name, v.Description, 0, v.SortOrder
FROM (VALUES
    ('Selected', 'Selected', 'Market has been selected for submission.', 15),
    ('Blocked', 'Blocked', 'Market request is blocked pending resolution.', 60),
    ('Bound', 'Bound', 'Market quote has been bound.', 85),
    ('Not Selected', 'Not Selected', 'Market was not selected for placement.', 95)
) v(Code, Name, Description, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption existing WHERE existing.TenantId = @TenantId AND existing.OptionGroup = 'MarketStatus' AND existing.OptionCode = v.Code AND existing.IsDeleted = 0);

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'SubmissionMethod' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'SubmissionMethod', 'ApiRating', 'API Rating', 'Submission and quote request are sent through a carrier or comparative rater API.', 0, 5),
        (@TenantId, 'SubmissionMethod', 'Email', 'Email', 'Submission package is delivered by email.', 1, 10),
        (@TenantId, 'SubmissionMethod', 'MgaPortal', 'MGA Portal', 'Submission package is delivered through an MGA, wholesaler, or carrier portal.', 0, 18),
        (@TenantId, 'SubmissionMethod', 'Portal', 'Portal', 'Submission package is delivered through a carrier portal.', 0, 20),
        (@TenantId, 'SubmissionMethod', 'API', 'API', 'Submission package is delivered through an API integration.', 0, 30),
        (@TenantId, 'SubmissionMethod', 'Download', 'Download', 'Submission package is prepared for manual download.', 0, 40),
        (@TenantId, 'SubmissionMethod', 'ManualUnderwriter', 'Manual Underwriter', 'Submission package is tracked through manual underwriter review.', 0, 45),
        (@TenantId, 'SubmissionMethod', 'InternalQueue', 'Internal Queue', 'Submission package is queued for internal processing.', 0, 50);
END;

INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
SELECT @TenantId, 'QuoteRequestMethod', v.Code, v.Name, v.Description, v.IsDefault, v.SortOrder
FROM (VALUES
    ('ApiRating', 'API Rating', 'Personal-lines or comparative-rater API path where request quote submits and rates in one workflow.', 1, 10),
    ('MgaPortal', 'MGA Portal', 'MGA, wholesaler, or carrier portal path where AMS tracks portal submission and quote response.', 0, 20),
    ('Email', 'Email', 'Email path where AMS tracks a quote request sent to the market or underwriter by email.', 0, 30),
    ('ManualUnderwriter', 'Manual Underwriter', 'Manual commercial underwriting path where an underwriter reviews before quote terms are returned.', 0, 40)
) v(Code, Name, Description, IsDefault, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption existing WHERE existing.TenantId = @TenantId AND existing.OptionGroup = 'QuoteRequestMethod' AND existing.OptionCode = v.Code AND existing.IsDeleted = 0);

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'ProposalDeliveryMethod' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'ProposalDeliveryMethod', 'Email', 'Email', 'Proposal is delivered by email.', 1, 10),
        (@TenantId, 'ProposalDeliveryMethod', 'Portal', 'Portal', 'Proposal is delivered through a client portal.', 0, 20),
        (@TenantId, 'ProposalDeliveryMethod', 'Download', 'Download', 'Proposal is generated for manual download.', 0, 30);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'QuoteRequestScope' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'QuoteRequestScope', 'Package', 'Package / Multi-line', 'Request quote terms for the selected package of coverage lines.', 1, 10),
        (@TenantId, 'QuoteRequestScope', 'SingleLine', 'Single Line', 'Request quote terms for one selected coverage line.', 0, 20);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'DeclineType' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'DeclineType', 'Carrier', 'Carrier', 'Carrier or market declined the submission.', 1, 10),
        (@TenantId, 'DeclineType', 'Internal', 'Internal', 'Agency or underwriting team declined the submission.', 0, 20),
        (@TenantId, 'DeclineType', 'Withdrawn', 'Withdrawn', 'Client or producer withdrew the submission.', 0, 30);
END;";

        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
