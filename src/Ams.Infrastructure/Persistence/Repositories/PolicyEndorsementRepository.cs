using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyEndorsements;
using Dapper;
using System.Data;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed partial class PolicyEndorsementRepository : IPolicyEndorsementRepository
{
    private const string EndorsementColumns = @"EndorsementId, TenantId, PolicyId, PolicyVersionBeforeId, PolicyVersionAfterId, AccountId, EndorsementNumber, PolicyNumber, AccountName, LineOfBusiness,
        Carrier, EndorsementType, ReasonCode, CarrierMethodCode, RequestSourceCode, ChangeCategoryCode, Description, EffectiveDate, ExpirationDate, RetroactiveDate,
        DiscoveryDate, RequestedDateUtc, PremiumDelta, AgencyFeeDelta, TaxDelta, TaxFeeDelta, TotalCostDelta, ProratedPremiumDelta, CurrencyCode,
        CASE WHEN Status IN (N'PendingReview', N'Pending Review', N'In Review') THEN N'InReview' ELSE Status END Status, Priority,
        RequestedByName, RequestedByEmail, RequestedByPhone, ClientContactName, ClientContactEmail, ClientContactPhone,
        AssignedToName, UnderwriterName, UnderwriterEmail, CarrierSubmissionDateUtc, CarrierResponseDueDate, CarrierReferenceNumber,
        BrokerOfRecordRequired, AgentAuthorityCode, ApprovalLevelCode, ApprovedByName, IssuedByName, BillingImpactCode,
        CommissionImpactCode, BillingInstruction, DocumentDeliveryCode, CertificateRequired, FormsRequired, AcordFormNumbers,
        ExternalReferenceNumber, ComplianceReviewRequired, EoExposureNotes, InternalNotes, ClientFacingNotes, Reason,
        RequiredDocuments, CASE WHEN WorkflowStage IN (N'PendingReview', N'Pending Review', N'In Review') THEN N'InReview' ELSE WorkflowStage END WorkflowStage,
        DueDate, ApprovedDateUtc, IssuedDateUtc, SubmittedDateUtc, CompletedDateUtc, RejectedDateUtc, CancelledDateUtc,
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
        SELECT approval.ApprovalId,approval.TenantId,approval.EndorsementId,approval.ApprovalLevelCode,approval.StatusCode,approval.RequestedDateUtc,approval.RequestedByUserId,approval.AssignedToUserId,assignedTo.FullName AssignedToName,approval.DecidedDateUtc,approval.DecidedByUserId,approval.DecisionNotes,approval.RowVersion FROM Policy.PolicyEndorsementApproval approval LEFT JOIN IAM.[User] assignedTo ON assignedTo.TenantId=approval.TenantId AND assignedTo.UserId=approval.AssignedToUserId AND assignedTo.IsDeleted=0 WHERE approval.TenantId=@TenantId AND approval.EndorsementId=@EndorsementId AND approval.IsDeleted=0 ORDER BY approval.RequestedDateUtc;
SELECT request.InformationRequestId,request.TenantId,request.EndorsementId,request.RequestNumber,request.StatusCode,request.RequestDetails,request.RequestedDateUtc,request.RequestedByUserId,requestedBy.FullName RequestedByName,request.AssignedToUserId,assignedTo.FullName AssignedToName,request.DueDateUtc,request.ResponseDetails,request.RespondedDateUtc,request.RespondedByUserId,respondedBy.FullName RespondedByName,request.ResubmittedDateUtc,request.ResubmittedByUserId,request.ClosedDateUtc,request.RowVersion
FROM Policy.PolicyEndorsementInformationRequest request
LEFT JOIN IAM.[User] requestedBy ON requestedBy.TenantId=request.TenantId AND requestedBy.UserId=request.RequestedByUserId AND requestedBy.IsDeleted=0
LEFT JOIN IAM.[User] assignedTo ON assignedTo.TenantId=request.TenantId AND assignedTo.UserId=request.AssignedToUserId AND assignedTo.IsDeleted=0
LEFT JOIN IAM.[User] respondedBy ON respondedBy.TenantId=request.TenantId AND respondedBy.UserId=request.RespondedByUserId AND respondedBy.IsDeleted=0
WHERE request.TenantId=@TenantId AND request.EndorsementId=@EndorsementId AND request.IsDeleted=0 ORDER BY request.RequestNumber DESC;
SELECT EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,FromStatusCode,ToStatusCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId FROM Policy.PolicyEndorsementEvent WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId ORDER BY OccurredDateUtc DESC;
SELECT workflowRule.EndorsementTypeWorkflowRuleId StatusTransitionId,workflowRule.FromStatusCode,workflowRule.ToStatusCode,workflowRule.RequiredPermissionCode,
       workflowRule.RequiresApproval,workflowRule.RequiresCarrierDispatch RequiresCarrierSubmission,workflowRule.RequiresPolicyVersion CreatesPolicyVersion,
       workflowRule.RequiresAccountingWork CreatesAccountingWork,workflowRule.RequiresDocumentWork CreatesDocumentWork,
       COALESCE(JSON_VALUE(workflowRule.RuleJson,N'$.actionLabel'),workflowRule.ToStatusCode) ActionLabel,
       COALESCE(JSON_VALUE(workflowRule.RuleJson,N'$.instruction'),N'') Instruction,
       COALESCE(JSON_VALUE(workflowRule.RuleJson,N'$.confirmationTitle'),JSON_VALUE(workflowRule.RuleJson,N'$.actionLabel'),workflowRule.ToStatusCode) ConfirmationTitle,
       COALESCE(JSON_VALUE(workflowRule.RuleJson,N'$.confirmationMessage'),N'') ConfirmationMessage,
       CASE WHEN JSON_VALUE(workflowRule.RuleJson,N'$.requiresNotes')=N'true' THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END RequiresNotes,
       COALESCE(JSON_VALUE(workflowRule.RuleJson,N'$.notesLabel'),N'Notes') NotesLabel,
       COALESCE(JSON_VALUE(workflowRule.RuleJson,N'$.notesPlaceholder'),N'Add workflow notes.') NotesPlaceholder
FROM Policy.EndorsementTypeWorkflowRule workflowRule
JOIN Policy.EndorsementType type ON type.TenantId=workflowRule.TenantId AND type.EndorsementTypeId=workflowRule.EndorsementTypeId AND type.IsActive=1 AND type.IsDeleted=0
JOIN Policy.EndorsementTypeProfile profile ON profile.TenantId=type.TenantId AND profile.EndorsementTypeId=type.EndorsementTypeId AND profile.IsActive=1 AND profile.IsDeleted=0
JOIN Policy.PolicyEndorsement endorsement ON endorsement.TenantId=type.TenantId AND endorsement.EndorsementId=@EndorsementId AND endorsement.IsDeleted=0
 AND (endorsement.EndorsementType=type.TypeCode OR (endorsement.EndorsementType=type.TypeName AND NOT EXISTS(SELECT 1 FROM Policy.EndorsementType duplicateType WHERE duplicateType.TenantId=type.TenantId AND duplicateType.TypeName=type.TypeName AND duplicateType.EndorsementTypeId<>type.EndorsementTypeId AND duplicateType.IsActive=1 AND duplicateType.IsDeleted=0)) OR EXISTS(SELECT 1 FROM Policy.EndorsementTypeAlias alias WHERE alias.TenantId=type.TenantId AND alias.EndorsementTypeId=type.EndorsementTypeId AND alias.LegacyTypeValue=endorsement.EndorsementType AND alias.IsActive=1 AND alias.IsDeleted=0 AND (alias.DescriptionContains IS NULL OR endorsement.Description LIKE N'%'+alias.DescriptionContains+N'%')))
WHERE workflowRule.TenantId=@TenantId
  AND workflowRule.FromStatusCode=CASE WHEN endorsement.Status IN(N'PendingReview',N'Pending Review',N'In Review') THEN N'InReview' ELSE endorsement.Status END
  AND (JSON_VALUE(workflowRule.RuleJson,N'$.conditionCode') IS NULL
       OR (JSON_VALUE(workflowRule.RuleJson,N'$.conditionCode')=N'ApprovalRequired' AND (profile.RequiresUnderwritingReview=1 OR profile.IsHighRisk=1))
       OR (JSON_VALUE(workflowRule.RuleJson,N'$.conditionCode')=N'ApprovalNotRequiredCarrier' AND profile.RequiresUnderwritingReview=0 AND profile.IsHighRisk=0 AND profile.RequiresCarrierApproval=1)
       OR (JSON_VALUE(workflowRule.RuleJson,N'$.conditionCode')=N'ApprovalNotRequiredPolicy' AND profile.RequiresUnderwritingReview=0 AND profile.IsHighRisk=0 AND profile.RequiresCarrierApproval=0))
  AND workflowRule.IsActive=1 AND workflowRule.IsDeleted=0 ORDER BY workflowRule.SortOrder;
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
            InformationRequests = (await multi.ReadAsync<PolicyEndorsementInformationRequestDto>()).AsList(),
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
SELECT policy.PolicyId,policy.PolicyNumber,COALESCE(account.AccountName,N'') AccountName,COALESCE(carrier.CarrierName,N'') CarrierName,policy.LineOfBusiness,COALESCE(policy.CoverageStatus,policy.Status,N'Active') Status,policy.EffectiveDate,policy.ExpirationDate,COALESCE(policy.AnnualPremium,0) AnnualPremium
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
DECLARE @PolicyNumber NVARCHAR(50),@AccountId UNIQUEIDENTIFIER,@AccountName NVARCHAR(200),@LineOfBusiness NVARCHAR(100),@Carrier NVARCHAR(160),@RequestedByName NVARCHAR(160),@RequestedByEmail NVARCHAR(254),@VersionBefore UNIQUEIDENTIFIER,@NextNumber INT,@EndorsementNumber NVARCHAR(50),@CategoryCode NVARCHAR(50),@SupportsReversal BIT;
SELECT @PolicyNumber=policy.PolicyNumber,@AccountId=policy.AccountId,@AccountName=account.AccountName,@LineOfBusiness=policy.LineOfBusiness,@Carrier=carrier.CarrierName FROM Submissions.BoundPolicy policy LEFT JOIN Client.Account account ON account.TenantId=policy.TenantId AND account.AccountId=policy.AccountId AND account.IsDeleted=0 LEFT JOIN Agency.Carrier carrier ON carrier.TenantId=policy.TenantId AND carrier.CarrierId=policy.CarrierId AND carrier.IsDeleted=0 WHERE policy.TenantId=@TenantId AND policy.PolicyId=@PolicyId AND policy.IsDeleted=0;
IF @PolicyNumber IS NULL THROW 52401,N'The policy was not found in the authenticated tenant.',1;
SELECT @CategoryCode=profile.CategoryCode,@SupportsReversal=profile.SupportsReversal
FROM Policy.EndorsementType type
JOIN Policy.EndorsementTypeProfile profile ON profile.TenantId=type.TenantId AND profile.EndorsementTypeId=type.EndorsementTypeId AND profile.IsActive=1 AND profile.IsDeleted=0
WHERE type.TenantId=@TenantId AND type.TypeCode=@EndorsementTypeCode AND type.IsActive=1 AND type.IsDeleted=0
  AND EXISTS(SELECT 1 FROM Policy.EndorsementTypeLineOfBusiness lob WHERE lob.TenantId=type.TenantId AND lob.EndorsementTypeId=type.EndorsementTypeId AND lob.IsActive=1 AND lob.IsDeleted=0 AND (lob.LineOfBusinessCode=N'*' OR lob.LineOfBusinessCode=@LineOfBusiness));
IF @CategoryCode IS NULL THROW 52405,N'The endorsement type is inactive, unconfigured, or not available for the policy line of business.',1;
IF @ReversalOfEndorsementId IS NOT NULL AND COALESCE(@SupportsReversal,0)=0 THROW 52420,N'The endorsement type does not support reversal.',1;
IF NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementOption WHERE TenantId=@TenantId AND OptionGroupCode=N'Reason' AND OptionCode=@ReasonCode AND IsActive=1 AND IsDeleted=0) THROW 52406,N'The endorsement reason is invalid.',1;
IF NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementOption WHERE TenantId=@TenantId AND OptionGroupCode=N'Priority' AND OptionCode=@PriorityCode AND IsActive=1 AND IsDeleted=0) THROW 52407,N'The endorsement priority is invalid.',1;
IF NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementOption WHERE TenantId=@TenantId AND OptionGroupCode=N'CarrierMethod' AND OptionCode=@CarrierMethodCode AND IsActive=1 AND IsDeleted=0) THROW 52408,N'The carrier method is invalid.',1;
IF NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementOption WHERE TenantId=@TenantId AND OptionGroupCode=N'BillingImpact' AND OptionCode=@BillingImpactCode AND IsActive=1 AND IsDeleted=0) THROW 52409,N'The billing impact is invalid.',1;
IF NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementOption WHERE TenantId=@TenantId AND OptionGroupCode=N'CommissionImpact' AND OptionCode=@CommissionImpactCode AND IsActive=1 AND IsDeleted=0) THROW 52410,N'The commission impact is invalid.',1;
SELECT @RequestedByName=COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(FirstName,N' ',LastName))),N''),Email),@RequestedByEmail=Email FROM IAM.[User] WHERE TenantId=@TenantId AND UserId=@CreatedByUserId AND IsDeleted=0;
IF @RequestedByName IS NULL THROW 52402,N'The authenticated user was not found in the tenant.',1;
SELECT TOP 1 @VersionBefore=PolicyVersionId FROM Policy.PolicyVersion WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 ORDER BY VersionNumber DESC;
SELECT @NextNumber=COUNT_BIG(1)+1 FROM Policy.PolicyEndorsement WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId;
SET @EndorsementNumber=CONCAT(N'END-',FORMAT(SYSUTCDATETIME(),N'yyyy'),N'-',FORMAT(@NextNumber,N'000000'));
IF @ReversalOfEndorsementId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsement WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND EndorsementId=@ReversalOfEndorsementId AND PolicyId=@PolicyId AND Status=N'Completed' AND ReversedByEndorsementId IS NULL AND RowVersion=@ReversalOfRowVersion AND IsDeleted=0) THROW 52404,N'The completed endorsement changed or is not eligible for reversal.',1;
INSERT Policy.PolicyEndorsement(EndorsementId,TenantId,PolicyId,PolicyVersionBeforeId,AccountId,EndorsementNumber,PolicyNumber,AccountName,LineOfBusiness,Carrier,EndorsementType,ReasonCode,CarrierMethodCode,RequestSourceCode,ChangeCategoryCode,Description,EffectiveDate,RequestedDateUtc,PremiumDelta,AgencyFeeDelta,TaxDelta,TaxFeeDelta,TotalCostDelta,ProratedPremiumDelta,CurrencyCode,Status,Priority,RequestedByName,RequestedByEmail,AssignedToName,BillingImpactCode,CommissionImpactCode,InternalNotes,ClientFacingNotes,Reason,WorkflowStage,DueDate,IsUrgent,IsArchived,ReversalOfEndorsementId,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(@EndorsementId,@TenantId,@PolicyId,@VersionBefore,@AccountId,@EndorsementNumber,@PolicyNumber,COALESCE(@AccountName,N''),COALESCE(@LineOfBusiness,N''),COALESCE(@Carrier,N''),@EndorsementTypeCode,@ReasonCode,@CarrierMethodCode,N'AgencyRequest',@CategoryCode,@Description,@EffectiveDate,SYSUTCDATETIME(),@PremiumChange,@AgencyFee,@Taxes,@Taxes,@PremiumChange+@AgencyFee+@Taxes,CASE WHEN @ProratedPremiumChange=0 THEN @PremiumChange ELSE @ProratedPremiumChange END,@CurrencyCode,N'Draft',@PriorityCode,@RequestedByName,@RequestedByEmail,@RequestedByName,@BillingImpactCode,@CommissionImpactCode,@InternalNotes,@ClientFacingNotes,@ReasonCode,N'Draft',@DueDate,@IsUrgent,0,@ReversalOfEndorsementId,SYSUTCDATETIME(),@CreatedByUserId,0);
IF @ReversalOfEndorsementId IS NOT NULL UPDATE Policy.PolicyEndorsement SET ReversedByEndorsementId=@EndorsementId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@CreatedByUserId WHERE TenantId=@TenantId AND EndorsementId=@ReversalOfEndorsementId;
INSERT Policy.PolicyEndorsementActivity(ActivityId,EndorsementId,TenantId,ActivityType,Subject,Notes,CreatedByName,ActivityDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@EndorsementId,@TenantId,N'Created',N'Endorsement draft created',@Description,@RequestedByName,SYSUTCDATETIME(),SYSUTCDATETIME(),@CreatedByUserId,0);
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,ToStatusCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId) VALUES(NEWID(),@TenantId,@EndorsementId,@PolicyId,N'Created',N'Draft',N'Endorsement draft created.',JSON_OBJECT(N'endorsementNumber':@EndorsementNumber,N'policyVersionBeforeId':@VersionBefore),NEWID(),SYSUTCDATETIME(),@CreatedByUserId);";
            await connection.ExecuteAsync(new CommandDefinition(sql, new { EndorsementId=endorsementId,request.TenantId,request.PolicyId,request.EndorsementTypeCode,request.ReasonCode,request.CarrierMethodCode,request.Description,request.EffectiveDate,request.PriorityCode,request.InternalNotes,request.ClientFacingNotes,request.DueDate,request.IsUrgent,request.CreatedByUserId,request.ReversalOfEndorsementId,request.ReversalOfRowVersion,request.FinancialImpact.CurrencyCode,request.FinancialImpact.PremiumChange,request.FinancialImpact.AgencyFee,request.FinancialImpact.Taxes,request.FinancialImpact.ProratedPremiumChange,request.FinancialImpact.BillingImpactCode,request.FinancialImpact.CommissionImpactCode }, transaction, cancellationToken:cancellationToken));
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
DECLARE @PolicyId UNIQUEIDENTIFIER,@PolicyLineOfBusiness NVARCHAR(100),@CategoryCode NVARCHAR(50);
SELECT @PolicyId=endorsement.PolicyId,@PolicyLineOfBusiness=policy.LineOfBusiness FROM Policy.PolicyEndorsement endorsement JOIN Submissions.BoundPolicy policy ON policy.TenantId=endorsement.TenantId AND policy.PolicyId=endorsement.PolicyId AND policy.IsDeleted=0 WHERE endorsement.TenantId=@TenantId AND endorsement.EndorsementId=@EndorsementId AND endorsement.IsDeleted=0;
SELECT @CategoryCode=profile.CategoryCode FROM Policy.EndorsementType type JOIN Policy.EndorsementTypeProfile profile ON profile.TenantId=type.TenantId AND profile.EndorsementTypeId=type.EndorsementTypeId AND profile.IsActive=1 AND profile.IsDeleted=0 WHERE type.TenantId=@TenantId AND type.TypeCode=@EndorsementTypeCode AND type.IsActive=1 AND type.IsDeleted=0 AND EXISTS(SELECT 1 FROM Policy.EndorsementTypeLineOfBusiness lob WHERE lob.TenantId=type.TenantId AND lob.EndorsementTypeId=type.EndorsementTypeId AND lob.IsActive=1 AND lob.IsDeleted=0 AND (lob.LineOfBusinessCode=N'*' OR lob.LineOfBusinessCode=@PolicyLineOfBusiness));
IF @PolicyId IS NULL OR @CategoryCode IS NULL THROW 52405,N'The endorsement type is not available for the policy line of business.',1;
IF NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementOption WHERE TenantId=@TenantId AND OptionGroupCode=N'Reason' AND OptionCode=@ReasonCode AND IsActive=1 AND IsDeleted=0) THROW 52406,N'The endorsement reason is invalid.',1;
IF NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementOption WHERE TenantId=@TenantId AND OptionGroupCode=N'Priority' AND OptionCode=@PriorityCode AND IsActive=1 AND IsDeleted=0) THROW 52407,N'The endorsement priority is invalid.',1;
IF NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementOption WHERE TenantId=@TenantId AND OptionGroupCode=N'CarrierMethod' AND OptionCode=@CarrierMethodCode AND IsActive=1 AND IsDeleted=0) THROW 52408,N'The carrier method is invalid.',1;
IF NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementOption WHERE TenantId=@TenantId AND OptionGroupCode=N'BillingImpact' AND OptionCode=@BillingImpactCode AND IsActive=1 AND IsDeleted=0) THROW 52409,N'The billing impact is invalid.',1;
IF NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementOption WHERE TenantId=@TenantId AND OptionGroupCode=N'CommissionImpact' AND OptionCode=@CommissionImpactCode AND IsActive=1 AND IsDeleted=0) THROW 52410,N'The commission impact is invalid.',1;
UPDATE Policy.PolicyEndorsement SET EndorsementType=@EndorsementTypeCode,ReasonCode=@ReasonCode,CarrierMethodCode=@CarrierMethodCode,Description=@Description,EffectiveDate=@EffectiveDate,PremiumDelta=@PremiumChange,AgencyFeeDelta=@AgencyFee,TaxDelta=@Taxes,TaxFeeDelta=@Taxes,TotalCostDelta=@PremiumChange+@AgencyFee+@Taxes,ProratedPremiumDelta=CASE WHEN @ProratedPremiumChange=0 THEN @PremiumChange ELSE @ProratedPremiumChange END,CurrencyCode=@CurrencyCode,Priority=@PriorityCode,BillingImpactCode=@BillingImpactCode,CommissionImpactCode=@CommissionImpactCode,InternalNotes=@InternalNotes,ClientFacingNotes=@ClientFacingNotes,DueDate=@DueDate,IsUrgent=@IsUrgent,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId
    ,ChangeCategoryCode=@CategoryCode
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
IF EXISTS(SELECT 1 FROM Policy.PolicyEndorsementEvent WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND CorrelationId=@CorrelationId AND EventTypeCode=N'StatusTransition') BEGIN COMMIT; RETURN; END;
DECLARE @FromStatus NVARCHAR(80),@PolicyId UNIQUEIDENTIFIER,@PolicyVersionId UNIQUEIDENTIFIER,@VersionNumber INT,@ActorName NVARCHAR(160),@RequiresApproval BIT,@RequiresCarrier BIT,@CreatesVersion BIT,@CreatesAccounting BIT,@CreatesCommission BIT,@CreatesDocuments BIT,@RequiresCertificateReview BIT,@RequiresNotes BIT,@ProfileRequiresVersion BIT,@EffectiveDate DATETIME2,@PolicyEffectiveDate DATETIME2,@PolicyExpirationDate DATETIME2,@ReversalOfEndorsementId UNIQUEIDENTIFIER;
SELECT @FromStatus=CASE WHEN endorsement.Status IN(N'PendingReview',N'Pending Review',N'In Review') THEN N'InReview' ELSE endorsement.Status END,@PolicyId=endorsement.PolicyId,@EffectiveDate=endorsement.EffectiveDate,@ReversalOfEndorsementId=endorsement.ReversalOfEndorsementId FROM Policy.PolicyEndorsement endorsement WITH(UPDLOCK,HOLDLOCK) WHERE endorsement.TenantId=@TenantId AND endorsement.EndorsementId=@EndorsementId AND endorsement.RowVersion=@RowVersion AND endorsement.IsDeleted=0;
IF @PolicyId IS NULL THROW 52416,N'The endorsement changed, is no longer available, or was not found in the authenticated tenant.',1;
SELECT @ActorName=COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(FirstName,N' ',LastName))),N''),Email) FROM IAM.[User] WHERE TenantId=@TenantId AND UserId=@ActorUserId AND IsDeleted=0;
IF @ActorName IS NULL THROW 52411,N'The authenticated user was not found in the tenant.',1;
SELECT @RequiresApproval=workflowRule.RequiresApproval,
       @RequiresCarrier=CASE WHEN workflowRule.RequiresCarrierDispatch=1 OR (@ToStatusCode=N'SubmittedToCarrier' AND profile.RequiresCarrierApproval=1) THEN 1 ELSE 0 END,
       @CreatesVersion=CASE WHEN workflowRule.RequiresPolicyVersion=1 OR (@ToStatusCode=N'PolicyUpdated' AND profile.RequiresPolicyVersion=1) THEN 1 ELSE 0 END,
       @CreatesAccounting=CASE WHEN workflowRule.RequiresAccountingWork=1 OR (@ToStatusCode=N'PolicyUpdated' AND profile.RequiresAccountingWork=1) THEN 1 ELSE 0 END,
       @CreatesCommission=CASE WHEN workflowRule.RequiresCommissionWork=1 OR (@ToStatusCode=N'PolicyUpdated' AND profile.RequiresCommissionWork=1) THEN 1 ELSE 0 END,
       @CreatesDocuments=CASE WHEN workflowRule.RequiresDocumentWork=1 OR (@ToStatusCode=N'Issued' AND profile.RequiresDocumentWork=1) THEN 1 ELSE 0 END,
       @RequiresCertificateReview=CASE WHEN workflowRule.RequiresCertificateReview=1 OR (@ToStatusCode=N'Issued' AND (profile.RequiresCertificateReview=1 OR profile.IsCertificateRelated=1)) THEN 1 ELSE 0 END,
       @RequiresNotes=CASE WHEN JSON_VALUE(workflowRule.RuleJson,N'$.requiresNotes')=N'true' THEN 1 ELSE 0 END,
       @ProfileRequiresVersion=profile.RequiresPolicyVersion
