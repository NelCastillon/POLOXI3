using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyEndorsements;
using Dapper;
using System.Data;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PolicyEndorsementRepository : IPolicyEndorsementRepository
{
    private const string EndorsementColumns = @"EndorsementId, TenantId, PolicyId, PolicyVersionBeforeId, PolicyVersionAfterId, AccountId, EndorsementNumber, PolicyNumber, AccountName, LineOfBusiness,
        Carrier, EndorsementType, ReasonCode, CarrierMethodCode, RequestSourceCode, ChangeCategoryCode, Description, EffectiveDate, ExpirationDate, RetroactiveDate,
        DiscoveryDate, RequestedDateUtc, PremiumDelta, AgencyFeeDelta, TaxDelta, TaxFeeDelta, TotalCostDelta, ProratedPremiumDelta, CurrencyCode, Status, Priority,
        RequestedByName, RequestedByEmail, RequestedByPhone, ClientContactName, ClientContactEmail, ClientContactPhone,
        AssignedToName, UnderwriterName, UnderwriterEmail, CarrierSubmissionDateUtc, CarrierResponseDueDate, CarrierReferenceNumber,
        BrokerOfRecordRequired, AgentAuthorityCode, ApprovalLevelCode, ApprovedByName, IssuedByName, BillingImpactCode,
        CommissionImpactCode, BillingInstruction, DocumentDeliveryCode, CertificateRequired, FormsRequired, AcordFormNumbers,
        ExternalReferenceNumber, ComplianceReviewRequired, EoExposureNotes, InternalNotes, ClientFacingNotes, Reason,
        RequiredDocuments, WorkflowStage, DueDate, ApprovedDateUtc, IssuedDateUtc, SubmittedDateUtc, CompletedDateUtc, RejectedDateUtc, CancelledDateUtc,
        ReversalOfEndorsementId, ReversedByEndorsementId, RowVersion, IsUrgent, IsArchived";

    private readonly ISqlConnectionFactory _connectionFactory;

    public PolicyEndorsementRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PolicyEndorsementCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {EndorsementColumns}
FROM Policy.PolicyEndorsement
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsArchived = 0
ORDER BY IsUrgent DESC, DueDate, RequestedDateUtc DESC;

SELECT ActivityId, EndorsementId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc
FROM Policy.PolicyEndorsementActivity
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY ActivityDateUtc DESC;

SELECT DeltaId, EndorsementId, TenantId, FieldName, BeforeValue, AfterValue, NumericDelta
FROM Policy.PolicyEndorsementDelta
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY CreatedDateUtc;

SELECT OptionId, TenantId, OptionGroupCode, OptionCode, DisplayName, Description, IsDefault, IsActive, SortOrder
FROM Policy.PolicyEndorsementOption
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
ORDER BY OptionGroupCode, SortOrder, DisplayName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return new PolicyEndorsementCenterDto
        {
            Endorsements = (await multi.ReadAsync<PolicyEndorsementDto>()).AsList(),
            Activities = (await multi.ReadAsync<PolicyEndorsementActivityDto>()).AsList(),
            Deltas = (await multi.ReadAsync<PolicyEndorsementDeltaDto>()).AsList(),
            Options = (await multi.ReadAsync<PolicyEndorsementOptionDto>()).AsList()
        };
    }

    public async Task<PolicyEndorsementWorkflowDetailDto?> GetWorkflowDetailAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {EndorsementColumns} FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND IsDeleted=0;
SELECT ChangeId,TenantId,EndorsementId,CategoryCode,OperationCode,EntityKey,SequenceNumber,Summary FROM Policy.PolicyEndorsementChange WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND IsDeleted=0 ORDER BY SequenceNumber;
SELECT typed.* FROM Policy.PolicyEndorsementInsuredChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0;
SELECT typed.* FROM Policy.PolicyEndorsementVehicleChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0;
SELECT typed.* FROM Policy.PolicyEndorsementDriverChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0;
SELECT typed.* FROM Policy.PolicyEndorsementCoverageChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0;
SELECT typed.* FROM Policy.PolicyEndorsementPropertyChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0;
SELECT typed.* FROM Policy.PolicyEndorsementCommercialChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0;
SELECT typed.* FROM Policy.PolicyEndorsementFinancialChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0;
SELECT typed.* FROM Policy.PolicyEndorsementLegalChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0;
SELECT ApprovalId,TenantId,EndorsementId,ApprovalLevelCode,StatusCode,RequestedDateUtc,RequestedByUserId,AssignedToUserId,DecidedDateUtc,DecidedByUserId,DecisionNotes FROM Policy.PolicyEndorsementApproval WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND IsDeleted=0 ORDER BY RequestedDateUtc;
SELECT EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,FromStatusCode,ToStatusCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId FROM Policy.PolicyEndorsementEvent WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId ORDER BY OccurredDateUtc DESC;
SELECT StatusTransitionId,FromStatusCode,ToStatusCode,RequiredPermissionCode,RequiresApproval,RequiresCarrierSubmission,CreatesPolicyVersion,CreatesAccountingWork,CreatesDocumentWork FROM Policy.PolicyEndorsementStatusTransition transitionRule WHERE transitionRule.TenantId=@TenantId AND transitionRule.FromStatusCode=(SELECT Status FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId) AND transitionRule.IsActive=1 AND transitionRule.IsDeleted=0 ORDER BY SortOrder;
SELECT CarrierDispatchId,ChannelCode,StatusCode,ExternalReferenceNumber,AttemptCount,MaxAttempts,NextAttemptDateUtc,CompletedDateUtc,ErrorCode,ErrorMessage FROM Policy.PolicyEndorsementCarrierDispatch WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;
SELECT AccountingWorkId,WorkTypeCode,StatusCode,CurrencyCode,PremiumAmount,FeeAmount,TaxAmount,TotalAmount,ResultEntityName,ResultEntityId,ErrorMessage FROM Policy.PolicyEndorsementAccountingWork WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND IsDeleted=0 ORDER BY CreatedDateUtc;
SELECT DocumentWorkId,DocumentTypeCode,StatusCode,DocumentId,ErrorMessage,CompletedDateUtc FROM Policy.PolicyEndorsementDocumentWork WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND IsDeleted=0 ORDER BY CreatedDateUtc;
SELECT PolicyVersionId,PolicyId,PolicyTermId,PolicyTransactionId,VersionNumber,VersionReasonCode,SnapshotJson,CreatedDateUtc FROM Policy.PolicyVersion WHERE TenantId=@TenantId AND PolicyId=(SELECT PolicyId FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId) AND IsDeleted=0 ORDER BY VersionNumber DESC;
SELECT ActivityId,EndorsementId,TenantId,ActivityType,Subject,Notes,CreatedByName,ActivityDateUtc FROM Policy.PolicyEndorsementActivity WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND IsDeleted=0 ORDER BY ActivityDateUtc DESC;";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, EndorsementId = endorsementId }, cancellationToken: cancellationToken));
        var endorsement = await multi.ReadSingleOrDefaultAsync<PolicyEndorsementDto>();
        if (endorsement is null) return null;

        var changes = (await multi.ReadAsync<PolicyEndorsementChangeDto>()).AsList();
        Attach(changes, (await multi.ReadAsync<PolicyEndorsementInsuredChangeDto>()).ToDictionary(x => x.ChangeId), static (change, value) => change.Insured = value);
        Attach(changes, (await multi.ReadAsync<PolicyEndorsementVehicleChangeDto>()).ToDictionary(x => x.ChangeId), static (change, value) => change.Vehicle = value);
        Attach(changes, (await multi.ReadAsync<PolicyEndorsementDriverChangeDto>()).ToDictionary(x => x.ChangeId), static (change, value) => change.Driver = value);
        Attach(changes, (await multi.ReadAsync<PolicyEndorsementCoverageChangeDto>()).ToDictionary(x => x.ChangeId), static (change, value) => change.Coverage = value);
        Attach(changes, (await multi.ReadAsync<PolicyEndorsementPropertyChangeDto>()).ToDictionary(x => x.ChangeId), static (change, value) => change.Property = value);
        Attach(changes, (await multi.ReadAsync<PolicyEndorsementCommercialChangeDto>()).ToDictionary(x => x.ChangeId), static (change, value) => change.Commercial = value);
        Attach(changes, (await multi.ReadAsync<PolicyEndorsementFinancialChangeDto>()).ToDictionary(x => x.ChangeId), static (change, value) => change.Financial = value);
        Attach(changes, (await multi.ReadAsync<PolicyEndorsementLegalChangeDto>()).ToDictionary(x => x.ChangeId), static (change, value) => change.Legal = value);

        return new PolicyEndorsementWorkflowDetailDto
        {
            Endorsement = endorsement,
            FinancialImpact = new PolicyEndorsementFinancialImpactDto { CurrencyCode=endorsement.CurrencyCode,PremiumChange=endorsement.PremiumDelta,AgencyFee=endorsement.AgencyFeeDelta,Taxes=endorsement.TaxDelta,TotalDue=endorsement.TotalCostDelta,ProratedPremiumChange=endorsement.ProratedPremiumDelta,BillingImpactCode=endorsement.BillingImpactCode,CommissionImpactCode=endorsement.CommissionImpactCode },
            Changes = changes,
            Approvals = (await multi.ReadAsync<PolicyEndorsementApprovalDto>()).AsList(),
            Timeline = (await multi.ReadAsync<PolicyEndorsementEventDto>()).AsList(),
            AvailableTransitions = (await multi.ReadAsync<PolicyEndorsementTransitionDto>()).AsList(),
            CarrierDispatches = (await multi.ReadAsync<PolicyEndorsementCarrierDispatchDto>()).AsList(),
            AccountingWork = (await multi.ReadAsync<PolicyEndorsementAccountingWorkDto>()).AsList(),
            DocumentWork = (await multi.ReadAsync<PolicyEndorsementDocumentWorkDto>()).AsList(),
            Versions = (await multi.ReadAsync<PolicyVersionDto>()).AsList(),
            Activities = (await multi.ReadAsync<PolicyEndorsementActivityDto>()).AsList()
        };
    }

    public async Task<PolicyEndorsementPolicyWorkspaceDto?> GetPolicyWorkspaceAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT policy.PolicyId,policy.PolicyNumber,COALESCE(account.AccountName,N'') AccountName,COALESCE(carrier.CarrierName,carrier.Name,N'') CarrierName,policy.LineOfBusiness,COALESCE(policy.CoverageStatus,policy.Status,N'Active') Status,policy.EffectiveDate,policy.ExpirationDate,COALESCE(policy.AnnualPremium,0) AnnualPremium
