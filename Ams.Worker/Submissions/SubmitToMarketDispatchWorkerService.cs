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
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
                var processed = await ProcessPendingDispatchesAsync(connectionFactory, _options.MaxSubmitToMarketDispatchesPerPoll, stoppingToken);

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

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, _options.SubmitToMarketPollIntervalSeconds)), stoppingToken);
        }
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
DECLARE @Batch TABLE (SubmissionMarketDispatchId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);
DECLARE @HasCarrierSetting BIT = CASE WHEN OBJECT_ID(N'Agency.CarrierSetting', N'U') IS NULL THEN 0 ELSE 1 END;

;WITH NextBatch AS
(
    SELECT TOP (@MaxDispatches) d.SubmissionMarketDispatchId
    FROM Submissions.SubmissionMarketDispatch d WITH (READPAST, UPDLOCK, ROWLOCK)
    WHERE d.IsDeleted = 0
      AND d.DispatchStatusCode IN (N'Pending', N'Failed')
      AND d.AttemptCount < d.MaxAttemptCount
      AND d.NextAttemptDateUtc <= SYSUTCDATETIME()
      AND (
          @HasCarrierSetting = 0
          OR NOT EXISTS
          (
              SELECT 1
              FROM Agency.CarrierSetting disabled
              WHERE disabled.TenantId = d.TenantId
                AND disabled.CarrierId IS NULL
                AND disabled.SettingCode = N'SUBMIT_TO_MARKET_DISPATCH_ENABLED'
                AND disabled.SettingValue = N'false'
                AND disabled.IsActive = 1
                AND disabled.IsDeleted = 0
          )
      )
    ORDER BY d.NextAttemptDateUtc, d.CreatedDateUtc
)
UPDATE d
SET DispatchStatusCode = N'Processing',
    LockedDateUtc = SYSUTCDATETIME(),
    LockedBy = @WorkerId,
    AttemptCount = AttemptCount + 1,
    LastAttemptDateUtc = SYSUTCDATETIME(),
    ModifiedDateUtc = SYSUTCDATETIME()
OUTPUT inserted.SubmissionMarketDispatchId INTO @Batch
FROM Submissions.SubmissionMarketDispatch d
INNER JOIN NextBatch b ON b.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId;

UPDATE d
SET DispatchStatusCode = CASE
        WHEN @HasCarrierSetting = 0 AND d.DispatchChannelCode = N'InternalQueue' THEN N'Completed'
        WHEN completable.SettingValue IS NOT NULL AND CHARINDEX(CONCAT(N'""', d.DispatchChannelCode, N'""'), completable.SettingValue) > 0 THEN N'Completed'
        ELSE N'ReadyForExternalConnector'
    END,
    CompletedDateUtc = CASE
        WHEN @HasCarrierSetting = 0 AND d.DispatchChannelCode = N'InternalQueue' THEN SYSUTCDATETIME()
        WHEN completable.SettingValue IS NOT NULL AND CHARINDEX(CONCAT(N'""', d.DispatchChannelCode, N'""'), completable.SettingValue) > 0 THEN SYSUTCDATETIME()
        ELSE NULL END,
    LockedDateUtc = NULL,
    LockedBy = NULL,
    LastError = NULL,
    ModifiedDateUtc = SYSUTCDATETIME()
FROM Submissions.SubmissionMarketDispatch d
OUTER APPLY
(
    SELECT TOP 1 setting.SettingValue
    FROM Agency.CarrierSetting setting
    WHERE @HasCarrierSetting = 1
      AND setting.TenantId = d.TenantId
      AND setting.CarrierId IS NULL
      AND setting.SettingCode = N'SUBMIT_TO_MARKET_WORKER_COMPLETABLE_CHANNELS'
      AND setting.IsActive = 1
      AND setting.IsDeleted = 0
) completable
INNER JOIN @Batch b ON b.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId;

INSERT INTO Submissions.SubmissionActionLog
    (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted, RelatedEntityName, RelatedEntityId, ActionSource)
SELECT NEWID(), d.SubmissionId, d.TenantId,
       CASE WHEN d.DispatchStatusCode = N'Completed' THEN N'SubmitToMarketDispatchCompleted' ELSE N'SubmitToMarketDispatchReady' END,
       CASE WHEN d.DispatchStatusCode = N'Completed'
            THEN N'Submit-to-market dispatch completed through internal queue processing.'
            ELSE CONCAT(N'Submit-to-market dispatch is ready for ', d.DispatchChannelCode, N' connector processing.') END,
       SYSUTCDATETIME(), 0, N'SubmissionMarketDispatch', d.SubmissionMarketDispatchId, N'SubmitToMarketDispatchWorker'
FROM Submissions.SubmissionMarketDispatch d
INNER JOIN @Batch b ON b.SubmissionMarketDispatchId = d.SubmissionMarketDispatchId
WHERE NOT EXISTS
(
    SELECT 1
    FROM Submissions.SubmissionActionLog existing
    WHERE existing.RelatedEntityName = N'SubmissionMarketDispatch'
      AND existing.RelatedEntityId = d.SubmissionMarketDispatchId
      AND existing.ActionSource = N'SubmitToMarketDispatchWorker'
      AND existing.IsDeleted = 0
);

SELECT COUNT(1) FROM @Batch;";

        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { MaxDispatches = Math.Max(1, maxDispatches) }, cancellationToken: cancellationToken));
    }
}
