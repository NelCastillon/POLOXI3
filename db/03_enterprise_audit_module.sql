-- ============================================================
-- ENTERPRISE AUDIT MODULE - AgencyBinder
-- Canonical audit timeline, details, category tables, retention,
-- legal hold, alert rules, and seed data.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Audit') EXEC(N'CREATE SCHEMA Audit');

IF OBJECT_ID(N'Audit.AuditEvent', N'U') IS NULL
CREATE TABLE Audit.AuditEvent (
	AuditEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditEvent PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NOT NULL,
	ActorUserId UNIQUEIDENTIFIER NULL,
	ActorUserName NVARCHAR(300) NULL,
	ActorRole NVARCHAR(200) NULL,
	ActorType NVARCHAR(100) NOT NULL DEFAULT N'User',
	ActionType NVARCHAR(100) NOT NULL,
	ActionCategory NVARCHAR(100) NOT NULL,
	ModuleName NVARCHAR(100) NOT NULL,
	EntityName NVARCHAR(256) NULL,
	EntityId UNIQUEIDENTIFIER NULL,
	EntityDisplayName NVARCHAR(300) NULL,
	ParentEntityName NVARCHAR(256) NULL,
	ParentEntityId UNIQUEIDENTIFIER NULL,
	OldValue NVARCHAR(MAX) NULL,
	NewValue NVARCHAR(MAX) NULL,
	IpAddress NVARCHAR(64) NULL,
	UserAgent NVARCHAR(500) NULL,
	CorrelationId NVARCHAR(120) NULL,
	RequestId NVARCHAR(120) NULL,
	SourceSystem NVARCHAR(100) NOT NULL DEFAULT N'Web',
	Severity NVARCHAR(50) NOT NULL DEFAULT N'Info',
	StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Success',
	IsSensitiveData BIT NOT NULL DEFAULT 0,
	IsPiiMasked BIT NOT NULL DEFAULT 0,
	IsLegalHold BIT NOT NULL DEFAULT 0,
	ChangeReason NVARCHAR(500) NULL,
	VersionNumber INT NULL,
	MetadataJson NVARCHAR(MAX) NULL,
	CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

IF OBJECT_ID(N'Audit.AuditEventDetail', N'U') IS NULL
CREATE TABLE Audit.AuditEventDetail (
	AuditEventDetailId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditEventDetail PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NOT NULL,
	AuditEventId UNIQUEIDENTIFIER NOT NULL,
	DetailName NVARCHAR(200) NOT NULL,
	OldValue NVARCHAR(MAX) NULL,
	NewValue NVARCHAR(MAX) NULL,
	DataTypeCode NVARCHAR(50) NULL,
	IsSensitive BIT NOT NULL DEFAULT 0,
	IsMasked BIT NOT NULL DEFAULT 0,
	CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

IF OBJECT_ID(N'Audit.AuditLoginEvent', N'U') IS NULL
CREATE TABLE Audit.AuditLoginEvent (AuditLoginEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditLoginEvent PRIMARY KEY DEFAULT NEWID(), TenantId UNIQUEIDENTIFIER NOT NULL, AuditEventId UNIQUEIDENTIFIER NULL, UserId UNIQUEIDENTIFIER NULL, UserName NVARCHAR(300) NULL, EventTypeCode NVARCHAR(100) NOT NULL, IsSuccess BIT NOT NULL, FailureReason NVARCHAR(500) NULL, IpAddress NVARCHAR(64) NULL, UserAgent NVARCHAR(500) NULL, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());

IF OBJECT_ID(N'Audit.AuditSecurityEvent', N'U') IS NULL
CREATE TABLE Audit.AuditSecurityEvent (AuditSecurityEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditSecurityEvent PRIMARY KEY DEFAULT NEWID(), TenantId UNIQUEIDENTIFIER NOT NULL, AuditEventId UNIQUEIDENTIFIER NULL, UserId UNIQUEIDENTIFIER NULL, EventTypeCode NVARCHAR(100) NOT NULL, Severity NVARCHAR(50) NOT NULL, RiskScore INT NOT NULL DEFAULT 0, Description NVARCHAR(1000) NULL, IpAddress NVARCHAR(64) NULL, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());

IF OBJECT_ID(N'Audit.AuditEntityChange', N'U') IS NULL
CREATE TABLE Audit.AuditEntityChange (AuditEntityChangeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditEntityChange PRIMARY KEY DEFAULT NEWID(), TenantId UNIQUEIDENTIFIER NOT NULL, AuditEventId UNIQUEIDENTIFIER NULL, EntityName NVARCHAR(256) NOT NULL, EntityId UNIQUEIDENTIFIER NOT NULL, ParentEntityName NVARCHAR(256) NULL, ParentEntityId UNIQUEIDENTIFIER NULL, FieldName NVARCHAR(256) NOT NULL, OldValue NVARCHAR(MAX) NULL, NewValue NVARCHAR(MAX) NULL, ChangeReason NVARCHAR(500) NULL, VersionNumber INT NULL, ChangedByUserId UNIQUEIDENTIFIER NULL, ChangedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());

IF OBJECT_ID(N'Audit.AuditDocumentEvent', N'U') IS NULL
CREATE TABLE Audit.AuditDocumentEvent (AuditDocumentEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditDocumentEvent PRIMARY KEY DEFAULT NEWID(), TenantId UNIQUEIDENTIFIER NOT NULL, AuditEventId UNIQUEIDENTIFIER NULL, DocumentId UNIQUEIDENTIFIER NULL, DocumentName NVARCHAR(300) NULL, EventTypeCode NVARCHAR(100) NOT NULL, VersionNumber INT NULL, PerformedByUserId UNIQUEIDENTIFIER NULL, IpAddress NVARCHAR(64) NULL, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());

IF OBJECT_ID(N'Audit.AuditExportEvent', N'U') IS NULL
CREATE TABLE Audit.AuditExportEvent (AuditExportEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditExportEvent PRIMARY KEY DEFAULT NEWID(), TenantId UNIQUEIDENTIFIER NOT NULL, AuditEventId UNIQUEIDENTIFIER NULL, EntityName NVARCHAR(256) NULL, ExportTypeCode NVARCHAR(100) NOT NULL, FormatCode NVARCHAR(50) NOT NULL, RecordCount INT NOT NULL DEFAULT 0, FileName NVARCHAR(300) NULL, PerformedByUserId UNIQUEIDENTIFIER NULL, IpAddress NVARCHAR(64) NULL, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());

IF OBJECT_ID(N'Audit.AuditImpersonationEvent', N'U') IS NULL
CREATE TABLE Audit.AuditImpersonationEvent (AuditImpersonationEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditImpersonationEvent PRIMARY KEY DEFAULT NEWID(), TenantId UNIQUEIDENTIFIER NOT NULL, AuditEventId UNIQUEIDENTIFIER NULL, AdminUserId UNIQUEIDENTIFIER NOT NULL, ImpersonatedUserId UNIQUEIDENTIFIER NOT NULL, Reason NVARCHAR(500) NULL, StartedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), EndedUtc DATETIME2 NULL);

IF OBJECT_ID(N'Audit.AuditRetentionPolicy', N'U') IS NULL
CREATE TABLE Audit.AuditRetentionPolicy (AuditRetentionPolicyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditRetentionPolicy PRIMARY KEY DEFAULT NEWID(), TenantId UNIQUEIDENTIFIER NOT NULL, PolicyName NVARCHAR(200) NOT NULL, RetentionYears INT NOT NULL, ActionOnExpiry NVARCHAR(50) NOT NULL, IsActive BIT NOT NULL DEFAULT 1, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL);

IF OBJECT_ID(N'Audit.AuditLegalHold', N'U') IS NULL
CREATE TABLE Audit.AuditLegalHold (AuditLegalHoldId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditLegalHold PRIMARY KEY DEFAULT NEWID(), TenantId UNIQUEIDENTIFIER NOT NULL, HoldName NVARCHAR(200) NOT NULL, EntityName NVARCHAR(256) NULL, EntityId UNIQUEIDENTIFIER NULL, Reason NVARCHAR(1000) NOT NULL, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Active', StartUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), EndUtc DATETIME2 NULL, CreatedByUserId UNIQUEIDENTIFIER NULL);

IF OBJECT_ID(N'Audit.AuditAlertRule', N'U') IS NULL
CREATE TABLE Audit.AuditAlertRule (AuditAlertRuleId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditAlertRule PRIMARY KEY DEFAULT NEWID(), TenantId UNIQUEIDENTIFIER NOT NULL, AlertCode NVARCHAR(100) NOT NULL, AlertName NVARCHAR(200) NOT NULL, Severity NVARCHAR(50) NOT NULL, ConditionJson NVARCHAR(MAX) NULL, IsActive BIT NOT NULL DEFAULT 1, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());

IF OBJECT_ID(N'Audit.AuditAlertEvent', N'U') IS NULL
CREATE TABLE Audit.AuditAlertEvent (AuditAlertEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditAlertEvent PRIMARY KEY DEFAULT NEWID(), TenantId UNIQUEIDENTIFIER NOT NULL, AuditEventId UNIQUEIDENTIFIER NULL, AlertCode NVARCHAR(100) NOT NULL, AlertName NVARCHAR(200) NOT NULL, Severity NVARCHAR(50) NOT NULL, StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Open', Description NVARCHAR(1000) NULL, AssignedToUserId UNIQUEIDENTIFIER NULL, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());

IF OBJECT_ID(N'Audit.AuditCapability', N'U') IS NULL
CREATE TABLE Audit.AuditCapability (AuditCapabilityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditCapability PRIMARY KEY DEFAULT NEWID(), TenantId UNIQUEIDENTIFIER NULL, CapabilityArea NVARCHAR(100) NOT NULL, FeatureName NVARCHAR(200) NOT NULL, Purpose NVARCHAR(500) NOT NULL, ModuleName NVARCHAR(100) NOT NULL, ActionType NVARCHAR(100) NOT NULL, IsImplemented BIT NOT NULL DEFAULT 1, IsSeeded BIT NOT NULL DEFAULT 1, RequiresInstrumentation BIT NOT NULL DEFAULT 1, DisplayOrder INT NOT NULL DEFAULT 0, CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());

IF OBJECT_ID(N'Audit.AuditSensitiveField', N'U') IS NULL
CREATE TABLE Audit.AuditSensitiveField (AuditSensitiveFieldId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AuditSensitiveField PRIMARY KEY DEFAULT NEWID(), TenantId UNIQUEIDENTIFIER NULL, EntityName NVARCHAR(256) NULL, FieldNamePattern NVARCHAR(256) NOT NULL, Description NVARCHAR(500) NULL, IsActive BIT NOT NULL DEFAULT 1, CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedByUserId UNIQUEIDENTIFIER NULL, ModifiedDateUtc DATETIME2 NULL, ModifiedByUserId UNIQUEIDENTIFIER NULL, IsDeleted BIT NOT NULL DEFAULT 0);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Audit.AuditEvent') AND name = N'IX_AuditEvent_Tenant_Created') CREATE INDEX IX_AuditEvent_Tenant_Created ON Audit.AuditEvent(TenantId, CreatedUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Audit.AuditEvent') AND name = N'IX_AuditEvent_Tenant_Category') CREATE INDEX IX_AuditEvent_Tenant_Category ON Audit.AuditEvent(TenantId, ActionCategory, ModuleName, CreatedUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Audit.AuditEvent') AND name = N'IX_AuditEvent_Tenant_Entity') CREATE INDEX IX_AuditEvent_Tenant_Entity ON Audit.AuditEvent(TenantId, EntityName, EntityId, CreatedUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Audit.AuditAlertEvent') AND name = N'IX_AuditAlertEvent_Tenant_Status') CREATE INDEX IX_AuditAlertEvent_Tenant_Status ON Audit.AuditAlertEvent(TenantId, StatusCode, Severity, CreatedUtc DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Audit.AuditCapability') AND name = N'IX_AuditCapability_Tenant_Area') CREATE INDEX IX_AuditCapability_Tenant_Area ON Audit.AuditCapability(TenantId, CapabilityArea, DisplayOrder);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Audit.AuditSensitiveField') AND name = N'IX_AuditSensitiveField_Tenant_Entity') CREATE INDEX IX_AuditSensitiveField_Tenant_Entity ON Audit.AuditSensitiveField(TenantId, EntityName, IsActive) INCLUDE (FieldNamePattern);