FROM Submissions.BoundPolicy policy LEFT JOIN Client.Account account ON account.TenantId=policy.TenantId AND account.AccountId=policy.AccountId AND account.IsDeleted=0 LEFT JOIN Agency.Carrier carrier ON carrier.TenantId=policy.TenantId AND carrier.CarrierId=policy.CarrierId AND carrier.IsDeleted=0
WHERE policy.TenantId=@TenantId AND policy.PolicyId=@PolicyId AND policy.IsDeleted=0;
SELECT TOP 1 PolicyVersionId,PolicyId,PolicyTermId,PolicyTransactionId,VersionNumber,VersionReasonCode,SnapshotJson,CreatedDateUtc FROM Policy.PolicyVersion WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 ORDER BY VersionNumber DESC;
SELECT {EndorsementColumns} FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 AND IsArchived=0 ORDER BY RequestedDateUtc DESC;
SELECT EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,FromStatusCode,ToStatusCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId FROM Policy.PolicyEndorsementEvent WHERE TenantId=@TenantId AND PolicyId=@PolicyId ORDER BY OccurredDateUtc DESC;
SELECT OptionId,TenantId,OptionGroupCode,OptionCode,DisplayName,Description,IsDefault,IsActive,SortOrder FROM Policy.PolicyEndorsementOption WHERE TenantId=@TenantId AND IsDeleted=0 AND IsActive=1 ORDER BY OptionGroupCode,SortOrder;";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId=tenantId, PolicyId=policyId }, cancellationToken:cancellationToken));
        var policy = await multi.ReadSingleOrDefaultAsync<PolicyLifecyclePolicySummaryDto>();
        if (policy is null) return null;
        return new PolicyEndorsementPolicyWorkspaceDto { TenantId=tenantId,PolicyId=policyId,Policy=policy,CurrentVersion=await multi.ReadSingleOrDefaultAsync<PolicyVersionDto>(),Endorsements=(await multi.ReadAsync<PolicyEndorsementDto>()).AsList(),Timeline=(await multi.ReadAsync<PolicyEndorsementEventDto>()).AsList(),Options=(await multi.ReadAsync<PolicyEndorsementOptionDto>()).AsList() };
    }

    public async Task<Guid> CreateTransactionAsync(CreatePolicyEndorsementTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var endorsementId = Guid.NewGuid();
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            const string sql = @"
DECLARE @LockResult INT; EXEC @LockResult=sys.sp_getapplock @Resource=CONCAT(N'PolicyEndorsementNumber:',CONVERT(NVARCHAR(36),@TenantId)),@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=30000; IF @LockResult<0 THROW 52400,N'Unable to allocate an endorsement transaction number.',1;
DECLARE @PolicyNumber NVARCHAR(50),@AccountId UNIQUEIDENTIFIER,@AccountName NVARCHAR(200),@LineOfBusiness NVARCHAR(100),@Carrier NVARCHAR(160),@RequestedByName NVARCHAR(160),@RequestedByEmail NVARCHAR(254),@VersionBefore UNIQUEIDENTIFIER,@NextNumber INT,@EndorsementNumber NVARCHAR(50);
SELECT @PolicyNumber=policy.PolicyNumber,@AccountId=policy.AccountId,@AccountName=account.AccountName,@LineOfBusiness=policy.LineOfBusiness,@Carrier=COALESCE(carrier.CarrierName,carrier.Name) FROM Submissions.BoundPolicy policy LEFT JOIN Client.Account account ON account.TenantId=policy.TenantId AND account.AccountId=policy.AccountId AND account.IsDeleted=0 LEFT JOIN Agency.Carrier carrier ON carrier.TenantId=policy.TenantId AND carrier.CarrierId=policy.CarrierId AND carrier.IsDeleted=0 WHERE policy.TenantId=@TenantId AND policy.PolicyId=@PolicyId AND policy.IsDeleted=0;
IF @PolicyNumber IS NULL THROW 52401,N'The policy was not found in the authenticated tenant.',1;
SELECT @RequestedByName=COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(FirstName,N' ',LastName))),N''),Email),@RequestedByEmail=Email FROM IAM.[User] WHERE TenantId=@TenantId AND UserId=@CreatedByUserId AND IsDeleted=0;
IF @RequestedByName IS NULL THROW 52402,N'The authenticated user was not found in the tenant.',1;
SELECT TOP 1 @VersionBefore=PolicyVersionId FROM Policy.PolicyVersion WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 ORDER BY VersionNumber DESC;
SELECT @NextNumber=COUNT_BIG(1)+1 FROM Policy.PolicyEndorsement WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId;
SET @EndorsementNumber=CONCAT(N'END-',FORMAT(SYSUTCDATETIME(),N'yyyy'),N'-',FORMAT(@NextNumber,N'000000'));
IF @ReversalOfEndorsementId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsement WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND EndorsementId=@ReversalOfEndorsementId AND PolicyId=@PolicyId AND Status=N'Completed' AND ReversedByEndorsementId IS NULL AND IsDeleted=0) THROW 52404,N'The completed endorsement is not eligible for reversal.',1;
INSERT Policy.PolicyEndorsement(EndorsementId,TenantId,PolicyId,PolicyVersionBeforeId,AccountId,EndorsementNumber,PolicyNumber,AccountName,LineOfBusiness,Carrier,EndorsementType,ReasonCode,CarrierMethodCode,RequestSourceCode,ChangeCategoryCode,Description,EffectiveDate,RequestedDateUtc,PremiumDelta,AgencyFeeDelta,TaxDelta,TaxFeeDelta,TotalCostDelta,ProratedPremiumDelta,CurrencyCode,Status,Priority,RequestedByName,RequestedByEmail,AssignedToName,BillingImpactCode,CommissionImpactCode,InternalNotes,ClientFacingNotes,Reason,WorkflowStage,DueDate,IsUrgent,IsArchived,ReversalOfEndorsementId,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(@EndorsementId,@TenantId,@PolicyId,@VersionBefore,@AccountId,@EndorsementNumber,@PolicyNumber,COALESCE(@AccountName,N''),COALESCE(@LineOfBusiness,N''),COALESCE(@Carrier,N''),@EndorsementTypeCode,@ReasonCode,@CarrierMethodCode,N'AgencyRequest',CASE WHEN @PremiumChange=0 AND @AgencyFee=0 AND @Taxes=0 THEN N'NonPremium' ELSE N'PremiumBearing' END,@Description,@EffectiveDate,SYSUTCDATETIME(),@PremiumChange,@AgencyFee,@Taxes,@Taxes,@PremiumChange+@AgencyFee+@Taxes,CASE WHEN @ProratedPremiumChange=0 THEN @PremiumChange ELSE @ProratedPremiumChange END,@CurrencyCode,N'Draft',@PriorityCode,@RequestedByName,@RequestedByEmail,@RequestedByName,@BillingImpactCode,@CommissionImpactCode,@InternalNotes,@ClientFacingNotes,@ReasonCode,N'Draft',@DueDate,@IsUrgent,0,@ReversalOfEndorsementId,SYSUTCDATETIME(),@CreatedByUserId,0);
IF @ReversalOfEndorsementId IS NOT NULL UPDATE Policy.PolicyEndorsement SET ReversedByEndorsementId=@EndorsementId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@CreatedByUserId WHERE TenantId=@TenantId AND EndorsementId=@ReversalOfEndorsementId;
INSERT Policy.PolicyEndorsementActivity(ActivityId,EndorsementId,TenantId,ActivityType,Subject,Notes,CreatedByName,ActivityDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@EndorsementId,@TenantId,N'Created',N'Endorsement draft created',@Description,@RequestedByName,SYSUTCDATETIME(),SYSUTCDATETIME(),@CreatedByUserId,0);
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,ToStatusCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId) VALUES(NEWID(),@TenantId,@EndorsementId,@PolicyId,N'Created',N'Draft',N'Endorsement draft created.',JSON_OBJECT(N'endorsementNumber':@EndorsementNumber,N'policyVersionBeforeId':@VersionBefore),NEWID(),SYSUTCDATETIME(),@CreatedByUserId);";
            await connection.ExecuteAsync(new CommandDefinition(sql, new { EndorsementId=endorsementId,request.TenantId,request.PolicyId,request.EndorsementTypeCode,request.ReasonCode,request.CarrierMethodCode,request.Description,request.EffectiveDate,request.PriorityCode,request.InternalNotes,request.ClientFacingNotes,request.DueDate,request.IsUrgent,request.CreatedByUserId,request.ReversalOfEndorsementId,request.FinancialImpact.CurrencyCode,request.FinancialImpact.PremiumChange,request.FinancialImpact.AgencyFee,request.FinancialImpact.Taxes,request.FinancialImpact.ProratedPremiumChange,request.FinancialImpact.BillingImpactCode,request.FinancialImpact.CommissionImpactCode }, transaction, cancellationToken:cancellationToken));
            await PersistChangesAsync(connection, transaction, request.TenantId, endorsementId, request.Changes, request.CreatedByUserId, cancellationToken);
            transaction.Commit();
            return endorsementId;
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task SaveDraftAsync(Guid endorsementId, SavePolicyEndorsementDraftRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        try
        {
            const string sql = @"
UPDATE Policy.PolicyEndorsement SET EndorsementType=@EndorsementTypeCode,ReasonCode=@ReasonCode,CarrierMethodCode=@CarrierMethodCode,Description=@Description,EffectiveDate=@EffectiveDate,PremiumDelta=@PremiumChange,AgencyFeeDelta=@AgencyFee,TaxDelta=@Taxes,TaxFeeDelta=@Taxes,TotalCostDelta=@PremiumChange+@AgencyFee+@Taxes,ProratedPremiumDelta=CASE WHEN @ProratedPremiumChange=0 THEN @PremiumChange ELSE @ProratedPremiumChange END,CurrencyCode=@CurrencyCode,Priority=@PriorityCode,BillingImpactCode=@BillingImpactCode,CommissionImpactCode=@CommissionImpactCode,InternalNotes=@InternalNotes,ClientFacingNotes=@ClientFacingNotes,DueDate=@DueDate,IsUrgent=@IsUrgent,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId
WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND Status IN(N'Draft',N'NeedMoreInfo') AND RowVersion=@RowVersion AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52403,N'The endorsement draft was changed, is no longer editable, or was not found in the tenant.',1;
UPDATE Policy.PolicyEndorsementChange SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND IsDeleted=0;
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,ToStatusCode,Description,CorrelationId,OccurredDateUtc,ActorUserId) SELECT NEWID(),TenantId,EndorsementId,PolicyId,N'DraftSaved',Status,N'Endorsement draft and typed changes saved.',NEWID(),SYSUTCDATETIME(),@ModifiedByUserId FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId;";
            await connection.ExecuteAsync(new CommandDefinition(sql, new { EndorsementId=endorsementId,request.TenantId,request.EndorsementTypeCode,request.ReasonCode,request.CarrierMethodCode,request.Description,request.EffectiveDate,request.PriorityCode,request.InternalNotes,request.ClientFacingNotes,request.DueDate,request.IsUrgent,request.RowVersion,request.ModifiedByUserId,request.FinancialImpact.CurrencyCode,request.FinancialImpact.PremiumChange,request.FinancialImpact.AgencyFee,request.FinancialImpact.Taxes,request.FinancialImpact.ProratedPremiumChange,request.FinancialImpact.BillingImpactCode,request.FinancialImpact.CommissionImpactCode }, transaction, cancellationToken:cancellationToken));
            await PersistChangesAsync(connection, transaction, request.TenantId, endorsementId, request.Changes, request.ModifiedByUserId, cancellationToken);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task TransitionAsync(Guid endorsementId, TransitionPolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON; SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRAN;
DECLARE @FromStatus NVARCHAR(80),@PolicyId UNIQUEIDENTIFIER,@PolicyVersionId UNIQUEIDENTIFIER,@VersionNumber INT,@SnapshotJson NVARCHAR(MAX),@ActorName NVARCHAR(160),@RequiresApproval BIT,@CreatesVersion BIT,@EffectiveDate DATETIME2,@PolicyEffectiveDate DATETIME2,@PolicyExpirationDate DATETIME2,@ReversalOfEndorsementId UNIQUEIDENTIFIER;
SELECT @FromStatus=endorsement.Status,@PolicyId=endorsement.PolicyId,@EffectiveDate=endorsement.EffectiveDate,@ReversalOfEndorsementId=endorsement.ReversalOfEndorsementId FROM Policy.PolicyEndorsement endorsement WITH(UPDLOCK,HOLDLOCK) WHERE endorsement.TenantId=@TenantId AND endorsement.EndorsementId=@EndorsementId AND endorsement.IsDeleted=0;
IF @PolicyId IS NULL THROW 52410,N'The endorsement was not found in the authenticated tenant.',1;
SELECT @ActorName=COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(FirstName,N' ',LastName))),N''),Email) FROM IAM.[User] WHERE TenantId=@TenantId AND UserId=@ActorUserId AND IsDeleted=0;
IF @ActorName IS NULL THROW 52411,N'The authenticated user was not found in the tenant.',1;
SELECT @RequiresApproval=RequiresApproval,@CreatesVersion=CreatesPolicyVersion FROM Policy.PolicyEndorsementStatusTransition WHERE TenantId=@TenantId AND FromStatusCode=@FromStatus AND ToStatusCode=@ToStatusCode AND IsActive=1 AND IsDeleted=0;
IF @RequiresApproval IS NULL THROW 52412,N'The requested endorsement status transition is not allowed.',1;
IF NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementChange WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND IsDeleted=0) THROW 52413,N'At least one typed policy change is required before workflow submission.',1;
SELECT @PolicyEffectiveDate=EffectiveDate,@PolicyExpirationDate=ExpirationDate FROM Submissions.BoundPolicy WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0;
IF @EffectiveDate<@PolicyEffectiveDate OR @EffectiveDate>@PolicyExpirationDate THROW 52414,N'The endorsement effective date must be within the active policy term.',1;
IF @RequiresApproval=1 AND NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementApproval WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND StatusCode=N'Approved' AND IsDeleted=0) THROW 52415,N'An approved internal review is required for this transition.',1;