FROM Policy.EndorsementTypeWorkflowRule workflowRule
JOIN Policy.EndorsementType type ON type.TenantId=workflowRule.TenantId AND type.EndorsementTypeId=workflowRule.EndorsementTypeId AND type.IsActive=1 AND type.IsDeleted=0
JOIN Policy.EndorsementTypeProfile profile ON profile.TenantId=type.TenantId AND profile.EndorsementTypeId=type.EndorsementTypeId AND profile.IsActive=1 AND profile.IsDeleted=0
JOIN Policy.PolicyEndorsement endorsement ON endorsement.TenantId=type.TenantId AND endorsement.EndorsementId=@EndorsementId AND endorsement.IsDeleted=0
 AND (endorsement.EndorsementType=type.TypeCode OR (endorsement.EndorsementType=type.TypeName AND NOT EXISTS(SELECT 1 FROM Policy.EndorsementType duplicateType WHERE duplicateType.TenantId=type.TenantId AND duplicateType.TypeName=type.TypeName AND duplicateType.EndorsementTypeId<>type.EndorsementTypeId AND duplicateType.IsActive=1 AND duplicateType.IsDeleted=0)) OR EXISTS(SELECT 1 FROM Policy.EndorsementTypeAlias alias WHERE alias.TenantId=type.TenantId AND alias.EndorsementTypeId=type.EndorsementTypeId AND alias.LegacyTypeValue=endorsement.EndorsementType AND alias.IsActive=1 AND alias.IsDeleted=0 AND (alias.DescriptionContains IS NULL OR endorsement.Description LIKE N'%'+alias.DescriptionContains+N'%')))
