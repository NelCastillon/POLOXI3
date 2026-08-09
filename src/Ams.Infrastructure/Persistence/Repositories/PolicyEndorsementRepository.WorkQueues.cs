using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyEndorsements;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed partial class PolicyEndorsementRepository
{
    public async Task<IReadOnlyList<PolicyEndorsementCarrierDispatchWorkItem>> ClaimCarrierDispatchesAsync(string workerId, int take, TimeSpan lease, CancellationToken cancellationToken = default)
    {
        const string sql = """
DECLARE @Claimed TABLE(CarrierDispatchId UNIQUEIDENTIFIER);
;WITH next_batch AS
(
    SELECT TOP (@Take) dispatch.*
    FROM Policy.PolicyEndorsementCarrierDispatch dispatch WITH(UPDLOCK,READPAST,ROWLOCK)
    WHERE dispatch.IsDeleted=0 AND dispatch.AttemptCount<dispatch.MaxAttempts
      AND dispatch.ChannelCode IN(N'Api',N'CarrierApi')
      AND ((dispatch.StatusCode IN(N'Queued',N'Failed') AND COALESCE(dispatch.NextAttemptDateUtc,dispatch.CreatedDateUtc)<=SYSUTCDATETIME())
        OR (dispatch.StatusCode=N'Processing' AND dispatch.ClaimExpiresDateUtc<SYSUTCDATETIME()))
    ORDER BY COALESCE(dispatch.NextAttemptDateUtc,dispatch.CreatedDateUtc),dispatch.CreatedDateUtc
)
UPDATE next_batch
SET StatusCode=N'Processing',AttemptCount=AttemptCount+1,ClaimedBy=@WorkerId,
    ClaimExpiresDateUtc=DATEADD(second,@LeaseSeconds,SYSUTCDATETIME()),LastAttemptDateUtc=SYSUTCDATETIME(),
    ErrorCode=NULL,ErrorMessage=NULL,ModifiedDateUtc=SYSUTCDATETIME()
OUTPUT inserted.CarrierDispatchId INTO @Claimed;
SELECT dispatch.CarrierDispatchId,dispatch.TenantId,dispatch.EndorsementId,endorsement.PolicyId,
       dispatch.CarrierConfigurationId,dispatch.ChannelCode,dispatch.IdempotencyKey,
       COALESCE(dispatch.RequestPayload,N'{}') RequestPayload,dispatch.AttemptCount,dispatch.MaxAttempts,
       configuration.EndpointUri,configuration.HttpMethod,configuration.AuthenticationTypeCode,configuration.SecretReference,
       configuration.SenderAddress,configuration.RecipientAddress,configuration.PortalInstructions,
       configuration.PayloadTemplate,configuration.HeaderTemplate,COALESCE(configuration.TimeoutSeconds,100) TimeoutSeconds
FROM @Claimed claimed
JOIN Policy.PolicyEndorsementCarrierDispatch dispatch ON dispatch.CarrierDispatchId=claimed.CarrierDispatchId
JOIN Policy.PolicyEndorsement endorsement ON endorsement.TenantId=dispatch.TenantId AND endorsement.EndorsementId=dispatch.EndorsementId
LEFT JOIN Policy.PolicyEndorsementCarrierConfiguration configuration ON configuration.TenantId=dispatch.TenantId AND configuration.CarrierConfigurationId=dispatch.CarrierConfigurationId AND configuration.IsDeleted=0;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<PolicyEndorsementCarrierDispatchWorkItem>(new CommandDefinition(sql, new
        {
            WorkerId = workerId,
            Take = Math.Clamp(take, 1, 100),
            LeaseSeconds = Math.Clamp((int)lease.TotalSeconds, 30, 7200)
        }, cancellationToken: cancellationToken))).AsList();
    }

    public Task CompleteCarrierDispatchAsync(Guid dispatchId, string workerId, CompletePolicyEndorsementCarrierDispatch result, CancellationToken cancellationToken = default)
        => ExecuteCarrierCompletionAsync(dispatchId, workerId, result, cancellationToken);

    private async Task ExecuteCarrierCompletionAsync(Guid dispatchId, string workerId, CompletePolicyEndorsementCarrierDispatch result, CancellationToken cancellationToken)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @TenantId UNIQUEIDENTIFIER,@EndorsementId UNIQUEIDENTIFIER,@PolicyId UNIQUEIDENTIFIER;
UPDATE dispatch SET StatusCode=@StatusCode,ExternalReferenceNumber=@ExternalReferenceNumber,ResponsePayload=@ResponsePayload,
    CompletedDateUtc=SYSUTCDATETIME(),NextAttemptDateUtc=NULL,ClaimExpiresDateUtc=NULL,ErrorCode=NULL,ErrorMessage=NULL,ModifiedDateUtc=SYSUTCDATETIME()
FROM Policy.PolicyEndorsementCarrierDispatch dispatch
WHERE dispatch.CarrierDispatchId=@DispatchId AND dispatch.StatusCode=N'Processing' AND dispatch.ClaimedBy=@WorkerId AND dispatch.IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52500,N'The carrier dispatch claim is no longer active.',1;
SELECT @TenantId=dispatch.TenantId,@EndorsementId=dispatch.EndorsementId,@PolicyId=endorsement.PolicyId
FROM Policy.PolicyEndorsementCarrierDispatch dispatch JOIN Policy.PolicyEndorsement endorsement ON endorsement.TenantId=dispatch.TenantId AND endorsement.EndorsementId=dispatch.EndorsementId
WHERE dispatch.CarrierDispatchId=@DispatchId;
INSERT Policy.PolicyEndorsementCarrierAttempt(CarrierAttemptId,TenantId,CarrierDispatchId,AttemptNumber,StatusCode,RequestPayload,ResponsePayload,HttpStatusCode,StartedDateUtc,CompletedDateUtc)
SELECT NEWID(),TenantId,CarrierDispatchId,AttemptCount,@StatusCode,RequestPayload,@ResponsePayload,@HttpStatusCode,COALESCE(LastAttemptDateUtc,SYSUTCDATETIME()),SYSUTCDATETIME()
FROM Policy.PolicyEndorsementCarrierDispatch WHERE CarrierDispatchId=@DispatchId;
UPDATE Policy.PolicyEndorsement SET CarrierReferenceNumber=COALESCE(@ExternalReferenceNumber,CarrierReferenceNumber),ModifiedDateUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId;
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,Description,DataJson,CorrelationId,OccurredDateUtc)
VALUES(NEWID(),@TenantId,@EndorsementId,@PolicyId,N'CarrierDispatchCompleted',N'Carrier submission completed.',JSON_OBJECT(N'dispatchId':@DispatchId,N'externalReference':@ExternalReferenceNumber),NEWID(),SYSUTCDATETIME());
COMMIT;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { DispatchId = dispatchId, WorkerId = workerId, result.StatusCode, result.ExternalReferenceNumber, result.ResponsePayload, result.HttpStatusCode }, cancellationToken: cancellationToken));
    }

    public async Task FailCarrierDispatchAsync(Guid dispatchId, string workerId, FailPolicyEndorsementCarrierDispatch result, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
UPDATE Policy.PolicyEndorsementCarrierDispatch
SET StatusCode=CASE WHEN @IsRetryable=1 AND AttemptCount<MaxAttempts THEN N'Failed' ELSE N'DeadLetter' END,
    NextAttemptDateUtc=CASE WHEN @IsRetryable=1 AND AttemptCount<MaxAttempts THEN COALESCE(@RetryAtUtc,DATEADD(minute,POWER(2,CASE WHEN AttemptCount>6 THEN 6 ELSE AttemptCount END),SYSUTCDATETIME())) END,
    ResponsePayload=@ResponsePayload,ErrorCode=@ErrorCode,ErrorMessage=LEFT(@ErrorMessage,2000),ClaimExpiresDateUtc=NULL,ModifiedDateUtc=SYSUTCDATETIME()
WHERE CarrierDispatchId=@DispatchId AND StatusCode=N'Processing' AND ClaimedBy=@WorkerId AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52501,N'The carrier dispatch claim is no longer active.',1;
INSERT Policy.PolicyEndorsementCarrierAttempt(CarrierAttemptId,TenantId,CarrierDispatchId,AttemptNumber,StatusCode,RequestPayload,ResponsePayload,HttpStatusCode,ErrorCode,ErrorMessage,StartedDateUtc,CompletedDateUtc)
SELECT NEWID(),TenantId,CarrierDispatchId,AttemptCount,StatusCode,RequestPayload,@ResponsePayload,@HttpStatusCode,@ErrorCode,LEFT(@ErrorMessage,2000),COALESCE(LastAttemptDateUtc,SYSUTCDATETIME()),SYSUTCDATETIME()
FROM Policy.PolicyEndorsementCarrierDispatch WHERE CarrierDispatchId=@DispatchId;
COMMIT;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { DispatchId = dispatchId, WorkerId = workerId, result.ErrorCode, result.ErrorMessage, result.IsRetryable, result.RetryAtUtc, result.ResponsePayload, result.HttpStatusCode }, cancellationToken: cancellationToken));
    }

    public Task<IReadOnlyList<PolicyEndorsementAccountingWorkItem>> ClaimAccountingWorkAsync(string workerId, int take, TimeSpan lease, CancellationToken cancellationToken = default)
        => ClaimWorkAsync<PolicyEndorsementAccountingWorkItem>("Accounting", workerId, take, lease, cancellationToken);

    public Task<IReadOnlyList<PolicyEndorsementDocumentWorkItem>> ClaimDocumentWorkAsync(string workerId, int take, TimeSpan lease, CancellationToken cancellationToken = default)
        => ClaimWorkAsync<PolicyEndorsementDocumentWorkItem>("Document", workerId, take, lease, cancellationToken);

    private async Task<IReadOnlyList<T>> ClaimWorkAsync<T>(string kind, string workerId, int take, TimeSpan lease, CancellationToken cancellationToken)
    {
        var table = kind == "Accounting" ? "Policy.PolicyEndorsementAccountingWork" : "Policy.PolicyEndorsementDocumentWork";
        var id = kind == "Accounting" ? "AccountingWorkId" : "DocumentWorkId";
        var sql = $"""
DECLARE @Claimed TABLE(Id UNIQUEIDENTIFIER);
;WITH next_batch AS
(
    SELECT TOP (@Take) * FROM {table} WITH(UPDLOCK,READPAST,ROWLOCK)
    WHERE IsDeleted=0 AND AttemptCount<MaxAttempts
      AND ((StatusCode IN(N'Queued',N'Failed') AND COALESCE(NextAttemptDateUtc,CreatedDateUtc)<=SYSUTCDATETIME())
        OR (StatusCode=N'Processing' AND ClaimExpiresDateUtc<SYSUTCDATETIME()))
    ORDER BY COALESCE(NextAttemptDateUtc,CreatedDateUtc),CreatedDateUtc
)
UPDATE next_batch SET StatusCode=N'Processing',AttemptCount=AttemptCount+1,ClaimedBy=@WorkerId,
    ClaimExpiresDateUtc=DATEADD(second,@LeaseSeconds,SYSUTCDATETIME()),ErrorMessage=NULL,ModifiedDateUtc=SYSUTCDATETIME()
OUTPUT inserted.{id} INTO @Claimed;
SELECT work.* FROM {table} work JOIN @Claimed claimed ON claimed.Id=work.{id};
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<T>(new CommandDefinition(sql, new
        {
            WorkerId = workerId,
            Take = Math.Clamp(take, 1, 100),
            LeaseSeconds = Math.Clamp((int)lease.TotalSeconds, 30, 7200)
        }, cancellationToken: cancellationToken))).AsList();
    }

    public Task CompleteAccountingWorkAsync(Guid workId, string workerId, CompletePolicyEndorsementAccountingWork result, CancellationToken cancellationToken = default)
        => CompleteWorkAsync("Accounting", workId, workerId, result.ResultEntityName, result.ResultEntityId, cancellationToken);

    public Task CompleteDocumentWorkAsync(Guid workId, string workerId, CompletePolicyEndorsementDocumentWork result, CancellationToken cancellationToken = default)
        => CompleteWorkAsync("Document", workId, workerId, "Document", result.DocumentId, cancellationToken);

    private async Task CompleteWorkAsync(string kind, Guid workId, string workerId, string resultEntityName, Guid resultEntityId, CancellationToken cancellationToken)
    {
        var table = kind == "Accounting" ? "Policy.PolicyEndorsementAccountingWork" : "Policy.PolicyEndorsementDocumentWork";
        var id = kind == "Accounting" ? "AccountingWorkId" : "DocumentWorkId";
        var resultColumns = kind == "Accounting" ? "ResultEntityName=@ResultEntityName,ResultEntityId=@ResultEntityId," : "DocumentId=@ResultEntityId,";
        var eventType = kind == "Accounting" ? "AccountingCompleted" : "DocumentsGenerated";
        var downstreamSql = kind == "Accounting"
            ? """
INSERT Policy.PolicyEndorsementDocumentWork(DocumentWorkId,TenantId,EndorsementId,PolicyId,DocumentTypeCode,IdempotencyKey,StatusCode,AttemptCount,MaxAttempts,NextAttemptDateUtc,CreatedDateUtc,IsDeleted)
SELECT NEWID(),@TenantId,@EndorsementId,@PolicyId,definition.DocumentTypeCode,CONCAT(N'endorsement:',CONVERT(NVARCHAR(36),@EndorsementId),N':document:',definition.DocumentTypeCode),N'Queued',0,5,SYSUTCDATETIME(),SYSUTCDATETIME(),0
FROM Policy.PolicyEndorsementDocumentWorkDefinition definition
WHERE definition.TenantId=@TenantId AND definition.TriggerCode=N'AccountingCompleted' AND definition.IsActive=1 AND definition.IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementDocumentWork existing WHERE existing.TenantId=@TenantId AND existing.IdempotencyKey=CONCAT(N'endorsement:',CONVERT(NVARCHAR(36),@EndorsementId),N':document:',definition.DocumentTypeCode) AND existing.IsDeleted=0);
"""
            : string.Empty;
        var sql = $"""
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @TenantId UNIQUEIDENTIFIER,@EndorsementId UNIQUEIDENTIFIER,@PolicyId UNIQUEIDENTIFIER;
UPDATE {table} SET StatusCode=N'Completed',{resultColumns}CompletedDateUtc=SYSUTCDATETIME(),ClaimExpiresDateUtc=NULL,NextAttemptDateUtc=NULL,ModifiedDateUtc=SYSUTCDATETIME()
WHERE {id}=@WorkId AND StatusCode=N'Processing' AND ClaimedBy=@WorkerId AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52502,N'The endorsement work claim is no longer active.',1;
SELECT @TenantId=TenantId,@EndorsementId=EndorsementId,@PolicyId=PolicyId FROM {table} WHERE {id}=@WorkId;
{downstreamSql}
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,Description,DataJson,CorrelationId,OccurredDateUtc)
VALUES(NEWID(),@TenantId,@EndorsementId,@PolicyId,N'{eventType}',N'Endorsement {kind.ToLowerInvariant()} work completed.',JSON_OBJECT(N'workId':@WorkId,N'resultEntityId':@ResultEntityId),NEWID(),SYSUTCDATETIME());
COMMIT;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { WorkId = workId, WorkerId = workerId, ResultEntityName = resultEntityName, ResultEntityId = resultEntityId }, cancellationToken: cancellationToken));
    }

    public Task FailAccountingWorkAsync(Guid workId, string workerId, FailPolicyEndorsementWork result, CancellationToken cancellationToken = default)
        => FailWorkAsync("Accounting", workId, workerId, result, cancellationToken);

    public Task FailDocumentWorkAsync(Guid workId, string workerId, FailPolicyEndorsementWork result, CancellationToken cancellationToken = default)
        => FailWorkAsync("Document", workId, workerId, result, cancellationToken);

    private async Task FailWorkAsync(string kind, Guid workId, string workerId, FailPolicyEndorsementWork result, CancellationToken cancellationToken)
    {
        var table = kind == "Accounting" ? "Policy.PolicyEndorsementAccountingWork" : "Policy.PolicyEndorsementDocumentWork";
        var id = kind == "Accounting" ? "AccountingWorkId" : "DocumentWorkId";
        var sql = $"""
UPDATE {table} SET StatusCode=CASE WHEN @IsRetryable=1 AND AttemptCount<MaxAttempts THEN N'Failed' ELSE N'DeadLetter' END,
    NextAttemptDateUtc=CASE WHEN @IsRetryable=1 AND AttemptCount<MaxAttempts THEN COALESCE(@RetryAtUtc,DATEADD(minute,POWER(2,CASE WHEN AttemptCount>6 THEN 6 ELSE AttemptCount END),SYSUTCDATETIME())) END,
    ErrorMessage=LEFT(@ErrorMessage,2000),ClaimExpiresDateUtc=NULL,ModifiedDateUtc=SYSUTCDATETIME()
WHERE {id}=@WorkId AND StatusCode=N'Processing' AND ClaimedBy=@WorkerId AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52503,N'The endorsement work claim is no longer active.',1;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { WorkId = workId, WorkerId = workerId, result.ErrorMessage, result.IsRetryable, result.RetryAtUtc }, cancellationToken: cancellationToken));
    }
}
