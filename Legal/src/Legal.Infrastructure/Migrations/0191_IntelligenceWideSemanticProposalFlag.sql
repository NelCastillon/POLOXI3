-- POLOXI Wide semantic-proposal feature flag (default OFF).
--
-- Seeds the Intelligence.Poloxi.EnableSemanticProposal platform configuration setting used by
-- IntelligenceWideService.GeneratePoloxiProposalAsync to select the feature-flagged semantic Wide path
-- (WIDE_SEMANTIC_PROPOSAL + WIDE_SEMANTIC_GROUNDING). Default is 'false' so the legacy
-- WIDE_POLOXI_HIERARCHY path remains authoritative until the semantic path is proven and benchmarked.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(2000),DataTypeCode NVARCHAR(50),Description NVARCHAR(1000));
	INSERT @Settings VALUES
	(N'Intelligence.Poloxi.EnableSemanticProposal',N'false',N'Boolean',N'Enable the feature-flagged POLOXI Wide semantic proposal path (WIDE_SEMANTIC_PROPOSAL + WIDE_SEMANTIC_GROUNDING) instead of the legacy WIDE_POLOXI_HIERARCHY path.');
	MERGE Core.ConfigurationSetting target USING @Settings source ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey AND target.IsDeleted=0
	WHEN MATCHED THEN UPDATE SET ModuleCode=N'Intelligence',DefaultValue=source.SettingValue,DataTypeCode=source.DataTypeCode,Description=source.Description,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,source.SettingValue,source.DataTypeCode,source.Description,0,0,SYSUTCDATETIME(),0);
END;

COMMIT TRANSACTION;