WHERE workflowRule.TenantId=@TenantId AND workflowRule.FromStatusCode=@FromStatus AND workflowRule.ToStatusCode=@ToStatusCode AND workflowRule.IsActive=1 AND workflowRule.IsDeleted=0;
IF @RequiresApproval IS NOT NULL AND NOT EXISTS
(
    SELECT 1 FROM Policy.EndorsementTypeWorkflowRule workflowRule
    JOIN Policy.EndorsementType type ON type.TenantId=workflowRule.TenantId AND type.EndorsementTypeId=workflowRule.EndorsementTypeId AND type.IsActive=1 AND type.IsDeleted=0
    JOIN Policy.EndorsementTypeProfile profile ON profile.TenantId=type.TenantId AND profile.EndorsementTypeId=type.EndorsementTypeId AND profile.IsActive=1 AND profile.IsDeleted=0
    JOIN Policy.PolicyEndorsement endorsement ON endorsement.TenantId=type.TenantId AND endorsement.EndorsementId=@EndorsementId AND endorsement.IsDeleted=0
      AND (endorsement.EndorsementType=type.TypeCode OR (endorsement.EndorsementType=type.TypeName AND NOT EXISTS(SELECT 1 FROM Policy.EndorsementType duplicateType WHERE duplicateType.TenantId=type.TenantId AND duplicateType.TypeName=type.TypeName AND duplicateType.EndorsementTypeId<>type.EndorsementTypeId AND duplicateType.IsActive=1 AND duplicateType.IsDeleted=0)) OR EXISTS(SELECT 1 FROM Policy.EndorsementTypeAlias alias WHERE alias.TenantId=type.TenantId AND alias.EndorsementTypeId=type.EndorsementTypeId AND alias.LegacyTypeValue=endorsement.EndorsementType AND alias.IsActive=1 AND alias.IsDeleted=0))
    WHERE workflowRule.TenantId=@TenantId AND workflowRule.FromStatusCode=@FromStatus AND workflowRule.ToStatusCode=@ToStatusCode AND workflowRule.IsActive=1 AND workflowRule.IsDeleted=0
      AND (JSON_VALUE(workflowRule.RuleJson,N'$.conditionCode') IS NULL
           OR (JSON_VALUE(workflowRule.RuleJson,N'$.conditionCode')=N'ApprovalRequired' AND (profile.RequiresUnderwritingReview=1 OR profile.IsHighRisk=1))
           OR (JSON_VALUE(workflowRule.RuleJson,N'$.conditionCode')=N'ApprovalNotRequiredCarrier' AND profile.RequiresUnderwritingReview=0 AND profile.IsHighRisk=0 AND profile.RequiresCarrierApproval=1)
           OR (JSON_VALUE(workflowRule.RuleJson,N'$.conditionCode')=N'ApprovalNotRequiredPolicy' AND profile.RequiresUnderwritingReview=0 AND profile.IsHighRisk=0 AND profile.RequiresCarrierApproval=0))
) THROW 52426,N'The requested endorsement transition does not satisfy the configured approval condition.',1;
IF @RequiresApproval IS NULL THROW 52412,N'The requested endorsement status transition is not allowed.',1;
IF @RequiresNotes=1 AND NULLIF(LTRIM(RTRIM(@Notes)),N'') IS NULL THROW 52420,N'Reviewer notes are required for the selected workflow action.',1;
IF NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementChange WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND IsDeleted=0) THROW 52413,N'At least one typed policy change is required before workflow submission.',1;
IF @ToStatusCode IN(N'Submitted',N'InReview',N'PendingApproval') AND EXISTS
(
    SELECT 1
    FROM Policy.EndorsementTypeDocumentRequirement requirement
    JOIN Policy.EndorsementType type ON type.TenantId=requirement.TenantId AND type.EndorsementTypeId=requirement.EndorsementTypeId AND type.IsActive=1 AND type.IsDeleted=0
    JOIN Policy.PolicyEndorsement endorsement ON endorsement.TenantId=type.TenantId AND endorsement.EndorsementId=@EndorsementId AND endorsement.IsDeleted=0
     AND (endorsement.EndorsementType=type.TypeCode OR (endorsement.EndorsementType=type.TypeName AND NOT EXISTS(SELECT 1 FROM Policy.EndorsementType duplicateType WHERE duplicateType.TenantId=type.TenantId AND duplicateType.TypeName=type.TypeName AND duplicateType.EndorsementTypeId<>type.EndorsementTypeId AND duplicateType.IsActive=1 AND duplicateType.IsDeleted=0)) OR EXISTS(SELECT 1 FROM Policy.EndorsementTypeAlias alias WHERE alias.TenantId=type.TenantId AND alias.EndorsementTypeId=type.EndorsementTypeId AND alias.LegacyTypeValue=endorsement.EndorsementType AND alias.IsActive=1 AND alias.IsDeleted=0 AND (alias.DescriptionContains IS NULL OR endorsement.Description LIKE N'%'+alias.DescriptionContains+N'%')))
    WHERE requirement.TenantId=@TenantId AND requirement.IsRequired=1 AND requirement.IsActive=1 AND requirement.IsDeleted=0
      AND NOT EXISTS
      (
          SELECT 1 FROM Policy.PolicyDocumentLink documentLink
          WHERE documentLink.TenantId=@TenantId AND documentLink.PolicyId=@PolicyId AND documentLink.SourceEntityName=N'PolicyEndorsement'
            AND documentLink.SourceEntityId=@EndorsementId AND documentLink.DocumentRoleCode=requirement.RequirementCode AND documentLink.IsDeleted=0
      )
) THROW 52419,N'One or more required endorsement documents have not been linked.',1;
SELECT @PolicyEffectiveDate=EffectiveDate,@PolicyExpirationDate=ExpirationDate FROM Submissions.BoundPolicy WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0;
IF @EffectiveDate<@PolicyEffectiveDate OR @EffectiveDate>@PolicyExpirationDate THROW 52414,N'The endorsement effective date must be within the active policy term.',1;
IF @RequiresApproval=1 AND NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementApproval WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND StatusCode=N'Approved' AND IsDeleted=0) THROW 52415,N'An approved internal review is required for this transition.',1;
IF @ToStatusCode=N'CarrierApproved' AND EXISTS(SELECT 1 FROM Policy.PolicyEndorsementCarrierDispatch WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND StatusCode<>N'Completed' AND IsDeleted=0) THROW 52417,N'Carrier submission must complete before carrier approval.',1;
IF @ToStatusCode=N'Completed' AND ((@ProfileRequiresVersion=1 AND (SELECT PolicyVersionAfterId FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId) IS NULL) OR EXISTS(SELECT 1 FROM Policy.PolicyEndorsementAccountingWork WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND StatusCode<>N'Completed' AND IsDeleted=0) OR EXISTS(SELECT 1 FROM Policy.PolicyEndorsementDocumentWork WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND StatusCode<>N'Completed' AND IsDeleted=0) OR EXISTS(SELECT 1 FROM Policy.PolicyEndorsementCarrierDispatch WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND StatusCode<>N'Completed' AND IsDeleted=0)) THROW 52418,N'Policy activation, carrier, accounting, and document work must complete before the endorsement can complete.',1;