IF @ToStatusCode=N'PendingReview' AND NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementApproval WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND IsDeleted=0)
INSERT Policy.PolicyEndorsementApproval(ApprovalId,TenantId,EndorsementId,ApprovalLevelCode,StatusCode,RequestedDateUtc,RequestedByUserId,CreatedDateUtc,IsDeleted)
SELECT NEWID(),TenantId,EndorsementId,COALESCE(ApprovalLevelCode,N'StandardAuthority'),N'Pending',SYSUTCDATETIME(),@ActorUserId,SYSUTCDATETIME(),0 FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId;

IF @CreatesVersion=1
BEGIN
    SELECT @VersionNumber=COALESCE(MAX(VersionNumber),0)+1 FROM Policy.PolicyVersion WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0;
    SET @PolicyVersionId=NEWID();
    SELECT @SnapshotJson=(SELECT policy.PolicyId,policy.PolicyNumber,policy.AccountId,policy.CarrierId,policy.LineOfBusiness,policy.EffectiveDate,policy.ExpirationDate,COALESCE(policy.AnnualPremium,0)+(SELECT PremiumDelta FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId) AnnualPremium,@EndorsementId endorsementId,@VersionNumber versionNumber,
        JSON_QUERY((SELECT change.ChangeId,change.CategoryCode,change.OperationCode,change.EntityKey,change.SequenceNumber,change.Summary FROM Policy.PolicyEndorsementChange change WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0 ORDER BY change.SequenceNumber FOR JSON PATH)) changes,
        JSON_QUERY((SELECT typed.* FROM Policy.PolicyEndorsementInsuredChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0 FOR JSON PATH)) insuredChanges,
        JSON_QUERY((SELECT typed.* FROM Policy.PolicyEndorsementVehicleChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0 FOR JSON PATH)) vehicleChanges,
        JSON_QUERY((SELECT typed.* FROM Policy.PolicyEndorsementDriverChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0 FOR JSON PATH)) driverChanges,
        JSON_QUERY((SELECT typed.* FROM Policy.PolicyEndorsementCoverageChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0 FOR JSON PATH)) coverageChanges,
        JSON_QUERY((SELECT typed.* FROM Policy.PolicyEndorsementPropertyChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0 FOR JSON PATH)) propertyChanges,
        JSON_QUERY((SELECT typed.* FROM Policy.PolicyEndorsementCommercialChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0 FOR JSON PATH)) commercialChanges,
        JSON_QUERY((SELECT typed.* FROM Policy.PolicyEndorsementFinancialChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0 FOR JSON PATH)) financialChanges,
        JSON_QUERY((SELECT typed.* FROM Policy.PolicyEndorsementLegalChange typed JOIN Policy.PolicyEndorsementChange change ON change.TenantId=typed.TenantId AND change.ChangeId=typed.ChangeId WHERE change.TenantId=@TenantId AND change.EndorsementId=@EndorsementId AND change.IsDeleted=0 FOR JSON PATH)) legalChanges
    FROM Submissions.BoundPolicy policy WHERE policy.TenantId=@TenantId AND policy.PolicyId=@PolicyId AND policy.IsDeleted=0 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);
    INSERT Policy.PolicyVersion(PolicyVersionId,TenantId,PolicyId,PolicyTermId,PolicyTransactionId,VersionNumber,VersionReasonCode,SnapshotJson,CreatedDateUtc,CreatedByUserId,IsDeleted)
    SELECT @PolicyVersionId,@TenantId,@PolicyId,(SELECT TOP 1 PolicyTermId FROM Policy.PolicyTerm WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 ORDER BY TermNumber DESC),NULL,@VersionNumber,N'Endorsement',@SnapshotJson,SYSUTCDATETIME(),@ActorUserId,0;
    UPDATE Submissions.BoundPolicy SET AnnualPremium=COALESCE(AnnualPremium,0)+(SELECT PremiumDelta FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId),CurrentPolicyVersionId=@PolicyVersionId,CurrentVersionNumber=@VersionNumber,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0;