DECLARE @TenantId UNIQUEIDENTIFIER = COALESCE((SELECT TOP 1 TenantId FROM Core.Tenant ORDER BY CreatedDateUtc), '00000000-0000-0000-0000-000000000001');
DECLARE @AdminUserId UNIQUEIDENTIFIER = COALESCE((SELECT TOP 1 UserId FROM IAM.[User] WHERE TenantId = @TenantId ORDER BY CreatedDateUtc), '00000000-0000-0000-0000-000000000002');
DECLARE @AdminName NVARCHAR(300) = COALESCE((SELECT TOP 1 FullName FROM IAM.[User] WHERE UserId = @AdminUserId), N'Tenant Admin');

IF NOT EXISTS (SELECT 1 FROM Audit.AuditEvent WHERE TenantId = @TenantId)
BEGIN
	INSERT INTO Audit.AuditEvent
	(AuditEventId, TenantId, ActorUserId, ActorUserName, ActorRole, ActorType, ActionType, ActionCategory, ModuleName, EntityName, EntityId, EntityDisplayName, ParentEntityName, ParentEntityId, OldValue, NewValue, IpAddress, UserAgent, CorrelationId, RequestId, SourceSystem, Severity, StatusCode, IsSensitiveData, IsPiiMasked, IsLegalHold, ChangeReason, VersionNumber, MetadataJson, CreatedUtc)
	VALUES
	(NEWID(), @TenantId, @AdminUserId, @AdminName, N'Tenant Admin', N'User', N'LOGIN_SUCCESS', N'User Activity', N'IAM', N'User', @AdminUserId, @AdminName, NULL, NULL, NULL, NULL, N'192.168.1.100', N'Mozilla/5.0 Chrome', N'corr-login-001', N'req-login-001', N'Web', N'Info', N'Success', 0, 0, 0, NULL, 1, N'{"feature":"Login audit"}', DATEADD(day, -10, SYSUTCDATETIME())),
	(NEWID(), @TenantId, @AdminUserId, @AdminName, N'Tenant Admin', N'User', N'MFA_CHALLENGE_SUCCESS', N'User Activity', N'IAM', N'MfaDevice', NULL, N'Authenticator App', NULL, NULL, N'ChallengeRequired', N'ChallengePassed', N'192.168.1.100', N'Mozilla/5.0 Chrome', N'corr-mfa-001', N'req-mfa-001', N'Web', N'Info', N'Success', 0, 0, 0, NULL, 2, N'{"feature":"MFA audit"}', DATEADD(day, -9, SYSUTCDATETIME())),
	(NEWID(), @TenantId, @AdminUserId, @AdminName, N'Tenant Admin', N'User', N'SESSION_TOKEN_REFRESH', N'User Activity', N'IAM', N'UserSession', NULL, N'Admin session', NULL, NULL, NULL, N'Refreshed', N'192.168.1.100', N'Mozilla/5.0 Chrome', N'corr-session-001', N'req-session-001', N'Web', N'Info', N'Success', 0, 0, 0, NULL, 3, N'{"feature":"Session audit"}', DATEADD(day, -8, SYSUTCDATETIME())),
	(NEWID(), @TenantId, @AdminUserId, @AdminName, N'Tenant Admin', N'User', N'POLICY_STATUS_CHANGED', N'Data Change', N'Policy', N'Policy', NEWID(), N'POL-2024-44821', N'Account', NEWID(), N'Quoted', N'Bound', N'192.168.1.15', N'Mozilla/5.0 Edge', N'corr-policy-001', N'req-policy-001', N'Web', N'Medium', N'Success', 0, 0, 0, N'Carrier binder received.', 4, N'{"field":"Status","restoreSupported":true}', DATEADD(day, -7, SYSUTCDATETIME())),
	(NEWID(), @TenantId, @AdminUserId, @AdminName, N'Tenant Admin', N'User', N'ROLE_ASSIGNED', N'Security', N'IAM', N'UserRole', NEWID(), N'System Administrator', N'User', @AdminUserId, N'User', N'Admin', N'192.168.1.100', N'Mozilla/5.0 Chrome', N'corr-role-001', N'req-role-001', N'Web', N'High', N'Success', 0, 0, 0, N'Approved access request.', 5, N'{"feature":"Role assignment"}', DATEADD(day, -6, SYSUTCDATETIME())),
	(NEWID(), @TenantId, @AdminUserId, @AdminName, N'Tenant Admin', N'User', N'TENANT_BRANDING_UPDATED', N'Tenant', N'Tenant Configuration', N'TenantBranding', NEWID(), N'AgencyBinder Brand', N'Tenant', @TenantId, N'Old Theme', N'Enterprise Theme', N'192.168.1.100', N'Mozilla/5.0 Chrome', N'corr-tenant-001', N'req-tenant-001', N'Web', N'Info', N'Success', 0, 0, 0, N'Brand refresh.', 6, N'{"feature":"Tenant configuration changes"}', DATEADD(day, -5, SYSUTCDATETIME())),
	(NEWID(), @TenantId, @AdminUserId, @AdminName, N'Producer', N'User', N'SUBMISSION_SENT_TO_CARRIER', N'Business Workflow', N'Submissions', N'Submission', NEWID(), N'SUB-2026-1042', N'Opportunity', NEWID(), N'Draft', N'Submitted', N'192.168.1.25', N'Mozilla/5.0 Safari', N'corr-sub-001', N'req-sub-001', N'Web', N'Info', N'Success', 0, 0, 0, N'Market submission package complete.', 7, N'{"feature":"Submission workflow audit"}', DATEADD(day, -4, SYSUTCDATETIME())),
	(NEWID(), @TenantId, @AdminUserId, @AdminName, N'CSR', N'User', N'DOCUMENT_DOWNLOADED', N'Document', N'DMS', N'Document', NEWID(), N'Loss Runs.pdf', N'Account', NEWID(), NULL, N'Downloaded', N'192.168.1.33', N'Mozilla/5.0 Chrome', N'corr-doc-001', N'req-doc-001', N'Web', N'Medium', N'Success', 1, 1, 0, NULL, 8, N'{"feature":"Document download tracking","documentType":"Loss Run"}', DATEADD(day, -3, SYSUTCDATETIME())),
	(NEWID(), @TenantId, @AdminUserId, @AdminName, N'Tenant Admin', N'User', N'AUDIT_REPORT_EXPORTED', N'Compliance', N'Audit', N'AuditEvent', NULL, N'Quarterly SOC 2 Evidence Export', NULL, NULL, NULL, N'CSV:2500', N'192.168.1.100', N'Mozilla/5.0 Chrome', N'corr-export-001', N'req-export-001', N'Web', N'High', N'Success', 0, 0, 0, N'Compliance evidence request.', 9, N'{"feature":"Exportable audit reports","format":"CSV"}', DATEADD(day, -2, SYSUTCDATETIME())),
	(NEWID(), @TenantId, @AdminUserId, @AdminName, N'Platform Admin', N'Admin', N'ADMIN_IMPERSONATION_STARTED', N'Security', N'Platform', N'User', NEWID(), N'CSR User', N'Tenant', @TenantId, NULL, N'Impersonation started', N'10.0.0.8', N'Mozilla/5.0 Chrome', N'corr-imp-001', N'req-imp-001', N'Web', N'Critical', N'Success', 0, 0, 0, N'Support ticket AB-1044.', 10, N'{"feature":"Admin impersonation audit"}', DATEADD(day, -1, SYSUTCDATETIME())),
	(NEWID(), @TenantId, @AdminUserId, @AdminName, N'System', N'System', N'LEGAL_HOLD_APPLIED', N'Compliance', N'Audit', N'Policy', NEWID(), N'POL-2024-44821', N'Account', NEWID(), N'Not Held', N'Legal Hold', N'127.0.0.1', N'Worker', N'corr-hold-001', N'req-hold-001', N'Worker', N'High', N'Success', 0, 0, 1, N'Claims dispute.', 11, N'{"feature":"Legal hold"}', DATEADD(hour, -12, SYSUTCDATETIME())),
	(NEWID(), @TenantId, @AdminUserId, @AdminName, N'System', N'System', N'FAILED_LOGIN_SPIKE', N'Security', N'IAM', N'User', @AdminUserId, @AdminName, NULL, NULL, NULL, N'10 failures', N'203.0.113.22', N'Unknown', N'corr-alert-001', N'req-alert-001', N'API', N'Critical', N'Open', 0, 0, 0, N'Risk alert generated.', 12, N'{"feature":"Failed login spike","threshold":10}', DATEADD(hour, -2, SYSUTCDATETIME()));

	INSERT INTO Audit.AuditEventDetail (AuditEventDetailId, TenantId, AuditEventId, DetailName, OldValue, NewValue, DataTypeCode, IsSensitive, IsMasked, CreatedUtc)
	SELECT NEWID(), TenantId, AuditEventId, COALESCE(JSON_VALUE(MetadataJson, '$.field'), ActionType), OldValue, NewValue, N'String', IsSensitiveData, IsPiiMasked, CreatedUtc
	FROM Audit.AuditEvent
	WHERE TenantId = @TenantId AND (OldValue IS NOT NULL OR NewValue IS NOT NULL);

	INSERT INTO Audit.AuditEntityChange (AuditEntityChangeId, TenantId, AuditEventId, EntityName, EntityId, ParentEntityName, ParentEntityId, FieldName, OldValue, NewValue, ChangeReason, VersionNumber, ChangedByUserId, ChangedUtc)
	SELECT NEWID(), TenantId, AuditEventId, COALESCE(EntityName, N'Entity'), COALESCE(EntityId, NEWID()), ParentEntityName, ParentEntityId, COALESCE(JSON_VALUE(MetadataJson, '$.field'), N'Status'), OldValue, NewValue, ChangeReason, VersionNumber, ActorUserId, CreatedUtc
	FROM Audit.AuditEvent
	WHERE TenantId = @TenantId AND ActionCategory = N'Data Change';

	INSERT INTO Audit.AuditAlertEvent (AuditAlertEventId, TenantId, AuditEventId, AlertCode, AlertName, Severity, StatusCode, Description, AssignedToUserId, CreatedUtc)
	SELECT NEWID(), TenantId, AuditEventId, N'FAILED_LOGIN_SPIKE', N'Failed login spike', N'Critical', N'Open', N'User failed login threshold exceeded from a suspicious IP.', @AdminUserId, CreatedUtc FROM Audit.AuditEvent WHERE TenantId = @TenantId AND ActionType = N'FAILED_LOGIN_SPIKE';