IF @ToStatusCode=N'PendingApproval' AND NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementApproval WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND StatusCode=N'Pending' AND IsDeleted=0)
BEGIN
    DECLARE @ApprovalRouteId UNIQUEIDENTIFIER,@ApprovalAssigneeId UNIQUEIDENTIFIER,@ApprovalPermissionCode NVARCHAR(120),@ApprovalRoleCode NVARCHAR(100),@ApprovalStrategyCode NVARCHAR(40),@ApprovalSubject NVARCHAR(300),@ApprovalBody NVARCHAR(1000),@EndorsementNumber NVARCHAR(80),@PolicyNumber NVARCHAR(80),@ApprovalLevelCode NVARCHAR(80),@ApprovalId UNIQUEIDENTIFIER;
    SELECT @ApprovalLevelCode=COALESCE(NULLIF(ApprovalLevelCode,N''),N'StandardAuthority') FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId;
    SELECT TOP(1) @ApprovalRouteId=route.EndorsementWorkflowRouteId,@ApprovalPermissionCode=route.RequiredPermissionCode,@ApprovalRoleCode=route.AssignedRoleCode,@ApprovalStrategyCode=route.AssignmentStrategyCode,@ApprovalAssigneeId=route.AssignedToUserId,@ApprovalSubject=route.NotificationSubjectTemplate,@ApprovalBody=route.NotificationBodyTemplate
    FROM Policy.EndorsementWorkflowRoute route
    JOIN Policy.EndorsementType type ON type.TenantId=route.TenantId AND type.EndorsementTypeId=route.EndorsementTypeId AND type.IsActive=1 AND type.IsDeleted=0
    JOIN Policy.PolicyEndorsement endorsement ON endorsement.TenantId=type.TenantId AND endorsement.EndorsementId=@EndorsementId AND endorsement.IsDeleted=0
      AND (endorsement.EndorsementType=type.TypeCode OR (endorsement.EndorsementType=type.TypeName AND NOT EXISTS(SELECT 1 FROM Policy.EndorsementType duplicateType WHERE duplicateType.TenantId=type.TenantId AND duplicateType.TypeName=type.TypeName AND duplicateType.EndorsementTypeId<>type.EndorsementTypeId AND duplicateType.IsActive=1 AND duplicateType.IsDeleted=0)) OR EXISTS(SELECT 1 FROM Policy.EndorsementTypeAlias alias WHERE alias.TenantId=type.TenantId AND alias.EndorsementTypeId=type.EndorsementTypeId AND alias.LegacyTypeValue=endorsement.EndorsementType AND alias.IsActive=1 AND alias.IsDeleted=0 AND (alias.DescriptionContains IS NULL OR endorsement.Description LIKE N'%'+alias.DescriptionContains+N'%')))
    WHERE route.TenantId=@TenantId AND route.RoutePurposeCode=N'Approval' AND (route.ApprovalLevelCode=@ApprovalLevelCode OR route.ApprovalLevelCode IS NULL) AND route.IsActive=1 AND route.IsDeleted=0 ORDER BY CASE WHEN route.ApprovalLevelCode=@ApprovalLevelCode THEN 0 ELSE 1 END,route.SortOrder,route.CreatedDateUtc;
    IF @ApprovalRouteId IS NULL THROW 52424,N'No active approval route is configured for this endorsement type.',1;
    IF @ApprovalStrategyCode=N'ExplicitUser'
        SELECT @ApprovalAssigneeId=appUser.UserId FROM IAM.[User] appUser WHERE appUser.TenantId=@TenantId AND appUser.UserId=@ApprovalAssigneeId AND appUser.IsActive=1 AND appUser.IsDeleted=0
        AND EXISTS(SELECT 1 FROM IAM.UserRole userRole JOIN IAM.RolePermission rolePermission ON rolePermission.TenantId=@TenantId AND rolePermission.RoleId=userRole.RoleId AND rolePermission.IsDeleted=0 LEFT JOIN IAM.Permission permission ON permission.TenantId=@TenantId AND permission.PermissionId=rolePermission.PermissionId AND permission.IsActive=1 AND permission.IsDeleted=0 WHERE userRole.TenantId=@TenantId AND userRole.UserId=appUser.UserId AND userRole.IsActive=1 AND userRole.IsDeleted=0 AND COALESCE(rolePermission.PermissionCode,permission.PermissionCode)=@ApprovalPermissionCode);
    ELSE
        SELECT TOP(1) @ApprovalAssigneeId=userRole.UserId
        FROM IAM.UserRole userRole
        JOIN IAM.Role role ON role.TenantId=@TenantId AND role.RoleId=userRole.RoleId AND role.IsActive=1 AND role.IsDeleted=0
        JOIN IAM.[User] appUser ON appUser.TenantId=@TenantId AND appUser.UserId=userRole.UserId AND appUser.IsActive=1 AND appUser.IsDeleted=0
        LEFT JOIN IAM.RolePermission rolePermission ON rolePermission.TenantId=@TenantId AND rolePermission.RoleId=role.RoleId AND rolePermission.IsDeleted=0
        LEFT JOIN IAM.Permission permission ON permission.TenantId=@TenantId AND permission.PermissionId=rolePermission.PermissionId AND permission.IsActive=1 AND permission.IsDeleted=0
        WHERE userRole.TenantId=@TenantId AND userRole.IsActive=1 AND userRole.IsDeleted=0 AND (userRole.EffectiveStartDateUtc IS NULL OR userRole.EffectiveStartDateUtc<=SYSUTCDATETIME()) AND (userRole.EffectiveEndDateUtc IS NULL OR userRole.EffectiveEndDateUtc>SYSUTCDATETIME())
          AND COALESCE(rolePermission.PermissionCode,permission.PermissionCode)=@ApprovalPermissionCode
          AND ((@ApprovalStrategyCode=N'Role' AND role.RoleCode=@ApprovalRoleCode) OR @ApprovalStrategyCode=N'Permission')
        ORDER BY CASE WHEN appUser.UserId=@ActorUserId THEN 1 ELSE 0 END,userRole.AssignedDateUtc,appUser.UserId;
    IF @ApprovalAssigneeId IS NULL
        SELECT TOP(1) @ApprovalAssigneeId=candidate.UserId,@ApprovalStrategyCode=candidate.StrategyCode
        FROM Policy.PolicyEndorsement endorsement
        CROSS APPLY
        (
            SELECT TOP(1) eligible.UserId,CASE WHEN eligible.Priority=0 THEN N'AccountManager' ELSE N'Producer' END StrategyCode
            FROM (SELECT accountAssignment.UserId,CASE WHEN UPPER(REPLACE(REPLACE(REPLACE(accountAssignment.AssignmentRoleCode,N'_',N''),N'-',N''),N' ',N''))=N'ACCOUNTMANAGER' THEN 0 ELSE 1 END Priority,accountAssignment.CreatedDateUtc AssignedDateUtc FROM Client.AccountServiceAssignment accountAssignment WHERE accountAssignment.TenantId=endorsement.TenantId AND accountAssignment.AccountId=endorsement.AccountId AND UPPER(REPLACE(REPLACE(REPLACE(accountAssignment.AssignmentRoleCode,N'_',N''),N'-',N''),N' ',N'')) IN(N'ACCOUNTMANAGER',N'PRODUCER') AND accountAssignment.IsPrimary=1 AND accountAssignment.EffectiveDate<=CONVERT(date,SYSUTCDATETIME()) AND (accountAssignment.ExpirationDate IS NULL OR accountAssignment.ExpirationDate>=CONVERT(date,SYSUTCDATETIME())) AND accountAssignment.IsDeleted=0) eligible
            JOIN IAM.[User] candidateUser ON candidateUser.TenantId=endorsement.TenantId AND candidateUser.UserId=eligible.UserId AND candidateUser.IsActive=1 AND candidateUser.IsDeleted=0
            WHERE EXISTS(SELECT 1 FROM IAM.UserRole userRole JOIN IAM.RolePermission rolePermission ON rolePermission.TenantId=userRole.TenantId AND rolePermission.RoleId=userRole.RoleId AND rolePermission.IsDeleted=0 LEFT JOIN IAM.Permission permission ON permission.TenantId=userRole.TenantId AND permission.PermissionId=rolePermission.PermissionId AND permission.IsActive=1 AND permission.IsDeleted=0 WHERE userRole.TenantId=endorsement.TenantId AND userRole.UserId=eligible.UserId AND userRole.IsActive=1 AND userRole.IsDeleted=0 AND (userRole.EffectiveStartDateUtc IS NULL OR userRole.EffectiveStartDateUtc<=SYSUTCDATETIME()) AND (userRole.EffectiveEndDateUtc IS NULL OR userRole.EffectiveEndDateUtc>SYSUTCDATETIME()) AND COALESCE(rolePermission.PermissionCode,permission.PermissionCode)=@ApprovalPermissionCode)
            ORDER BY eligible.Priority,eligible.AssignedDateUtc DESC,eligible.UserId
        ) candidate
        WHERE endorsement.TenantId=@TenantId AND endorsement.EndorsementId=@EndorsementId AND endorsement.IsDeleted=0
    IF @ApprovalAssigneeId IS NULL THROW 52425,N'No active tenant user satisfies the configured approval route.',1;
    SELECT @ApprovalId=ApprovalId FROM Policy.PolicyEndorsementApproval WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND ApprovalLevelCode=@ApprovalLevelCode AND IsDeleted=0;
    IF @ApprovalId IS NULL
    BEGIN
        SET @ApprovalId=NEWID();
        INSERT Policy.PolicyEndorsementApproval(ApprovalId,TenantId,EndorsementId,ApprovalLevelCode,StatusCode,RequestedDateUtc,RequestedByUserId,AssignedToUserId,CreatedDateUtc,IsDeleted)
        VALUES(@ApprovalId,@TenantId,@EndorsementId,@ApprovalLevelCode,N'Pending',SYSUTCDATETIME(),@ActorUserId,@ApprovalAssigneeId,SYSUTCDATETIME(),0);
    END
    ELSE
        UPDATE Policy.PolicyEndorsementApproval SET StatusCode=N'Pending',RequestedDateUtc=SYSUTCDATETIME(),RequestedByUserId=@ActorUserId,AssignedToUserId=@ApprovalAssigneeId,DecidedDateUtc=NULL,DecidedByUserId=NULL,DecisionNotes=NULL WHERE TenantId=@TenantId AND ApprovalId=@ApprovalId AND StatusCode=N'InformationRequested' AND IsDeleted=0;
    SELECT @EndorsementNumber=EndorsementNumber,@PolicyNumber=PolicyNumber FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId;
    INSERT Core.Notification(NotificationId,TenantId,RecipientUserId,ChannelCode,Subject,Body,EntityName,EntityId,StatusCode,IsRead,CreatedDateUtc,CreatedByUserId,IsDeleted)
    VALUES(NEWID(),@TenantId,@ApprovalAssigneeId,N'InApp',REPLACE(REPLACE(@ApprovalSubject,N'{EndorsementNumber}',@EndorsementNumber),N'{PolicyNumber}',@PolicyNumber),REPLACE(REPLACE(@ApprovalBody,N'{EndorsementNumber}',@EndorsementNumber),N'{PolicyNumber}',@PolicyNumber),N'PolicyEndorsementApproval',@ApprovalId,N'Queued',0,SYSUTCDATETIME(),@ActorUserId,0);
