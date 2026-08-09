SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Core.Notification',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Core.Notification',N'Priority') IS NULL ALTER TABLE Core.Notification ADD Priority NVARCHAR(40) NOT NULL CONSTRAINT DF_CoreNotification_Priority_0301 DEFAULT N'Normal';
	IF COL_LENGTH(N'Core.Notification',N'Category') IS NULL ALTER TABLE Core.Notification ADD Category NVARCHAR(80) NOT NULL CONSTRAINT DF_CoreNotification_Category_0301 DEFAULT N'General';
	IF COL_LENGTH(N'Core.Notification',N'DeliveryProvider') IS NULL ALTER TABLE Core.Notification ADD DeliveryProvider NVARCHAR(120) NOT NULL CONSTRAINT DF_CoreNotification_Provider_0301 DEFAULT N'AMS';
	IF COL_LENGTH(N'Core.Notification',N'DeliveryStatus') IS NULL ALTER TABLE Core.Notification ADD DeliveryStatus NVARCHAR(60) NOT NULL CONSTRAINT DF_CoreNotification_Delivery_0301 DEFAULT N'Queued';
	IF COL_LENGTH(N'Core.Notification',N'PolicyStatus') IS NULL ALTER TABLE Core.Notification ADD PolicyStatus NVARCHAR(60) NOT NULL CONSTRAINT DF_CoreNotification_Policy_0301 DEFAULT N'Compliant';
	IF COL_LENGTH(N'Core.Notification',N'SyncStatus') IS NULL ALTER TABLE Core.Notification ADD SyncStatus NVARCHAR(60) NOT NULL CONSTRAINT DF_CoreNotification_Sync_0301 DEFAULT N'Synced';
	IF COL_LENGTH(N'Core.Notification',N'AttemptCount') IS NULL ALTER TABLE Core.Notification ADD AttemptCount INT NOT NULL CONSTRAINT DF_CoreNotification_Attempts_0301 DEFAULT 0;
	IF COL_LENGTH(N'Core.Notification',N'MaxAttempts') IS NULL ALTER TABLE Core.Notification ADD MaxAttempts INT NOT NULL CONSTRAINT DF_CoreNotification_MaxAttempts_0301 DEFAULT 5;
	IF COL_LENGTH(N'Core.Notification',N'CreatedByUserId') IS NULL ALTER TABLE Core.Notification ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Core.Notification',N'ModifiedDateUtc') IS NULL ALTER TABLE Core.Notification ADD ModifiedDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'Core.Notification',N'ModifiedByUserId') IS NULL ALTER TABLE Core.Notification ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
END;

IF OBJECT_ID(N'Policy.EndorsementWorkflowRoute',N'U') IS NULL
BEGIN
	CREATE TABLE Policy.EndorsementWorkflowRoute
	(
		EndorsementWorkflowRouteId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EndorsementWorkflowRoute PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementTypeId UNIQUEIDENTIFIER NOT NULL,
		RoutePurposeCode NVARCHAR(40) NOT NULL,
		ApprovalLevelCode NVARCHAR(80) NULL,
		AssignmentStrategyCode NVARCHAR(40) NOT NULL,
		AssignedToUserId UNIQUEIDENTIFIER NULL,
		AssignedRoleCode NVARCHAR(100) NULL,
		RequiredPermissionCode NVARCHAR(120) NOT NULL,
		ApprovedStatusCode NVARCHAR(80) NULL,
		RejectedStatusCode NVARCHAR(80) NULL,
		NotificationCategory NVARCHAR(100) NOT NULL,
		NotificationSubjectTemplate NVARCHAR(300) NOT NULL,
		NotificationBodyTemplate NVARCHAR(1000) NOT NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_EndorsementWorkflowRoute_Active DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_EndorsementWorkflowRoute_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementWorkflowRoute_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementWorkflowRoute_Deleted DEFAULT 0,
		CONSTRAINT CK_EndorsementWorkflowRoute_Purpose CHECK(RoutePurposeCode IN(N'Approval',N'InformationRequest')),
		CONSTRAINT CK_EndorsementWorkflowRoute_Strategy CHECK(AssignmentStrategyCode IN(N'ExplicitUser',N'Role',N'Permission',N'Requestor')),
		CONSTRAINT CK_EndorsementWorkflowRoute_Target CHECK((AssignmentStrategyCode=N'ExplicitUser' AND AssignedToUserId IS NOT NULL AND AssignedRoleCode IS NULL) OR (AssignmentStrategyCode=N'Role' AND AssignedRoleCode IS NOT NULL AND AssignedToUserId IS NULL) OR (AssignmentStrategyCode IN(N'Permission',N'Requestor') AND AssignedToUserId IS NULL AND AssignedRoleCode IS NULL)),
		CONSTRAINT FK_EndorsementWorkflowRoute_Type FOREIGN KEY(TenantId,EndorsementTypeId) REFERENCES Policy.EndorsementType(TenantId,EndorsementTypeId)
	);
	CREATE UNIQUE INDEX UX_EndorsementWorkflowRoute_Default ON Policy.EndorsementWorkflowRoute(TenantId,EndorsementTypeId,RoutePurposeCode) WHERE ApprovalLevelCode IS NULL AND IsDeleted=0;
	CREATE UNIQUE INDEX UX_EndorsementWorkflowRoute_Level ON Policy.EndorsementWorkflowRoute(TenantId,EndorsementTypeId,RoutePurposeCode,ApprovalLevelCode) WHERE ApprovalLevelCode IS NOT NULL AND IsDeleted=0;
	CREATE INDEX IX_EndorsementWorkflowRoute_Resolve ON Policy.EndorsementWorkflowRoute(TenantId,EndorsementTypeId,RoutePurposeCode,IsActive,IsDeleted,SortOrder);