END;

IF NOT EXISTS (SELECT 1 FROM Audit.AuditRetentionPolicy WHERE TenantId = @TenantId)
	INSERT INTO Audit.AuditRetentionPolicy (AuditRetentionPolicyId, TenantId, PolicyName, RetentionYears, ActionOnExpiry, IsActive, CreatedUtc, CreatedByUserId) VALUES (NEWID(), @TenantId, N'Enterprise audit retention - 7 years', 7, N'Archive', 1, SYSUTCDATETIME(), @AdminUserId);

IF NOT EXISTS (SELECT 1 FROM Audit.AuditLegalHold WHERE TenantId = @TenantId)
	INSERT INTO Audit.AuditLegalHold (AuditLegalHoldId, TenantId, HoldName, EntityName, Reason, StatusCode, StartUtc, CreatedByUserId) VALUES (NEWID(), @TenantId, N'Claims dispute preservation', N'Policy', N'Preserve policy and claim audit evidence during dispute.', N'Active', SYSUTCDATETIME(), @AdminUserId);

IF NOT EXISTS (SELECT 1 FROM Audit.AuditAlertRule WHERE TenantId = @TenantId)
BEGIN
	INSERT INTO Audit.AuditAlertRule (AuditAlertRuleId, TenantId, AlertCode, AlertName, Severity, ConditionJson, IsActive, CreatedUtc) VALUES
	(NEWID(), @TenantId, N'FAILED_LOGIN_SPIKE', N'Failed login spike', N'Critical', N'{"event":"LOGIN_FAILED","threshold":10,"windowMinutes":15}', 1, SYSUTCDATETIME()),
	(NEWID(), @TenantId, N'ROLE_ESCALATION', N'Role escalation', N'High', N'{"event":"ROLE_ASSIGNED","role":"Admin"}', 1, SYSUTCDATETIME()),
	(NEWID(), @TenantId, N'MASS_EXPORT', N'Mass export', N'High', N'{"event":"EXPORT","recordThreshold":10000}', 1, SYSUTCDATETIME()),
	(NEWID(), @TenantId, N'AFTER_HOURS_ACCESS', N'After-hours access', N'Medium', N'{"event":"LOGIN_SUCCESS","startHour":20,"endHour":6}', 1, SYSUTCDATETIME());
