SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- POLOXI Candidate Competition tab: DB-backed computation details.
-- Surfaces the composite-score dimension weights and the algorithm version so
-- the Candidate tab never hardcodes scoring constants. Platform defaults mirror
-- the deterministic DecisionCoreMath.CompositeScore weights
-- (Legal .25 / Fact .20 / Evidence .20 / Authority .15 / Verification .20) and
-- the current algorithm version, so behavior is unchanged when no override
-- exists. Values are resolved by IntelligenceWide2Repository.GetPoloxiComputationDetailsAsync
-- (tenant override -> platform default). Idempotent MERGE; safe to re-run.
-- ============================================================================

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
		(N'Intelligence.Decision.Composite.WeightLegal',N'0.25',N'Decimal',N'POLOXI composite-score weight for the Legal support dimension. Displayed in the Candidate Competition computation details.'),
		(N'Intelligence.Decision.Composite.WeightFact',N'0.20',N'Decimal',N'POLOXI composite-score weight for the Fact support dimension. Displayed in the Candidate Competition computation details.'),
		(N'Intelligence.Decision.Composite.WeightEvidence',N'0.20',N'Decimal',N'POLOXI composite-score weight for the Evidence support dimension. Displayed in the Candidate Competition computation details.'),
		(N'Intelligence.Decision.Composite.WeightAuthority',N'0.15',N'Decimal',N'POLOXI composite-score weight for the Authority support dimension. Displayed in the Candidate Competition computation details.'),
		(N'Intelligence.Decision.Composite.WeightVerification',N'0.20',N'Decimal',N'POLOXI composite-score weight for the Verification dimension. Displayed in the Candidate Competition computation details.'),
		(N'Intelligence.Decision.AlgorithmVersion',N'POLOXI_WIDE_V3.21',N'String',N'POLOXI decision algorithm version label shown in the Candidate Competition computation details.');

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