END;

UPDATE Policy.PolicyEndorsement SET Status=@ToStatusCode,WorkflowStage=@ToStatusCode,PolicyVersionAfterId=COALESCE(@PolicyVersionId,PolicyVersionAfterId),SubmittedDateUtc=CASE WHEN @ToStatusCode=N'SubmittedToCarrier' THEN SYSUTCDATETIME() ELSE SubmittedDateUtc END,ApprovedDateUtc=CASE WHEN @ToStatusCode=N'CarrierApproved' THEN SYSUTCDATETIME() ELSE ApprovedDateUtc END,IssuedDateUtc=CASE WHEN @ToStatusCode=N'PolicyUpdated' THEN SYSUTCDATETIME() ELSE IssuedDateUtc END,CompletedDateUtc=CASE WHEN @ToStatusCode=N'Completed' THEN SYSUTCDATETIME() ELSE CompletedDateUtc END,RejectedDateUtc=CASE WHEN @ToStatusCode=N'Rejected' THEN SYSUTCDATETIME() ELSE RejectedDateUtc END,CancelledDateUtc=CASE WHEN @ToStatusCode=N'Cancelled' THEN SYSUTCDATETIME() ELSE CancelledDateUtc END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId;
IF @ReversalOfEndorsementId IS NOT NULL AND @ToStatusCode=N'Completed'
BEGIN
    UPDATE Policy.PolicyEndorsement SET Status=N'Reversed',WorkflowStage=N'Reversed',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND EndorsementId=@ReversalOfEndorsementId AND ReversedByEndorsementId=@EndorsementId AND IsDeleted=0;
    INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,FromStatusCode,ToStatusCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId) VALUES(NEWID(),@TenantId,@ReversalOfEndorsementId,@PolicyId,N'Reversed',N'Completed',N'Reversed',N'Completed endorsement reversed by a compensating transaction.',JSON_OBJECT(N'reversalEndorsementId':@EndorsementId),@CorrelationId,SYSUTCDATETIME(),@ActorUserId);