END;

DELETE FROM Audit.AuditCapability WHERE TenantId = @TenantId AND IsSeeded = 1;

INSERT INTO Audit.AuditCapability (AuditCapabilityId, TenantId, CapabilityArea, FeatureName, Purpose, ModuleName, ActionType, IsImplemented, IsSeeded, RequiresInstrumentation, DisplayOrder) VALUES
(NEWID(), @TenantId, N'User Activity Audit', N'Login audit', N'Successful login, failed login, logout', N'IAM', N'LOGIN_AUDIT', 1, 1, 1, 1010),
(NEWID(), @TenantId, N'User Activity Audit', N'MFA audit', N'MFA challenge, success, failure, reset', N'IAM', N'MFA_AUDIT', 1, 1, 1, 1020),
(NEWID(), @TenantId, N'User Activity Audit', N'Session audit', N'Session start, timeout, token refresh, forced logout', N'IAM', N'SESSION_AUDIT', 1, 1, 1, 1030),
(NEWID(), @TenantId, N'User Activity Audit', N'Page/module access', N'Which module or page was accessed', N'Web', N'PAGE_ACCESS_AUDIT', 1, 1, 1, 1040),
(NEWID(), @TenantId, N'User Activity Audit', N'Create audit', N'New record created', N'Core', N'CREATE_AUDIT', 1, 1, 1, 1050),
(NEWID(), @TenantId, N'User Activity Audit', N'Update audit', N'Record modified', N'Core', N'UPDATE_AUDIT', 1, 1, 1, 1060),
(NEWID(), @TenantId, N'User Activity Audit', N'Delete audit', N'Record deleted or soft-deleted', N'Core', N'DELETE_AUDIT', 1, 1, 1, 1070),
(NEWID(), @TenantId, N'User Activity Audit', N'View audit', N'Sensitive record viewed', N'Core', N'VIEW_AUDIT', 1, 1, 1, 1080),
(NEWID(), @TenantId, N'User Activity Audit', N'Export audit', N'Data exported to Excel, PDF, CSV', N'Audit', N'EXPORT_AUDIT', 1, 1, 1, 1090),
(NEWID(), @TenantId, N'User Activity Audit', N'Print audit', N'Documents or reports printed', N'DMS', N'PRINT_AUDIT', 1, 1, 1, 1100),
(NEWID(), @TenantId, N'User Activity Audit', N'Download audit', N'Files downloaded', N'DMS', N'DOWNLOAD_AUDIT', 1, 1, 1, 1110),
(NEWID(), @TenantId, N'User Activity Audit', N'Upload audit', N'Documents uploaded', N'DMS', N'UPLOAD_AUDIT', 1, 1, 1, 1120),
(NEWID(), @TenantId, N'User Activity Audit', N'Search audit', N'Sensitive searches performed', N'Core', N'SEARCH_AUDIT', 1, 1, 1, 1130),
(NEWID(), @TenantId, N'User Activity Audit', N'Bulk action audit', N'Mass update, import, export, delete', N'Core', N'BULK_ACTION_AUDIT', 1, 1, 1, 1140),
(NEWID(), @TenantId, N'Data Change History', N'Before value', N'Old field value', N'Audit', N'OLD_VALUE_TRACKING', 1, 1, 0, 2010),
(NEWID(), @TenantId, N'Data Change History', N'After value', N'New field value', N'Audit', N'NEW_VALUE_TRACKING', 1, 1, 0, 2020),
(NEWID(), @TenantId, N'Data Change History', N'Changed field name', N'Exact field modified', N'Audit', N'FIELD_NAME_TRACKING', 1, 1, 0, 2030),
(NEWID(), @TenantId, N'Data Change History', N'Entity name', N'Lead, Account, Submission, Policy, Claim, and other entity names', N'Audit', N'ENTITY_NAME_TRACKING', 1, 1, 0, 2040),
(NEWID(), @TenantId, N'Data Change History', N'Entity ID', N'Record affected', N'Audit', N'ENTITY_ID_TRACKING', 1, 1, 0, 2050),
(NEWID(), @TenantId, N'Data Change History', N'Parent entity', N'Parent entity such as Policy under Account', N'Audit', N'PARENT_ENTITY_TRACKING', 1, 1, 0, 2060),
(NEWID(), @TenantId, N'Data Change History', N'Change reason', N'Optional required reason for sensitive changes', N'Audit', N'CHANGE_REASON_TRACKING', 1, 1, 1, 2070),
(NEWID(), @TenantId, N'Data Change History', N'Version number', N'Record version tracking', N'Audit', N'VERSION_NUMBER_TRACKING', 1, 1, 1, 2080),
(NEWID(), @TenantId, N'Data Change History', N'Restore support', N'Ability to compare or restore previous values', N'Audit', N'RESTORE_SUPPORT', 1, 1, 1, 2090),
(NEWID(), @TenantId, N'Security Audit', N'Password change', N'User changed password', N'IAM', N'PASSWORD_CHANGE_AUDIT', 1, 1, 1, 3010),
(NEWID(), @TenantId, N'Security Audit', N'Password reset', N'Admin or user reset password', N'IAM', N'PASSWORD_RESET_AUDIT', 1, 1, 1, 3020),
(NEWID(), @TenantId, N'Security Audit', N'Role assignment', N'User role changed', N'IAM', N'ROLE_ASSIGNMENT_AUDIT', 1, 1, 1, 3030),
(NEWID(), @TenantId, N'Security Audit', N'Permission change', N'Permission added or removed', N'IAM', N'PERMISSION_CHANGE_AUDIT', 1, 1, 1, 3040),
(NEWID(), @TenantId, N'Security Audit', N'User activation', N'User enabled', N'IAM', N'USER_ACTIVATION_AUDIT', 1, 1, 1, 3050),
(NEWID(), @TenantId, N'Security Audit', N'User deactivation', N'User disabled', N'IAM', N'USER_DEACTIVATION_AUDIT', 1, 1, 1, 3060),
(NEWID(), @TenantId, N'Security Audit', N'Locked account', N'Failed login threshold reached', N'IAM', N'LOCKED_ACCOUNT_AUDIT', 1, 1, 1, 3070),
(NEWID(), @TenantId, N'Security Audit', N'Admin impersonation', N'Admin accessed as another user', N'Platform', N'ADMIN_IMPERSONATION_AUDIT', 1, 1, 1, 3080),
(NEWID(), @TenantId, N'Security Audit', N'API key created', N'New API credential', N'Integration', N'API_KEY_CREATED_AUDIT', 1, 1, 1, 3090),
(NEWID(), @TenantId, N'Security Audit', N'API key revoked', N'API credential disabled', N'Integration', N'API_KEY_REVOKED_AUDIT', 1, 1, 1, 3100),
(NEWID(), @TenantId, N'Security Audit', N'Tenant setting change', N'Security or configuration setting modified', N'Tenant Configuration', N'TENANT_SETTING_CHANGE_AUDIT', 1, 1, 1, 3110),
(NEWID(), @TenantId, N'Tenant-Level Audit', N'Tenant ID tracking', N'Every audit record includes TenantId', N'Audit', N'TENANT_ID_TRACKING', 1, 1, 0, 4010),
(NEWID(), @TenantId, N'Tenant-Level Audit', N'Tenant configuration changes', N'Branding, workflow, subscription, carrier config', N'Tenant Configuration', N'TENANT_CONFIGURATION_AUDIT', 1, 1, 1, 4020),
(NEWID(), @TenantId, N'Tenant-Level Audit', N'Feature flag changes', N'Module enabled or disabled', N'Tenant Configuration', N'FEATURE_FLAG_AUDIT', 1, 1, 1, 4030),
(NEWID(), @TenantId, N'Tenant-Level Audit', N'Subscription changes', N'Plan upgrades or downgrades', N'Billing', N'SUBSCRIPTION_AUDIT', 1, 1, 1, 4040),
(NEWID(), @TenantId, N'Tenant-Level Audit', N'Billing setting changes', N'Payment and contact settings', N'Billing', N'BILLING_SETTING_AUDIT', 1, 1, 1, 4050),
(NEWID(), @TenantId, N'Tenant-Level Audit', N'User invitation audit', N'Invited, accepted, expired', N'IAM', N'USER_INVITATION_AUDIT', 1, 1, 1, 4060),
(NEWID(), @TenantId, N'Tenant-Level Audit', N'Branch/office changes', N'Branch created, updated, disabled', N'Tenant Configuration', N'BRANCH_OFFICE_AUDIT', 1, 1, 1, 4070),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Lead', N'Created, assigned, converted', N'CRM', N'LEAD_WORKFLOW_AUDIT', 1, 1, 1, 5010),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Prospect', N'Created, qualified, disqualified', N'CRM', N'PROSPECT_WORKFLOW_AUDIT', 1, 1, 1, 5020),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Opportunity', N'Created, stage changed, lost/won', N'CRM', N'OPPORTUNITY_WORKFLOW_AUDIT', 1, 1, 1, 5030),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Account', N'Created, merged, updated', N'CRM', N'ACCOUNT_WORKFLOW_AUDIT', 1, 1, 1, 5040),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Submission', N'Created, submitted to carrier, declined', N'Submissions', N'SUBMISSION_WORKFLOW_AUDIT', 1, 1, 1, 5050),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Quote', N'Created, revised, selected', N'Policy', N'QUOTE_WORKFLOW_AUDIT', 1, 1, 1, 5060),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Proposal', N'Generated, sent, viewed', N'Policy', N'PROPOSAL_WORKFLOW_AUDIT', 1, 1, 1, 5070),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Bind', N'Bound, binder issued', N'Policy', N'BIND_WORKFLOW_AUDIT', 1, 1, 1, 5080),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Policy', N'Created, renewed, canceled, reinstated', N'Policy', N'POLICY_WORKFLOW_AUDIT', 1, 1, 1, 5090),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Endorsement', N'Requested, approved, issued', N'Policy', N'ENDORSEMENT_WORKFLOW_AUDIT', 1, 1, 1, 5100),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Certificate', N'Generated, emailed, downloaded', N'Certificates', N'CERTIFICATE_WORKFLOW_AUDIT', 1, 1, 1, 5110),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Claim', N'Opened, updated, closed', N'Claims', N'CLAIM_WORKFLOW_AUDIT', 1, 1, 1, 5120),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Renewal', N'Triggered, quoted, renewed, non-renewed', N'Renewals', N'RENEWAL_WORKFLOW_AUDIT', 1, 1, 1, 5130),
(NEWID(), @TenantId, N'Business Workflow Audit', N'Commission', N'Posted, adjusted, reversed', N'Commissions', N'COMMISSION_WORKFLOW_AUDIT', 1, 1, 1, 5140),
(NEWID(), @TenantId, N'Document Audit', N'Document upload tracking', N'Who uploaded what', N'DMS', N'DOCUMENT_UPLOAD_AUDIT', 1, 1, 1, 6010),
(NEWID(), @TenantId, N'Document Audit', N'Document download tracking', N'Who downloaded', N'DMS', N'DOCUMENT_DOWNLOAD_AUDIT', 1, 1, 1, 6020),
(NEWID(), @TenantId, N'Document Audit', N'Document preview audit', N'Who viewed sensitive documents', N'DMS', N'DOCUMENT_PREVIEW_AUDIT', 1, 1, 1, 6030),
(NEWID(), @TenantId, N'Document Audit', N'Document delete audit', N'Who deleted or archived', N'DMS', N'DOCUMENT_DELETE_AUDIT', 1, 1, 1, 6040),
(NEWID(), @TenantId, N'Document Audit', N'Version history', N'Track document replacements', N'DMS', N'DOCUMENT_VERSION_AUDIT', 1, 1, 1, 6050),
(NEWID(), @TenantId, N'Document Audit', N'Metadata change audit', N'Category, tags, expiration dates', N'DMS', N'DOCUMENT_METADATA_AUDIT', 1, 1, 1, 6060),
(NEWID(), @TenantId, N'Document Audit', N'Access permission audit', N'Who was granted or removed access', N'DMS', N'DOCUMENT_PERMISSION_AUDIT', 1, 1, 1, 6070),
(NEWID(), @TenantId, N'Document Audit', N'Retention audit', N'Lifecycle or retention action logged', N'DMS', N'DOCUMENT_RETENTION_AUDIT', 1, 1, 1, 6080),
(NEWID(), @TenantId, N'Compliance Features', N'Immutable logs', N'Users cannot edit audit records', N'Audit', N'IMMUTABLE_LOGS', 1, 1, 0, 7010),
(NEWID(), @TenantId, N'Compliance Features', N'Retention policy', N'Keep logs for 3, 5, 7, or 10 years', N'Audit', N'RETENTION_POLICY', 1, 1, 1, 7020),
(NEWID(), @TenantId, N'Compliance Features', N'Legal hold', N'Prevent deletion during dispute', N'Audit', N'LEGAL_HOLD', 1, 1, 1, 7030),
(NEWID(), @TenantId, N'Compliance Features', N'Exportable audit reports', N'CSV/PDF/Excel', N'Audit', N'EXPORTABLE_AUDIT_REPORTS', 1, 1, 1, 7040),
(NEWID(), @TenantId, N'Compliance Features', N'Audit search', N'Search by user, date, tenant, module, action', N'Audit', N'AUDIT_SEARCH', 1, 1, 0, 7050),
(NEWID(), @TenantId, N'Compliance Features', N'Evidence report', N'Generate compliance-ready activity report', N'Audit', N'EVIDENCE_REPORT', 1, 1, 1, 7060),
(NEWID(), @TenantId, N'Compliance Features', N'PII masking', N'Mask SSN, DOB, tax ID, bank info', N'Audit', N'PII_MASKING', 1, 1, 1, 7070),
(NEWID(), @TenantId, N'Compliance Features', N'Sensitive data access log', N'Track who viewed private data', N'Audit', N'SENSITIVE_DATA_ACCESS_LOG', 1, 1, 1, 7080),
(NEWID(), @TenantId, N'Compliance Features', N'Regulatory support', N'SOC 2, HIPAA-like controls, GLBA, CCPA readiness', N'Audit', N'REGULATORY_SUPPORT', 1, 1, 0, 7090),
(NEWID(), @TenantId, N'Admin Audit Dashboard', N'Audit timeline', N'Chronological activity view', N'Audit', N'AUDIT_TIMELINE', 1, 1, 0, 8010),
(NEWID(), @TenantId, N'Admin Audit Dashboard', N'User activity profile', N'All activity by a user', N'Audit', N'USER_ACTIVITY_PROFILE', 1, 1, 1, 8020),
(NEWID(), @TenantId, N'Admin Audit Dashboard', N'Entity audit history', N'All changes to a record', N'Audit', N'ENTITY_AUDIT_HISTORY', 1, 1, 1, 8030),
(NEWID(), @TenantId, N'Admin Audit Dashboard', N'Security events dashboard', N'Login failures, role changes, lockouts', N'Audit', N'SECURITY_EVENTS_DASHBOARD', 1, 1, 0, 8040),
(NEWID(), @TenantId, N'Admin Audit Dashboard', N'Risk alerts', N'Unusual activity detection', N'Audit', N'RISK_ALERTS', 1, 1, 1, 8050),
(NEWID(), @TenantId, N'Admin Audit Dashboard', N'Filters', N'Date, user, module, action, IP filters', N'Audit', N'AUDIT_FILTERS', 1, 1, 0, 8060),
(NEWID(), @TenantId, N'Admin Audit Dashboard', N'Export button', N'Export audit logs', N'Audit', N'AUDIT_EXPORT_BUTTON', 1, 1, 0, 8070),
(NEWID(), @TenantId, N'Admin Audit Dashboard', N'Drill-down detail', N'View full audit event payload', N'Audit', N'AUDIT_DRILLDOWN_DETAIL', 1, 1, 0, 8080),
(NEWID(), @TenantId, N'Alerting and Monitoring', N'Failed login spike', N'User failed login 10 times', N'IAM', N'FAILED_LOGIN_SPIKE_ALERT', 1, 1, 1, 9010),
(NEWID(), @TenantId, N'Alerting and Monitoring', N'Role escalation', N'User made Admin', N'IAM', N'ROLE_ESCALATION_ALERT', 1, 1, 1, 9020),
(NEWID(), @TenantId, N'Alerting and Monitoring', N'Mass export', N'10,000 records exported', N'Audit', N'MASS_EXPORT_ALERT', 1, 1, 1, 9030),
(NEWID(), @TenantId, N'Alerting and Monitoring', N'Mass delete', N'Many records deleted', N'Core', N'MASS_DELETE_ALERT', 1, 1, 1, 9040),
(NEWID(), @TenantId, N'Alerting and Monitoring', N'After-hours access', N'Login at unusual time', N'IAM', N'AFTER_HOURS_ACCESS_ALERT', 1, 1, 1, 9050),
(NEWID(), @TenantId, N'Alerting and Monitoring', N'Foreign IP login', N'New country or location', N'IAM', N'FOREIGN_IP_LOGIN_ALERT', 1, 1, 1, 9060),
(NEWID(), @TenantId, N'Alerting and Monitoring', N'Impersonation use', N'Platform admin accessed tenant', N'Platform', N'IMPERSONATION_USE_ALERT', 1, 1, 1, 9070),
(NEWID(), @TenantId, N'Alerting and Monitoring', N'Sensitive document access', N'W-9, loss runs, policy docs viewed', N'DMS', N'SENSITIVE_DOCUMENT_ACCESS_ALERT', 1, 1, 1, 9080),
(NEWID(), @TenantId, N'Technical Architecture', N'Central Audit Service', N'IAuditService and IEnterpriseAuditService in Application layer', N'Application', N'CENTRAL_AUDIT_SERVICE', 1, 1, 0, 10010),
(NEWID(), @TenantId, N'Technical Architecture', N'Database tables', N'AuditEvent and AuditEventDetail core tables', N'Audit', N'AUDIT_DATABASE_TABLES', 1, 1, 0, 10020),
(NEWID(), @TenantId, N'Technical Architecture', N'Background processing', N'Queue audit writes to avoid slowing UI', N'Infrastructure', N'BACKGROUND_AUDIT_PROCESSING', 1, 1, 0, 10030),
(NEWID(), @TenantId, N'Technical Architecture', N'Correlation ID', N'Track one request across API and services', N'Audit', N'CORRELATION_ID', 1, 1, 0, 10040),
(NEWID(), @TenantId, N'Technical Architecture', N'Request ID', N'Link API request to audit event', N'Audit', N'REQUEST_ID', 1, 1, 0, 10050),
(NEWID(), @TenantId, N'Technical Architecture', N'IP address', N'Store client IP', N'Audit', N'IP_ADDRESS', 1, 1, 0, 10060),
(NEWID(), @TenantId, N'Technical Architecture', N'User agent', N'Browser and device info', N'Audit', N'USER_AGENT', 1, 1, 0, 10070),
(NEWID(), @TenantId, N'Technical Architecture', N'Tenant ID', N'Required tenant column', N'Audit', N'TENANT_ID', 1, 1, 0, 10080),
(NEWID(), @TenantId, N'Technical Architecture', N'Actor type', N'User, System, API, Worker, Admin', N'Audit', N'ACTOR_TYPE', 1, 1, 0, 10090),
(NEWID(), @TenantId, N'Technical Architecture', N'Source', N'Web, API, Worker, Import, Integration', N'Audit', N'SOURCE_SYSTEM', 1, 1, 0, 10100),
(NEWID(), @TenantId, N'Technical Architecture', N'JSON payload', N'Store detailed metadata', N'Audit', N'METADATA_JSON', 1, 1, 0, 10110),
(NEWID(), @TenantId, N'Technical Architecture', N'Append-only design', N'No update/delete by normal users', N'Audit', N'APPEND_ONLY_DESIGN', 1, 1, 0, 10120);

