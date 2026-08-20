using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;
using Dapper;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ESignRepository : IESignRepository
{
    private const string SelectColumns = @"
        e.ESignRequestId, e.TenantId, e.DocumentId, e.PolicyId,
        COALESCE(d.FileName, 'Document unavailable') AS Document, policy.PolicyNumber,
        e.SignerName, e.SignerEmail, e.Priority, e.Status,
        e.ProviderCode, e.ProviderEnvelopeId, e.ProviderStatus, e.IdempotencyKey,
        CASE WHEN e.Status IN ('Sent','Viewed') AND e.DueDate < GETUTCDATE() THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsOverdue,
        e.SentDate, e.DueDate, e.CompletedDate, e.Message, e.VoidReason";

    private readonly ISqlConnectionFactory _connectionFactory;
    public ESignRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<ESignRequestDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {SelectColumns}
FROM DMS.ESignRequest e
LEFT JOIN DMS.Document d ON d.DocumentId = e.DocumentId
LEFT JOIN Submissions.BoundPolicy policy ON policy.TenantId=e.TenantId AND policy.PolicyId=e.PolicyId AND policy.IsDeleted=0
WHERE e.TenantId = @TenantId AND e.IsDeleted = 0
ORDER BY e.SentDate DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var result = await cn.QueryAsync<ESignRequestDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<ESignRequestDto?> GetByIdAsync(Guid tenantId, Guid eSignRequestId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {SelectColumns}
FROM DMS.ESignRequest e
LEFT JOIN DMS.Document d ON d.DocumentId = e.DocumentId
LEFT JOIN Submissions.BoundPolicy policy ON policy.TenantId=e.TenantId AND policy.PolicyId=e.PolicyId AND policy.IsDeleted=0
WHERE e.TenantId=@TenantId AND e.ESignRequestId = @Id AND e.IsDeleted = 0;
SELECT ESignSignerId,TenantId,ESignRequestId,RoutingOrder,SignerName,SignerEmail,StatusCode,ViewedDateUtc,SignedDateUtc,DeclinedDateUtc,DeclineReason FROM DMS.ESignSigner WHERE TenantId=@TenantId AND ESignRequestId=@Id AND IsDeleted=0 ORDER BY RoutingOrder;
SELECT ESignEnvelopeEventId,TenantId,ESignRequestId,ProviderEventId,EventTypeCode,ProviderStatus,IsSignatureVerified,OccurredDateUtc,ReceivedDateUtc FROM DMS.ESignEnvelopeEvent WHERE TenantId=@TenantId AND ESignRequestId=@Id ORDER BY OccurredDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var grid = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, Id = eSignRequestId }, cancellationToken: cancellationToken));
        var item = await grid.ReadSingleOrDefaultAsync<ESignRequestDto>();
        if (item is null) return null;
        return WithDetails(item, (await grid.ReadAsync<ESignSignerDto>()).AsList(), (await grid.ReadAsync<ESignEnvelopeEventDto>()).AsList());
    }

    public async Task<Guid> SendAsync(SendESignRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var sql = @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @ExistingId UNIQUEIDENTIFIER=(SELECT ESignRequestId FROM DMS.ESignRequest WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND IdempotencyKey=@IdempotencyKey AND IsDeleted=0);
IF @ExistingId IS NOT NULL BEGIN SELECT @ExistingId; COMMIT; RETURN; END;
IF NOT EXISTS(SELECT 1 FROM DMS.Document WHERE TenantId=@TenantId AND DocumentId=@DocumentId AND IsDeleted=0) THROW 52300,N'The e-sign document was not found in the tenant.',1;
IF @PolicyId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Submissions.BoundPolicy WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0) THROW 52301,N'The e-sign policy was not found in the tenant.',1;
DECLARE @Configured BIT=CASE WHEN EXISTS(SELECT 1 FROM DMS.ESignProviderConfiguration WHERE TenantId=@TenantId AND ProviderCode=N'DocuSign' AND IsEnabled=1 AND IsConfigured=1 AND IsDeleted=0) THEN 1 ELSE 0 END;
DECLARE @Status NVARCHAR(80)=CASE WHEN @Configured=1 THEN N'Queued' ELSE N'ConfigurationRequired' END;
DECLARE @MaxAttempts INT=COALESCE((SELECT TOP 1 MaxAttempts FROM DMS.ESignProviderConfiguration WHERE TenantId=@TenantId AND ProviderCode=N'DocuSign' AND IsDeleted=0),5);
INSERT DMS.ESignRequest(ESignRequestId,TenantId,DocumentId,PolicyId,SignerName,SignerEmail,Priority,Status,SentDate,DueDate,Message,ProviderCode,IdempotencyKey,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(@ESignRequestId,@TenantId,@DocumentId,@PolicyId,@SignerName,@SignerEmail,@Priority,@Status,SYSUTCDATETIME(),@DueDate,@Message,N'DocuSign',@IdempotencyKey,SYSUTCDATETIME(),@RequestedByUserId,0);
INSERT DMS.ESignSigner(ESignSignerId,TenantId,ESignRequestId,RoutingOrder,SignerName,SignerEmail,StatusCode,CreatedDateUtc,IsDeleted) VALUES(NEWID(),@TenantId,@ESignRequestId,1,@SignerName,@SignerEmail,N'Created',SYSUTCDATETIME(),0);
INSERT DMS.ESignDispatch(ESignDispatchId,TenantId,ESignRequestId,StatusCode,AttemptCount,MaxAttempts,NextAttemptDateUtc,CreatedDateUtc,IsDeleted) VALUES(NEWID(),@TenantId,@ESignRequestId,@Status,0,@MaxAttempts,CASE WHEN @Status=N'Queued' THEN SYSUTCDATETIME() END,SYSUTCDATETIME(),0);
INSERT DMS.ESignEnvelopeEvent(ESignEnvelopeEventId,TenantId,ESignRequestId,EventTypeCode,ProviderStatus,IsSignatureVerified,OccurredDateUtc,ReceivedDateUtc) VALUES(NEWID(),@TenantId,@ESignRequestId,N'Created',@Status,1,SYSUTCDATETIME(),SYSUTCDATETIME());
SELECT @ESignRequestId; COMMIT;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { ESignRequestId=id, request.TenantId, request.DocumentId, request.PolicyId, request.SignerName, request.SignerEmail, request.Priority, request.DueDate, request.Message, request.IdempotencyKey, request.RequestedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task VoidAsync(VoidESignRequest request, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE DMS.ESignRequest
SET Status = 'Voided', VoidReason = @VoidReason, ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId=@ModifiedByUserId
WHERE TenantId=@TenantId AND ESignRequestId = @ESignRequestId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task RemindAsync(Guid tenantId, Guid eSignRequestId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE DMS.ESignRequest
SET LastReminderSentDateUtc = GETUTCDATE(), ModifiedDateUtc = GETUTCDATE(), ModifiedByUserId=@ModifiedByUserId
WHERE TenantId=@TenantId AND ESignRequestId = @ESignRequestId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId=tenantId, ESignRequestId = eSignRequestId, ModifiedByUserId=modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task ProcessDocuSignCallbackAsync(ProcessDocuSignCallbackRequest request, CancellationToken cancellationToken = default)
    {
        const string configSql = "SELECT TOP 1 ConnectHmacSecretReference FROM DMS.ESignProviderConfiguration WHERE TenantId=@TenantId AND ProviderCode=N'DocuSign' AND IsEnabled=1 AND IsConfigured=1 AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var secretReference = await cn.ExecuteScalarAsync<string?>(new CommandDefinition(configSql, new { request.TenantId }, cancellationToken: cancellationToken));
        var secret = string.IsNullOrWhiteSpace(secretReference) ? null : Environment.GetEnvironmentVariable(secretReference);
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(request.Signature)) throw new UnauthorizedAccessException("DocuSign callback signing is not configured.");
        var expected = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(request.Payload)));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(request.Signature.Trim()))) throw new UnauthorizedAccessException("DocuSign callback signature is invalid.");

        using var json = JsonDocument.Parse(request.Payload);
        var root = json.RootElement;
        var envelopeId = root.TryGetProperty("envelopeId", out var envelope) ? envelope.GetString() : null;
        var status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
        var eventId = root.TryGetProperty("eventId", out var eventElement) ? eventElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(envelopeId) || string.IsNullOrWhiteSpace(status)) throw new InvalidOperationException("DocuSign callback is missing envelopeId or status.");
        var normalizedStatus = status.Equals("completed", StringComparison.OrdinalIgnoreCase) ? "Completed" : status.Equals("declined", StringComparison.OrdinalIgnoreCase) ? "Declined" : status.Equals("voided", StringComparison.OrdinalIgnoreCase) ? "Voided" : status.Equals("delivered", StringComparison.OrdinalIgnoreCase) ? "Viewed" : "Sent";
        const string sql = @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @RequestId UNIQUEIDENTIFIER=(SELECT ESignRequestId FROM DMS.ESignRequest WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND ProviderEnvelopeId=@EnvelopeId AND IsDeleted=0);
