using Ams.Application.Abstractions.Persistence;
using Ams.Worker.Automation;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Submissions;

public sealed class SubmitToMarketDispatchWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<SubmitToMarketDispatchWorkerService> _logger;

    public SubmitToMarketDispatchWorkerService(IServiceProvider serviceProvider, IOptions<WorkerOptions> options, ILogger<SubmitToMarketDispatchWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AMS submit-to-market dispatch worker started with {PollIntervalSeconds}s polling interval.", _options.SubmitToMarketPollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = new DispatchWorkerSettings(_options.SubmitToMarketPollIntervalSeconds, _options.MaxSubmitToMarketDispatchesPerPoll);
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
                settings = await GetDispatchWorkerSettingsAsync(connectionFactory, stoppingToken);
                var processed = await ProcessPendingDispatchesAsync(connectionFactory, settings.MaxDispatchesPerPoll, stoppingToken);

                if (processed > 0)
                {
                    _logger.LogInformation("Submit-to-market dispatch worker processed {DispatchCount} dispatch records.", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Submit-to-market dispatch worker polling cycle failed: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, settings.PollIntervalSeconds)), stoppingToken);
        }
    }

    private async Task<DispatchWorkerSettings> GetDispatchWorkerSettingsAsync(ISqlConnectionFactory connectionFactory, CancellationToken cancellationToken)
    {
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = @"
IF OBJECT_ID(N'Agency.CarrierSetting', N'U') IS NULL
BEGIN
    SELECT @PollIntervalSeconds AS PollIntervalSeconds, @MaxDispatchesPerPoll AS MaxDispatchesPerPoll;
    RETURN;
END;

SELECT
    COALESCE(TRY_CONVERT(INT, MAX(CASE WHEN SettingCode = N'SUBMIT_TO_MARKET_WORKER_POLL_SECONDS' THEN COALESCE(SettingValue, DefaultValue) END)), @PollIntervalSeconds) AS PollIntervalSeconds,
    COALESCE(TRY_CONVERT(INT, MAX(CASE WHEN SettingCode = N'SUBMIT_TO_MARKET_WORKER_BATCH_SIZE' THEN COALESCE(SettingValue, DefaultValue) END)), @MaxDispatchesPerPoll) AS MaxDispatchesPerPoll
FROM Agency.CarrierSetting
WHERE CarrierId IS NULL
  AND IsActive = 1
  AND IsDeleted = 0
  AND SettingCode IN (N'SUBMIT_TO_MARKET_WORKER_POLL_SECONDS', N'SUBMIT_TO_MARKET_WORKER_BATCH_SIZE');";
        var settings = await cn.QuerySingleAsync<DispatchWorkerSettings>(new CommandDefinition(sql, new { PollIntervalSeconds = _options.SubmitToMarketPollIntervalSeconds, MaxDispatchesPerPoll = _options.MaxSubmitToMarketDispatchesPerPoll }, cancellationToken: cancellationToken));
        return new DispatchWorkerSettings(Math.Clamp(settings.PollIntervalSeconds, 10, 3600), Math.Clamp(settings.MaxDispatchesPerPoll, 1, 250));
    }

    private static async Task<int> ProcessPendingDispatchesAsync(ISqlConnectionFactory connectionFactory, int maxDispatches, CancellationToken cancellationToken)
    {
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string sql = @"
IF OBJECT_ID(N'Submissions.SubmissionMarketDispatch', N'U') IS NULL
BEGIN
    SELECT 0;
    RETURN;
END;

DECLARE @WorkerId NVARCHAR(120) = CONCAT(HOST_NAME(), N':SubmitToMarketDispatch');
IF OBJECT_ID(N'tempdb..#Batch') IS NOT NULL DROP TABLE #Batch;
CREATE TABLE #Batch (SubmissionMarketDispatchId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);
IF OBJECT_ID(N'tempdb..#DispatchConnector') IS NOT NULL DROP TABLE #DispatchConnector;
CREATE TABLE #DispatchConnector (SubmissionMarketDispatchId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, CarrierExternalConnectorId UNIQUEIDENTIFIER NULL, EndpointUri NVARCHAR(1000) NULL);
IF OBJECT_ID(N'tempdb..#DispatchTenantSetting') IS NOT NULL DROP TABLE #DispatchTenantSetting;
CREATE TABLE #DispatchTenantSetting (TenantId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, CompletableChannels NVARCHAR(MAX) NOT NULL, SandboxConfirmation BIT NOT NULL);
DECLARE @HasCarrierSetting BIT = CASE WHEN OBJECT_ID(N'Agency.CarrierSetting', N'U') IS NULL THEN 0 ELSE 1 END;
DECLARE @HasCarrierExternalConnector BIT = CASE WHEN OBJECT_ID(N'Agency.CarrierExternalConnector', N'U') IS NULL THEN 0 ELSE 1 END;
DECLARE @CompletableChannels NVARCHAR(MAX) = N'[""InternalQueue"",""Manual"",""Portal""]';
DECLARE @SandboxConfirmation BIT = 0;

IF @HasCarrierSetting = 1
BEGIN
    EXEC sp_executesql
        N'SELECT @CompletableChannelsOut = COALESCE((SELECT TOP 1 COALESCE(SettingValue, DefaultValue) FROM Agency.CarrierSetting WHERE SettingCode = N''SUBMIT_TO_MARKET_WORKER_COMPLETABLE_CHANNELS'' AND CarrierId IS NULL AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC), @CompletableChannelsOut);',
        N'@CompletableChannelsOut NVARCHAR(MAX) OUTPUT',
        @CompletableChannelsOut = @CompletableChannels OUTPUT;

    EXEC sp_executesql
        N'SELECT @SandboxConfirmationOut = CASE WHEN LOWER(COALESCE((SELECT TOP 1 COALESCE(SettingValue, DefaultValue) FROM Agency.CarrierSetting WHERE SettingCode = N''CARRIER_TRANSMISSION_SANDBOX_CONFIRMATION'' AND CarrierId IS NULL AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC), N''false'')) IN (N''true'', N''1'', N''yes'', N''on'') THEN 1 ELSE 0 END;',
        N'@SandboxConfirmationOut BIT OUTPUT',
        @SandboxConfirmationOut = @SandboxConfirmation OUTPUT;
END;

;WITH NextBatch AS
(
    SELECT TOP (@MaxDispatches) d.SubmissionMarketDispatchId
    FROM Submissions.SubmissionMarketDispatch d WITH (READPAST, UPDLOCK, ROWLOCK)
    OUTER APPLY
    (
        SELECT TOP 1 IsDisabled = CAST(1 AS bit)
        FROM Agency.CarrierSetting setting
        WHERE @HasCarrierSetting = 1
          AND setting.TenantId = d.TenantId
          AND setting.CarrierId IS NULL
          AND setting.SettingCode = N'SUBMIT_TO_MARKET_DISPATCH_ENABLED'
          AND setting.IsActive = 1
          AND setting.IsDeleted = 0
          AND LOWER(COALESCE(setting.SettingValue, setting.DefaultValue, N'true')) IN (N'false', N'0', N'no', N'off')
    ) disabled
    WHERE d.IsDeleted = 0
      AND d.DispatchStatusCode IN (N'Pending', N'Failed')
      AND d.AttemptCount < d.MaxAttemptCount
      AND d.NextAttemptDateUtc <= SYSUTCDATETIME()
      AND disabled.IsDisabled IS NULL
    ORDER BY d.NextAttemptDateUtc, d.CreatedDateUtc
)
UPDATE d
SET DispatchStatusCode = N'Processing',
    LockedDateUtc = SYSUTCDATETIME(),
    LockedBy = @WorkerId,
    AttemptCount = AttemptCount + 1,
    LastAttemptDateUtc = SYSUTCDATETIME(),
    ModifiedDateUtc = SYSUTCDATETIME()
OUTPUT inserted.SubmissionMarketDispatchId INTO #Batch
FROM Submissions.SubmissionMarketDispatch d
INNER JOIN NextBatch b ON b.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId;

INSERT INTO #DispatchTenantSetting (TenantId, CompletableChannels, SandboxConfirmation)
SELECT DISTINCT d.TenantId, @CompletableChannels, @SandboxConfirmation
FROM Submissions.SubmissionMarketDispatch d
INNER JOIN #Batch b ON b.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId;

IF @HasCarrierSetting = 1
BEGIN
    EXEC(N'
    UPDATE target
    SET CompletableChannels = COALESCE(NULLIF(completable.SettingValue, N''''), NULLIF(completable.DefaultValue, N''''), target.CompletableChannels),
        SandboxConfirmation = CASE WHEN LOWER(COALESCE(NULLIF(sandbox.SettingValue, N''''), NULLIF(sandbox.DefaultValue, N''''), CASE WHEN target.SandboxConfirmation = 1 THEN N''true'' ELSE N''false'' END)) IN (N''true'', N''1'', N''yes'', N''on'') THEN 1 ELSE 0 END
    FROM #DispatchTenantSetting target
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = target.TenantId AND CarrierId IS NULL AND SettingCode = N''SUBMIT_TO_MARKET_WORKER_COMPLETABLE_CHANNELS'' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) completable
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = target.TenantId AND CarrierId IS NULL AND SettingCode = N''CARRIER_TRANSMISSION_SANDBOX_CONFIRMATION'' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) sandbox;');
END;

IF @HasCarrierExternalConnector = 1
BEGIN
    EXEC(N'
    INSERT INTO #DispatchConnector (SubmissionMarketDispatchId, CarrierExternalConnectorId, EndpointUri)
    SELECT d.SubmissionMarketDispatchId, connector.CarrierExternalConnectorId, connector.EndpointUri
    FROM Submissions.SubmissionMarketDispatch d
    INNER JOIN #Batch b ON b.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId
    OUTER APPLY
    (
        SELECT TOP 1 c.CarrierExternalConnectorId, c.EndpointUri
        FROM Agency.CarrierExternalConnector c
        OUTER APPLY
        (
            SELECT TOP 1 COALESCE(NULLIF(setting.SettingValue, N''''), NULLIF(setting.DefaultValue, N'''')) AS ConnectorCode
            FROM Agency.CarrierSetting setting
            WHERE setting.TenantId = d.TenantId
              AND setting.CarrierId = d.CarrierId
              AND setting.SettingCode = N''CARRIER_DELIVERY_CONNECTOR_CODE''
              AND setting.IsActive = 1
              AND setting.IsDeleted = 0
            ORDER BY setting.ModifiedDateUtc DESC, setting.CreatedDateUtc DESC
        ) preferredConnector
        WHERE c.TenantId = d.TenantId
          AND (c.DefaultChannelCode = d.DispatchChannelCode OR c.ConnectorCode = preferredConnector.ConnectorCode)
          AND c.IsActive = 1
          AND c.IsDeleted = 0
          AND (c.CarrierId = d.CarrierId OR c.CarrierId IS NULL)
        ORDER BY CASE WHEN c.ConnectorCode = preferredConnector.ConnectorCode THEN 0 ELSE 1 END, CASE WHEN c.CarrierId = d.CarrierId THEN 0 ELSE 1 END, c.SortOrder
    ) connector;');
END;

UPDATE d
SET DispatchStatusCode = CASE
        WHEN CHARINDEX(CONCAT(N'""', d.DispatchChannelCode, N'""'), tenantSetting.CompletableChannels) > 0 THEN N'Completed'
        WHEN d.DispatchChannelCode = N'Email' AND tenantSetting.SandboxConfirmation = 1 THEN N'Completed'
        WHEN @HasCarrierExternalConnector = 0 THEN N'ReadyForExternalConnector'
        WHEN connector.CarrierExternalConnectorId IS NULL THEN N'Failed'
        ELSE N'ReadyForExternalConnector'
    END,
    CompletedDateUtc = CASE
        WHEN CHARINDEX(CONCAT(N'""', d.DispatchChannelCode, N'""'), tenantSetting.CompletableChannels) > 0 THEN SYSUTCDATETIME()
        WHEN d.DispatchChannelCode = N'Email' AND tenantSetting.SandboxConfirmation = 1 THEN SYSUTCDATETIME()
        ELSE NULL END,
    LockedDateUtc = NULL,
    LockedBy = NULL,
    LastError = CASE WHEN @HasCarrierExternalConnector = 1 AND connector.CarrierExternalConnectorId IS NULL AND CHARINDEX(CONCAT(N'""', d.DispatchChannelCode, N'""'), tenantSetting.CompletableChannels) = 0 AND NOT (d.DispatchChannelCode = N'Email' AND tenantSetting.SandboxConfirmation = 1) THEN CONCAT(N'No active carrier connector is configured for channel ', d.DispatchChannelCode, N'.') ELSE NULL END,
    NextAttemptDateUtc = CASE WHEN connector.CarrierExternalConnectorId IS NULL AND d.AttemptCount < d.MaxAttemptCount THEN DATEADD(minute, POWER(2, d.AttemptCount), SYSUTCDATETIME()) ELSE d.NextAttemptDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME()
FROM Submissions.SubmissionMarketDispatch d
INNER JOIN #Batch b ON b.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId
INNER JOIN #DispatchTenantSetting tenantSetting ON tenantSetting.TenantId = d.TenantId
LEFT JOIN #DispatchConnector connector ON connector.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId;

IF OBJECT_ID(N'tempdb..#DispatchDocumentPackage') IS NOT NULL DROP TABLE #DispatchDocumentPackage;
CREATE TABLE #DispatchDocumentPackage
(
    SubmissionMarketDispatchId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    DocumentPackageJson NVARCHAR(MAX) NOT NULL
);

IF OBJECT_ID(N'Submissions.SubmissionMarketDocument', N'U') IS NOT NULL AND OBJECT_ID(N'DMS.Document', N'U') IS NOT NULL
BEGIN
    EXEC(N'
    INSERT INTO #DispatchDocumentPackage (SubmissionMarketDispatchId, DocumentPackageJson)
    SELECT d.SubmissionMarketDispatchId,
           COALESCE(CONCAT(N''['', STRING_AGG(CONCAT(N''{'',
                N''""documentId"":""'', CONVERT(NVARCHAR(36), md.DocumentId), N''""'',
                N'',""fileName"":""'', STRING_ESCAPE(COALESCE(doc.FileName, N''''), ''json''), N''""'',
                N'',""categoryCode"":""'', STRING_ESCAPE(COALESCE(doc.CategoryCode, N''''), ''json''), N''""'',
                N'',""documentTypeCode"":""'', STRING_ESCAPE(COALESCE(doc.DocumentTypeCode, N''''), ''json''), N''""'',
                N'',""createdDateUtc"":""'', CONVERT(NVARCHAR(33), doc.CreatedDateUtc, 126), N''""'',
           N''}''), N'',''), N'']''), N''[]'') AS DocumentPackageJson
    FROM Submissions.SubmissionMarketDispatch d
    INNER JOIN #Batch b ON b.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId
    INNER JOIN Submissions.SubmissionMarketDocument md ON md.SubmissionMarketId = d.SubmissionMarketId AND md.IsDeleted = 0
    INNER JOIN DMS.Document doc ON doc.DocumentId = md.DocumentId AND doc.TenantId = d.TenantId AND doc.IsDeleted = 0
    GROUP BY d.SubmissionMarketDispatchId;');
END;

INSERT INTO Submissions.CarrierTransmission
    (CarrierTransmissionId, TenantId, SubmissionId, SubmissionMarketId, SubmissionMarketDispatchId, CarrierId, CarrierExternalConnectorId,
     TransmissionTypeCode, ChannelCode, StatusCode, Recipient, Subject, EndpointUri, PayloadJson, DocumentPackageJson, ExternalReferenceNumber,
     AttemptCount, LastAttemptDateUtc, SentDateUtc, ConfirmedDateUtc, FailedDateUtc, BounceDateUtc, LastError, CreatedDateUtc, IsDeleted)
SELECT NEWID(), d.TenantId, d.SubmissionId, d.SubmissionMarketId, d.SubmissionMarketDispatchId, d.CarrierId, connector.CarrierExternalConnectorId,
       CASE WHEN JSON_VALUE(d.PayloadJson, '$.quoteRequestId') IS NOT NULL THEN N'QuoteRequest' ELSE N'SubmitToMarket' END, d.DispatchChannelCode,
       CASE
           WHEN d.DispatchStatusCode = N'Completed' THEN N'Delivered'
           WHEN d.DispatchStatusCode = N'ReadyForExternalConnector' THEN N'AwaitingExternalConnector'
           ELSE d.DispatchStatusCode END,
       d.Recipient, d.Subject, connector.EndpointUri, d.PayloadJson, COALESCE(documentPackage.DocumentPackageJson, N'[]'),
       CONCAT(N'AMS-', CONVERT(NVARCHAR(36), d.SubmissionMarketDispatchId)),
       d.AttemptCount, d.LastAttemptDateUtc,
        CASE WHEN d.DispatchStatusCode IN (N'Completed', N'ReadyForExternalConnector') THEN SYSUTCDATETIME() ELSE NULL END,
       CASE WHEN d.DispatchStatusCode = N'Completed' THEN SYSUTCDATETIME() ELSE NULL END,
        CASE WHEN d.DispatchStatusCode = N'Failed' THEN SYSUTCDATETIME() ELSE NULL END, NULL, d.LastError, SYSUTCDATETIME(), 0
FROM Submissions.SubmissionMarketDispatch d
INNER JOIN #Batch b ON b.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId
LEFT JOIN #DispatchConnector connector ON connector.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId
LEFT JOIN #DispatchDocumentPackage documentPackage ON documentPackage.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId
WHERE NOT EXISTS
(
    SELECT 1
    FROM Submissions.CarrierTransmission existing
    WHERE existing.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId
      AND existing.AttemptCount = d.AttemptCount
      AND existing.IsDeleted = 0
);

UPDATE qr
SET StatusCode = CASE
        WHEN d.DispatchStatusCode = N'Failed' THEN N'Failed'
        WHEN t.StatusCode = N'Delivered' THEN N'Acknowledged'
        ELSE N'Submitted' END,
    RetryCount = d.AttemptCount,
    LastAttemptDateUtc = d.LastAttemptDateUtc,
    DispatchedDateUtc = COALESCE(qr.DispatchedDateUtc, t.SentDateUtc, d.LastAttemptDateUtc, SYSUTCDATETIME()),
    AcknowledgedDateUtc = CASE WHEN t.StatusCode = N'Delivered' THEN COALESCE(qr.AcknowledgedDateUtc, t.ConfirmedDateUtc, SYSUTCDATETIME()) ELSE qr.AcknowledgedDateUtc END,
    LastError = CASE WHEN d.DispatchStatusCode = N'Failed' THEN COALESCE(d.LastError, t.LastError, N'Carrier dispatch failed.') ELSE NULL END,
    ClosedDateUtc = CASE WHEN d.DispatchStatusCode = N'Failed' THEN COALESCE(qr.ClosedDateUtc, SYSUTCDATETIME()) ELSE qr.ClosedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME()
FROM Submissions.QuoteRequest qr
INNER JOIN Submissions.SubmissionMarketDispatch d ON d.SubmissionMarketId = qr.SubmissionMarketId AND d.IsDeleted = 0
INNER JOIN #Batch b ON b.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId
INNER JOIN Submissions.CarrierTransmission t ON t.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId AND t.IsDeleted = 0
WHERE qr.IsDeleted = 0
  AND JSON_VALUE(d.PayloadJson, '$.quoteRequestId') = CONVERT(NVARCHAR(36), qr.QuoteRequestId)
  AND qr.StatusCode IN (N'PendingDispatch', N'Submitted', N'Failed');

UPDATE qrh
SET StatusCode = qr.StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME()
FROM Submissions.QuoteRequestHistory qrh
INNER JOIN Submissions.QuoteRequest qr ON qr.SubmissionMarketId = qrh.SubmissionMarketId AND qr.RequestVersion = qrh.RequestVersion AND qr.IsDeleted = 0
INNER JOIN Submissions.SubmissionMarketDispatch d ON d.SubmissionMarketId = qr.SubmissionMarketId AND d.IsDeleted = 0
INNER JOIN #Batch b ON b.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId
WHERE qrh.IsDeleted = 0
  AND JSON_VALUE(d.PayloadJson, '$.quoteRequestId') = CONVERT(NVARCHAR(36), qr.QuoteRequestId)
  AND qrh.StatusCode IN (N'PendingDispatch', N'Submitted', N'Failed');

INSERT INTO Submissions.CarrierTransmissionEvent
    (CarrierTransmissionEventId, TenantId, CarrierTransmissionId, SubmissionId, SubmissionMarketId, EventCode, EventMessage, EventPayloadJson, CreatedDateUtc, IsDeleted)
SELECT NEWID(), t.TenantId, t.CarrierTransmissionId, t.SubmissionId, t.SubmissionMarketId,
       CASE WHEN t.StatusCode = N'Delivered' THEN N'DeliveryConfirmed' WHEN t.StatusCode = N'Failed' THEN N'DeliveryFailed' ELSE N'ExternalConnectorQueued' END,
       CASE WHEN t.StatusCode = N'Delivered'
            THEN N'Carrier transmission delivery was confirmed by the configured connector.'
             WHEN t.StatusCode = N'Failed'
             THEN COALESCE(t.LastError, N'Carrier transmission failed before connector execution.')
            ELSE CONCAT(N'Carrier transmission is queued for ', t.ChannelCode, N' connector execution.') END,
       N'{}', SYSUTCDATETIME(), 0
FROM Submissions.CarrierTransmission t
INNER JOIN #Batch b ON b.SubmissionMarketDispatchId = t.SubmissionMarketDispatchId
WHERE NOT EXISTS
(
    SELECT 1
    FROM Submissions.CarrierTransmissionEvent existing
    WHERE existing.CarrierTransmissionId = t.CarrierTransmissionId
      AND existing.EventCode IN (N'DeliveryConfirmed', N'DeliveryFailed', N'ExternalConnectorQueued')
      AND existing.IsDeleted = 0
);

INSERT INTO Submissions.CarrierTransmissionEvent
    (CarrierTransmissionEventId, TenantId, CarrierTransmissionId, SubmissionId, SubmissionMarketId, EventCode, EventMessage, EventPayloadJson, CreatedDateUtc, IsDeleted)
SELECT NEWID(), t.TenantId, t.CarrierTransmissionId, t.SubmissionId, t.SubmissionMarketId,
       CASE WHEN qr.StatusCode = N'Failed' THEN N'QuoteRequestDispatchFailed' WHEN qr.StatusCode = N'Acknowledged' THEN N'QuoteRequestAcknowledged' ELSE N'QuoteRequestSubmitted' END,
       CASE WHEN qr.StatusCode = N'Failed' THEN COALESCE(qr.LastError, N'Quote request dispatch failed.') WHEN qr.StatusCode = N'Acknowledged' THEN N'Quote request delivery was acknowledged.' ELSE N'Quote request was submitted to the configured carrier connector.' END,
       CONCAT(N'{""quoteRequestId"":""', CONVERT(NVARCHAR(36), qr.QuoteRequestId), N'"",""statusCode"":""', qr.StatusCode, N'""}'),
       SYSUTCDATETIME(), 0
FROM Submissions.CarrierTransmission t
INNER JOIN #Batch b ON b.SubmissionMarketDispatchId = t.SubmissionMarketDispatchId
INNER JOIN Submissions.SubmissionMarketDispatch d ON d.SubmissionMarketDispatchId = t.SubmissionMarketDispatchId
INNER JOIN Submissions.QuoteRequest qr ON qr.SubmissionMarketId = t.SubmissionMarketId AND qr.IsDeleted = 0 AND JSON_VALUE(d.PayloadJson, '$.quoteRequestId') = CONVERT(NVARCHAR(36), qr.QuoteRequestId)
WHERE NOT EXISTS
(
    SELECT 1
    FROM Submissions.CarrierTransmissionEvent existing
    WHERE existing.CarrierTransmissionId = t.CarrierTransmissionId
      AND existing.EventCode IN (N'QuoteRequestSubmitted', N'QuoteRequestAcknowledged', N'QuoteRequestDispatchFailed')
      AND existing.IsDeleted = 0
);

INSERT INTO Submissions.CarrierInboundResponse
    (CarrierInboundResponseId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, CarrierTransmissionId, SourceChannelCode, ResponseTypeCode, StatusCode,
     CarrierReferenceNumber, PayloadJson, ReceivedDateUtc, ProcessedDateUtc, CreatedDateUtc, IsDeleted)
SELECT NEWID(), t.TenantId, t.SubmissionId, t.SubmissionMarketId, t.CarrierId, t.CarrierTransmissionId, t.ChannelCode,
       N'DeliveryConfirmation', N'Processed', t.ExternalReferenceNumber, N'{""source"":""sandbox-confirmation""}', SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME(), 0
FROM Submissions.CarrierTransmission t
INNER JOIN #Batch b ON b.SubmissionMarketDispatchId = t.SubmissionMarketDispatchId
INNER JOIN #DispatchTenantSetting tenantSetting ON tenantSetting.TenantId = t.TenantId
WHERE t.StatusCode = N'Delivered'
  AND tenantSetting.SandboxConfirmation = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM Submissions.CarrierInboundResponse existing
      WHERE existing.CarrierTransmissionId = t.CarrierTransmissionId
        AND existing.ResponseTypeCode = N'DeliveryConfirmation'
        AND existing.IsDeleted = 0
  );

INSERT INTO Submissions.SubmissionActionLog
    (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted, RelatedEntityName, RelatedEntityId, ActionSource)
SELECT NEWID(), d.SubmissionId, d.TenantId,
       CASE WHEN d.DispatchStatusCode = N'Completed' THEN N'SubmitToMarketTransmissionDelivered' WHEN d.DispatchStatusCode = N'Failed' THEN N'SubmitToMarketTransmissionFailed' ELSE N'SubmitToMarketTransmissionQueued' END,
       CASE WHEN d.DispatchStatusCode = N'Completed'
             THEN N'Submit-to-market carrier transmission delivery was confirmed through the configured connector.'
             WHEN d.DispatchStatusCode = N'Failed'
              THEN COALESCE(d.LastError, N'Submit-to-market carrier transmission failed before connector execution.')
             ELSE CONCAT(N'Submit-to-market carrier transmission is ready for ', d.DispatchChannelCode, N' connector processing.') END,
       SYSUTCDATETIME(), 0, N'SubmissionMarketDispatch', d.SubmissionMarketDispatchId, N'SubmitToMarketDispatchWorker'
FROM Submissions.SubmissionMarketDispatch d
INNER JOIN #Batch b ON b.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId
WHERE NOT EXISTS
(
    SELECT 1
    FROM Submissions.SubmissionActionLog existing
    WHERE existing.RelatedEntityName = N'SubmissionMarketDispatch'
      AND existing.RelatedEntityId = d.SubmissionMarketDispatchId
      AND existing.ActionSource = N'SubmitToMarketDispatchWorker'
      AND existing.IsDeleted = 0
);

SELECT COUNT(1) FROM #Batch;";

        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { MaxDispatches = Math.Max(1, maxDispatches) }, cancellationToken: cancellationToken));
    }

    private sealed record DispatchWorkerSettings(int PollIntervalSeconds, int MaxDispatchesPerPoll);
}
