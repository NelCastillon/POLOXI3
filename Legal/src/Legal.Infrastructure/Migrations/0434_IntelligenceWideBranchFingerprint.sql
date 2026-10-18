SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0434: Option 5 incremental reuse - per (tenant, matter, L2 target) fingerprint of the last successful
-- proposition generation. When the fingerprint (passage ids + content hashes + prompt version + batch
-- settings) is unchanged on a later run, the previous propositions are cloned instead of calling the
-- model. Any passage change forces regeneration. Reconstructed from the live schema (DB is the source
-- of truth); idempotent.
-- ============================================================================
IF OBJECT_ID(N'POLOXI.Legal_WideBranchFingerprint',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_WideBranchFingerprint
	(
		WideBranchFingerprintId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideBranchFingerprint PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		MatterId UNIQUEIDENTIFIER NOT NULL,
		TargetCode NVARCHAR(120) NOT NULL,
		FingerprintHash CHAR(64) NOT NULL,
		SourceWideExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_WideBranchFingerprint_Execution REFERENCES POLOXI.Legal_WideExecution (WideExecutionId),
		SourceWideBranchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_WideBranchFingerprint_Branch REFERENCES POLOXI.Legal_WideBranch (WideBranchId),
		ProcessingResultCode NVARCHAR(40) NOT NULL,
		PropositionCount INT NOT NULL CONSTRAINT DF_Legal_WideBranchFingerprint_Props DEFAULT 0,
		PassageCount INT NOT NULL CONSTRAINT DF_Legal_WideBranchFingerprint_Passages DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideBranchFingerprint_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_WideBranchFingerprint_Deleted DEFAULT 0,
		CONSTRAINT CK_Legal_WideBranchFingerprint_Result CHECK (ProcessingResultCode IN (N'PROPOSITIONS_GENERATED',N'INFORMATION_GAP',N'NOT_APPLICABLE'))
	);
	CREATE UNIQUE INDEX UX_Legal_WideBranchFingerprint_Target ON POLOXI.Legal_WideBranchFingerprint (TenantId, MatterId, TargetCode) WHERE IsDeleted=0;
END;
GO

IF COL_LENGTH(N'POLOXI.Legal_WideBranchProcessing',N'SourceWideExecutionId') IS NULL
	ALTER TABLE POLOXI.Legal_WideBranchProcessing ADD SourceWideExecutionId UNIQUEIDENTIFIER NULL; -- set when the result was reused from a prior run
GO

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Core.ConfigurationSetting WHERE TenantId IS NULL AND ScopeCode=N'Platform' AND SettingKey=N'Intelligence.SearchWide.L3ReuseUnchangedResults' AND IsDeleted=0)
BEGIN
	INSERT Core.ConfigurationSetting(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
	VALUES(NEWID(),NULL,N'Platform',N'Intelligence',N'Intelligence.SearchWide.L3ReuseUnchangedResults',N'true',N'true',N'Boolean',
		N'Option 5 incremental reuse: when an L2 factor''s evidence passages, prompt and batch settings are unchanged since the last successful run of the same matter, clone the previous propositions instead of calling the model. Any passage change forces regeneration.',
		0,0,SYSUTCDATETIME(),0);
END;
GO
