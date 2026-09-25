-- R4 Legal Decision Normalization Gate feature flag for the dynamic wide pipeline.
-- Root cause fixed: the Personal Injury Decision UI (/legal/personalinjury_decision2) launches through
-- IntelligenceWide2Service.SearchDynamicAsync (the dynamic WideSearchRequest pipeline), NOT the dedicated
-- LegalDecisionService.DecidePersonalInjuryAsync path. As a result the shared Outcome-Dependency
-- Normalization Gate (deterministic material identity + shared-dependency registration) was never reached
-- from the real matter execution: dispositions stayed as L1 interpretation groups, no normalized candidate
-- pool was registered, and no Candidate x Dependency competition was driven by normalized candidates.
-- This setting enables a fail-soft, strictly EVALUATE-gated pre-competition step in the dynamic pipeline
-- that projects the proposed dispositions into the shared gate, registers the normalized candidates and
-- shared dependencies, and feeds the eligible representative candidates into the existing candidate
-- competition. It changes NO scoring math and NO evidence-admission rule: zero admitted evidence still
-- blocks authoritative winner selection. Any failure or non-legal run degrades to the unchanged pipeline.
--   EnableLegalDecisionNormalizationGate (default true) - master switch for the gate integration.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(400),DataTypeCode NVARCHAR(30),Description NVARCHAR(500));

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.EnableLegalDecisionNormalizationGate',N'true',N'Boolean',N'R4 When enabled AND the run is a matter-backed legal EVALUATE run, the dynamic pipeline routes its proposed dispositions through the shared Outcome-Dependency Normalization Gate before candidate competition so the eligible normalized candidates drive the ranking. Fail-soft and EVALUATE-gated; changes no scoring or evidence rules.');

	MERGE Core.ConfigurationSetting AS target
	USING @Settings AS source
	   ON target.TenantId IS NULL
	  AND target.ScopeCode=N'Platform'
	  AND target.SettingKey=source.SettingKey
	  AND target.IsDeleted=0
	WHEN MATCHED THEN
		UPDATE SET
			target.ModuleCode=N'Intelligence',
			target.SettingValue=COALESCE(NULLIF(target.SettingValue,N''),source.SettingValue),
			target.DefaultValue=source.SettingValue,
			target.DataTypeCode=source.DataTypeCode,
			target.Description=source.Description,
			target.IsEncrypted=0,
			target.IsReadOnly=0,
			target.ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN
		INSERT
		(
			SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,
			DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted
		)
		VALUES
		(
			NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,source.SettingValue,
			source.DataTypeCode,source.Description,0,0,SYSUTCDATETIME(),0
		);
END
