using System.Data;
using System.Globalization;
using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Models;
using Ams.Application.Features.DocumentIntake;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DocumentIntakeRepository : IDocumentIntakeRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public DocumentIntakeRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SessionColumns = @"s.IntakeSessionId,s.TenantId,s.SessionNumber,s.ModuleCode,s.EntryPointCode,s.StatusCode,s.PriorityCode,s.TargetEntityId,s.AssignedToUserId,s.OverallConfidence,s.WarningCount,s.ErrorCount,s.PromotedEntityId,s.PromotedDateUtc,s.CreatedDateUtc,s.CreatedByUserId,s.RowVersion";

    public async Task<PagedResult<DocumentIntakeSessionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? moduleCode, string? statusCode, Guid? assignedToUserId, Guid? targetEntityId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT s.IntakeSessionId,s.TenantId,s.SessionNumber,s.ModuleCode,s.EntryPointCode,s.StatusCode,s.PriorityCode,s.TargetEntityId,s.AssignedToUserId,s.OverallConfidence,s.WarningCount,s.ErrorCount,s.PromotedEntityId,s.PromotedDateUtc,s.CreatedDateUtc,s.CreatedByUserId,s.RowVersion
FROM DMS.IntakeSession s
WHERE s.TenantId=@TenantId
 AND (@SearchTerm IS NULL OR @SearchTerm=N'' OR s.SessionNumber LIKE N'%'+@SearchTerm+N'%' OR s.CorrelationId LIKE N'%'+@SearchTerm+N'%')
 AND (@ModuleCode IS NULL OR @ModuleCode=N'' OR s.ModuleCode=@ModuleCode)
 AND (@StatusCode IS NULL OR @StatusCode=N'' OR s.StatusCode=@StatusCode)
 AND (@AssignedToUserId IS NULL OR s.AssignedToUserId=@AssignedToUserId)
 AND (@TargetEntityId IS NULL OR s.TargetEntityId=@TargetEntityId)
