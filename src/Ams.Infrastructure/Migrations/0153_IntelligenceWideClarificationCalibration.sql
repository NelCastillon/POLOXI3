SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- POLOXI V2.8.5: Clarification Calibration.
-- Persists the intent-side uncertainty loop so POLOXI can MEASURE whether its
-- clarification questions work: IntentEntropy (normalized Shannon entropy over
-- the top candidates), PriorIntentEntropy (the entropy before the user's
-- answer, carried from the previous execution of the round), ClarificationGain
-- (prior - current; the intent-side analogue of Actual Information Gain), and
-- ClarificationRound (which ask/answer round produced this execution).
-- Calibration queries over these columns tell POLOXI which clarification targets
-- actually resolve entity ambiguity - measured, never invented by the LLM.
-- ============================================================================

IF OBJECT_ID(N'POLOXI.WideExecution',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.WideExecution',N'IntentEntropy') IS NULL
		ALTER TABLE POLOXI.WideExecution ADD IntentEntropy DECIMAL(9,4) NULL;
	IF COL_LENGTH(N'POLOXI.WideExecution',N'PriorIntentEntropy') IS NULL
		ALTER TABLE POLOXI.WideExecution ADD PriorIntentEntropy DECIMAL(9,4) NULL;
	IF COL_LENGTH(N'POLOXI.WideExecution',N'ClarificationGain') IS NULL
		ALTER TABLE POLOXI.WideExecution ADD ClarificationGain DECIMAL(9,4) NULL;
	IF COL_LENGTH(N'POLOXI.WideExecution',N'ClarificationRound') IS NULL
		ALTER TABLE POLOXI.WideExecution ADD ClarificationRound INT NOT NULL CONSTRAINT DF_WideExecution_ClarificationRound DEFAULT(0);
END;

-- ── V2.8.5 configuration settings (DB is the source of truth) ──
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
		(N'Intelligence.SearchWide.MaximumClarificationRounds',N'2',N'Integer',N'Maximum ask/answer clarification rounds per reasoning context. After this many rounds POLOXI answers with the best available candidate instead of asking again - clarification must converge, never loop.'),
		(N'Intelligence.SearchWide.MinimumClarificationGain',N'0.10',N'Decimal',N'Minimum measured Clarification Gain (prior intent entropy minus current) required for a FOLLOW-UP clarification question. If the previous answer did not reduce intent uncertainty by at least this much, asking again is unlikely to help and POLOXI answers instead.');

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