END;
IF @ReversalOfEndorsementId IS NOT NULL AND @ToStatusCode IN(N'Cancelled',N'Rejected',N'Expired')
BEGIN
    UPDATE Policy.PolicyEndorsement SET ReversedByEndorsementId=NULL,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND EndorsementId=@ReversalOfEndorsementId AND ReversedByEndorsementId=@EndorsementId AND IsDeleted=0;
    UPDATE Policy.PolicyEndorsement SET ReversalOfEndorsementId=NULL WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId;
END;
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,FromStatusCode,ToStatusCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId) VALUES(NEWID(),@TenantId,@EndorsementId,@PolicyId,N'StatusTransition',@FromStatus,@ToStatusCode,CONCAT(N'Endorsement transitioned from ',@FromStatus,N' to ',@ToStatusCode,N'.'),JSON_OBJECT(N'notes':@Notes,N'policyVersionId':@PolicyVersionId),@CorrelationId,SYSUTCDATETIME(),@ActorUserId);
INSERT Policy.PolicyEndorsementActivity(ActivityId,EndorsementId,TenantId,ActivityType,Subject,Notes,CreatedByName,ActivityDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@EndorsementId,@TenantId,N'Status',CONCAT(N'Status changed to ',@ToStatusCode),@Notes,@ActorName,SYSUTCDATETIME(),SYSUTCDATETIME(),@ActorUserId,0);
COMMIT;";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { EndorsementId=endorsementId,request.TenantId,request.ToStatusCode,request.Notes,request.CorrelationId,request.ActorUserId }, cancellationToken:cancellationToken));
    }

    public async Task DecideApprovalAsync(Guid endorsementId, Guid approvalId, DecidePolicyEndorsementApprovalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON; BEGIN TRAN;
IF @DecisionCode NOT IN(N'Approved',N'Rejected') THROW 52420,N'Approval decision must be Approved or Rejected.',1;
DECLARE @PolicyId UNIQUEIDENTIFIER,@ActorName NVARCHAR(160);
SELECT @PolicyId=PolicyId FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND IsDeleted=0;
SELECT @ActorName=COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(FirstName,N' ',LastName))),N''),Email) FROM IAM.[User] WHERE TenantId=@TenantId AND UserId=@ActorUserId AND IsDeleted=0;
IF @PolicyId IS NULL OR @ActorName IS NULL THROW 52421,N'Endorsement or authenticated user was not found in the tenant.',1;
UPDATE Policy.PolicyEndorsementApproval SET StatusCode=@DecisionCode,DecidedDateUtc=SYSUTCDATETIME(),DecidedByUserId=@ActorUserId,DecisionNotes=@Notes WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND ApprovalId=@ApprovalId AND StatusCode=N'Pending' AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52422,N'The pending endorsement approval was not found.',1;
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId) VALUES(NEWID(),@TenantId,@EndorsementId,@PolicyId,N'ApprovalDecision',CONCAT(N'Endorsement review ',LOWER(@DecisionCode),N' by ',@ActorName,N'.'),JSON_OBJECT(N'approvalId':@ApprovalId,N'decision':@DecisionCode,N'notes':@Notes),NEWID(),SYSUTCDATETIME(),@ActorUserId);
COMMIT;";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { EndorsementId=endorsementId,ApprovalId=approvalId,request.TenantId,request.DecisionCode,request.Notes,request.ActorUserId }, cancellationToken:cancellationToken));
    }

    public async Task LinkReversalAsync(Guid tenantId, Guid originalEndorsementId, Guid reversalEndorsementId, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON; BEGIN TRAN;
UPDATE Policy.PolicyEndorsement SET ReversedByEndorsementId=@ReversalEndorsementId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND EndorsementId=@OriginalEndorsementId AND Status=N'Completed' AND ReversedByEndorsementId IS NULL AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52430,N'The completed endorsement is not eligible for reversal.',1;
UPDATE Policy.PolicyEndorsement SET ReversalOfEndorsementId=@OriginalEndorsementId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND EndorsementId=@ReversalEndorsementId AND Status=N'Draft' AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52431,N'The reversal draft was not found in the tenant.',1;
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId) SELECT NEWID(),TenantId,EndorsementId,PolicyId,N'ReversalCreated',N'A compensating reversal endorsement was created.',JSON_OBJECT(N'reversalEndorsementId':@ReversalEndorsementId),NEWID(),SYSUTCDATETIME(),@ActorUserId FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@OriginalEndorsementId;
COMMIT;";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId=tenantId,OriginalEndorsementId=originalEndorsementId,ReversalEndorsementId=reversalEndorsementId,ActorUserId=actorUserId }, cancellationToken:cancellationToken));
    }

    public async Task<IReadOnlyList<PolicyEndorsementOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT OptionId, TenantId, OptionGroupCode, OptionCode, DisplayName, Description, IsDefault, IsActive, SortOrder
FROM Policy.PolicyEndorsementOption
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
ORDER BY OptionGroupCode, SortOrder, DisplayName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<PolicyEndorsementOptionDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<PolicyEndorsementDetailDto?> GetDetailAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {EndorsementColumns}
FROM Policy.PolicyEndorsement
WHERE TenantId = @TenantId AND EndorsementId = @EndorsementId AND IsDeleted = 0;

SELECT ActivityId, EndorsementId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc
FROM Policy.PolicyEndorsementActivity
WHERE TenantId = @TenantId AND EndorsementId = @EndorsementId AND IsDeleted = 0
ORDER BY ActivityDateUtc DESC;