ORDER BY s.CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM DMS.IntakeSession s WHERE s.TenantId=@TenantId
 AND (@SearchTerm IS NULL OR @SearchTerm=N'' OR s.SessionNumber LIKE N'%'+@SearchTerm+N'%' OR s.CorrelationId LIKE N'%'+@SearchTerm+N'%')
 AND (@ModuleCode IS NULL OR @ModuleCode=N'' OR s.ModuleCode=@ModuleCode)
 AND (@StatusCode IS NULL OR @StatusCode=N'' OR s.StatusCode=@StatusCode)
 AND (@AssignedToUserId IS NULL OR s.AssignedToUserId=@AssignedToUserId)
 AND (@TargetEntityId IS NULL OR s.TargetEntityId=@TargetEntityId);";
        var safePage = Math.Max(1, pageNumber);
        var safeSize = Math.Clamp(pageSize, 1, 500);
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId=tenantId,SearchTerm=searchTerm,ModuleCode=moduleCode,StatusCode=statusCode,AssignedToUserId=assignedToUserId,TargetEntityId=targetEntityId,Offset=(safePage-1)*safeSize,PageSize=safeSize }, cancellationToken:cancellationToken));
        return new() { Items=(await multi.ReadAsync<DocumentIntakeSessionDto>()).AsList(),TotalCount=await multi.ReadSingleAsync<int>(),PageNumber=safePage,PageSize=safeSize };
    }

    public async Task<DocumentIntakeDetailDto?> GetAsync(Guid tenantId, Guid intakeSessionId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {SessionColumns} FROM DMS.IntakeSession s WHERE s.TenantId=@TenantId AND s.IntakeSessionId=@SessionId;
SELECT l.IntakeSessionDocumentId,l.DocumentId,d.FileName,d.ContentType,d.FileSizeBytes,l.DocumentRoleCode,l.ContentHashSha256,l.SequenceNumber FROM DMS.IntakeSessionDocument l JOIN DMS.Document d ON d.DocumentId=l.DocumentId AND d.TenantId=l.TenantId AND d.IsDeleted=0 WHERE l.TenantId=@TenantId AND l.IntakeSessionId=@SessionId ORDER BY l.SequenceNumber;
SELECT IntakeDraftFieldId,EntityTypeCode,EntityKey,FieldPath,ExtractedValue,NormalizedValue,ReviewedValue,ValueTypeCode,Confidence,SourceDocumentId,SourcePageNumber,SourceBoundingBoxJson,KnowledgeConceptId,MappingStatusCode,ReviewStatusCode,RowVersion FROM DMS.IntakeDraftField WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId ORDER BY EntityTypeCode,EntityKey,FieldPath;
SELECT IntakeIssueId,IssueCode,IssueTypeCode,SeverityCode,FieldPath,Message,ExistingValue,ExtractedValue,StatusCode,ResolvedByUserId,ResolvedDateUtc,ResolutionNotes,CreatedDateUtc,RowVersion FROM DMS.IntakeIssue WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId ORDER BY CASE SeverityCode WHEN N'ERROR' THEN 1 WHEN N'WARNING' THEN 2 ELSE 3 END,CreatedDateUtc;
SELECT IntakeWorkItemId,IntakeSessionId,DocumentId,WorkTypeCode,StatusCode,AttemptCount,MaxAttempts,AvailableDateUtc,LeaseOwner,LeaseExpiresDateUtc,LastErrorCode,LastErrorMessage,CorrelationId,RowVersion FROM DMS.IntakeWorkItem WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId ORDER BY SequenceNumber,CreatedDateUtc;
SELECT IntakeReviewHistoryId,IntakeDraftFieldId,ActionCode,PreviousValue,NewValue,Reason,ReviewedByUserId,CorrelationId,CreatedDateUtc FROM DMS.IntakeReviewHistory WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId ORDER BY CreatedDateUtc DESC;";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql,new { TenantId=tenantId,SessionId=intakeSessionId },cancellationToken:cancellationToken));
        var session=await multi.ReadSingleOrDefaultAsync<DocumentIntakeSessionDto>();
        if(session is null)return null;
        return new(session,(await multi.ReadAsync<DocumentIntakeDocumentDto>()).AsList(),(await multi.ReadAsync<DocumentIntakeDraftFieldDto>()).AsList(),(await multi.ReadAsync<DocumentIntakeIssueDto>()).AsList(),(await multi.ReadAsync<DocumentIntakeWorkItemDto>()).AsList(),(await multi.ReadAsync<DocumentIntakeReviewHistoryDto>()).AsList());
    }

    public async Task<IReadOnlyCollection<DocumentIntakeDocumentStatusDto>> GetDocumentStatusesAsync(Guid tenantId, string moduleCode, Guid targetEntityId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
WITH RankedSessions AS
(
    SELECT link.DocumentId,session.IntakeSessionId,session.SessionNumber,session.StatusCode,session.CreatedDateUtc,
           ROW_NUMBER() OVER(PARTITION BY link.DocumentId ORDER BY session.CreatedDateUtc DESC,session.IntakeSessionId DESC) AS SessionRank
    FROM DMS.IntakeSessionDocument link
    JOIN DMS.IntakeSession session ON session.TenantId=link.TenantId AND session.IntakeSessionId=link.IntakeSessionId
    JOIN DMS.Document document ON document.TenantId=link.TenantId AND document.DocumentId=link.DocumentId AND document.IsDeleted=0
    WHERE session.TenantId=@TenantId AND session.ModuleCode=@ModuleCode AND session.TargetEntityId=@TargetEntityId
)
SELECT ranked.DocumentId,ranked.IntakeSessionId,ranked.SessionNumber,ranked.StatusCode AS SessionStatusCode,
       progress.WorkStatusCode,progress.CurrentWorkTypeCode,COALESCE(progress.CompletedWorkItemCount,0) AS CompletedWorkItemCount,
       COALESCE(progress.TotalWorkItemCount,0) AS TotalWorkItemCount,progress.LastErrorCode,progress.LastErrorMessage,
       ranked.CreatedDateUtc AS SessionCreatedDateUtc
FROM RankedSessions ranked
OUTER APPLY
(
    SELECT
        CASE
            WHEN SUM(CASE WHEN work.StatusCode IN(N'FAILED',N'DEAD_LETTERED') THEN 1 ELSE 0 END)>0 THEN N'FAILED'
            WHEN SUM(CASE WHEN work.StatusCode=N'PROCESSING' THEN 1 ELSE 0 END)>0 THEN N'PROCESSING'
            WHEN SUM(CASE WHEN work.StatusCode=N'RETRY_SCHEDULED' THEN 1 ELSE 0 END)>0 THEN N'RETRY_SCHEDULED'
            WHEN SUM(CASE WHEN work.StatusCode=N'PENDING' THEN 1 ELSE 0 END)>0 THEN N'PENDING'
            WHEN COUNT(work.IntakeWorkItemId)>0 THEN N'COMPLETED'
            ELSE NULL
        END AS WorkStatusCode,
        MAX(CASE WHEN work.StatusCode<>N'COMPLETED' THEN work.WorkTypeCode END) AS CurrentWorkTypeCode,
        SUM(CASE WHEN work.StatusCode=N'COMPLETED' THEN 1 ELSE 0 END) AS CompletedWorkItemCount,
        COUNT(work.IntakeWorkItemId) AS TotalWorkItemCount,
        MAX(work.LastErrorCode) AS LastErrorCode,
        MAX(work.LastErrorMessage) AS LastErrorMessage
    FROM DMS.IntakeWorkItem work
    WHERE work.TenantId=@TenantId AND work.IntakeSessionId=ranked.IntakeSessionId AND work.DocumentId=ranked.DocumentId
) progress
WHERE ranked.SessionRank=1
ORDER BY ranked.CreatedDateUtc DESC;";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DocumentIntakeDocumentStatusDto>(new CommandDefinition(sql, new { TenantId=tenantId,ModuleCode=moduleCode,TargetEntityId=targetEntityId }, cancellationToken:cancellationToken));
        return rows.AsList();
    }

    public async Task<Guid> CreateSessionAsync(CreateDocumentIntakeSessionCommand command, CancellationToken cancellationToken = default)
    {
        const string sql=@"
DECLARE @Existing UNIQUEIDENTIFIER=(SELECT IntakeSessionId FROM DMS.IntakeSession WITH (UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND IdempotencyKey=@IdempotencyKey);
IF @Existing IS NOT NULL SELECT @Existing;
ELSE BEGIN
 DECLARE @Id UNIQUEIDENTIFIER=NEWID();
 DECLARE @Number NVARCHAR(50)=N'AI-'+CONVERT(CHAR(8),SYSUTCDATETIME(),112)+N'-'+UPPER(RIGHT(REPLACE(CONVERT(NVARCHAR(36),@Id),N'-',N''),8));
 INSERT DMS.IntakeSession(IntakeSessionId,TenantId,SessionNumber,IdempotencyKey,ModuleCode,EntryPointCode,StatusCode,PriorityCode,TargetEntityId,AssignedToUserId,CorrelationId,CreatedByUserId)
 VALUES(@Id,@TenantId,@Number,@IdempotencyKey,UPPER(@ModuleCode),UPPER(@EntryPointCode),N'DRAFT',UPPER(@PriorityCode),@TargetEntityId,@AssignedToUserId,@CorrelationId,@CreatedByUserId);
 SELECT @Id;
END";
        using var connection=await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql,command,cancellationToken:cancellationToken));
    }

    public async Task AttachDocumentAsync(AttachDocumentToIntakeCommand command, CancellationToken cancellationToken = default)
    {
        const string sql=@"
IF NOT EXISTS(SELECT 1 FROM DMS.IntakeSession WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND StatusCode=N'DRAFT') THROW 51000,'Draft intake session not found for tenant.',1;
IF NOT EXISTS(SELECT 1 FROM DMS.Document WHERE TenantId=@TenantId AND DocumentId=@DocumentId AND IsDeleted=0) THROW 51000,'Document not found for tenant.',1;
IF NOT EXISTS(SELECT 1 FROM DMS.IntakeSessionDocument WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND DocumentId=@DocumentId)
 INSERT DMS.IntakeSessionDocument(TenantId,IntakeSessionId,DocumentId,DocumentRoleCode,ContentHashSha256,SequenceNumber,CreatedByUserId) VALUES(@TenantId,@IntakeSessionId,@DocumentId,UPPER(@DocumentRoleCode),@ContentHashSha256,@SequenceNumber,@ActorUserId);
MERGE DMS.IntakeMalwareScan target USING(SELECT d.TenantId,d.DocumentId,d.StoragePath FROM DMS.Document d WHERE d.TenantId=@TenantId AND d.DocumentId=@DocumentId AND d.IsDeleted=0) source ON target.TenantId=source.TenantId AND target.DocumentId=source.DocumentId WHEN MATCHED THEN UPDATE SET StoragePath=source.StoragePath,StatusCode=CASE WHEN target.StatusCode IN(N'INFECTED',N'QUARANTINED') THEN target.StatusCode ELSE N'PENDING' END,ScanRequestedDateUtc=CASE WHEN target.StatusCode IN(N'INFECTED',N'QUARANTINED') THEN target.ScanRequestedDateUtc ELSE SYSUTCDATETIME() END,ModifiedDateUtc=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(TenantId,DocumentId,StoragePath,StatusCode) VALUES(source.TenantId,source.DocumentId,source.StoragePath,N'PENDING');";
        using var connection=await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,command,cancellationToken:cancellationToken));
    }

    public Task QueueAsync(QueueDocumentIntakeCommand command, CancellationToken cancellationToken = default)
        => ExecuteTransactionAsync(async (connection,transaction) =>
        {
            const string update=@"UPDATE DMS.IntakeSession SET StatusCode=N'QUEUED',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND StatusCode IN(N'DRAFT',N'FAILED',N'REVIEW_REQUIRED',N'READY') AND RowVersion=@RowVersion; SELECT @@ROWCOUNT;";
            EnsureOne(await connection.ExecuteScalarAsync<int>(new CommandDefinition(update,command,transaction,cancellationToken:cancellationToken)),"Intake session changed or cannot be queued.");
            const string insert=@"
INSERT DMS.IntakeWorkItem(TenantId,IntakeSessionId,DocumentId,WorkTypeCode,StatusCode,IdempotencyKey,SequenceNumber,CorrelationId)
SELECT @TenantId,@IntakeSessionId,l.DocumentId,N'OCR',N'PENDING',CONCAT(@IntakeSessionId,N':',l.DocumentId,N':OCR'),1,@CorrelationId FROM DMS.IntakeSessionDocument l WHERE l.TenantId=@TenantId AND l.IntakeSessionId=@IntakeSessionId AND NOT EXISTS(SELECT 1 FROM DMS.IntakeWorkItem w WHERE w.TenantId=@TenantId AND w.IdempotencyKey=CONCAT(@IntakeSessionId,N':',l.DocumentId,N':OCR'));
IF NOT EXISTS(SELECT 1 FROM DMS.IntakeSessionDocument WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId) THROW 51000,'At least one evidence document is required.',1;";
            await connection.ExecuteAsync(new CommandDefinition(insert,command,transaction,cancellationToken:cancellationToken));
        },cancellationToken);

    public Task ReviewFieldAsync(ReviewDocumentIntakeFieldCommand command, CancellationToken cancellationToken = default)
        => ExecuteTransactionAsync(async (connection,transaction) =>
        {
            const string sql=@"
DECLARE @Previous NVARCHAR(MAX),@Extracted NVARCHAR(MAX),@Normalized NVARCHAR(MAX); SELECT @Previous=ReviewedValue,@Extracted=ExtractedValue,@Normalized=NormalizedValue FROM DMS.IntakeDraftField WITH(UPDLOCK) WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND IntakeDraftFieldId=@IntakeDraftFieldId AND RowVersion=@RowVersion;
IF @Previous IS NULL AND @Extracted IS NULL AND @Normalized IS NULL AND NOT EXISTS(SELECT 1 FROM DMS.IntakeDraftField WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND IntakeDraftFieldId=@IntakeDraftFieldId AND RowVersion=@RowVersion) THROW 51000,'Draft field changed or was not found for tenant.',1;
DECLARE @New NVARCHAR(MAX)=CASE WHEN @DecisionCode=N'CORRECTED' THEN @ReviewedValue WHEN @DecisionCode=N'APPROVED' THEN COALESCE(@Previous,@Normalized,@Extracted) ELSE @Previous END;
UPDATE DMS.IntakeDraftField SET ReviewedValue=@New,ReviewStatusCode=@DecisionCode,CorrectedByUserId=@ReviewerUserId,CorrectedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND IntakeDraftFieldId=@IntakeDraftFieldId;
INSERT DMS.IntakeReviewHistory(TenantId,IntakeSessionId,IntakeDraftFieldId,ActionCode,PreviousValue,NewValue,Reason,ReviewedByUserId,CorrelationId) VALUES(@TenantId,@IntakeSessionId,@IntakeDraftFieldId,@DecisionCode,@Previous,@New,@Reason,@ReviewerUserId,@CorrelationId);
UPDATE DMS.IntakeSession SET StatusCode=CASE WHEN NOT EXISTS(SELECT 1 FROM DMS.IntakeDraftField WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND ReviewStatusCode=N'PENDING') AND NOT EXISTS(SELECT 1 FROM DMS.IntakeIssue WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND SeverityCode=N'ERROR' AND StatusCode=N'OPEN') THEN N'READY' ELSE N'REVIEW_REQUIRED' END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ReviewerUserId WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId;";
            await connection.ExecuteAsync(new CommandDefinition(sql,command,transaction,cancellationToken:cancellationToken));
        },cancellationToken);

    public Task ResolveIssueAsync(ResolveDocumentIntakeIssueCommand command, CancellationToken cancellationToken = default)
        => ExecuteTransactionAsync(async (connection, transaction) =>
        {
            const string sql=@"
UPDATE DMS.IntakeIssue SET StatusCode=@ResolutionCode,ResolvedByUserId=@ReviewerUserId,ResolvedDateUtc=SYSUTCDATETIME(),ResolutionNotes=@ResolutionNotes WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND IntakeIssueId=@IntakeIssueId AND RowVersion=@RowVersion;
IF @@ROWCOUNT=0 THROW 51000,'Issue changed or was not found for tenant.',1;
UPDATE DMS.IntakeSession SET StatusCode=CASE WHEN NOT EXISTS(SELECT 1 FROM DMS.IntakeDraftField WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND ReviewStatusCode=N'PENDING') AND NOT EXISTS(SELECT 1 FROM DMS.IntakeIssue WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND SeverityCode=N'ERROR' AND StatusCode=N'OPEN') THEN N'READY' ELSE N'REVIEW_REQUIRED' END,WarningCount=(SELECT COUNT(1) FROM DMS.IntakeIssue WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND SeverityCode=N'WARNING' AND StatusCode=N'OPEN'),ErrorCount=(SELECT COUNT(1) FROM DMS.IntakeIssue WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND SeverityCode=N'ERROR' AND StatusCode=N'OPEN'),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ReviewerUserId WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND StatusCode IN(N'REVIEW_REQUIRED',N'READY');";
            await connection.ExecuteAsync(new CommandDefinition(sql,command,transaction,cancellationToken:cancellationToken));
        },cancellationToken);

    public Task ReprocessAsync(ReprocessDocumentIntakeCommand command, CancellationToken cancellationToken = default)
        => ExecuteTransactionAsync(async (connection,transaction) =>
        {
            const string sql=@"
IF @FromWorkTypeCode NOT IN(N'OCR',N'CLASSIFICATION',N'EXTRACTION',N'KNOWLEDGE_MAPPING',N'VALIDATION',N'SEARCH_INDEXING') THROW 51000,'Invalid intake reprocess stage.',1;
DECLARE @Sequence INT=(SELECT MIN(SequenceNumber) FROM DMS.IntakeWorkItem WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND WorkTypeCode=@FromWorkTypeCode);
IF @Sequence IS NULL THROW 51000,'The requested intake reprocess stage does not exist.',1;
UPDATE DMS.IntakeSession SET StatusCode=N'QUEUED',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND RowVersion=@RowVersion AND StatusCode IN(N'FAILED',N'REVIEW_REQUIRED',N'READY');
IF @@ROWCOUNT=0 THROW 51000,'Session changed or cannot be reprocessed.',1;
UPDATE DMS.IntakeWorkItem SET StatusCode=N'RETRY_SCHEDULED',AvailableDateUtc=SYSUTCDATETIME(),LeaseOwner=NULL,LeaseExpiresDateUtc=NULL,CompletedDateUtc=NULL,LastErrorCode=NULL,LastErrorMessage=NULL WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND SequenceNumber>=@Sequence;";
            await connection.ExecuteAsync(new CommandDefinition(sql,command,transaction,cancellationToken:cancellationToken));
        },cancellationToken);

    public Task CancelAsync(CancelDocumentIntakeCommand command, CancellationToken cancellationToken = default)
        => ExecuteTransactionAsync(async (connection, transaction) =>
        {
            const string sql=@"
UPDATE DMS.IntakeSession SET StatusCode=N'CANCELLED',CancelledDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND RowVersion=@RowVersion AND StatusCode NOT IN(N'COMPLETED',N'CANCELLED');
IF @@ROWCOUNT=0 THROW 51000,'Session changed or cannot be cancelled.',1;
UPDATE attempt SET StatusCode=N'CANCELLED',CompletedDateUtc=SYSUTCDATETIME(),DurationMilliseconds=DATEDIFF_BIG(MILLISECOND,attempt.StartedDateUtc,SYSUTCDATETIME()),ErrorCode=N'INTAKE_CANCELLED',ErrorMessage=N'Processing was cancelled by an authorized user.'
FROM DMS.IntakeWorkAttempt attempt
JOIN DMS.IntakeWorkItem work ON work.TenantId=attempt.TenantId AND work.IntakeWorkItemId=attempt.IntakeWorkItemId AND work.AttemptCount=attempt.AttemptNumber
WHERE work.TenantId=@TenantId AND work.IntakeSessionId=@IntakeSessionId AND work.StatusCode=N'PROCESSING' AND attempt.StatusCode=N'PROCESSING';
UPDATE DMS.IntakeWorkItem SET StatusCode=N'CANCELLED',LeaseOwner=NULL,LeaseExpiresDateUtc=NULL,LastErrorCode=CASE WHEN StatusCode=N'PROCESSING' THEN N'INTAKE_CANCELLED' ELSE LastErrorCode END,LastErrorMessage=CASE WHEN StatusCode=N'PROCESSING' THEN N'Processing was cancelled by an authorized user.' ELSE LastErrorMessage END WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId AND StatusCode IN(N'PENDING',N'PROCESSING',N'RETRY_SCHEDULED',N'FAILED',N'DEAD_LETTERED');";
            await connection.ExecuteAsync(new CommandDefinition(sql,command,transaction,cancellationToken:cancellationToken));
        },cancellationToken);

    public async Task<SubmissionIntakeDraft> BuildReviewedSubmissionDraftAsync(Guid tenantId, Guid intakeSessionId, CancellationToken cancellationToken = default)
    {
        const string sql=@"SELECT FieldPath,COALESCE(ReviewedValue,NormalizedValue,ExtractedValue) AS Value FROM DMS.IntakeDraftField WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND ReviewStatusCode IN(N'APPROVED',N'CORRECTED');";
        using var connection=await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var values=(await connection.QueryAsync<FieldValue>(new CommandDefinition(sql,new{TenantId=tenantId,SessionId=intakeSessionId},cancellationToken:cancellationToken))).ToDictionary(x=>x.FieldPath,x=>x.Value,StringComparer.OrdinalIgnoreCase);
        string? Get(string path)=>values.GetValueOrDefault(path);
        DateTime? Date(string path)=>DateTime.TryParse(Get(path),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var value)?value:null;
        decimal? Decimal(string path)=>decimal.TryParse(Get(path),NumberStyles.Any,CultureInfo.InvariantCulture,out var value)?value:null;
        return new(Get("submission.source")??"DocumentIntake",Get("submission.applicantName"),Get("submission.businessName")??throw new InvalidOperationException("Reviewed business name is required."),Get("submission.fein"),Get("submission.email"),Get("submission.phone"),Get("submission.addressLine"),Get("submission.city"),Get("submission.state"),Get("submission.postalCode"),Get("submission.existingPolicyNumber"),Get("submission.producerCode"),Get("submission.lineOfBusiness")??throw new InvalidOperationException("Reviewed line of business is required."),Date("submission.requestedEffectiveDate"),Decimal("submission.estimatedPremium"),Get("submission.notes"));
    }

    public async Task<DocumentIntakePromotionConfigurationDto?> GetPromotionConfigurationAsync(Guid tenantId, string moduleCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT IntakePromotionConfigurationId, TenantId, ModuleCode, RequireReadyStatus, RequireCanonicalLob,
       LinkSourceDocuments, CreateFollowUpTask, FollowUpTaskTitle, FollowUpTaskDescription, FollowUpDueDays,
       FollowUpTaskPriorityCode, OpportunityLinePriorityCode, OpportunityLineStatusCode,
       OpportunityCloseDays, OpportunityWinProbability, SubmissionTermMonths
FROM DMS.IntakePromotionConfiguration
WHERE TenantId = @TenantId AND ModuleCode = @ModuleCode AND IsActive = 1 AND IsDeleted = 0;";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DocumentIntakePromotionConfigurationDto>(new CommandDefinition(sql, new { TenantId = tenantId, ModuleCode = moduleCode }, cancellationToken: cancellationToken));
    }

    public async Task<DocumentIntakePromotionRecord?> GetPromotionAsync(Guid tenantId,Guid intakeSessionId,string idempotencyKey,CancellationToken cancellationToken=default){const string sql=@"SELECT TOP(1) IntakePromotionId,StatusCode,TargetEntityId,ResultJson,SubmissionIntakeId,AccountId,OpportunityId,LobId,LastErrorMessage FROM DMS.IntakePromotion WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId ORDER BY CASE WHEN IdempotencyKey=@IdempotencyKey THEN 0 ELSE 1 END,PromotedDateUtc DESC;";using var c=await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);return await c.QuerySingleOrDefaultAsync<DocumentIntakePromotionRecord>(new CommandDefinition(sql,new{TenantId=tenantId,IntakeSessionId=intakeSessionId,IdempotencyKey=idempotencyKey},cancellationToken:cancellationToken));}
    public async Task<DocumentIntakePromotionStart> BeginPromotionAsync(PromoteDocumentIntakeCommand command,string requestJson,CancellationToken cancellationToken=default){const string sql=@"DECLARE @Id UNIQUEIDENTIFIER,@Created BIT=0; SELECT TOP(1) @Id=IntakePromotionId FROM DMS.IntakePromotion WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId; IF @Id IS NULL BEGIN SET @Id=NEWID(); INSERT DMS.IntakePromotion(IntakePromotionId,TenantId,IntakeSessionId,ModuleCode,IdempotencyKey,StatusCode,RequestJson,PromotedByUserId) SELECT @Id,@TenantId,@IntakeSessionId,ModuleCode,@IdempotencyKey,N'PROCESSING',@RequestJson,@ActorUserId FROM DMS.IntakeSession WHERE TenantId=@TenantId AND IntakeSessionId=@IntakeSessionId; IF @@ROWCOUNT=0 THROW 51000,'Intake session not found for tenant.',1; SET @Created=1; END SELECT @Id IntakePromotionId,@Created Created;";using var c=await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);using var t=c.BeginTransaction();var result=await c.QuerySingleAsync<DocumentIntakePromotionStart>(new CommandDefinition(sql,new{command.TenantId,command.IntakeSessionId,command.IdempotencyKey,command.ActorUserId,RequestJson=requestJson},t,cancellationToken:cancellationToken));t.Commit();return result;}
    public async Task UpdatePromotionProgressAsync(Guid tenantId, Guid promotionId, Guid? submissionIntakeId, Guid? accountId, Guid? opportunityId, Guid? lobId, string? errorMessage, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.IntakePromotion
SET SubmissionIntakeId = COALESCE(@SubmissionIntakeId, SubmissionIntakeId),
    AccountId = COALESCE(@AccountId, AccountId),
    OpportunityId = COALESCE(@OpportunityId, OpportunityId),
    LobId = COALESCE(@LobId, LobId),
    LastErrorMessage = @ErrorMessage,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND IntakePromotionId = @PromotionId;";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, PromotionId = promotionId, SubmissionIntakeId = submissionIntakeId, AccountId = accountId, OpportunityId = opportunityId, LobId = lobId, ErrorMessage = errorMessage }, cancellationToken: cancellationToken));
    }

    public Task LinkDocumentsToSubmissionAsync(Guid tenantId, Guid intakeSessionId, Guid promotionId, Guid submissionId, Guid actorUserId, CancellationToken cancellationToken = default)
        => ExecuteTransactionAsync(async (connection, transaction) =>
        {
            const string sql = @"
INSERT INTO DMS.IntakePromotedDocument
    (IntakePromotedDocumentId, TenantId, IntakeSessionId, IntakePromotionId, DocumentId, SubmissionId, OriginalEntityName, OriginalEntityId, DocumentRoleCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), link.TenantId, link.IntakeSessionId, @PromotionId, link.DocumentId, @SubmissionId, document.EntityName, document.EntityId, link.DocumentRoleCode, SYSUTCDATETIME(), @ActorUserId, 0
FROM DMS.IntakeSessionDocument link
INNER JOIN DMS.Document document ON document.TenantId = link.TenantId AND document.DocumentId = link.DocumentId AND document.IsDeleted = 0
WHERE link.TenantId = @TenantId AND link.IntakeSessionId = @IntakeSessionId
  AND NOT EXISTS
  (
      SELECT 1 FROM DMS.IntakePromotedDocument existing
      WHERE existing.TenantId = link.TenantId AND existing.IntakeSessionId = link.IntakeSessionId
        AND existing.DocumentId = link.DocumentId AND existing.SubmissionId = @SubmissionId AND existing.IsDeleted = 0
  );

UPDATE document
SET EntityName = N'Submission', EntityId = @SubmissionId, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ActorUserId
FROM DMS.Document document
INNER JOIN DMS.IntakeSessionDocument link ON link.TenantId = document.TenantId AND link.DocumentId = document.DocumentId
WHERE link.TenantId = @TenantId AND link.IntakeSessionId = @IntakeSessionId AND document.IsDeleted = 0;";
            await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, IntakeSessionId = intakeSessionId, PromotionId = promotionId, SubmissionId = submissionId, ActorUserId = actorUserId }, transaction, cancellationToken: cancellationToken));
        }, cancellationToken);
    public Task CompletePromotionAsync(Guid tenantId,Guid intakeSessionId,Guid promotionId,Guid targetEntityId,string resultJson,Guid actorUserId,byte[] expectedSessionRowVersion,CancellationToken cancellationToken=default)=>ExecuteTransactionAsync(async(c,t)=>{const string sql=@"UPDATE DMS.IntakePromotion SET StatusCode=N'COMPLETED',TargetEntityId=@TargetEntityId,ResultJson=@ResultJson,CompletedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND IntakePromotionId=@PromotionId AND StatusCode=N'PROCESSING'; UPDATE DMS.IntakeSession SET StatusCode=N'COMPLETED',PromotedEntityId=@TargetEntityId,PromotedDateUtc=SYSUTCDATETIME(),CompletedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND RowVersion=@RowVersion AND StatusCode=N'READY'; IF @@ROWCOUNT=0 THROW 51000,'Session changed before promotion completed.',1;";await c.ExecuteAsync(new CommandDefinition(sql,new{TenantId=tenantId,SessionId=intakeSessionId,PromotionId=promotionId,TargetEntityId=targetEntityId,ResultJson=resultJson,ActorUserId=actorUserId,RowVersion=expectedSessionRowVersion},t,cancellationToken:cancellationToken));},cancellationToken);

    public async Task<IReadOnlyCollection<DocumentIntakeWorkItemDto>> LeaseWorkItemsAsync(string leaseOwner,int batchSize,TimeSpan leaseDuration,bool malwareEnabled=true,bool malwareFailClosed=true,CancellationToken cancellationToken=default)
    {
        const string sql=@"DECLARE @Leased TABLE(IntakeWorkItemId UNIQUEIDENTIFIER,IntakeSessionId UNIQUEIDENTIFIER,DocumentId UNIQUEIDENTIFIER NULL,WorkTypeCode NVARCHAR(50),StatusCode NVARCHAR(50),AttemptCount INT,MaxAttempts INT,AvailableDateUtc DATETIME2,LeaseOwner NVARCHAR(200),LeaseExpiresDateUtc DATETIME2,LastErrorCode NVARCHAR(100),LastErrorMessage NVARCHAR(4000),CorrelationId NVARCHAR(120),RowVersion BINARY(8),TenantId UNIQUEIDENTIFIER);
UPDATE attempt SET StatusCode=N'FAILED',CompletedDateUtc=SYSUTCDATETIME(),DurationMilliseconds=DATEDIFF_BIG(MILLISECOND,attempt.StartedDateUtc,SYSUTCDATETIME()),ErrorCode=N'WORK_LEASE_EXPIRED',ErrorMessage=N'The worker lease expired before processing completed.'
FROM DMS.IntakeWorkAttempt attempt
JOIN DMS.IntakeWorkItem work ON work.TenantId=attempt.TenantId AND work.IntakeWorkItemId=attempt.IntakeWorkItemId AND work.AttemptCount=attempt.AttemptNumber
WHERE work.StatusCode=N'PROCESSING' AND work.LeaseExpiresDateUtc<SYSUTCDATETIME() AND attempt.StatusCode=N'PROCESSING';
UPDATE work SET StatusCode=CASE WHEN work.AttemptCount>=work.MaxAttempts THEN N'DEAD_LETTERED' ELSE N'RETRY_SCHEDULED' END,AvailableDateUtc=SYSUTCDATETIME(),LeaseOwner=NULL,LeaseExpiresDateUtc=NULL,LastErrorCode=N'WORK_LEASE_EXPIRED',LastErrorMessage=N'The worker lease expired before processing completed.'
FROM DMS.IntakeWorkItem work WHERE work.StatusCode=N'PROCESSING' AND work.LeaseExpiresDateUtc<SYSUTCDATETIME();
UPDATE session SET StatusCode=N'FAILED',ModifiedDateUtc=SYSUTCDATETIME()
FROM DMS.IntakeSession session WHERE session.StatusCode<>N'CANCELLED' AND EXISTS(SELECT 1 FROM DMS.IntakeWorkItem work WHERE work.TenantId=session.TenantId AND work.IntakeSessionId=session.IntakeSessionId AND work.StatusCode=N'DEAD_LETTERED');
;WITH candidates AS(SELECT TOP(@BatchSize)* FROM DMS.IntakeWorkItem WITH(UPDLOCK,READPAST,ROWLOCK) WHERE StatusCode IN(N'PENDING',N'RETRY_SCHEDULED') AND AvailableDateUtc<=SYSUTCDATETIME() AND EXISTS(SELECT 1 FROM DMS.IntakeSession session WHERE session.TenantId=DMS.IntakeWorkItem.TenantId AND session.IntakeSessionId=DMS.IntakeWorkItem.IntakeSessionId AND session.StatusCode IN(N'QUEUED',N'PROCESSING')) AND (@MalwareEnabled=0 OR WorkTypeCode<>N'OCR' OR DocumentId IS NULL OR EXISTS(SELECT 1 FROM DMS.IntakeMalwareScan scan WHERE scan.TenantId=DMS.IntakeWorkItem.TenantId AND scan.DocumentId=DMS.IntakeWorkItem.DocumentId AND (scan.StatusCode=N'CLEAN' OR (@MalwareFailClosed=0 AND scan.StatusCode NOT IN(N'INFECTED',N'QUARANTINED'))))) AND NOT EXISTS(SELECT 1 FROM DMS.IntakeWorkItem predecessor WHERE predecessor.TenantId=DMS.IntakeWorkItem.TenantId AND predecessor.IntakeSessionId=DMS.IntakeWorkItem.IntakeSessionId AND predecessor.SequenceNumber<DMS.IntakeWorkItem.SequenceNumber AND predecessor.StatusCode<>N'COMPLETED') ORDER BY SequenceNumber,AvailableDateUtc)
UPDATE candidates SET StatusCode=N'PROCESSING',LeaseOwner=@LeaseOwner,LeaseExpiresDateUtc=DATEADD(SECOND,@LeaseSeconds,SYSUTCDATETIME()),StartedDateUtc=COALESCE(StartedDateUtc,SYSUTCDATETIME()),AttemptCount=AttemptCount+1
OUTPUT inserted.IntakeWorkItemId,inserted.IntakeSessionId,inserted.DocumentId,inserted.WorkTypeCode,inserted.StatusCode,inserted.AttemptCount,inserted.MaxAttempts,inserted.AvailableDateUtc,inserted.LeaseOwner,inserted.LeaseExpiresDateUtc,inserted.LastErrorCode,inserted.LastErrorMessage,inserted.CorrelationId,inserted.RowVersion,inserted.TenantId INTO @Leased;
INSERT DMS.IntakeWorkAttempt(TenantId,IntakeWorkItemId,AttemptNumber,StatusCode,LeaseOwner,StartedDateUtc) SELECT TenantId,IntakeWorkItemId,AttemptCount,N'PROCESSING',@LeaseOwner,SYSUTCDATETIME() FROM @Leased;
UPDATE session SET StatusCode=N'PROCESSING',ModifiedDateUtc=SYSUTCDATETIME() FROM DMS.IntakeSession session JOIN(SELECT DISTINCT TenantId,IntakeSessionId FROM @Leased)leased ON leased.TenantId=session.TenantId AND leased.IntakeSessionId=session.IntakeSessionId WHERE session.StatusCode=N'QUEUED';
SELECT IntakeWorkItemId,IntakeSessionId,DocumentId,WorkTypeCode,StatusCode,AttemptCount,MaxAttempts,AvailableDateUtc,LeaseOwner,LeaseExpiresDateUtc,LastErrorCode,LastErrorMessage,CorrelationId,RowVersion FROM @Leased;";
        using var c=await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);return (await c.QueryAsync<DocumentIntakeWorkItemDto>(new CommandDefinition(sql,new{LeaseOwner=leaseOwner,BatchSize=Math.Clamp(batchSize,1,100),LeaseSeconds=(int)leaseDuration.TotalSeconds,MalwareEnabled=malwareEnabled,MalwareFailClosed=malwareFailClosed},cancellationToken:cancellationToken))).AsList();
    }

    public async Task<DocumentIntakeProcessingContext?> GetProcessingContextAsync(Guid workItemId,string leaseOwner,CancellationToken cancellationToken=default)
    {
        const string sql=@"SELECT w.IntakeWorkItemId,w.IntakeSessionId,w.DocumentId,w.WorkTypeCode,w.StatusCode,w.AttemptCount,w.MaxAttempts,w.AvailableDateUtc,w.LeaseOwner,w.LeaseExpiresDateUtc,w.LastErrorCode,w.LastErrorMessage,w.CorrelationId,w.RowVersion FROM DMS.IntakeWorkItem w JOIN DMS.IntakeSession s ON s.TenantId=w.TenantId AND s.IntakeSessionId=w.IntakeSessionId WHERE w.IntakeWorkItemId=@WorkItemId AND w.LeaseOwner=@LeaseOwner AND w.StatusCode=N'PROCESSING' AND w.LeaseExpiresDateUtc>SYSUTCDATETIME() AND s.StatusCode=N'PROCESSING'; SELECT s.IntakeSessionId,s.TenantId,s.SessionNumber,s.ModuleCode,s.EntryPointCode,s.StatusCode,s.PriorityCode,s.TargetEntityId,s.AssignedToUserId,s.OverallConfidence,s.WarningCount,s.ErrorCount,s.PromotedEntityId,s.PromotedDateUtc,s.CreatedDateUtc,s.CreatedByUserId,s.RowVersion FROM DMS.IntakeWorkItem w JOIN DMS.IntakeSession s ON s.IntakeSessionId=w.IntakeSessionId AND s.TenantId=w.TenantId WHERE w.IntakeWorkItemId=@WorkItemId AND w.LeaseOwner=@LeaseOwner AND w.StatusCode=N'PROCESSING' AND w.LeaseExpiresDateUtc>SYSUTCDATETIME() AND s.StatusCode=N'PROCESSING'; SELECT TOP 1 l.IntakeSessionDocumentId,l.DocumentId,d.FileName,d.ContentType,d.FileSizeBytes,l.DocumentRoleCode,l.ContentHashSha256,l.SequenceNumber,d.StoragePath FROM DMS.IntakeWorkItem w JOIN DMS.IntakeSessionDocument l ON l.IntakeSessionId=w.IntakeSessionId AND l.TenantId=w.TenantId AND(w.DocumentId IS NULL OR l.DocumentId=w.DocumentId) JOIN DMS.Document d ON d.DocumentId=l.DocumentId AND d.TenantId=l.TenantId AND d.IsDeleted=0 WHERE w.IntakeWorkItemId=@WorkItemId AND w.LeaseOwner=@LeaseOwner; SELECT TOP 1 OutputReference AS OcrOutputReference FROM DMS.AiExecution e JOIN DMS.IntakeWorkItem w ON w.IntakeSessionId=e.IntakeSessionId AND w.TenantId=e.TenantId WHERE w.IntakeWorkItemId=@WorkItemId AND e.ExecutionTypeCode=N'OCR' AND e.DocumentId=w.DocumentId ORDER BY e.CreatedDateUtc DESC; SELECT TOP 1 PromptCode,VersionLabel AS PromptVersion,SystemPrompt,OutputSchemaJson FROM DMS.AiPromptDefinition p JOIN DMS.IntakeWorkItem w ON w.IntakeWorkItemId=@WorkItemId JOIN DMS.IntakeSession s ON s.TenantId=w.TenantId AND s.IntakeSessionId=w.IntakeSessionId WHERE (p.TenantId=w.TenantId OR p.TenantId IS NULL) AND p.StatusCode=N'APPROVED' AND p.EffectiveFromUtc<=SYSUTCDATETIME() AND(p.EffectiveToUtc IS NULL OR p.EffectiveToUtc>SYSUTCDATETIME()) AND p.PromptCode=CASE w.WorkTypeCode WHEN N'CLASSIFICATION' THEN N'DOCUMENT.CLASSIFICATION' ELSE CONCAT(s.ModuleCode,N'.EXTRACTION') END ORDER BY CASE WHEN p.TenantId=w.TenantId THEN 0 ELSE 1 END,p.EffectiveFromUtc DESC;";
        using var c=await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);using var m=await c.QueryMultipleAsync(new CommandDefinition(sql,new{WorkItemId=workItemId,LeaseOwner=leaseOwner},cancellationToken:cancellationToken));var work=await m.ReadSingleOrDefaultAsync<DocumentIntakeWorkItemDto>();if(work is null)return null;var session=await m.ReadSingleAsync<DocumentIntakeSessionDto>();var documentRow=await m.ReadSingleOrDefaultAsync<DocumentRow>();var output=await m.ReadSingleOrDefaultAsync<OutputRow>();var prompt=await m.ReadSingleOrDefaultAsync<PromptRow>();return new(work,session,documentRow?.ToDto(),documentRow?.StoragePath,output?.OcrOutputReference,null,prompt?.PromptCode,prompt?.PromptVersion,prompt?.SystemPrompt,prompt?.OutputSchemaJson);
    }

    public async Task<IReadOnlyCollection<ExtractedDocumentField>> GetExtractedFieldsAsync(Guid tenantId,Guid intakeSessionId,CancellationToken cancellationToken=default)
    {
        const string sql=@"SELECT EntityTypeCode,EntityKey,FieldPath AS [Path],ExtractedValue AS Value,ValueTypeCode,Confidence,SourcePageNumber AS SourcePage,SourceBoundingBoxJson AS BoundingBoxJson FROM DMS.IntakeDraftField WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId;";
        using var c=await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);return (await c.QueryAsync<ExtractedDocumentField>(new CommandDefinition(sql,new{TenantId=tenantId,SessionId=intakeSessionId},cancellationToken:cancellationToken))).AsList();
    }

    public async Task ValidateDraftAsync(DocumentIntakeProcessingContext context,CancellationToken cancellationToken=default)
    {
        const string sql=@"DELETE FROM DMS.IntakeIssue WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND IssueTypeCode=N'DETERMINISTIC_VALIDATION' AND StatusCode=N'OPEN';
IF @ModuleCode=N'SUBMISSION'
BEGIN
 IF NOT EXISTS(SELECT 1 FROM DMS.IntakeDraftField WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND FieldPath=N'submission.businessName' AND NULLIF(COALESCE(ReviewedValue,NormalizedValue,ExtractedValue),N'') IS NOT NULL) INSERT DMS.IntakeIssue(TenantId,IntakeSessionId,IssueCode,IssueTypeCode,SeverityCode,FieldPath,Message,StatusCode)VALUES(@TenantId,@SessionId,N'SUBMISSION.BUSINESS_NAME.REQUIRED',N'DETERMINISTIC_VALIDATION',N'ERROR',N'submission.businessName',N'Business name is required before Submission promotion.',N'OPEN');
 IF NOT EXISTS(SELECT 1 FROM DMS.IntakeDraftField WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND FieldPath=N'submission.lineOfBusiness' AND NULLIF(COALESCE(ReviewedValue,NormalizedValue,ExtractedValue),N'') IS NOT NULL) INSERT DMS.IntakeIssue(TenantId,IntakeSessionId,IssueCode,IssueTypeCode,SeverityCode,FieldPath,Message,StatusCode)VALUES(@TenantId,@SessionId,N'SUBMISSION.LOB.REQUIRED',N'DETERMINISTIC_VALIDATION',N'ERROR',N'submission.lineOfBusiness',N'Line of business is required before Submission promotion.',N'OPEN');
END;
INSERT DMS.IntakeIssue(TenantId,IntakeSessionId,IssueCode,IssueTypeCode,SeverityCode,FieldPath,Message,ExtractedValue,StatusCode)
SELECT @TenantId,@SessionId,N'FIELD.LOW_CONFIDENCE',N'DETERMINISTIC_VALIDATION',N'WARNING',FieldPath,N'Extracted confidence is below the review threshold.',ExtractedValue,N'OPEN' FROM DMS.IntakeDraftField field WHERE field.TenantId=@TenantId AND field.IntakeSessionId=@SessionId AND field.Confidence<0.70 AND NOT EXISTS(SELECT 1 FROM DMS.IntakeIssue issue WHERE issue.TenantId=@TenantId AND issue.IntakeSessionId=@SessionId AND issue.IssueCode=N'FIELD.LOW_CONFIDENCE' AND issue.FieldPath=field.FieldPath AND issue.StatusCode=N'OPEN');";
        using var c=await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);await c.ExecuteAsync(new CommandDefinition(sql,new{TenantId=context.Session.TenantId,SessionId=context.Session.IntakeSessionId,ModuleCode=context.Session.ModuleCode},cancellationToken:cancellationToken));
    }

    public Task SaveOcrResultAsync(DocumentIntakeProcessingContext context,DocumentOcrResult result,string outputReference,string inputHash,string outputHash,CancellationToken cancellationToken=default)=>SaveExecutionAsync(context,"OCR",result.ProviderCode,result.ModelName,null,null,context.StoragePath??string.Empty,outputReference,inputHash,outputHash,result.Confidence,result.DurationMilliseconds,null,null,cancellationToken);
    public Task SaveInterpretationAsync(DocumentIntakeProcessingContext context,DocumentInterpretationResult result,string outputReference,string inputHash,string outputHash,CancellationToken cancellationToken=default)=>ExecuteTransactionAsync(async(c,t)=>{await InsertExecutionAsync(c,t,context,"INTERPRETATION",result.ProviderCode,result.ModelName,result.PromptCode,result.PromptVersion,context.OcrOutputReference??string.Empty,outputReference,inputHash,outputHash,result.Fields.Count==0?null:result.Fields.Average(x=>x.Confidence),result.DurationMilliseconds,result.InputTokenCount,result.OutputTokenCount,cancellationToken);foreach(var field in result.Fields)await c.ExecuteAsync(new CommandDefinition(@"MERGE DMS.IntakeDraftField AS target USING(SELECT @TenantId TenantId,@SessionId IntakeSessionId,@EntityType EntityTypeCode,@EntityKey EntityKey,@Path FieldPath)source ON target.TenantId=source.TenantId AND target.IntakeSessionId=source.IntakeSessionId AND target.EntityTypeCode=source.EntityTypeCode AND target.EntityKey=source.EntityKey AND target.FieldPath=source.FieldPath WHEN MATCHED THEN UPDATE SET ExtractedValue=@Value,ValueTypeCode=@ValueType,Confidence=@Confidence,SourceDocumentId=@DocumentId,SourcePageNumber=@Page,SourceBoundingBoxJson=@Box,ReviewStatusCode=N'PENDING',ModifiedDateUtc=SYSUTCDATETIME() WHEN NOT MATCHED THEN INSERT(TenantId,IntakeSessionId,EntityTypeCode,EntityKey,FieldPath,ExtractedValue,ValueTypeCode,Confidence,SourceDocumentId,SourcePageNumber,SourceBoundingBoxJson)VALUES(@TenantId,@SessionId,@EntityType,@EntityKey,@Path,@Value,@ValueType,@Confidence,@DocumentId,@Page,@Box);",new{TenantId=context.Session.TenantId,SessionId=context.Session.IntakeSessionId,EntityType=field.EntityTypeCode,EntityKey=field.EntityKey,Path=field.Path,Value=field.Value,ValueType=field.ValueTypeCode,Confidence=field.Confidence,DocumentId=context.WorkItem.DocumentId,Page=field.SourcePage,Box=field.BoundingBoxJson},t,cancellationToken:cancellationToken));foreach(var warning in result.Warnings)await c.ExecuteAsync(new CommandDefinition(@"INSERT DMS.IntakeIssue(TenantId,IntakeSessionId,IssueCode,IssueTypeCode,SeverityCode,FieldPath,Message,StatusCode)VALUES(@TenantId,@SessionId,@Code,N'AI_WARNING',@Severity,@Path,@Message,N'OPEN');",new{TenantId=context.Session.TenantId,SessionId=context.Session.IntakeSessionId,Code=warning.Code,Severity=warning.SeverityCode,Path=warning.FieldPath,warning.Message},t,cancellationToken:cancellationToken));},cancellationToken);
    public Task SaveNormalizedFieldsAsync(DocumentIntakeProcessingContext context,IReadOnlyCollection<KnowledgeNormalizedField> fields,CancellationToken cancellationToken=default)=>ExecuteTransactionAsync(async(c,t)=>{foreach(var f in fields)await c.ExecuteAsync(new CommandDefinition(@"UPDATE DMS.IntakeDraftField SET NormalizedValue=@NormalizedValue,KnowledgeConceptId=@KnowledgeConceptId,MappingStatusCode=@MappingStatusCode,ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND EntityTypeCode=@EntityType AND EntityKey=@EntityKey AND FieldPath=@FieldPath;",new{f.NormalizedValue,f.KnowledgeConceptId,f.MappingStatusCode,TenantId=context.Session.TenantId,SessionId=context.Session.IntakeSessionId,EntityType=f.EntityTypeCode,EntityKey=f.EntityKey,f.FieldPath},t,cancellationToken:cancellationToken));},cancellationToken);
    public async Task CompleteWorkItemAsync(Guid workItemId,string leaseOwner,CancellationToken cancellationToken=default){const string sql=@"DECLARE @TenantId UNIQUEIDENTIFIER,@SessionId UNIQUEIDENTIFIER,@DocumentId UNIQUEIDENTIFIER,@Type NVARCHAR(50),@Attempt INT,@CorrelationId NVARCHAR(120); SELECT @TenantId=TenantId,@SessionId=IntakeSessionId,@DocumentId=DocumentId,@Type=WorkTypeCode,@Attempt=AttemptCount,@CorrelationId=CorrelationId FROM DMS.IntakeWorkItem WITH(UPDLOCK) WHERE IntakeWorkItemId=@Id AND LeaseOwner=@Owner AND StatusCode=N'PROCESSING'; IF @SessionId IS NULL THROW 51000,'Work item lease was lost.',1;
UPDATE DMS.IntakeWorkItem SET StatusCode=N'COMPLETED',CompletedDateUtc=SYSUTCDATETIME(),LeaseOwner=NULL,LeaseExpiresDateUtc=NULL,LastErrorCode=NULL,LastErrorMessage=NULL WHERE IntakeWorkItemId=@Id AND LeaseOwner=@Owner;
UPDATE DMS.IntakeWorkAttempt SET StatusCode=N'COMPLETED',CompletedDateUtc=SYSUTCDATETIME(),DurationMilliseconds=DATEDIFF_BIG(MILLISECOND,StartedDateUtc,SYSUTCDATETIME()) WHERE IntakeWorkItemId=@Id AND AttemptNumber=@Attempt;
DECLARE @NextType NVARCHAR(50)=CASE @Type WHEN N'OCR' THEN N'CLASSIFICATION' WHEN N'CLASSIFICATION' THEN N'EXTRACTION' WHEN N'EXTRACTION' THEN N'KNOWLEDGE_MAPPING' WHEN N'KNOWLEDGE_MAPPING' THEN N'VALIDATION' WHEN N'VALIDATION' THEN N'SEARCH_INDEXING' END;
DECLARE @NextSequence INT=CASE @NextType WHEN N'CLASSIFICATION' THEN 2 WHEN N'EXTRACTION' THEN 3 WHEN N'KNOWLEDGE_MAPPING' THEN 4 WHEN N'VALIDATION' THEN 5 WHEN N'SEARCH_INDEXING' THEN 6 END;
IF @NextType IS NOT NULL AND NOT EXISTS(SELECT 1 FROM DMS.IntakeWorkItem WHERE TenantId=@TenantId AND IdempotencyKey=CONCAT(@SessionId,N':',COALESCE(CONVERT(NVARCHAR(36),@DocumentId),N'SESSION'),N':',@NextType))
 INSERT DMS.IntakeWorkItem(TenantId,IntakeSessionId,DocumentId,WorkTypeCode,StatusCode,IdempotencyKey,SequenceNumber,CorrelationId) VALUES(@TenantId,@SessionId,@DocumentId,@NextType,N'PENDING',CONCAT(@SessionId,N':',COALESCE(CONVERT(NVARCHAR(36),@DocumentId),N'SESSION'),N':',@NextType),@NextSequence,@CorrelationId);
IF @NextType IS NULL AND NOT EXISTS(SELECT 1 FROM DMS.IntakeWorkItem WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND StatusCode NOT IN(N'COMPLETED',N'CANCELLED') AND IntakeWorkItemId<>@Id) UPDATE DMS.IntakeSession SET StatusCode=CASE WHEN EXISTS(SELECT 1 FROM DMS.IntakeDraftField WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND ReviewStatusCode=N'PENDING') OR EXISTS(SELECT 1 FROM DMS.IntakeIssue WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND StatusCode=N'OPEN') THEN N'REVIEW_REQUIRED' ELSE N'READY' END,OverallConfidence=(SELECT AVG(Confidence) FROM DMS.IntakeDraftField WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND Confidence IS NOT NULL),WarningCount=(SELECT COUNT(1) FROM DMS.IntakeIssue WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND SeverityCode=N'WARNING' AND StatusCode=N'OPEN'),ErrorCount=(SELECT COUNT(1) FROM DMS.IntakeIssue WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND SeverityCode=N'ERROR' AND StatusCode=N'OPEN'),ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND StatusCode=N'PROCESSING';";await ExecuteTransactionAsync((c,t)=>c.ExecuteAsync(new CommandDefinition(sql,new{Id=workItemId,Owner=leaseOwner},t,cancellationToken:cancellationToken)),cancellationToken);}
    public async Task FailWorkItemAsync(Guid workItemId,string leaseOwner,string errorCode,string errorMessage,bool retryable,CancellationToken cancellationToken=default){const string sql=@"DECLARE @SessionId UNIQUEIDENTIFIER,@TenantId UNIQUEIDENTIFIER,@Attempt INT,@Terminal BIT=0; SELECT @SessionId=IntakeSessionId,@TenantId=TenantId,@Attempt=AttemptCount,@Terminal=CASE WHEN @Retryable=0 OR AttemptCount>=MaxAttempts THEN 1 ELSE 0 END FROM DMS.IntakeWorkItem WITH(UPDLOCK) WHERE IntakeWorkItemId=@Id AND LeaseOwner=@Owner AND StatusCode=N'PROCESSING'; IF @SessionId IS NULL THROW 51000,'Work item lease was lost.',1;
UPDATE DMS.IntakeWorkItem SET StatusCode=CASE WHEN @Retryable=1 AND AttemptCount<MaxAttempts THEN N'RETRY_SCHEDULED' WHEN AttemptCount>=MaxAttempts THEN N'DEAD_LETTERED' ELSE N'FAILED' END,AvailableDateUtc=DATEADD(SECOND,CASE AttemptCount WHEN 1 THEN 30 WHEN 2 THEN 120 WHEN 3 THEN 600 WHEN 4 THEN 1800 ELSE 7200 END,SYSUTCDATETIME()),LastErrorCode=@Code,LastErrorMessage=LEFT(@Message,4000),LeaseOwner=NULL,LeaseExpiresDateUtc=NULL WHERE IntakeWorkItemId=@Id AND LeaseOwner=@Owner;
UPDATE DMS.IntakeWorkAttempt SET StatusCode=CASE WHEN @Terminal=1 THEN N'FAILED' ELSE N'RETRY_SCHEDULED' END,CompletedDateUtc=SYSUTCDATETIME(),DurationMilliseconds=DATEDIFF_BIG(MILLISECOND,StartedDateUtc,SYSUTCDATETIME()),ErrorCode=@Code,ErrorMessage=LEFT(@Message,4000) WHERE IntakeWorkItemId=@Id AND AttemptNumber=@Attempt;
IF @Terminal=1 UPDATE DMS.IntakeSession SET StatusCode=N'FAILED',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId=@TenantId AND IntakeSessionId=@SessionId AND StatusCode<>N'CANCELLED';";await ExecuteTransactionAsync((c,t)=>c.ExecuteAsync(new CommandDefinition(sql,new{Id=workItemId,Owner=leaseOwner,Retryable=retryable,Code=errorCode,Message=errorMessage},t,cancellationToken:cancellationToken)),cancellationToken);}

    private Task SaveExecutionAsync(DocumentIntakeProcessingContext context,string type,string provider,string model,string? promptCode,string? promptVersion,string inputReference,string outputReference,string inputHash,string outputHash,decimal? confidence,long duration,int? inputTokens,int? outputTokens,CancellationToken cancellationToken)=>ExecuteTransactionAsync((c,t)=>InsertExecutionAsync(c,t,context,type,provider,model,promptCode,promptVersion,inputReference,outputReference,inputHash,outputHash,confidence,duration,inputTokens,outputTokens,cancellationToken),cancellationToken);
    private static Task InsertExecutionAsync(IDbConnection c,IDbTransaction t,DocumentIntakeProcessingContext context,string type,string provider,string model,string? promptCode,string? promptVersion,string inputReference,string outputReference,string inputHash,string outputHash,decimal? confidence,long duration,int? inputTokens,int? outputTokens,CancellationToken token)=>c.ExecuteAsync(new CommandDefinition(@"INSERT DMS.AiExecution(TenantId,IntakeSessionId,IntakeWorkItemId,DocumentId,ExecutionTypeCode,ProviderCode,ModelName,PromptCode,PromptVersion,InputReference,OutputReference,InputHashSha256,OutputHashSha256,Confidence,DurationMilliseconds,InputTokenCount,OutputTokenCount,StatusCode)VALUES(@TenantId,@SessionId,@WorkId,@DocumentId,@Type,@Provider,@Model,@PromptCode,@PromptVersion,@InputReference,@OutputReference,@InputHash,@OutputHash,@Confidence,@Duration,@InputTokens,@OutputTokens,N'COMPLETED');",new{TenantId=context.Session.TenantId,SessionId=context.Session.IntakeSessionId,WorkId=context.WorkItem.IntakeWorkItemId,DocumentId=context.WorkItem.DocumentId,Type=type,Provider=provider,Model=model,PromptCode=promptCode,PromptVersion=promptVersion,InputReference=inputReference,OutputReference=outputReference,InputHash=inputHash,OutputHash=outputHash,Confidence=confidence,Duration=duration,InputTokens=inputTokens,OutputTokens=outputTokens},t,cancellationToken:token));
    private async Task ExecuteTransactionAsync(Func<IDbConnection,IDbTransaction,Task> action,CancellationToken token){using var connection=await _connectionFactory.CreateOpenConnectionAsync(token);using var transaction=connection.BeginTransaction();try{await action(connection,transaction);transaction.Commit();}catch{transaction.Rollback();throw;}}
    private static void EnsureOne(int count,string message){if(count!=1)throw new DBConcurrencyException(message);}
    private sealed record FieldValue(string FieldPath,string? Value);
    private sealed record DocumentRow(Guid IntakeSessionDocumentId,Guid DocumentId,string FileName,string ContentType,long FileSizeBytes,string DocumentRoleCode,string? ContentHashSha256,int SequenceNumber,string StoragePath){public DocumentIntakeDocumentDto ToDto()=>new(IntakeSessionDocumentId,DocumentId,FileName,ContentType,FileSizeBytes,DocumentRoleCode,ContentHashSha256,SequenceNumber);}
    private sealed record OutputRow(string OcrOutputReference);
    private sealed record PromptRow(string PromptCode,string PromptVersion,string SystemPrompt,string OutputSchemaJson);
}
