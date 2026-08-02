using Ams.Application.Abstractions.Persistence;
using Ams.Worker.Automation;
using Dapper;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Accounting;

public sealed class PolicyCreatedAccountingWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<PolicyCreatedAccountingWorkerService> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public PolicyCreatedAccountingWorkerService(IServiceProvider serviceProvider, IOptions<WorkerOptions> options, ILogger<PolicyCreatedAccountingWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
                var accounting = scope.ServiceProvider.GetRequiredService<IPolicyAccountingRepository>();
                foreach (var item in await ClaimAsync(connectionFactory, stoppingToken))
                    await ProcessAsync(connectionFactory, accounting, item, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Policy accounting polling cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, _options.PolicyAccountingPollIntervalSeconds)), stoppingToken);
        }
    }

    private async Task<IReadOnlyList<ClaimedEvent>> ClaimAsync(ISqlConnectionFactory connectionFactory, CancellationToken cancellationToken)
    {
        const string sql = """
DECLARE @Claimed TABLE(PolicyCreatedEventId UNIQUEIDENTIFIER,TenantId UNIQUEIDENTIFIER,PolicyId UNIQUEIDENTIFIER,CreatedByUserId UNIQUEIDENTIFIER NULL);
;WITH next_batch AS
(
    SELECT TOP (@BatchSize) *
    FROM Accounting.PolicyCreatedEvent WITH(UPDLOCK,READPAST,ROWLOCK)
    WHERE ((StatusCode IN(N'Pending',N'Failed') AND COALESCE(NextAttemptDateUtc,OccurredDateUtc)<=SYSUTCDATETIME())
       OR (StatusCode=N'Processing' AND ProcessingStartedDateUtc<DATEADD(minute,-@ClaimLeaseMinutes,SYSUTCDATETIME())))
      AND EventTypeCode=N'PolicyCreated' AND IsDeleted=0 AND AttemptCount<@MaxAttempts
    ORDER BY OccurredDateUtc
)
UPDATE next_batch
SET StatusCode=N'Processing',ProcessingStartedDateUtc=SYSUTCDATETIME(),AttemptCount=AttemptCount+1,WorkerId=@WorkerId,ErrorDetails=NULL,ModifiedDateUtc=SYSUTCDATETIME()
OUTPUT inserted.PolicyCreatedEventId,inserted.TenantId,inserted.PolicyId,inserted.CreatedByUserId INTO @Claimed;
SELECT * FROM @Claimed;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ClaimedEvent>(new CommandDefinition(sql, new
        {
            BatchSize = Math.Clamp(_options.MaxPolicyAccountingEventsPerPoll, 1, 100),
            MaxAttempts = Math.Clamp(_options.MaxPolicyAccountingAttempts, 1, 20),
            ClaimLeaseMinutes = Math.Clamp(_options.PolicyAccountingClaimLeaseMinutes, 1, 120),
            WorkerId = _workerId
        }, cancellationToken: cancellationToken))).AsList();
    }

    private async Task ProcessAsync(ISqlConnectionFactory connectionFactory, IPolicyAccountingRepository accounting, ClaimedEvent item, CancellationToken cancellationToken)
    {
        try
        {
            await accounting.ProcessPolicyCreatedEventAsync(item.PolicyCreatedEventId, item.TenantId, cancellationToken);
            using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
UPDATE Accounting.PolicyCreatedEvent
SET StatusCode=N'Completed',ProcessedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME()
WHERE PolicyCreatedEventId=@PolicyCreatedEventId AND TenantId=@TenantId AND StatusCode=N'Processing' AND WorkerId=@WorkerId AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52310,N'Policy accounting claim is no longer active.',1;
""";
            await connection.ExecuteAsync(new CommandDefinition(sql, new { item.PolicyCreatedEventId, item.TenantId, WorkerId = _workerId }, cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Policy accounting event {EventId} failed.", item.PolicyCreatedEventId);
            using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
UPDATE Accounting.PolicyCreatedEvent
SET StatusCode=N'Failed',NextAttemptDateUtc=DATEADD(minute,POWER(2,CASE WHEN AttemptCount>6 THEN 6 ELSE AttemptCount END),SYSUTCDATETIME()),ErrorDetails=LEFT(@ErrorDetails,4000),ModifiedDateUtc=SYSUTCDATETIME()
WHERE PolicyCreatedEventId=@PolicyCreatedEventId AND TenantId=@TenantId AND StatusCode=N'Processing' AND WorkerId=@WorkerId AND IsDeleted=0;
INSERT Accounting.PolicyAccountingWorkItem(PolicyAccountingWorkItemId,TenantId,PolicyId,PolicyCreatedEventId,WorkItemTypeCode,QueueCode,Title,ReferenceNumber,Amount,PriorityCode,StatusCode,DueDateUtc,AssignedToUserId,DetailUrl,Notes,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),TenantId,PolicyId,PolicyCreatedEventId,N'AccountingFailure',N'accounting-failures',N'Policy accounting requires attention',CONCAT(N'PA-',RIGHT(CONVERT(NVARCHAR(36),PolicyCreatedEventId),8)),0,N'High',N'Open',SYSUTCDATETIME(),CreatedByUserId,CONCAT(N'/policies/',CONVERT(NVARCHAR(36),PolicyId)),LEFT(@ErrorDetails,2000),SYSUTCDATETIME(),CreatedByUserId,0
FROM Accounting.PolicyCreatedEvent e
WHERE e.PolicyCreatedEventId=@PolicyCreatedEventId AND e.TenantId=@TenantId AND e.AttemptCount>=@MaxAttempts
  AND NOT EXISTS(SELECT 1 FROM Accounting.PolicyAccountingWorkItem w WHERE w.TenantId=e.TenantId AND w.PolicyId=e.PolicyId AND w.WorkItemTypeCode=N'AccountingFailure' AND w.IsDeleted=0);
""";
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                item.PolicyCreatedEventId,
                item.TenantId,
                WorkerId = _workerId,
                ErrorDetails = ex.Message,
                MaxAttempts = Math.Clamp(_options.MaxPolicyAccountingAttempts, 1, 20)
            }, cancellationToken: cancellationToken));
        }
    }

    private sealed record ClaimedEvent(Guid PolicyCreatedEventId, Guid TenantId, Guid PolicyId, Guid? CreatedByUserId);
}