DECLARE @SensitivePatterns TABLE (FieldNamePattern NVARCHAR(256), Description NVARCHAR(500));
INSERT INTO @SensitivePatterns VALUES
(N'%Password%', N'Password and password-hash fields.'),
(N'%Secret%', N'Client secrets and shared secrets.'),
(N'%ApiKey%', N'API key credentials.'),
(N'%AccessToken%', N'OAuth or bearer access tokens.'),
(N'%RefreshToken%', N'OAuth refresh tokens.'),
(N'%PrivateKey%', N'Private key material.'),
(N'Ssn', N'Social Security Number.'),
(N'%SocialSecurity%', N'Social Security Number variants.'),
(N'%TaxId%', N'Tax identification numbers.'),
(N'%Ein%', N'Employer identification numbers.'),
(N'%CreditCard%', N'Credit card numbers.'),
(N'%CardNumber%', N'Payment card numbers.'),
(N'%SecurityCode%', N'Card security codes (CVV).'),
(N'%BankAccount%', N'Bank account numbers.'),
(N'%RoutingNumber%', N'Bank routing numbers.'),
(N'%AccountNumber%', N'Financial account numbers.'),
(N'DateOfBirth', N'Date of birth.'),
(N'%DriversLicense%', N'Driver license numbers.'),
(N'%PassportNumber%', N'Passport numbers.'),
(N'%MedicalRecord%', N'Medical record identifiers.');

