SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ============================================================================
-- POLOXI V3.12: Explicit branch semantic ROLE + marginal-value hierarchy
-- stopping + guardrail penalty configuration.
-- BranchRoleCode is assigned by the hierarchy LLM at proposal time (the same
-- LLM-proposes/POLOXI-enforces pattern as SemanticTypeCode) and deterministic
-- scoring math acts on the role:
--   HARD_CONSTRAINT -> failure can invalidate a candidate
--   GUARDRAIL       -> weak performance applies a veto-style penalty
--   PREFERENCE      -> ordinary compensatory criterion (default)
--   CONTEXT         -> never scores candidates directly
-- Marginal-value stopping ends hierarchy expansion when the latest level's
-- measured evidence-coverage and confidence deltas both fall below floors,
-- replacing "traverse to configured depth" with "stop when reasoning stops
-- changing the answer".
-- ============================================================================

-- ── POLOXI.WideBranch: explicit semantic role ──
IF OBJECT_ID(N'POLOXI.WideBranch',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.WideBranch',N'BranchRoleCode') IS NULL
		ALTER TABLE POLOXI.WideBranch ADD BranchRoleCode NVARCHAR(20) NOT NULL CONSTRAINT DF_PoloxiWideBranch_BranchRole DEFAULT N'PREFERENCE';
END;

-- ── V3.11/V3.12 configuration settings (DB is the source of truth) ──
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
		(N'Intelligence.SearchWide.EnableGuardrailPenalty',N'true',N'Boolean',N'Enables the Guardrail-Constrained Weighted Utility penalty: weak performance on a GUARDRAIL-role criterion multiplies the candidate composite by a continuous veto-style factor instead of being fully compensated by strong preference scores.'),
		(N'Intelligence.SearchWide.GuardrailVetoThreshold',N'0.20',N'Decimal',N'Guardrail score at or below which the candidate composite is vetoed to zero (ELECTRE-inspired veto point).'),
		(N'Intelligence.SearchWide.GuardrailAcceptableThreshold',N'0.65',N'Decimal',N'Guardrail score at or above which no penalty applies; scores between the veto and this threshold receive the continuous penalty curve.'),
		(N'Intelligence.SearchWide.GuardrailPenaltyExponent',N'0.50',N'Decimal',N'Exponent of the continuous guardrail penalty curve P(x)=((x-v)/(t-v))^gamma; lower values penalize more severely near the veto point.'),
		(N'Intelligence.SearchWide.EnableMarginalValueStopping',N'true',N'Boolean',N'Enables marginal-contribution hierarchy stopping: expansion ends when the latest level added neither meaningful evidence coverage nor confidence, instead of continuing to the configured depth ceiling.'),
		(N'Intelligence.SearchWide.MarginalValueMinimumDepth',N'3',N'Integer',N'Depth at or beyond which the marginal-value stop rule is evaluated, preserving the minimum-depth narrowing semantics for shallow hierarchies.'),
		(N'Intelligence.SearchWide.MarginalCoverageDeltaFloor',N'0.05',N'Decimal',N'Minimum increase in surviving-branch evidence coverage a new level must contribute; below this (together with the confidence floor) expansion stops with MARGINAL_VALUE_STOP.'),
		(N'Intelligence.SearchWide.MarginalConfidenceDeltaFloor',N'0.03',N'Decimal',N'Minimum increase in aggregate confidence a new level must contribute; below this (together with the coverage floor) expansion stops with MARGINAL_VALUE_STOP.');

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
