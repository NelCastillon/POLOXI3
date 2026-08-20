using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyEndorsements;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed partial class PolicyEndorsementRepository
{
    public async Task<PolicyEndorsementRoutePreviewDto?> GetRoutePreviewAsync(Guid tenantId, Guid endorsementId, string routePurposeCode, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        const string sql = """
DECLARE @ApprovalLevelCode NVARCHAR(80),@AssignmentStrategyCode NVARCHAR(40),@AssignedToUserId UNIQUEIDENTIFIER,@ConfiguredUserId UNIQUEIDENTIFIER,@AssignedRoleCode NVARCHAR(100),@RequiredPermissionCode NVARCHAR(120),@DestinationStatusCode NVARCHAR(80),@NotificationCategory NVARCHAR(100);
SELECT @ApprovalLevelCode=COALESCE(NULLIF(endorsement.ApprovalLevelCode,N''),N'StandardAuthority') FROM Policy.PolicyEndorsement endorsement WHERE endorsement.TenantId=@TenantId AND endorsement.EndorsementId=@EndorsementId AND endorsement.IsDeleted=0;
IF @ApprovalLevelCode IS NULL RETURN;
SELECT TOP(1) @AssignmentStrategyCode=route.AssignmentStrategyCode,@ConfiguredUserId=route.AssignedToUserId,@AssignedRoleCode=route.AssignedRoleCode,@RequiredPermissionCode=route.RequiredPermissionCode,@DestinationStatusCode=CASE WHEN @RoutePurposeCode=N'Approval' THEN N'PendingApproval' ELSE N'NeedMoreInfo' END,@NotificationCategory=route.NotificationCategory,
       @AssignedToUserId=CASE WHEN route.AssignmentStrategyCode=N'Requestor' THEN COALESCE(approval.RequestedByUserId,endorsement.CreatedByUserId) END
FROM Policy.PolicyEndorsement endorsement
JOIN Policy.EndorsementType type ON type.TenantId=endorsement.TenantId AND (type.TypeCode=endorsement.EndorsementType OR type.TypeName=endorsement.EndorsementType OR EXISTS(SELECT 1 FROM Policy.EndorsementTypeAlias alias WHERE alias.TenantId=type.TenantId AND alias.EndorsementTypeId=type.EndorsementTypeId AND alias.LegacyTypeValue=endorsement.EndorsementType AND alias.IsActive=1 AND alias.IsDeleted=0)) AND type.IsActive=1 AND type.IsDeleted=0
JOIN Policy.EndorsementWorkflowRoute route ON route.TenantId=type.TenantId AND route.EndorsementTypeId=type.EndorsementTypeId AND route.RoutePurposeCode=@RoutePurposeCode AND route.IsActive=1 AND route.IsDeleted=0
LEFT JOIN Policy.PolicyEndorsementApproval approval ON approval.TenantId=endorsement.TenantId AND approval.EndorsementId=endorsement.EndorsementId AND approval.StatusCode=N'Pending' AND approval.IsDeleted=0
WHERE endorsement.TenantId=@TenantId AND endorsement.EndorsementId=@EndorsementId AND (@RoutePurposeCode<>N'Approval' OR route.ApprovalLevelCode=@ApprovalLevelCode OR route.ApprovalLevelCode IS NULL)
ORDER BY CASE WHEN @RoutePurposeCode=N'Approval' AND route.ApprovalLevelCode=@ApprovalLevelCode THEN 0 ELSE 1 END,route.SortOrder,route.CreatedDateUtc;
IF @AssignmentStrategyCode=N'ExplicitUser' SET @AssignedToUserId=@ConfiguredUserId;
IF @AssignmentStrategyCode IN(N'Role',N'Permission')
    SELECT TOP(1) @AssignedToUserId=userRole.UserId FROM IAM.UserRole userRole JOIN IAM.Role role ON role.TenantId=@TenantId AND role.RoleId=userRole.RoleId AND role.IsActive=1 AND role.IsDeleted=0 JOIN IAM.[User] appUser ON appUser.TenantId=@TenantId AND appUser.UserId=userRole.UserId AND appUser.IsActive=1 AND appUser.IsDeleted=0 JOIN IAM.RolePermission rolePermission ON rolePermission.TenantId=@TenantId AND rolePermission.RoleId=role.RoleId AND rolePermission.IsDeleted=0 LEFT JOIN IAM.Permission permission ON permission.TenantId=@TenantId AND permission.PermissionId=rolePermission.PermissionId AND permission.IsActive=1 AND permission.IsDeleted=0 WHERE userRole.TenantId=@TenantId AND userRole.IsActive=1 AND userRole.IsDeleted=0 AND (userRole.EffectiveStartDateUtc IS NULL OR userRole.EffectiveStartDateUtc<=SYSUTCDATETIME()) AND (userRole.EffectiveEndDateUtc IS NULL OR userRole.EffectiveEndDateUtc>SYSUTCDATETIME()) AND COALESCE(rolePermission.PermissionCode,permission.PermissionCode)=@RequiredPermissionCode AND (@AssignmentStrategyCode=N'Permission' OR role.RoleCode=@AssignedRoleCode) ORDER BY CASE WHEN @RoutePurposeCode=N'Approval' AND appUser.UserId=@ActorUserId THEN 1 ELSE 0 END,userRole.AssignedDateUtc,appUser.UserId;
IF @AssignedToUserId IS NULL
BEGIN
    SELECT TOP(1) @AssignedToUserId=candidate.UserId,@AssignmentStrategyCode=candidate.StrategyCode
    FROM Policy.PolicyEndorsement endorsement
    CROSS APPLY
    (
        SELECT TOP(1) eligible.UserId,CASE WHEN eligible.Priority=0 THEN N'AccountManager' ELSE N'Producer' END StrategyCode
        FROM (SELECT accountAssignment.UserId,CASE WHEN UPPER(REPLACE(REPLACE(REPLACE(accountAssignment.AssignmentRoleCode,N'_',N''),N'-',N''),N' ',N''))=N'ACCOUNTMANAGER' THEN 0 ELSE 1 END Priority,accountAssignment.CreatedDateUtc AssignedDateUtc FROM Client.AccountServiceAssignment accountAssignment WHERE accountAssignment.TenantId=endorsement.TenantId AND accountAssignment.AccountId=endorsement.AccountId AND UPPER(REPLACE(REPLACE(REPLACE(accountAssignment.AssignmentRoleCode,N'_',N''),N'-',N''),N' ',N'')) IN(N'ACCOUNTMANAGER',N'PRODUCER') AND accountAssignment.IsPrimary=1 AND accountAssignment.EffectiveDate<=CONVERT(date,SYSUTCDATETIME()) AND (accountAssignment.ExpirationDate IS NULL OR accountAssignment.ExpirationDate>=CONVERT(date,SYSUTCDATETIME())) AND accountAssignment.IsDeleted=0) eligible
        JOIN IAM.[User] candidateUser ON candidateUser.TenantId=endorsement.TenantId AND candidateUser.UserId=eligible.UserId AND candidateUser.IsActive=1 AND candidateUser.IsDeleted=0
        WHERE EXISTS(SELECT 1 FROM IAM.UserRole userRole JOIN IAM.RolePermission rolePermission ON rolePermission.TenantId=userRole.TenantId AND rolePermission.RoleId=userRole.RoleId AND rolePermission.IsDeleted=0 LEFT JOIN IAM.Permission permission ON permission.TenantId=userRole.TenantId AND permission.PermissionId=rolePermission.PermissionId AND permission.IsActive=1 AND permission.IsDeleted=0 WHERE userRole.TenantId=endorsement.TenantId AND userRole.UserId=eligible.UserId AND userRole.IsActive=1 AND userRole.IsDeleted=0 AND (userRole.EffectiveStartDateUtc IS NULL OR userRole.EffectiveStartDateUtc<=SYSUTCDATETIME()) AND (userRole.EffectiveEndDateUtc IS NULL OR userRole.EffectiveEndDateUtc>SYSUTCDATETIME()) AND COALESCE(rolePermission.PermissionCode,permission.PermissionCode)=@RequiredPermissionCode)
        ORDER BY eligible.Priority,eligible.AssignedDateUtc DESC,eligible.UserId
    ) candidate
    WHERE endorsement.TenantId=@TenantId AND endorsement.EndorsementId=@EndorsementId AND endorsement.IsDeleted=0
END;
SELECT @RoutePurposeCode RoutePurposeCode,@AssignmentStrategyCode AssignmentStrategyCode,CASE WHEN @RoutePurposeCode=N'Approval' THEN @ApprovalLevelCode END ApprovalLevelCode,@RequiredPermissionCode RequiredPermissionCode,@AssignedRoleCode AssignedRoleCode,appUser.UserId AssignedToUserId,COALESCE(NULLIF(appUser.FullName,N''),NULLIF(appUser.DisplayName,N''),appUser.Email) AssignedToName,appUser.Email AssignedToEmail,@DestinationStatusCode DestinationStatusCode,@NotificationCategory NotificationCategory
FROM IAM.[User] appUser WHERE appUser.TenantId=@TenantId AND appUser.UserId=@AssignedToUserId AND appUser.IsActive=1 AND appUser.IsDeleted=0
AND EXISTS(SELECT 1 FROM IAM.UserRole userRole JOIN IAM.RolePermission rolePermission ON rolePermission.TenantId=@TenantId AND rolePermission.RoleId=userRole.RoleId AND rolePermission.IsDeleted=0 LEFT JOIN IAM.Permission permission ON permission.TenantId=@TenantId AND permission.PermissionId=rolePermission.PermissionId AND permission.IsActive=1 AND permission.IsDeleted=0 WHERE userRole.TenantId=@TenantId AND userRole.UserId=appUser.UserId AND userRole.IsActive=1 AND userRole.IsDeleted=0 AND COALESCE(rolePermission.PermissionCode,permission.PermissionCode)=@RequiredPermissionCode);
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PolicyEndorsementRoutePreviewDto>(new CommandDefinition(sql, new { TenantId = tenantId, EndorsementId = endorsementId, RoutePurposeCode = routePurposeCode, ActorUserId = actorUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PolicyEndorsementApprovalInboxItemDto>> GetApprovalInboxAsync(Guid tenantId, Guid assignedToUserId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT approval.ApprovalId,approval.TenantId,approval.EndorsementId,endorsement.PolicyId,endorsement.EndorsementNumber,endorsement.PolicyNumber,endorsement.AccountName,endorsement.EndorsementType,approval.ApprovalLevelCode,approval.StatusCode,endorsement.Priority,approval.RequestedDateUtc,approval.RequestedByUserId,requestedBy.FullName RequestedByName,approval.AssignedToUserId,assignedTo.FullName AssignedToName,endorsement.DueDate DueDateUtc,endorsement.RowVersion EndorsementRowVersion,approval.RowVersion ApprovalRowVersion
FROM Policy.PolicyEndorsementApproval approval
JOIN Policy.PolicyEndorsement endorsement ON endorsement.TenantId=approval.TenantId AND endorsement.EndorsementId=approval.EndorsementId AND endorsement.IsDeleted=0
LEFT JOIN IAM.[User] requestedBy ON requestedBy.TenantId=approval.TenantId AND requestedBy.UserId=approval.RequestedByUserId AND requestedBy.IsDeleted=0
LEFT JOIN IAM.[User] assignedTo ON assignedTo.TenantId=approval.TenantId AND assignedTo.UserId=approval.AssignedToUserId AND assignedTo.IsDeleted=0
WHERE approval.TenantId=@TenantId AND approval.AssignedToUserId=@AssignedToUserId AND approval.StatusCode=N'Pending' AND approval.IsDeleted=0
ORDER BY endorsement.IsUrgent DESC,endorsement.DueDate,approval.RequestedDateUtc;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<PolicyEndorsementApprovalInboxItemDto>(new CommandDefinition(sql, new { TenantId = tenantId, AssignedToUserId = assignedToUserId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task AssignApprovalAsync(Guid endorsementId, Guid approvalId, AssignPolicyEndorsementApprovalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
IF EXISTS(SELECT 1 FROM Policy.PolicyEndorsementEvent WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND CorrelationId=@CorrelationId AND EventTypeCode=N'ApprovalAssigned') BEGIN COMMIT; RETURN; END;
DECLARE @PolicyId UNIQUEIDENTIFIER,@ActorName NVARCHAR(200),@AssignedName NVARCHAR(200);
SELECT @PolicyId=PolicyId FROM Policy.PolicyEndorsement WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND IsDeleted=0;
SELECT @ActorName=FullName FROM IAM.[User] WHERE TenantId=@TenantId AND UserId=@ActorUserId AND IsActive=1 AND IsDeleted=0;
SELECT @AssignedName=appUser.FullName FROM IAM.[User] appUser
WHERE appUser.TenantId=@TenantId AND appUser.UserId=@AssignedToUserId AND appUser.IsActive=1 AND appUser.IsDeleted=0
AND EXISTS(SELECT 1 FROM IAM.UserRole userRole JOIN IAM.RolePermission rolePermission ON rolePermission.TenantId=@TenantId AND rolePermission.RoleId=userRole.RoleId AND rolePermission.IsDeleted=0 LEFT JOIN IAM.Permission permission ON permission.TenantId=@TenantId AND permission.PermissionId=rolePermission.PermissionId AND permission.IsDeleted=0 WHERE userRole.TenantId=@TenantId AND userRole.UserId=appUser.UserId AND userRole.IsActive=1 AND userRole.IsDeleted=0 AND COALESCE(rolePermission.PermissionCode,permission.PermissionCode)=N'ENDORSEMENT_APPROVE');
IF @PolicyId IS NULL OR @ActorName IS NULL THROW 52440,N'Endorsement or authenticated user was not found in the tenant.',1;
IF @AssignedName IS NULL THROW 52441,N'The selected assignee is not an active endorsement approver in the tenant.',1;
UPDATE Policy.PolicyEndorsementApproval SET AssignedToUserId=@AssignedToUserId WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND ApprovalId=@ApprovalId AND StatusCode=N'Pending' AND RowVersion=@ApprovalRowVersion AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52442,N'The pending approval changed or was not found.',1;
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId) VALUES(NEWID(),@TenantId,@EndorsementId,@PolicyId,N'ApprovalAssigned',CONCAT(N'Approval assigned to ',@AssignedName,N'.'),JSON_OBJECT(N'approvalId':@ApprovalId,N'assignedToUserId':@AssignedToUserId),@CorrelationId,SYSUTCDATETIME(),@ActorUserId);
INSERT Core.Notification(NotificationId,TenantId,RecipientUserId,ChannelCode,Subject,Body,EntityName,EntityId,StatusCode,IsRead,Priority,Category,DeliveryProvider,DeliveryStatus,PolicyStatus,SyncStatus,AttemptCount,MaxAttempts,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@AssignedToUserId,N'InApp',N'Endorsement approval assigned',N'An endorsement approval has been assigned to you.',N'PolicyEndorsementApproval',@ApprovalId,N'Queued',0,N'Normal',N'Policy Endorsement Approval',N'PLATFORM_IN_APP',N'Queued',N'Compliant',N'Synced',0,5,SYSUTCDATETIME(),@ActorUserId,0);
COMMIT;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { EndorsementId = endorsementId, ApprovalId = approvalId, request.TenantId, request.AssignedToUserId, request.ApprovalRowVersion, request.CorrelationId, request.ActorUserId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> RequestInformationAsync(Guid endorsementId, RequestPolicyEndorsementInformationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRAN;
DECLARE @ExistingId UNIQUEIDENTIFIER=(SELECT TOP(1) TRY_CONVERT(UNIQUEIDENTIFIER,JSON_VALUE(DataJson,N'$.informationRequestId')) FROM Policy.PolicyEndorsementEvent WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND CorrelationId=@CorrelationId AND EventTypeCode=N'InformationRequested');
IF @ExistingId IS NOT NULL BEGIN SELECT @ExistingId; COMMIT; RETURN; END;
DECLARE @PolicyId UNIQUEIDENTIFIER,@AssignedToUserId UNIQUEIDENTIFIER,@RequestNumber INT,@InformationRequestId UNIQUEIDENTIFIER=NEWID(),@ActorName NVARCHAR(200),@EndorsementNumber NVARCHAR(80),@PolicyNumber NVARCHAR(80),@Subject NVARCHAR(300),@Body NVARCHAR(1000),@FromStatus NVARCHAR(80),@AssignmentStrategyCode NVARCHAR(40),@AssignedRoleCode NVARCHAR(100),@RequiredPermissionCode NVARCHAR(120),@ConfiguredUserId UNIQUEIDENTIFIER;
SELECT @PolicyId=PolicyId,@EndorsementNumber=EndorsementNumber,@PolicyNumber=PolicyNumber,@FromStatus=Status FROM Policy.PolicyEndorsement WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND RowVersion=@EndorsementRowVersion AND Status IN(N'InReview',N'PendingApproval') AND IsDeleted=0;
SELECT @ActorName=FullName FROM IAM.[User] WHERE TenantId=@TenantId AND UserId=@ActorUserId AND IsActive=1 AND IsDeleted=0;
SELECT TOP(1) @AssignedToUserId=CASE WHEN route.AssignmentStrategyCode=N'Requestor' THEN COALESCE(approval.RequestedByUserId,endorsement.CreatedByUserId) END,@ConfiguredUserId=route.AssignedToUserId,@AssignmentStrategyCode=route.AssignmentStrategyCode,@AssignedRoleCode=route.AssignedRoleCode,@RequiredPermissionCode=route.RequiredPermissionCode,@Subject=route.NotificationSubjectTemplate,@Body=route.NotificationBodyTemplate
FROM Policy.PolicyEndorsement endorsement
JOIN Policy.EndorsementType type ON type.TenantId=endorsement.TenantId AND (type.TypeCode=endorsement.EndorsementType OR type.TypeName=endorsement.EndorsementType OR EXISTS(SELECT 1 FROM Policy.EndorsementTypeAlias alias WHERE alias.TenantId=type.TenantId AND alias.EndorsementTypeId=type.EndorsementTypeId AND alias.LegacyTypeValue=endorsement.EndorsementType AND alias.IsActive=1 AND alias.IsDeleted=0)) AND type.IsActive=1 AND type.IsDeleted=0
JOIN Policy.EndorsementWorkflowRoute route ON route.TenantId=type.TenantId AND route.EndorsementTypeId=type.EndorsementTypeId AND route.RoutePurposeCode=N'InformationRequest' AND route.IsActive=1 AND route.IsDeleted=0
LEFT JOIN Policy.PolicyEndorsementApproval approval ON approval.TenantId=endorsement.TenantId AND approval.EndorsementId=endorsement.EndorsementId AND approval.StatusCode=N'Pending' AND approval.IsDeleted=0
WHERE endorsement.TenantId=@TenantId AND endorsement.EndorsementId=@EndorsementId ORDER BY route.SortOrder;
IF @PolicyId IS NULL OR @ActorName IS NULL THROW 52443,N'Endorsement is not eligible for an information request or the actor is invalid.',1;
IF @AssignmentStrategyCode IS NULL THROW 52444,N'No active information-request route is configured for this endorsement type.',1;
IF @FromStatus=N'PendingApproval' AND NOT EXISTS(SELECT 1 FROM Policy.PolicyEndorsementApproval WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND AssignedToUserId=@ActorUserId AND StatusCode=N'Pending' AND IsDeleted=0) THROW 52450,N'Only the assigned approver can request information for a pending approval.',1;
IF @DueDateUtc IS NOT NULL AND @DueDateUtc<=SYSUTCDATETIME() THROW 52451,N'The information request due date must be in the future.',1;
IF @AssignmentStrategyCode=N'ExplicitUser' SET @AssignedToUserId=@ConfiguredUserId;
IF @AssignmentStrategyCode IN(N'Role',N'Permission')
    SELECT TOP(1) @AssignedToUserId=userRole.UserId FROM IAM.UserRole userRole JOIN IAM.Role role ON role.TenantId=@TenantId AND role.RoleId=userRole.RoleId AND role.IsActive=1 AND role.IsDeleted=0 JOIN IAM.[User] appUser ON appUser.TenantId=@TenantId AND appUser.UserId=userRole.UserId AND appUser.IsActive=1 AND appUser.IsDeleted=0 JOIN IAM.RolePermission rolePermission ON rolePermission.TenantId=@TenantId AND rolePermission.RoleId=role.RoleId AND rolePermission.IsDeleted=0 LEFT JOIN IAM.Permission permission ON permission.TenantId=@TenantId AND permission.PermissionId=rolePermission.PermissionId AND permission.IsActive=1 AND permission.IsDeleted=0 WHERE userRole.TenantId=@TenantId AND userRole.IsActive=1 AND userRole.IsDeleted=0 AND (userRole.EffectiveStartDateUtc IS NULL OR userRole.EffectiveStartDateUtc<=SYSUTCDATETIME()) AND (userRole.EffectiveEndDateUtc IS NULL OR userRole.EffectiveEndDateUtc>SYSUTCDATETIME()) AND COALESCE(rolePermission.PermissionCode,permission.PermissionCode)=@RequiredPermissionCode AND (@AssignmentStrategyCode=N'Permission' OR role.RoleCode=@AssignedRoleCode) ORDER BY userRole.AssignedDateUtc,appUser.UserId;
IF @AssignedToUserId IS NULL OR NOT EXISTS(SELECT 1 FROM IAM.[User] appUser WHERE appUser.TenantId=@TenantId AND appUser.UserId=@AssignedToUserId AND appUser.IsActive=1 AND appUser.IsDeleted=0 AND EXISTS(SELECT 1 FROM IAM.UserRole userRole JOIN IAM.RolePermission rolePermission ON rolePermission.TenantId=@TenantId AND rolePermission.RoleId=userRole.RoleId AND rolePermission.IsDeleted=0 LEFT JOIN IAM.Permission permission ON permission.TenantId=@TenantId AND permission.PermissionId=rolePermission.PermissionId AND permission.IsActive=1 AND permission.IsDeleted=0 WHERE userRole.TenantId=@TenantId AND userRole.UserId=appUser.UserId AND userRole.IsActive=1 AND userRole.IsDeleted=0 AND COALESCE(rolePermission.PermissionCode,permission.PermissionCode)=@RequiredPermissionCode)) THROW 52452,N'No active tenant user satisfies the configured information-request route.',1;
IF EXISTS(SELECT 1 FROM Policy.PolicyEndorsementInformationRequest WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND StatusCode=N'Open' AND IsDeleted=0) THROW 52445,N'An open information request already exists for this endorsement.',1;
SELECT @RequestNumber=COALESCE(MAX(RequestNumber),0)+1 FROM Policy.PolicyEndorsementInformationRequest WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId;
INSERT Policy.PolicyEndorsementInformationRequest(InformationRequestId,TenantId,EndorsementId,RequestNumber,StatusCode,RequestDetails,RequestedDateUtc,RequestedByUserId,AssignedToUserId,DueDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@InformationRequestId,@TenantId,@EndorsementId,@RequestNumber,N'Open',@RequestDetails,SYSUTCDATETIME(),@ActorUserId,@AssignedToUserId,@DueDateUtc,SYSUTCDATETIME(),@ActorUserId,0);
UPDATE Policy.PolicyEndorsement SET Status=N'NeedMoreInfo',WorkflowStage=N'NeedMoreInfo',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND RowVersion=@EndorsementRowVersion;
UPDATE Policy.PolicyEndorsementApproval SET StatusCode=N'InformationRequested',DecisionNotes=@RequestDetails WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND StatusCode=N'Pending' AND IsDeleted=0;
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,FromStatusCode,ToStatusCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId) VALUES(NEWID(),@TenantId,@EndorsementId,@PolicyId,N'InformationRequested',@FromStatus,N'NeedMoreInfo',N'Additional information was requested.',JSON_OBJECT(N'informationRequestId':@InformationRequestId,N'requestNumber':@RequestNumber,N'details':@RequestDetails,N'dueDateUtc':@DueDateUtc),@CorrelationId,SYSUTCDATETIME(),@ActorUserId);
INSERT Core.Notification(NotificationId,TenantId,RecipientUserId,ChannelCode,Subject,Body,EntityName,EntityId,StatusCode,IsRead,Priority,Category,DeliveryProvider,DeliveryStatus,PolicyStatus,SyncStatus,AttemptCount,MaxAttempts,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@AssignedToUserId,N'InApp',REPLACE(REPLACE(@Subject,N'{EndorsementNumber}',@EndorsementNumber),N'{PolicyNumber}',@PolicyNumber),CONCAT(REPLACE(REPLACE(@Body,N'{EndorsementNumber}',@EndorsementNumber),N'{PolicyNumber}',@PolicyNumber),N' ',@RequestDetails),N'PolicyEndorsementInformationRequest',@InformationRequestId,N'Queued',0,N'High',N'Policy Endorsement Information Request',N'PLATFORM_IN_APP',N'Queued',N'Compliant',N'Synced',0,5,SYSUTCDATETIME(),@ActorUserId,0);
SELECT @InformationRequestId; COMMIT;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { EndorsementId = endorsementId, request.TenantId, request.RequestDetails, request.DueDateUtc, request.EndorsementRowVersion, request.CorrelationId, request.ActorUserId }, cancellationToken: cancellationToken));
    }

    public async Task RespondToInformationRequestAsync(Guid endorsementId, Guid informationRequestId, RespondPolicyEndorsementInformationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
IF EXISTS(SELECT 1 FROM Policy.PolicyEndorsementEvent WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND CorrelationId=@CorrelationId AND EventTypeCode=N'InformationResponded') BEGIN COMMIT; RETURN; END;
DECLARE @PolicyId UNIQUEIDENTIFIER,@RequestedByUserId UNIQUEIDENTIFIER;
SELECT @PolicyId=PolicyId FROM Policy.PolicyEndorsement WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND Status=N'NeedMoreInfo' AND RowVersion=@EndorsementRowVersion AND IsDeleted=0;
IF @PolicyId IS NULL THROW 52446,N'The endorsement is not awaiting information.',1;
UPDATE Policy.PolicyEndorsementInformationRequest SET StatusCode=N'Responded',ResponseDetails=@ResponseDetails,RespondedDateUtc=SYSUTCDATETIME(),RespondedByUserId=@ActorUserId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND InformationRequestId=@InformationRequestId AND AssignedToUserId=@ActorUserId AND StatusCode=N'Open' AND RowVersion=@InformationRequestRowVersion AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52447,N'The open information request changed, was not found, or is assigned to another user.',1;
SELECT @RequestedByUserId=RequestedByUserId FROM Policy.PolicyEndorsementInformationRequest WHERE TenantId=@TenantId AND InformationRequestId=@InformationRequestId;
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId) VALUES(NEWID(),@TenantId,@EndorsementId,@PolicyId,N'InformationResponded',N'Additional information was provided.',JSON_OBJECT(N'informationRequestId':@InformationRequestId,N'response':@ResponseDetails),@CorrelationId,SYSUTCDATETIME(),@ActorUserId);
IF EXISTS(SELECT 1 FROM IAM.[User] WHERE TenantId=@TenantId AND UserId=@RequestedByUserId AND IsActive=1 AND IsDeleted=0)
INSERT Core.Notification(NotificationId,TenantId,RecipientUserId,ChannelCode,Subject,Body,EntityName,EntityId,StatusCode,IsRead,Priority,Category,DeliveryProvider,DeliveryStatus,PolicyStatus,SyncStatus,AttemptCount,MaxAttempts,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@RequestedByUserId,N'InApp',N'Endorsement information received',N'The requested endorsement information has been provided and is ready for resubmission review.',N'PolicyEndorsementInformationRequest',@InformationRequestId,N'Queued',0,N'Normal',N'Policy Endorsement Information Request',N'PLATFORM_IN_APP',N'Queued',N'Compliant',N'Synced',0,5,SYSUTCDATETIME(),@ActorUserId,0);
COMMIT;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { EndorsementId = endorsementId, InformationRequestId = informationRequestId, request.TenantId, request.ResponseDetails, request.EndorsementRowVersion, request.InformationRequestRowVersion, request.CorrelationId, request.ActorUserId }, cancellationToken: cancellationToken));
    }

    public async Task ResubmitInformationRequestAsync(Guid endorsementId, Guid informationRequestId, ResubmitPolicyEndorsementInformationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
IF EXISTS(SELECT 1 FROM Policy.PolicyEndorsementEvent WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND CorrelationId=@CorrelationId AND EventTypeCode=N'InformationResubmitted') BEGIN COMMIT; RETURN; END;
DECLARE @PolicyId UNIQUEIDENTIFIER,@ReviewerUserId UNIQUEIDENTIFIER;
SELECT @PolicyId=PolicyId FROM Policy.PolicyEndorsement WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND Status=N'NeedMoreInfo' AND RowVersion=@EndorsementRowVersion AND IsDeleted=0;
IF @PolicyId IS NULL THROW 52448,N'The endorsement changed or is not awaiting information.',1;
UPDATE Policy.PolicyEndorsementInformationRequest SET StatusCode=N'Resubmitted',ResubmittedDateUtc=SYSUTCDATETIME(),ResubmittedByUserId=@ActorUserId,ClosedDateUtc=SYSUTCDATETIME(),ClosedByUserId=@ActorUserId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND InformationRequestId=@InformationRequestId AND AssignedToUserId=@ActorUserId AND StatusCode=N'Responded' AND RowVersion=@InformationRequestRowVersion AND IsDeleted=0;
IF @@ROWCOUNT<>1 THROW 52449,N'The responded information request changed, was not found, or is assigned to another user.',1;
SELECT @ReviewerUserId=RequestedByUserId FROM Policy.PolicyEndorsementInformationRequest WHERE TenantId=@TenantId AND InformationRequestId=@InformationRequestId;
UPDATE Policy.PolicyEndorsement SET Status=N'InReview',WorkflowStage=N'InReview',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND RowVersion=@EndorsementRowVersion;
INSERT Policy.PolicyEndorsementEvent(EventId,TenantId,EndorsementId,PolicyId,EventTypeCode,FromStatusCode,ToStatusCode,Description,DataJson,CorrelationId,OccurredDateUtc,ActorUserId) VALUES(NEWID(),@TenantId,@EndorsementId,@PolicyId,N'InformationResubmitted',N'NeedMoreInfo',N'InReview',N'Endorsement information was resubmitted for review.',JSON_OBJECT(N'informationRequestId':@InformationRequestId,N'notes':@Notes),@CorrelationId,SYSUTCDATETIME(),@ActorUserId);
IF EXISTS(SELECT 1 FROM IAM.[User] WHERE TenantId=@TenantId AND UserId=@ReviewerUserId AND IsActive=1 AND IsDeleted=0)
INSERT Core.Notification(NotificationId,TenantId,RecipientUserId,ChannelCode,Subject,Body,EntityName,EntityId,StatusCode,IsRead,Priority,Category,DeliveryProvider,DeliveryStatus,PolicyStatus,SyncStatus,AttemptCount,MaxAttempts,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@ReviewerUserId,N'InApp',N'Endorsement resubmitted for review',N'The requested information has been supplied and the endorsement is ready for review.',N'PolicyEndorsement',@EndorsementId,N'Queued',0,N'Normal',N'Policy Endorsement Review',N'PLATFORM_IN_APP',N'Queued',N'Compliant',N'Synced',0,5,SYSUTCDATETIME(),@ActorUserId,0);
COMMIT;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { EndorsementId = endorsementId, InformationRequestId = informationRequestId, request.TenantId, request.Notes, request.EndorsementRowVersion, request.InformationRequestRowVersion, request.CorrelationId, request.ActorUserId }, cancellationToken: cancellationToken));
    }
}
