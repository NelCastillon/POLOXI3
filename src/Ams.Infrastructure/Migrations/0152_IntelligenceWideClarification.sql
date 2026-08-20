SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- POLOXI V2.8: Clarification Gate.
-- When the evidence rounds end with low decision confidence AND an unstable
-- winner AND thin candidate separation AND a high-value unresolved information
-- target, POLOXI returns USER_CLARIFICATION_REQUIRED with a deterministic
-- clarification question instead of pretending certainty. The clarification
-- state is persisted so the follow-up answer continues the reasoning context.
-- ============================================================================

IF OBJECT_ID(N'POLOXI.WideExecution',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.WideExecution',N'DecisionConfidence') IS NULL
		ALTER TABLE POLOXI.WideExecution ADD DecisionConfidence DECIMAL(9,4) NULL;
	IF COL_LENGTH(N'POLOXI.WideExecution',N'ClarificationTarget') IS NULL
		ALTER TABLE POLOXI.WideExecution ADD ClarificationTarget NVARCHAR(300) NULL;
	IF COL_LENGTH(N'POLOXI.WideExecution',N'ClarificationQuestion') IS NULL
		ALTER TABLE POLOXI.WideExecution ADD ClarificationQuestion NVARCHAR(1000) NULL;
END;

-- ── V2.8 configuration settings (DB is the source of truth) ──
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE
	(
		SettingKey NVARCHAR(200) NOT NULL,
		SettingValue NVARCHAR(2000) NOT NULL,
		DataTypeCode NVARCHAR(50) NOT NULL,
		Description NVARCHAR(1000) NOT NULL
	);

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.EnableClarificationGate',N'true',N'Boolean',N'When true, POLOXI may finish with USER_CLARIFICATION_REQUIRED and a clarification question instead of an answer when the compound uncertainty gate fires. Fail-soft: disabling always returns the best available answer.'),
		(N'Intelligence.SearchWide.ClarificationConfidenceThreshold',N'0.60',N'Decimal',N'Decision confidence below which the clarification gate MAY fire. All gate conditions must hold simultaneously; a single low metric never triggers a question.'),
		(N'Intelligence.SearchWide.ClarificationWinnerStabilityThreshold',N'0.50',N'Decimal',N'Winner stability below which the clarification gate MAY fire. A stable winner suppresses clarification even at moderate confidence.'),
		(N'Intelligence.SearchWide.ClarificationMarginThreshold',N'0.10',N'Decimal',N'Top-candidate quality margin (winner minus runner-up) below which the clarification gate MAY fire. An obvious winner suppresses clarification.');

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
			NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,
			source.SettingValue,source.DataTypeCode,source.Description,0,0,
			SYSUTCDATETIME(),0
		);
END;

COMMIT TRANSACTION;