END;

IF OBJECT_ID(N'Policy.PolicyEndorsementInformationRequest',N'U') IS NULL
BEGIN
	CREATE TABLE Policy.PolicyEndorsementInformationRequest
	(
		InformationRequestId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PolicyEndorsementInformationRequest PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EndorsementId UNIQUEIDENTIFIER NOT NULL,
		RequestNumber INT NOT NULL,
		StatusCode NVARCHAR(40) NOT NULL CONSTRAINT DF_EndorsementInformationRequest_Status DEFAULT N'Open',
		RequestDetails NVARCHAR(2000) NOT NULL,
		RequestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementInformationRequest_Requested DEFAULT SYSUTCDATETIME(),
		RequestedByUserId UNIQUEIDENTIFIER NOT NULL,
		AssignedToUserId UNIQUEIDENTIFIER NOT NULL,
		DueDateUtc DATETIME2 NULL,
		ResponseDetails NVARCHAR(2000) NULL,
		RespondedDateUtc DATETIME2 NULL,
		RespondedByUserId UNIQUEIDENTIFIER NULL,
		ResubmittedDateUtc DATETIME2 NULL,
		ResubmittedByUserId UNIQUEIDENTIFIER NULL,
		ClosedDateUtc DATETIME2 NULL,
		ClosedByUserId UNIQUEIDENTIFIER NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_EndorsementInformationRequest_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EndorsementInformationRequest_Deleted DEFAULT 0,
		RowVersion ROWVERSION,
		CONSTRAINT CK_EndorsementInformationRequest_Status CHECK(StatusCode IN(N'Open',N'Responded',N'Resubmitted',N'Closed',N'Cancelled')),
		CONSTRAINT CK_EndorsementInformationRequest_Number CHECK(RequestNumber>0),
		CONSTRAINT CK_EndorsementInformationRequest_DueDate CHECK(DueDateUtc IS NULL OR DueDateUtc>RequestedDateUtc),
		CONSTRAINT CK_EndorsementInformationRequest_Response CHECK((StatusCode=N'Open' AND ResponseDetails IS NULL AND RespondedDateUtc IS NULL AND RespondedByUserId IS NULL) OR StatusCode<>N'Open'),
		CONSTRAINT FK_EndorsementInformationRequest_Endorsement FOREIGN KEY(TenantId,EndorsementId) REFERENCES Policy.PolicyEndorsement(TenantId,EndorsementId)
	);
	CREATE UNIQUE INDEX UX_EndorsementInformationRequest_Number ON Policy.PolicyEndorsementInformationRequest(TenantId,EndorsementId,RequestNumber) WHERE IsDeleted=0;
	CREATE UNIQUE INDEX UX_EndorsementInformationRequest_Open ON Policy.PolicyEndorsementInformationRequest(TenantId,EndorsementId) WHERE StatusCode=N'Open' AND IsDeleted=0;
	CREATE INDEX IX_EndorsementInformationRequest_Assignee ON Policy.PolicyEndorsementInformationRequest(TenantId,AssignedToUserId,StatusCode,RequestedDateUtc DESC) WHERE IsDeleted=0;
END;

INSERT Policy.EndorsementWorkflowRoute
(EndorsementWorkflowRouteId,TenantId,EndorsementTypeId,RoutePurposeCode,ApprovalLevelCode,AssignmentStrategyCode,RequiredPermissionCode,ApprovedStatusCode,RejectedStatusCode,NotificationCategory,NotificationSubjectTemplate,NotificationBodyTemplate,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),type.TenantId,type.EndorsementTypeId,N'Approval',NULL,N'Permission',N'ENDORSEMENT_APPROVE',N'Approved',N'Rejected',N'Policy Endorsement Approval',N'Endorsement approval required',N'Endorsement {EndorsementNumber} for policy {PolicyNumber} is awaiting your approval.',1,10,SYSUTCDATETIME(),0
FROM Policy.EndorsementType type
WHERE type.IsActive=1 AND type.IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM Policy.EndorsementWorkflowRoute route WHERE route.TenantId=type.TenantId AND route.EndorsementTypeId=type.EndorsementTypeId AND route.RoutePurposeCode=N'Approval' AND route.IsDeleted=0);

INSERT Policy.EndorsementWorkflowRoute
(EndorsementWorkflowRouteId,TenantId,EndorsementTypeId,RoutePurposeCode,ApprovalLevelCode,AssignmentStrategyCode,RequiredPermissionCode,ApprovedStatusCode,RejectedStatusCode,NotificationCategory,NotificationSubjectTemplate,NotificationBodyTemplate,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),type.TenantId,type.EndorsementTypeId,N'InformationRequest',NULL,N'Requestor',N'ENDORSEMENT_EDIT_DRAFT',N'InReview',NULL,N'Policy Endorsement Information Request',N'Information required for endorsement',N'Additional information is required for endorsement {EndorsementNumber} on policy {PolicyNumber}.',1,20,SYSUTCDATETIME(),0
FROM Policy.EndorsementType type
WHERE type.IsActive=1 AND type.IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM Policy.EndorsementWorkflowRoute route WHERE route.TenantId=type.TenantId AND route.EndorsementTypeId=type.EndorsementTypeId AND route.RoutePurposeCode=N'InformationRequest' AND route.IsDeleted=0);