END;

IF @RequiresCarrier=1
BEGIN
    DECLARE @CarrierConfigurationId UNIQUEIDENTIFIER,@ChannelCode NVARCHAR(50),@MaxAttempts INT;
    SELECT TOP 1 @CarrierConfigurationId=configuration.CarrierConfigurationId,@ChannelCode=configuration.ChannelCode,@MaxAttempts=configuration.MaxAttempts
    FROM Submissions.BoundPolicy policy
    JOIN Policy.PolicyEndorsement endorsement ON endorsement.TenantId=policy.TenantId AND endorsement.PolicyId=policy.PolicyId AND endorsement.EndorsementId=@EndorsementId
    LEFT JOIN Policy.PolicyEndorsementCarrierConfiguration configuration ON configuration.TenantId=policy.TenantId AND (configuration.CarrierId=policy.CarrierId OR configuration.CarrierId IS NULL) AND (configuration.LineOfBusiness=policy.LineOfBusiness OR configuration.LineOfBusiness IS NULL) AND configuration.IsConfigured=1 AND configuration.IsActive=1 AND configuration.IsDeleted=0
    WHERE policy.TenantId=@TenantId AND policy.PolicyId=@PolicyId
    ORDER BY CASE WHEN configuration.CarrierId=policy.CarrierId THEN 0 ELSE 1 END,CASE WHEN configuration.LineOfBusiness=policy.LineOfBusiness THEN 0 ELSE 1 END;
    SELECT @ChannelCode=COALESCE(NULLIF(CarrierMethodCode,N''),@ChannelCode,N'Manual') FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId;
    INSERT Policy.PolicyEndorsementCarrierDispatch(CarrierDispatchId,TenantId,EndorsementId,CarrierConfigurationId,ChannelCode,IdempotencyKey,StatusCode,RequestPayload,AttemptCount,MaxAttempts,NextAttemptDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
    VALUES(NEWID(),@TenantId,@EndorsementId,@CarrierConfigurationId,@ChannelCode,CONCAT(N'endorsement:',CONVERT(NVARCHAR(36),@EndorsementId),N':carrier'),N'Queued',JSON_OBJECT(N'endorsementId':@EndorsementId,N'policyId':@PolicyId,N'notes':@Notes),0,COALESCE(@MaxAttempts,5),SYSUTCDATETIME(),SYSUTCDATETIME(),@ActorUserId,0);
END;

IF @CreatesVersion=1
BEGIN
    SELECT @VersionNumber=COALESCE(MAX(VersionNumber),0)+1 FROM Policy.PolicyVersion WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0;
    SET @PolicyVersionId=NEWID();
    EXEC Policy.usp_ApplyPolicyEndorsement @TenantId,@PolicyId,@EndorsementId,@PolicyVersionId,@VersionNumber,@ActorUserId;
END;

IF @CreatesAccounting=1
INSERT Policy.PolicyEndorsementAccountingWork(AccountingWorkId,TenantId,EndorsementId,PolicyId,WorkTypeCode,IdempotencyKey,CurrencyCode,PremiumAmount,FeeAmount,TaxAmount,TotalAmount,StatusCode,AttemptCount,MaxAttempts,NextAttemptDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),TenantId,EndorsementId,PolicyId,CASE WHEN TotalCostDelta<0 THEN N'Refund' ELSE N'Invoice' END,CONCAT(N'endorsement:',CONVERT(NVARCHAR(36),EndorsementId),N':accounting'),CurrencyCode,PremiumDelta,AgencyFeeDelta,TaxDelta,TotalCostDelta,N'Queued',0,8,SYSUTCDATETIME(),SYSUTCDATETIME(),@ActorUserId,0
FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId;

IF @CreatesCommission=1 AND @CreatesAccounting=0
INSERT Policy.PolicyEndorsementAccountingWork(AccountingWorkId,TenantId,EndorsementId,PolicyId,WorkTypeCode,IdempotencyKey,CurrencyCode,PremiumAmount,FeeAmount,TaxAmount,TotalAmount,StatusCode,AttemptCount,MaxAttempts,NextAttemptDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),TenantId,EndorsementId,PolicyId,N'CommissionOnly',CONCAT(N'endorsement:',CONVERT(NVARCHAR(36),EndorsementId),N':commission'),CurrencyCode,PremiumDelta,0,0,0,N'Queued',0,8,SYSUTCDATETIME(),SYSUTCDATETIME(),@ActorUserId,0
FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId
AND NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementAccountingWork existing WHERE existing.TenantId=@TenantId AND existing.EndorsementId=@EndorsementId AND existing.WorkTypeCode=N'CommissionOnly' AND existing.IsDeleted=0);

