SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'AI.SafetyEvent',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'AI.SafetyEvent',N'IdempotencyKey') IS NULL
		ALTER TABLE AI.SafetyEvent ADD IdempotencyKey NVARCHAR(240) NULL;

	IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'AI.SafetyEvent') AND name=N'UX_AI_SafetyEvent_Idempotency')
		EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_AI_SafetyEvent_Idempotency ON AI.SafetyEvent(TenantId,IdempotencyKey) WHERE IsDeleted=0 AND IdempotencyKey IS NOT NULL;';
END;

IF OBJECT_ID(N'AI.PromptDefinition',N'U') IS NOT NULL
AND NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'AI.PromptDefinition') AND name=N'UX_AI_PromptDefinition_TenantVersion')
	CREATE UNIQUE INDEX UX_AI_PromptDefinition_TenantVersion ON AI.PromptDefinition(TenantId,PromptCode,VersionLabel) WHERE TenantId IS NOT NULL AND IsDeleted=0;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Config TABLE(SettingKey NVARCHAR(200),Value NVARCHAR(2000),DataType NVARCHAR(50),Name NVARCHAR(200),Description NVARCHAR(1000));
	INSERT @Config VALUES
	(N'Intelligence.Workflow.ApprovalDelayDays',N'2',N'Integer',N'Approval delay threshold',N'Days a pending governed approval may wait before workflow intelligence creates an advisory signal.'),
	(N'Intelligence.Workflow.HighSeverityApprovalDays',N'5',N'Integer',N'High-severity approval delay',N'Days a pending governed approval may wait before its advisory workflow signal becomes high severity.'),
	(N'Intelligence.Claims.ReviewThreshold',N'100000',N'Decimal',N'Claims review threshold',N'Configured incurred amount used with authoritative claim flags to create an advisory claim review signal.'),
	(N'Intelligence.Customer.CommunicationGapDays',N'30',N'Integer',N'Customer communication gap',N'Days without an authoritative account activity before customer intelligence creates an advisory follow-up signal.'),
	(N'Intelligence.Signal.DefaultDueDays',N'2',N'Integer',N'Default signal due period',N'Default due period for intelligence signals when the source workflow has no authoritative due date.');

	MERGE Core.ConfigurationSetting target USING @Config source
	ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey
	WHEN MATCHED THEN UPDATE SET ModuleCode=N'Intelligence',DefaultValue=source.Value,DataTypeCode=source.DataType,Description=source.Name+N'. '+source.Description,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,IsDeleted,CreatedDateUtc)
	VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.Value,source.Value,source.DataType,source.Name+N'. '+source.Description,0,0,0,SYSUTCDATETIME());
END;