IF @RequestId IS NULL THROW 52302,N'The DocuSign envelope is not registered for this tenant.',1;
IF @EventId IS NOT NULL AND EXISTS(SELECT 1 FROM DMS.ESignEnvelopeEvent WHERE TenantId=@TenantId AND ProviderEventId=@EventId) BEGIN COMMIT; RETURN; END;
UPDATE DMS.ESignRequest SET Status=@NormalizedStatus,ProviderStatus=@ProviderStatus,LastProviderEventDateUtc=SYSUTCDATETIME(),CompletedDate=CASE WHEN @NormalizedStatus=N'Completed' THEN SYSUTCDATETIME() ELSE CompletedDate END,ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND ESignRequestId=@RequestId;
UPDATE DMS.ESignSigner SET StatusCode=@NormalizedStatus,ViewedDateUtc=CASE WHEN @NormalizedStatus=N'Viewed' AND ViewedDateUtc IS NULL THEN SYSUTCDATETIME() ELSE ViewedDateUtc END,SignedDateUtc=CASE WHEN @NormalizedStatus=N'Completed' THEN SYSUTCDATETIME() ELSE SignedDateUtc END,DeclinedDateUtc=CASE WHEN @NormalizedStatus=N'Declined' THEN SYSUTCDATETIME() ELSE DeclinedDateUtc END WHERE TenantId=@TenantId AND ESignRequestId=@RequestId AND IsDeleted=0;
INSERT DMS.ESignEnvelopeEvent(ESignEnvelopeEventId,TenantId,ESignRequestId,ProviderEventId,EventTypeCode,ProviderStatus,PayloadJson,IsSignatureVerified,OccurredDateUtc,ReceivedDateUtc) VALUES(NEWID(),@TenantId,@RequestId,@EventId,N'ProviderStatusChanged',@ProviderStatus,@Payload,1,SYSUTCDATETIME(),SYSUTCDATETIME()); COMMIT;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.TenantId, EnvelopeId=envelopeId, ProviderStatus=status, NormalizedStatus=normalizedStatus, EventId=eventId, request.Payload }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ESignDispatchWorkItem>> ClaimDispatchesAsync(string workerId, int batchSize, TimeSpan claimLease, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @Now DATETIME2=SYSUTCDATETIME(), @ClaimExpiresDateUtc DATETIME2=DATEADD(SECOND,@ClaimLeaseSeconds,SYSUTCDATETIME());
DECLARE @Claimed TABLE (ESignDispatchId UNIQUEIDENTIFIER PRIMARY KEY);
;WITH candidates AS
(
    SELECT TOP (@BatchSize) dispatch.*
    FROM DMS.ESignDispatch dispatch WITH(UPDLOCK,READPAST,ROWLOCK)
    WHERE dispatch.IsDeleted=0
      AND dispatch.AttemptCount < dispatch.MaxAttempts
      AND ((dispatch.StatusCode IN (N'Queued',N'RetryScheduled') AND (dispatch.NextAttemptDateUtc IS NULL OR dispatch.NextAttemptDateUtc<=@Now))
        OR (dispatch.StatusCode=N'Processing' AND dispatch.ClaimExpiresDateUtc<=@Now))
    ORDER BY COALESCE(dispatch.NextAttemptDateUtc,dispatch.CreatedDateUtc),dispatch.CreatedDateUtc
)
UPDATE candidates
SET StatusCode=N'Processing',AttemptCount=AttemptCount+1,ClaimedBy=@WorkerId,ClaimExpiresDateUtc=@ClaimExpiresDateUtc,LastAttemptDateUtc=@Now
OUTPUT inserted.ESignDispatchId INTO @Claimed;

SELECT dispatch.ESignDispatchId,dispatch.TenantId,dispatch.ESignRequestId,request.DocumentId,
       document.FileName,document.StoragePath,document.ContentType,request.SignerName,request.SignerEmail,request.Message,
       request.IdempotencyKey,dispatch.AttemptCount,dispatch.MaxAttempts,
       configuration.AccountId,configuration.IntegrationKey,configuration.UserId,configuration.OAuthBaseUri,
       configuration.ApiBaseUri,configuration.SecretReference
FROM @Claimed claimed
JOIN DMS.ESignDispatch dispatch ON dispatch.ESignDispatchId=claimed.ESignDispatchId
JOIN DMS.ESignRequest request ON request.TenantId=dispatch.TenantId AND request.ESignRequestId=dispatch.ESignRequestId AND request.IsDeleted=0
JOIN DMS.Document document ON document.TenantId=request.TenantId AND document.DocumentId=request.DocumentId AND document.IsDeleted=0
JOIN DMS.ESignProviderConfiguration configuration ON configuration.TenantId=request.TenantId AND configuration.ProviderCode=N'DocuSign' AND configuration.IsEnabled=1 AND configuration.IsConfigured=1 AND configuration.IsDeleted=0;
COMMIT;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await cn.QueryAsync<ESignDispatchWorkItem>(new CommandDefinition(sql, new
        {
            WorkerId = workerId,
            BatchSize = Math.Clamp(batchSize, 1, 100),
            ClaimLeaseSeconds = Math.Max(1, (int)claimLease.TotalSeconds)
        }, cancellationToken: cancellationToken));
        return items.AsList();
    }

    public async Task CompleteDispatchAsync(ESignDispatchWorkItem workItem, ESignEnvelopeDispatchResult result, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON; BEGIN TRAN;
UPDATE DMS.ESignDispatch SET StatusCode=N'Completed',CompletedDateUtc=SYSUTCDATETIME(),NextAttemptDateUtc=NULL,ClaimedBy=NULL,ClaimExpiresDateUtc=NULL,ErrorCode=NULL,ErrorMessage=NULL
WHERE TenantId=@TenantId AND ESignDispatchId=@ESignDispatchId AND ESignRequestId=@ESignRequestId AND StatusCode=N'Processing' AND IsDeleted=0;
IF @@ROWCOUNT=0 THROW 52303,N'The e-sign dispatch is no longer claimed.',1;
UPDATE DMS.ESignRequest SET Status=N'Sent',ProviderEnvelopeId=@ProviderEnvelopeId,ProviderStatus=@ProviderStatus,LastProviderEventDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ESignRequestId=@ESignRequestId AND IsDeleted=0;
UPDATE DMS.ESignSigner SET StatusCode=N'Sent',ProviderRecipientId=@ProviderRecipientId
WHERE TenantId=@TenantId AND ESignRequestId=@ESignRequestId AND IsDeleted=0;
INSERT DMS.ESignEnvelopeEvent(ESignEnvelopeEventId,TenantId,ESignRequestId,EventTypeCode,ProviderStatus,IsSignatureVerified,OccurredDateUtc,ReceivedDateUtc)
VALUES(NEWID(),@TenantId,@ESignRequestId,N'EnvelopeSent',@ProviderStatus,1,SYSUTCDATETIME(),SYSUTCDATETIME());
COMMIT;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            workItem.TenantId,
            workItem.ESignDispatchId,
            workItem.ESignRequestId,
            result.ProviderEnvelopeId,
            result.ProviderStatus,
            result.ProviderRecipientId
        }, cancellationToken: cancellationToken));
    }

    public async Task FailDispatchAsync(ESignDispatchWorkItem workItem, ESignDispatchFailure failure, CancellationToken cancellationToken = default)
    {
        var retry = failure.IsRetryable && workItem.AttemptCount < workItem.MaxAttempts;
        DateTime? retryAtUtc = retry
            ? failure.RetryAtUtc ?? DateTime.UtcNow.AddMinutes(Math.Pow(2, Math.Min(workItem.AttemptCount, 8)))
            : null;
        const string sql = @"
SET XACT_ABORT ON; BEGIN TRAN;
UPDATE DMS.ESignDispatch
SET StatusCode=@DispatchStatus,NextAttemptDateUtc=@RetryAtUtc,ClaimedBy=NULL,ClaimExpiresDateUtc=NULL,CompletedDateUtc=CASE WHEN @Retry=0 THEN SYSUTCDATETIME() END,ErrorCode=@ErrorCode,ErrorMessage=@ErrorMessage
WHERE TenantId=@TenantId AND ESignDispatchId=@ESignDispatchId AND ESignRequestId=@ESignRequestId AND StatusCode=N'Processing' AND IsDeleted=0;
IF @@ROWCOUNT=0 THROW 52303,N'The e-sign dispatch is no longer claimed.',1;
UPDATE DMS.ESignRequest SET Status=@RequestStatus,ProviderStatus=@RequestStatus,ModifiedDateUtc=SYSUTCDATETIME()
WHERE TenantId=@TenantId AND ESignRequestId=@ESignRequestId AND IsDeleted=0;
INSERT DMS.ESignEnvelopeEvent(ESignEnvelopeEventId,TenantId,ESignRequestId,EventTypeCode,ProviderStatus,PayloadJson,IsSignatureVerified,OccurredDateUtc,ReceivedDateUtc)
VALUES(NEWID(),@TenantId,@ESignRequestId,CASE WHEN @Retry=1 THEN N'DispatchRetryScheduled' ELSE N'DispatchFailed' END,@RequestStatus,@ErrorPayload,1,SYSUTCDATETIME(),SYSUTCDATETIME());
COMMIT;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            workItem.TenantId,
            workItem.ESignDispatchId,
            workItem.ESignRequestId,
            Retry = retry,
            DispatchStatus = retry ? "RetryScheduled" : "Failed",
            RequestStatus = retry ? "Queued" : "Failed",
            RetryAtUtc = retryAtUtc,
            failure.ErrorCode,
            ErrorMessage = failure.ErrorMessage.Length <= 2000 ? failure.ErrorMessage : failure.ErrorMessage[..2000],
            ErrorPayload = JsonSerializer.Serialize(new { failure.ErrorCode, failure.ErrorMessage, retryAtUtc })
        }, cancellationToken: cancellationToken));
    }

    private static ESignRequestDto WithDetails(ESignRequestDto item, IReadOnlyList<ESignSignerDto> signers, IReadOnlyList<ESignEnvelopeEventDto> events) => new()
    {
        ESignRequestId=item.ESignRequestId,TenantId=item.TenantId,DocumentId=item.DocumentId,PolicyId=item.PolicyId,Document=item.Document,PolicyNumber=item.PolicyNumber,
        SignerName=item.SignerName,SignerEmail=item.SignerEmail,Priority=item.Priority,Status=item.Status,ProviderCode=item.ProviderCode,ProviderEnvelopeId=item.ProviderEnvelopeId,
        ProviderStatus=item.ProviderStatus,IdempotencyKey=item.IdempotencyKey,IsOverdue=item.IsOverdue,SentDate=item.SentDate,DueDate=item.DueDate,CompletedDate=item.CompletedDate,
        Message=item.Message,VoidReason=item.VoidReason,Signers=signers,Events=events
    };
}