SELECT DeltaId, EndorsementId, TenantId, FieldName, BeforeValue, AfterValue, NumericDelta
FROM Policy.PolicyEndorsementDelta
WHERE TenantId = @TenantId AND EndorsementId = @EndorsementId AND IsDeleted = 0
ORDER BY CreatedDateUtc;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, EndorsementId = endorsementId }, cancellationToken: cancellationToken));
        var endorsement = await multi.ReadSingleOrDefaultAsync<PolicyEndorsementDto>();
        if (endorsement is null) return null;

        return new PolicyEndorsementDetailDto
        {
            Endorsement = endorsement,
            Activities = (await multi.ReadAsync<PolicyEndorsementActivityDto>()).AsList(),
            Deltas = (await multi.ReadAsync<PolicyEndorsementDeltaDto>()).AsList()
        };
    }

    public async Task<Guid> CreateAsync(CreatePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NextNumber INT = ISNULL((SELECT COUNT(1) + 1 FROM Policy.PolicyEndorsement WHERE TenantId = @TenantId), 1);
DECLARE @EndorsementNumber NVARCHAR(50) = CONCAT(N'END-', FORMAT(SYSUTCDATETIME(), N'yyyy'), N'-', FORMAT(@NextNumber, N'0000'));

INSERT INTO Policy.PolicyEndorsement
(EndorsementId, TenantId, PolicyId, AccountId, EndorsementNumber, PolicyNumber, AccountName, LineOfBusiness, Carrier, EndorsementType,
 RequestSourceCode, ChangeCategoryCode, Description, EffectiveDate, ExpirationDate, RetroactiveDate, DiscoveryDate, RequestedDateUtc,
 PremiumDelta, TaxFeeDelta, TotalCostDelta, ProratedPremiumDelta, Status, Priority, RequestedByName, RequestedByEmail, RequestedByPhone,
 ClientContactName, ClientContactEmail, ClientContactPhone, AssignedToName, UnderwriterName, UnderwriterEmail, CarrierSubmissionDateUtc,
 CarrierResponseDueDate, CarrierReferenceNumber, BrokerOfRecordRequired, AgentAuthorityCode, ApprovalLevelCode, ApprovedByName,
 IssuedByName, BillingImpactCode, CommissionImpactCode, BillingInstruction, DocumentDeliveryCode, CertificateRequired, FormsRequired,
 AcordFormNumbers, ExternalReferenceNumber, ComplianceReviewRequired, EoExposureNotes, InternalNotes, ClientFacingNotes, Reason,
 RequiredDocuments, WorkflowStage, DueDate, IsUrgent, IsArchived, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@EndorsementId, @TenantId, @PolicyId, @AccountId, @EndorsementNumber, @PolicyNumber, @AccountName, @LineOfBusiness, @Carrier, @EndorsementType,
 COALESCE(NULLIF(@RequestSourceCode, N''), N'AgencyRequest'), COALESCE(NULLIF(@ChangeCategoryCode, N''), CASE WHEN @PremiumDelta = 0 THEN N'NonPremium' ELSE N'PremiumBearing' END),
 @Description, @EffectiveDate, @ExpirationDate, @RetroactiveDate, @DiscoveryDate, SYSUTCDATETIME(), @PremiumDelta, @TaxFeeDelta,
 CASE WHEN @TotalCostDelta = 0 THEN @PremiumDelta + @TaxFeeDelta ELSE @TotalCostDelta END,
 CASE WHEN @ProratedPremiumDelta = 0 THEN @PremiumDelta ELSE @ProratedPremiumDelta END,
 N'Pending', @Priority, @RequestedByName, @RequestedByEmail, @RequestedByPhone, @ClientContactName, @ClientContactEmail,
 @ClientContactPhone, @AssignedToName, @UnderwriterName, @UnderwriterEmail, @CarrierSubmissionDateUtc, @CarrierResponseDueDate,
 @CarrierReferenceNumber, @BrokerOfRecordRequired, COALESCE(NULLIF(@AgentAuthorityCode, N''), N'CarrierApprovalRequired'),
 COALESCE(NULLIF(@ApprovalLevelCode, N''), CASE WHEN ABS(@PremiumDelta) >= 5000 THEN N'ManagerApproval' ELSE N'StandardAuthority' END),
 @ApprovedByName, @IssuedByName, COALESCE(NULLIF(@BillingImpactCode, N''), CASE WHEN @PremiumDelta = 0 THEN N'NoBillingImpact' ELSE N'BillInstallment' END),
 COALESCE(NULLIF(@CommissionImpactCode, N''), CASE WHEN @PremiumDelta = 0 THEN N'NoCommissionImpact' ELSE N'RecalculateCommission' END),
 @BillingInstruction, COALESCE(NULLIF(@DocumentDeliveryCode, N''), N'PortalEmail'), @CertificateRequired, @FormsRequired,
 @AcordFormNumbers, @ExternalReferenceNumber, @ComplianceReviewRequired, @EoExposureNotes, @InternalNotes, @ClientFacingNotes,
 @Reason, @RequiredDocuments, N'Intake', @DueDate, @IsUrgent, 0, SYSUTCDATETIME(), @CreatedByUserId, 0);

INSERT INTO Policy.PolicyEndorsementActivity
(ActivityId, EndorsementId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(NEWID(), @EndorsementId, @TenantId, N'Created', N'Endorsement request created', @Description, @RequestedByName, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);

INSERT INTO Policy.PolicyEndorsementDelta
(DeltaId, EndorsementId, TenantId, FieldName, BeforeValue, AfterValue, NumericDelta, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(NEWID(), @EndorsementId, @TenantId, N'Annual Premium', N'Current policy premium', FORMAT(@PremiumDelta, N'+$#,##0;-$#,##0;$0'), @PremiumDelta, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            EndorsementId = id,
            request.TenantId,
            request.PolicyId,
            request.AccountId,
            request.PolicyNumber,
            request.AccountName,
            request.LineOfBusiness,
            request.Carrier,
            request.EndorsementType,
            request.RequestSourceCode,
            request.ChangeCategoryCode,
            request.Description,
            request.EffectiveDate,
            request.ExpirationDate,
            request.RetroactiveDate,
            request.DiscoveryDate,
            request.PremiumDelta,
            request.TaxFeeDelta,
            request.TotalCostDelta,
            request.ProratedPremiumDelta,
            request.Priority,
            request.RequestedByName,
            request.RequestedByEmail,
            request.RequestedByPhone,
            request.ClientContactName,
            request.ClientContactEmail,
            request.ClientContactPhone,
            request.AssignedToName,
            request.UnderwriterName,
            request.UnderwriterEmail,
            request.CarrierSubmissionDateUtc,
            request.CarrierResponseDueDate,
            request.CarrierReferenceNumber,
            request.BrokerOfRecordRequired,
            request.AgentAuthorityCode,
            request.ApprovalLevelCode,
            request.ApprovedByName,
            request.IssuedByName,
            request.BillingImpactCode,
            request.CommissionImpactCode,
            request.BillingInstruction,
            request.DocumentDeliveryCode,
            request.CertificateRequired,
            request.FormsRequired,
            request.AcordFormNumbers,
            request.ExternalReferenceNumber,
            request.ComplianceReviewRequired,
            request.EoExposureNotes,
            request.InternalNotes,
            request.ClientFacingNotes,
            request.Reason,
            request.RequiredDocuments,
            request.DueDate,
            request.IsUrgent,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid endorsementId, UpdatePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyEndorsement
SET EndorsementType = @EndorsementType,
    RequestSourceCode = COALESCE(NULLIF(@RequestSourceCode, N''), RequestSourceCode),
    ChangeCategoryCode = COALESCE(NULLIF(@ChangeCategoryCode, N''), CASE WHEN @PremiumDelta = 0 THEN N'NonPremium' ELSE N'PremiumBearing' END),
    Description = @Description,
    EffectiveDate = @EffectiveDate,
    ExpirationDate = @ExpirationDate,
    RetroactiveDate = @RetroactiveDate,
    DiscoveryDate = @DiscoveryDate,
    PremiumDelta = @PremiumDelta,
    TaxFeeDelta = @TaxFeeDelta,
    TotalCostDelta = CASE WHEN @TotalCostDelta = 0 THEN @PremiumDelta + @TaxFeeDelta ELSE @TotalCostDelta END,
    ProratedPremiumDelta = CASE WHEN @ProratedPremiumDelta = 0 THEN @PremiumDelta ELSE @ProratedPremiumDelta END,
    Priority = @Priority,
    AssignedToName = @AssignedToName,
    UnderwriterName = @UnderwriterName,
    UnderwriterEmail = @UnderwriterEmail,
    CarrierSubmissionDateUtc = @CarrierSubmissionDateUtc,
    CarrierResponseDueDate = @CarrierResponseDueDate,
    CarrierReferenceNumber = @CarrierReferenceNumber,
    BrokerOfRecordRequired = @BrokerOfRecordRequired,
    AgentAuthorityCode = COALESCE(NULLIF(@AgentAuthorityCode, N''), AgentAuthorityCode),
    ApprovalLevelCode = COALESCE(NULLIF(@ApprovalLevelCode, N''), CASE WHEN ABS(@PremiumDelta) >= 5000 THEN N'ManagerApproval' ELSE N'StandardAuthority' END),
    ApprovedByName = @ApprovedByName,
    IssuedByName = @IssuedByName,
    BillingImpactCode = COALESCE(NULLIF(@BillingImpactCode, N''), CASE WHEN @PremiumDelta = 0 THEN N'NoBillingImpact' ELSE N'BillInstallment' END),
    CommissionImpactCode = COALESCE(NULLIF(@CommissionImpactCode, N''), CASE WHEN @PremiumDelta = 0 THEN N'NoCommissionImpact' ELSE N'RecalculateCommission' END),
    BillingInstruction = @BillingInstruction,
    DocumentDeliveryCode = COALESCE(NULLIF(@DocumentDeliveryCode, N''), DocumentDeliveryCode),
    CertificateRequired = @CertificateRequired,
    FormsRequired = @FormsRequired,
    AcordFormNumbers = @AcordFormNumbers,
    ExternalReferenceNumber = @ExternalReferenceNumber,
    ComplianceReviewRequired = @ComplianceReviewRequired,
    EoExposureNotes = @EoExposureNotes,
    InternalNotes = @InternalNotes,
    ClientFacingNotes = @ClientFacingNotes,
    Reason = @Reason,
    RequiredDocuments = @RequiredDocuments,
    DueDate = @DueDate,
    IsUrgent = @IsUrgent,
    WorkflowStage = CASE WHEN Status = N'Pending' THEN N'Intake' ELSE WorkflowStage END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE EndorsementId = @EndorsementId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            EndorsementId = endorsementId,
            request.EndorsementType,
            request.RequestSourceCode,
            request.ChangeCategoryCode,
            request.Description,
            request.EffectiveDate,
            request.ExpirationDate,
            request.RetroactiveDate,
            request.DiscoveryDate,
            request.PremiumDelta,
            request.TaxFeeDelta,
            request.TotalCostDelta,
            request.ProratedPremiumDelta,
            request.Priority,
            request.AssignedToName,
            request.UnderwriterName,
            request.UnderwriterEmail,
            request.CarrierSubmissionDateUtc,
            request.CarrierResponseDueDate,
            request.CarrierReferenceNumber,
            request.BrokerOfRecordRequired,
            request.AgentAuthorityCode,
            request.ApprovalLevelCode,
            request.ApprovedByName,
            request.IssuedByName,
            request.BillingImpactCode,
            request.CommissionImpactCode,
            request.BillingInstruction,
            request.DocumentDeliveryCode,
            request.CertificateRequired,
            request.FormsRequired,
            request.AcordFormNumbers,
            request.ExternalReferenceNumber,
            request.ComplianceReviewRequired,
            request.EoExposureNotes,
            request.InternalNotes,
            request.ClientFacingNotes,
            request.Reason,
            request.RequiredDocuments,
            request.DueDate,
            request.IsUrgent,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(Guid endorsementId, UpdatePolicyEndorsementStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Policy.PolicyEndorsement WHERE EndorsementId = @EndorsementId AND IsDeleted = 0);

UPDATE Policy.PolicyEndorsement
SET Status = @Status,
    WorkflowStage = CASE @Status
        WHEN N'Pending' THEN N'Intake'
        WHEN N'In Review' THEN N'Underwriting Review'
        WHEN N'Approved' THEN N'Approved Pending Issue'
        WHEN N'Declined' THEN N'Closed Declined'
        WHEN N'Issued' THEN N'Issued to Policy'
        WHEN N'Info Needed' THEN N'Awaiting Information'
        ELSE WorkflowStage
    END,
    ApprovedDateUtc = CASE WHEN @Status = N'Approved' THEN SYSUTCDATETIME() ELSE ApprovedDateUtc END,
    IssuedDateUtc = CASE WHEN @Status = N'Issued' THEN SYSUTCDATETIME() ELSE IssuedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE EndorsementId = @EndorsementId AND IsDeleted = 0;

INSERT INTO Policy.PolicyEndorsementActivity
(ActivityId, EndorsementId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(NEWID(), @EndorsementId, @TenantId, N'Status', CONCAT(N'Status changed to ', @Status), @Notes, @CreatedByName, SYSUTCDATETIME(), SYSUTCDATETIME(), @ModifiedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            EndorsementId = endorsementId,
            request.Status,
            request.Notes,
            request.CreatedByName,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> AddActivityAsync(AddPolicyEndorsementActivityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Policy.PolicyEndorsement WHERE EndorsementId = @EndorsementId AND IsDeleted = 0);
INSERT INTO Policy.PolicyEndorsementActivity
(ActivityId, EndorsementId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@ActivityId, @EndorsementId, @TenantId, @ActivityType, @Subject, @Notes, @CreatedByName, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);
UPDATE Policy.PolicyEndorsement
SET ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @CreatedByUserId
WHERE EndorsementId = @EndorsementId AND IsDeleted = 0;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ActivityId = id,
            request.EndorsementId,
            request.ActivityType,
            request.Subject,
            request.Notes,
            request.CreatedByName,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<Guid> UpsertDeltaAsync(UpsertPolicyEndorsementDeltaRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Policy.PolicyEndorsement WHERE EndorsementId = @EndorsementId AND IsDeleted = 0);
INSERT INTO Policy.PolicyEndorsementDelta
(DeltaId, EndorsementId, TenantId, FieldName, BeforeValue, AfterValue, NumericDelta, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@DeltaId, @EndorsementId, @TenantId, @FieldName, @BeforeValue, @AfterValue, @NumericDelta, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            DeltaId = id,
            request.EndorsementId,
            request.FieldName,
            request.BeforeValue,
            request.AfterValue,
            request.NumericDelta,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task ArchiveAsync(Guid endorsementId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyEndorsement
SET IsArchived = 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE EndorsementId = @EndorsementId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { EndorsementId = endorsementId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    private static async Task PersistChangesAsync(IDbConnection connection, IDbTransaction transaction, Guid tenantId, Guid endorsementId, IReadOnlyList<PolicyEndorsementChangeInput> changes, Guid? userId, CancellationToken cancellationToken)
    {
        const string commonSql = @"
INSERT Policy.PolicyEndorsementChange(ChangeId,TenantId,EndorsementId,CategoryCode,OperationCode,EntityKey,SequenceNumber,Summary,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(@ChangeId,@TenantId,@EndorsementId,@CategoryCode,@OperationCode,@EntityKey,@SequenceNumber,@Summary,SYSUTCDATETIME(),@UserId,0);";
        for (var index = 0; index < changes.Count; index++)
        {
            var change = changes[index];
            var changeId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(commonSql, new { ChangeId = changeId, TenantId = tenantId, EndorsementId = endorsementId, change.CategoryCode, change.OperationCode, change.EntityKey, SequenceNumber = index + 1, change.Summary, UserId = userId }, transaction, cancellationToken: cancellationToken));
            await PersistTypedChangeAsync(connection, transaction, tenantId, changeId, change, cancellationToken);
        }
    }

    private static Task PersistTypedChangeAsync(IDbConnection connection, IDbTransaction transaction, Guid tenantId, Guid changeId, PolicyEndorsementChangeInput change, CancellationToken cancellationToken)
    {
        string sql;
        object value;
        if (change.Insured is not null)
        {
            sql = @"INSERT Policy.PolicyEndorsementInsuredChange(ChangeId,TenantId,BeforeName,AfterName,BeforeDba,AfterDba,BeforeFein,AfterFein,BeforePhone,AfterPhone,BeforeEmail,AfterEmail,BeforeMailingAddress,AfterMailingAddress,BeforeGaragingAddress,AfterGaragingAddress) VALUES(@PersistedChangeId,@TenantId,@BeforeName,@AfterName,@BeforeDba,@AfterDba,@BeforeFein,@AfterFein,@BeforePhone,@AfterPhone,@BeforeEmail,@AfterEmail,@BeforeMailingAddress,@AfterMailingAddress,@BeforeGaragingAddress,@AfterGaragingAddress);";
            value = change.Insured;
        }
        else if (change.Vehicle is not null)
        {
            sql = @"INSERT Policy.PolicyEndorsementVehicleChange(ChangeId,TenantId,BeforeVehicleId,AfterVehicleId,BeforeVin,AfterVin,BeforeYear,AfterYear,BeforeMake,AfterMake,BeforeModel,AfterModel,BeforeUsageCode,AfterUsageCode,BeforeGaragingAddress,AfterGaragingAddress,BeforeLienholder,AfterLienholder) VALUES(@PersistedChangeId,@TenantId,@BeforeVehicleId,@AfterVehicleId,@BeforeVin,@AfterVin,@BeforeYear,@AfterYear,@BeforeMake,@AfterMake,@BeforeModel,@AfterModel,@BeforeUsageCode,@AfterUsageCode,@BeforeGaragingAddress,@AfterGaragingAddress,@BeforeLienholder,@AfterLienholder);";
            value = change.Vehicle;
        }
        else if (change.Driver is not null)
        {
            sql = @"INSERT Policy.PolicyEndorsementDriverChange(ChangeId,TenantId,BeforeDriverId,AfterDriverId,BeforeName,AfterName,BeforeLicenseNumber,AfterLicenseNumber,BeforeLicenseState,AfterLicenseState,BeforeBirthDate,AfterBirthDate,BeforeExcluded,AfterExcluded) VALUES(@PersistedChangeId,@TenantId,@BeforeDriverId,@AfterDriverId,@BeforeName,@AfterName,@BeforeLicenseNumber,@AfterLicenseNumber,@BeforeLicenseState,@AfterLicenseState,@BeforeBirthDate,@AfterBirthDate,@BeforeExcluded,@AfterExcluded);";
            value = change.Driver;
        }
        else if (change.Coverage is not null)
        {
            sql = @"INSERT Policy.PolicyEndorsementCoverageChange(ChangeId,TenantId,CoverageCode,BeforeCoverageName,AfterCoverageName,BeforeLimitAmount,AfterLimitAmount,BeforeLimitDescription,AfterLimitDescription,BeforeDeductibleAmount,AfterDeductibleAmount,BeforePremiumAmount,AfterPremiumAmount) VALUES(@PersistedChangeId,@TenantId,@CoverageCode,@BeforeCoverageName,@AfterCoverageName,@BeforeLimitAmount,@AfterLimitAmount,@BeforeLimitDescription,@AfterLimitDescription,@BeforeDeductibleAmount,@AfterDeductibleAmount,@BeforePremiumAmount,@AfterPremiumAmount);";
            value = change.Coverage;
        }
        else if (change.Property is not null)
        {
            sql = @"INSERT Policy.PolicyEndorsementPropertyChange(ChangeId,TenantId,BeforePropertyId,AfterPropertyId,BeforeLocationAddress,AfterLocationAddress,BeforeBuildingNumber,AfterBuildingNumber,BeforeOccupancyCode,AfterOccupancyCode,BeforeConstructionCode,AfterConstructionCode,BeforeSquareFeet,AfterSquareFeet,BeforeBuildingValue,AfterBuildingValue) VALUES(@PersistedChangeId,@TenantId,@BeforePropertyId,@AfterPropertyId,@BeforeLocationAddress,@AfterLocationAddress,@BeforeBuildingNumber,@AfterBuildingNumber,@BeforeOccupancyCode,@AfterOccupancyCode,@BeforeConstructionCode,@AfterConstructionCode,@BeforeSquareFeet,@AfterSquareFeet,@BeforeBuildingValue,@AfterBuildingValue);";
            value = change.Property;
        }
        else if (change.Commercial is not null)
        {
            sql = @"INSERT Policy.PolicyEndorsementCommercialChange(ChangeId,TenantId,ClassificationCode,BeforePayrollAmount,AfterPayrollAmount,BeforeRevenueAmount,AfterRevenueAmount,BeforeEmployeeCount,AfterEmployeeCount,BeforeEquipmentValue,AfterEquipmentValue,BeforeBlanketLimit,AfterBlanketLimit,BeforeLocationCount,AfterLocationCount) VALUES(@PersistedChangeId,@TenantId,@ClassificationCode,@BeforePayrollAmount,@AfterPayrollAmount,@BeforeRevenueAmount,@AfterRevenueAmount,@BeforeEmployeeCount,@AfterEmployeeCount,@BeforeEquipmentValue,@AfterEquipmentValue,@BeforeBlanketLimit,@AfterBlanketLimit,@BeforeLocationCount,@AfterLocationCount);";
            value = change.Commercial;
        }
        else if (change.Financial is not null)
        {
            sql = @"INSERT Policy.PolicyEndorsementFinancialChange(ChangeId,TenantId,BeforeBillingPlanCode,AfterBillingPlanCode,BeforeFinancingProvider,AfterFinancingProvider,BeforeInstallmentCount,AfterInstallmentCount,BeforeCommissionRate,AfterCommissionRate,BeforeCommissionAmount,AfterCommissionAmount,BeforeFinancedAmount,AfterFinancedAmount) VALUES(@PersistedChangeId,@TenantId,@BeforeBillingPlanCode,@AfterBillingPlanCode,@BeforeFinancingProvider,@AfterFinancingProvider,@BeforeInstallmentCount,@AfterInstallmentCount,@BeforeCommissionRate,@AfterCommissionRate,@BeforeCommissionAmount,@AfterCommissionAmount,@BeforeFinancedAmount,@AfterFinancedAmount);";
            value = change.Financial;
        }
        else if (change.Legal is not null)
        {
            sql = @"INSERT Policy.PolicyEndorsementLegalChange(ChangeId,TenantId,PartyTypeCode,BeforePartyName,AfterPartyName,BeforeRelationshipCode,AfterRelationshipCode,BeforeAddress,AfterAddress,BeforeReferenceNumber,AfterReferenceNumber) VALUES(@PersistedChangeId,@TenantId,@PartyTypeCode,@BeforePartyName,@AfterPartyName,@BeforeRelationshipCode,@AfterRelationshipCode,@BeforeAddress,@AfterAddress,@BeforeReferenceNumber,@AfterReferenceNumber);";
            value = change.Legal;
        }
        else
        {
            throw new ArgumentException("A typed endorsement change is required.", nameof(change));
        }

        var parameters = new DynamicParameters(value);
        parameters.Add("PersistedChangeId", changeId);
        parameters.Add("TenantId", tenantId);
        return connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
    }

    private static void Attach<T>(IReadOnlyList<PolicyEndorsementChangeDto> changes, IReadOnlyDictionary<Guid, T> values, Action<PolicyEndorsementChangeDto, T> setter)
    {
        foreach (var change in changes)
            if (values.TryGetValue(change.ChangeId, out var value)) setter(change, value);
    }
}