IF @CreatesDocuments=1
INSERT Policy.PolicyEndorsementDocumentWork(DocumentWorkId,TenantId,EndorsementId,PolicyId,DocumentTypeCode,IdempotencyKey,StatusCode,AttemptCount,MaxAttempts,NextAttemptDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),@TenantId,@EndorsementId,@PolicyId,definition.DocumentTypeCode,CONCAT(N'endorsement:',CONVERT(NVARCHAR(36),@EndorsementId),N':document:',definition.DocumentTypeCode),N'Queued',0,8,SYSUTCDATETIME(),SYSUTCDATETIME(),@ActorUserId,0
FROM Policy.PolicyEndorsementDocumentWorkDefinition definition
WHERE definition.TenantId=@TenantId AND definition.TriggerCode=N'Workflow' AND definition.IsActive=1 AND definition.IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementDocumentWork existing WHERE existing.TenantId=@TenantId AND existing.EndorsementId=@EndorsementId AND existing.DocumentTypeCode=definition.DocumentTypeCode AND existing.IsDeleted=0);

IF @CreatesCommission=1
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId)
VALUES(NEWID(),@TenantId,@EndorsementId,@PolicyId,N'CommissionReviewRequired',N'Commission recalculation or review is required by endorsement configuration.',JSON_OBJECT(N'accountingWorkCreated':@CreatesAccounting),@CorrelationId,SYSUTCDATETIME(),@ActorUserId);

