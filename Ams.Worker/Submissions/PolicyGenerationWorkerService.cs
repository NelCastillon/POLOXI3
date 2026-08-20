using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Submissions;
using Ams.Worker.Automation;
using Dapper;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Submissions;

public sealed class PolicyGenerationWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<PolicyGenerationWorkerService> _logger;

    public PolicyGenerationWorkerService(IServiceProvider serviceProvider, IOptions<WorkerOptions> options, ILogger<PolicyGenerationWorkerService> logger)
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
                var policyCreation = scope.ServiceProvider.GetRequiredService<IPolicyCreationService>();
                var requests = await ClaimAsync(connectionFactory, stoppingToken);
                foreach (var request in requests)
                {
                    await ProcessAsync(connectionFactory, policyCreation, request, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Policy generation polling cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, _options.PolicyGenerationPollIntervalSeconds)), stoppingToken);
        }
    }

    private async Task<IReadOnlyList<ClaimedGeneration>> ClaimAsync(ISqlConnectionFactory connectionFactory, CancellationToken cancellationToken)
    {
        const string sql = """
DECLARE @Claimed TABLE(PolicyGenerationRequestId UNIQUEIDENTIFIER,TenantId UNIQUEIDENTIFIER,PolicyBindTransactionId UNIQUEIDENTIFIER,RequestedByUserId UNIQUEIDENTIFIER NULL);
;WITH next_batch AS
(
 SELECT TOP (@BatchSize) * FROM Submissions.PolicyGenerationRequest WITH(UPDLOCK,READPAST,ROWLOCK)
 WHERE ((StatusCode IN(N'Queued',N'Failed') AND COALESCE(NextAttemptDateUtc,RequestedDateUtc)<=SYSUTCDATETIME()) OR (StatusCode=N'Processing' AND ProcessingStartedDateUtc<DATEADD(minute,-@ClaimLeaseMinutes,SYSUTCDATETIME())))
   AND IsDeleted=0 AND AttemptCount<@MaxAttempts
 ORDER BY RequestedDateUtc
)
UPDATE next_batch SET StatusCode=N'Processing',ProcessingStartedDateUtc=SYSUTCDATETIME(),AttemptCount=AttemptCount+1,WorkerId=@WorkerId,ErrorDetails=NULL,ModifiedDateUtc=SYSUTCDATETIME()
OUTPUT inserted.PolicyGenerationRequestId,inserted.TenantId,inserted.PolicyBindTransactionId,inserted.RequestedByUserId INTO @Claimed;
SELECT * FROM @Claimed;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ClaimedGeneration>(new CommandDefinition(sql, new { BatchSize = Math.Clamp(_options.MaxPolicyGenerationsPerPoll, 1, 100), MaxAttempts = Math.Clamp(_options.MaxPolicyGenerationAttempts, 1, 20), ClaimLeaseMinutes = Math.Clamp(_options.PolicyGenerationClaimLeaseMinutes, 1, 120), WorkerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}" }, cancellationToken: cancellationToken))).AsList();
    }

    private async Task ProcessAsync(ISqlConnectionFactory connectionFactory, IPolicyCreationService policyCreation, ClaimedGeneration request, CancellationToken cancellationToken)
    {
        try
        {
            var policyId = await policyCreation.CreatePolicyFromConfirmedBindAsync(new PolicyCreationFromConfirmedBindRequest(request.TenantId, request.PolicyBindTransactionId, request.RequestedByUserId), cancellationToken);
            using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
SET XACT_ABORT ON;
BEGIN TRANSACTION;
UPDATE Submissions.PolicyGenerationRequest SET StatusCode=N'Completed',PolicyId=@PolicyId,CompletedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME() WHERE PolicyGenerationRequestId=@PolicyGenerationRequestId AND TenantId=@TenantId AND StatusCode=N'Processing' AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52220,N'Policy generation claim is no longer active.',1;
UPDATE Submissions.BinderReview SET StatusCode=N'PolicyCreated',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@RequestedByUserId WHERE PolicyBindTransactionId=@PolicyBindTransactionId AND TenantId=@TenantId AND IsDeleted=0;
UPDATE Submissions.PolicyBindTransaction SET BindStatusCode=N'PolicyCreated',PolicyId=@PolicyId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@RequestedByUserId WHERE PolicyBindTransactionId=@PolicyBindTransactionId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Submissions.BindStatusHistory(BindStatusHistoryId,TenantId,PolicyBindTransactionId,OldStatusCode,NewStatusCode,Comments,ChangedDateUtc,ChangedByUserId,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@PolicyBindTransactionId,N'PolicyGenerationQueued',N'PolicyCreated',N'Background policy generation completed.',SYSUTCDATETIME(),@RequestedByUserId,SYSUTCDATETIME(),@RequestedByUserId,0);
INSERT INTO Core.Notification(NotificationId,TenantId,RecipientUserId,ChannelCode,Subject,Body,EntityName,EntityId,StatusCode,IsRead,Priority,Category,DeliveryProvider,DeliveryStatus,PolicyStatus,SyncStatus,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),@TenantId,@RequestedByUserId,N'InApp',N'Policy generated',N'The accepted carrier binder was converted into a policy workspace.',N'Policy',@PolicyId,N'Pending',0,N'Normal',N'Policy',N'Internal',N'Pending',N'Active',N'Pending',SYSUTCDATETIME(),@RequestedByUserId,0 WHERE @RequestedByUserId IS NOT NULL;
COMMIT;
""";
            await connection.ExecuteAsync(new CommandDefinition(sql, new { request.PolicyGenerationRequestId, request.TenantId, request.PolicyBindTransactionId, request.RequestedByUserId, PolicyId = policyId }, cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Policy generation request {RequestId} failed.", request.PolicyGenerationRequestId);
            using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            const string sql = """
UPDATE Submissions.PolicyGenerationRequest SET StatusCode=N'Failed',FailedDateUtc=SYSUTCDATETIME(),NextAttemptDateUtc=DATEADD(minute,POWER(2,CASE WHEN AttemptCount>6 THEN 6 ELSE AttemptCount END),SYSUTCDATETIME()),ErrorDetails=LEFT(@ErrorDetails,4000),ModifiedDateUtc=SYSUTCDATETIME() WHERE PolicyGenerationRequestId=@PolicyGenerationRequestId AND TenantId=@TenantId AND StatusCode=N'Processing' AND IsDeleted=0;
INSERT INTO Core.Notification(NotificationId,TenantId,RecipientUserId,ChannelCode,Subject,Body,EntityName,EntityId,StatusCode,IsRead,Priority,Category,DeliveryProvider,DeliveryStatus,PolicyStatus,SyncStatus,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),@TenantId,RequestedByUserId,N'InApp',N'Policy generation requires attention',CONCAT(N'Policy generation failed after ',AttemptCount,N' attempts: ',LEFT(@ErrorDetails,1000)),N'PolicyGenerationRequest',PolicyGenerationRequestId,N'Pending',0,N'High',N'Policy',N'Internal',N'Pending',N'Active',N'Pending',SYSUTCDATETIME(),RequestedByUserId,0 FROM Submissions.PolicyGenerationRequest WHERE PolicyGenerationRequestId=@PolicyGenerationRequestId AND TenantId=@TenantId AND AttemptCount>=@MaxAttempts AND RequestedByUserId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Core.Notification WHERE TenantId=@TenantId AND EntityName=N'PolicyGenerationRequest' AND EntityId=@PolicyGenerationRequestId AND Subject=N'Policy generation requires attention' AND IsDeleted=0);
""";
            await connection.ExecuteAsync(new CommandDefinition(sql, new { request.PolicyGenerationRequestId, request.TenantId, ErrorDetails = ex.Message, MaxAttempts = Math.Clamp(_options.MaxPolicyGenerationAttempts, 1, 20) }, cancellationToken: cancellationToken));
        }
    }

    private sealed record ClaimedGeneration(Guid PolicyGenerationRequestId, Guid TenantId, Guid PolicyBindTransactionId, Guid? RequestedByUserId);
}