MERGE Audit.AuditSensitiveField AS target
USING @SensitivePatterns AS source
ON target.TenantId IS NULL AND target.EntityName IS NULL AND target.FieldNamePattern = source.FieldNamePattern
WHEN MATCHED THEN
    UPDATE SET Description = source.Description,
               IsActive = 1,
               IsDeleted = 0,
               ModifiedDateUtc = SYSUTCDATETIME(),
               ModifiedByUserId = @AdminUserId
WHEN NOT MATCHED THEN
    INSERT (AuditSensitiveFieldId, TenantId, EntityName, FieldNamePattern, Description, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (NEWID(), NULL, NULL, source.FieldNamePattern, source.Description, 1, SYSUTCDATETIME(), @AdminUserId, 0);


-- ============================================================
-- Actor attribution data sync (mirror of migration 0185)
-- Remaps audit rows whose user ids have no IAM.[User] record
-- (e.g. legacy dev-auth placeholder ids) to the tenant's first
-- active user so user name/email always resolve from the DB.
-- ============================================================
UPDATE a SET a.UserId = fallback.UserId
FROM IAM.UserAuditTrail a
CROSS APPLY (SELECT TOP 1 u.UserId FROM IAM.[User] u
             WHERE u.TenantId = a.TenantId AND u.IsDeleted = 0 AND u.IsActive = 1
             ORDER BY u.CreatedDateUtc) fallback
WHERE NOT EXISTS (SELECT 1 FROM IAM.[User] u WHERE u.UserId = a.UserId AND u.IsDeleted = 0);

UPDATE a SET a.ChangedByUserId = a.UserId
FROM IAM.UserAuditTrail a
WHERE a.ChangedByUserId IS NULL
   OR NOT EXISTS (SELECT 1 FROM IAM.[User] u WHERE u.UserId = a.ChangedByUserId AND u.IsDeleted = 0);

UPDATE e SET e.ActorUserId = fallback.UserId
FROM Audit.AuditEvent e
CROSS APPLY (SELECT TOP 1 u.UserId FROM IAM.[User] u
             WHERE u.TenantId = e.TenantId AND u.IsDeleted = 0 AND u.IsActive = 1
             ORDER BY u.CreatedDateUtc) fallback
WHERE e.ActorType = N'User'
  AND (e.ActorUserId IS NULL
       OR NOT EXISTS (SELECT 1 FROM IAM.[User] u WHERE u.UserId = e.ActorUserId AND u.IsDeleted = 0));

UPDATE e SET
    e.ActorUserName = COALESCE(NULLIF(u.FullName, N''), NULLIF(u.UserName, N''), u.Email),
    e.ActorRole = COALESCE(
        LEFT((SELECT STRING_AGG(r.RoleName, N', ') WITHIN GROUP (ORDER BY r.RoleName)
              FROM IAM.UserRole ur
              JOIN IAM.Role r ON r.RoleId = ur.RoleId AND r.IsDeleted = 0 AND r.IsActive = 1
              WHERE ur.UserId = u.UserId AND ur.IsDeleted = 0 AND ur.IsActive = 1), 200),
        e.ActorRole)
FROM Audit.AuditEvent e
JOIN IAM.[User] u ON u.UserId = e.ActorUserId AND u.IsDeleted = 0
WHERE e.ActorUserName IS NULL OR e.ActorUserName IN (N'', N'Unknown User', N'Development User');

UPDATE c SET c.ChangedByUserId = e.ActorUserId
FROM Audit.AuditEntityChange c
JOIN Audit.AuditEvent e ON e.AuditEventId = c.AuditEventId
WHERE c.ChangedByUserId IS NULL
   OR NOT EXISTS (SELECT 1 FROM IAM.[User] u WHERE u.UserId = c.ChangedByUserId AND u.IsDeleted = 0);


PRINT 'Enterprise audit module schema and seed data synchronized.';