IF @RequiresCertificateReview=1
BEGIN
INSERT Policy.PolicyEndorsementDocumentWork(DocumentWorkId,TenantId,EndorsementId,PolicyId,DocumentTypeCode,IdempotencyKey,StatusCode,AttemptCount,MaxAttempts,NextAttemptDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),@TenantId,@EndorsementId,@PolicyId,N'CertificateReview',CONCAT(N'endorsement:',CONVERT(NVARCHAR(36),@EndorsementId),N':certificate-review'),N'Queued',0,8,SYSUTCDATETIME(),SYSUTCDATETIME(),@ActorUserId,0
WHERE NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementDocumentWork existing WHERE existing.TenantId=@TenantId AND existing.EndorsementId=@EndorsementId AND existing.DocumentTypeCode=N'CertificateReview' AND existing.IsDeleted=0);
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId)
VALUES(NEWID(),@TenantId,@EndorsementId,@PolicyId,N'CertificateReviewRequired',N'Certificates affected by this endorsement require review.',JSON_OBJECT(N'endorsementId':@EndorsementId),@CorrelationId,SYSUTCDATETIME(),@ActorUserId);
END;

UPDATE Policy.PolicyEndorsement SET Status=@ToStatusCode,WorkflowStage=@ToStatusCode,PolicyVersionAfterId=COALESCE(@PolicyVersionId,PolicyVersionAfterId),LastTransitionCorrelationId=@CorrelationId,SubmittedDateUtc=CASE WHEN @ToStatusCode=N'SubmittedToCarrier' THEN SYSUTCDATETIME() ELSE SubmittedDateUtc END,ApprovedDateUtc=CASE WHEN @ToStatusCode=N'CarrierApproved' THEN SYSUTCDATETIME() ELSE ApprovedDateUtc END,IssuedDateUtc=CASE WHEN @ToStatusCode=N'PolicyUpdated' THEN SYSUTCDATETIME() ELSE IssuedDateUtc END,CompletedDateUtc=CASE WHEN @ToStatusCode=N'Completed' THEN SYSUTCDATETIME() ELSE CompletedDateUtc END,RejectedDateUtc=CASE WHEN @ToStatusCode=N'Rejected' THEN SYSUTCDATETIME() ELSE RejectedDateUtc END,CancelledDateUtc=CASE WHEN @ToStatusCode=N'Cancelled' THEN SYSUTCDATETIME() ELSE CancelledDateUtc END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND RowVersion=@RowVersion;
IF @@ROWCOUNT<>1 THROW 52416,N'The endorsement changed while the transition was being processed.',1;
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
        await connection.ExecuteAsync(new CommandDefinition(sql, new { EndorsementId=endorsementId,request.TenantId,request.ToStatusCode,request.Notes,request.CorrelationId,request.RowVersion,request.ActorUserId }, cancellationToken:cancellationToken));
    }

    public async Task DecideApprovalAsync(Guid endorsementId, Guid approvalId, DecidePolicyEndorsementApprovalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON; BEGIN TRAN;
IF EXISTS(SELECT 1 FROM Policy.PolicyEndorsementEvent WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND CorrelationId=@CorrelationId AND EventTypeCode=N'ApprovalDecision') BEGIN COMMIT; RETURN; END;
IF @DecisionCode NOT IN(N'Approved',N'Rejected') THROW 52420,N'Approval decision must be Approved or Rejected.',1;
DECLARE @PolicyId UNIQUEIDENTIFIER,@ActorName NVARCHAR(160),@OutcomeStatusCode NVARCHAR(80),@RequestedByUserId UNIQUEIDENTIFIER,@EndorsementNumber NVARCHAR(80);
SELECT @PolicyId=PolicyId FROM Policy.PolicyEndorsement WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND Status=N'PendingApproval' AND RowVersion=@EndorsementRowVersion AND IsDeleted=0;
SELECT @ActorName=COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(FirstName,N' ',LastName))),N''),Email) FROM IAM.[User] WHERE TenantId=@TenantId AND UserId=@ActorUserId AND IsDeleted=0;
IF @PolicyId IS NULL OR @ActorName IS NULL THROW 52421,N'Endorsement or authenticated user was not found in the tenant.',1;
UPDATE Policy.PolicyEndorsementApproval SET StatusCode=@DecisionCode,DecidedDateUtc=SYSUTCDATETIME(),DecidedByUserId=@ActorUserId,DecisionNotes=@Notes WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND ApprovalId=@ApprovalId AND AssignedToUserId=@ActorUserId AND StatusCode=N'Pending' AND RowVersion=@ApprovalRowVersion AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52422,N'The pending endorsement approval was not found.',1;
SELECT @RequestedByUserId=RequestedByUserId FROM Policy.PolicyEndorsementApproval WHERE TenantId=@TenantId AND ApprovalId=@ApprovalId;
SELECT TOP(1) @OutcomeStatusCode=CASE WHEN @DecisionCode=N'Approved' THEN route.ApprovedStatusCode ELSE route.RejectedStatusCode END
FROM Policy.EndorsementWorkflowRoute route
JOIN Policy.EndorsementType type ON type.TenantId=route.TenantId AND type.EndorsementTypeId=route.EndorsementTypeId AND type.IsActive=1 AND type.IsDeleted=0
JOIN Policy.PolicyEndorsement endorsement ON endorsement.TenantId=type.TenantId AND endorsement.EndorsementId=@EndorsementId AND (endorsement.EndorsementType=type.TypeCode OR endorsement.EndorsementType=type.TypeName OR EXISTS(SELECT 1 FROM Policy.EndorsementTypeAlias alias WHERE alias.TenantId=type.TenantId AND alias.EndorsementTypeId=type.EndorsementTypeId AND alias.LegacyTypeValue=endorsement.EndorsementType AND alias.IsActive=1 AND alias.IsDeleted=0))
WHERE route.TenantId=@TenantId AND route.RoutePurposeCode=N'Approval' AND route.IsActive=1 AND route.IsDeleted=0 ORDER BY route.SortOrder;
IF @OutcomeStatusCode IS NULL THROW 52426,N'The approval outcome status is not configured for this endorsement type.',1;
UPDATE Policy.PolicyEndorsement SET Status=@OutcomeStatusCode,WorkflowStage=@OutcomeStatusCode,ApprovedDateUtc=CASE WHEN @DecisionCode=N'Approved' THEN SYSUTCDATETIME() ELSE ApprovedDateUtc END,RejectedDateUtc=CASE WHEN @DecisionCode=N'Rejected' THEN SYSUTCDATETIME() ELSE RejectedDateUtc END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND RowVersion=@EndorsementRowVersion;
IF @@ROWCOUNT<>1 THROW 52423,N'The endorsement changed while the approval decision was being processed.',1;
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,FromStatusCode,ToStatusCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId) VALUES(NEWID(),@TenantId,@EndorsementId,@PolicyId,N'ApprovalDecision',N'PendingApproval',@OutcomeStatusCode,CONCAT(N'Endorsement review ',LOWER(@DecisionCode),N' by ',@ActorName,N'.'),JSON_OBJECT(N'approvalId':@ApprovalId,N'decision':@DecisionCode,N'notes':@Notes),@CorrelationId,SYSUTCDATETIME(),@ActorUserId);
SELECT @EndorsementNumber=EndorsementNumber FROM Policy.PolicyEndorsement WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId;
IF EXISTS(SELECT 1 FROM IAM.[User] WHERE TenantId=@TenantId AND UserId=@RequestedByUserId AND IsActive=1 AND IsDeleted=0)
INSERT Core.Notification(NotificationId,TenantId,RecipientUserId,ChannelCode,Subject,Body,EntityName,EntityId,StatusCode,IsRead,Priority,Category,DeliveryProvider,DeliveryStatus,PolicyStatus,SyncStatus,AttemptCount,MaxAttempts,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@RequestedByUserId,N'InApp',CONCAT(N'Endorsement ',LOWER(@DecisionCode)),CONCAT(N'Endorsement ',@EndorsementNumber,N' was ',LOWER(@DecisionCode),N'.'),N'PolicyEndorsementApproval',@ApprovalId,N'Queued',0,N'Normal',N'Policy Endorsement Approval',N'PLATFORM_IN_APP',N'Queued',N'Compliant',N'Synced',0,5,SYSUTCDATETIME(),@ActorUserId,0);
COMMIT;";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { EndorsementId=endorsementId,ApprovalId=approvalId,request.TenantId,request.DecisionCode,request.Notes,request.EndorsementRowVersion,request.ApprovalRowVersion,request.CorrelationId,request.ActorUserId }, cancellationToken:cancellationToken));
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
