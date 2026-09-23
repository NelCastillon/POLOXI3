SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Epistemic Authority Layer — DB-backed configuration (§34).
--
-- Seeds Platform-scope defaults for every EpistemicAuthoritySettings value so the whole EA layer is
-- configurable from /legal/configuration. Values mirror the code defaults so runtime behavior is
-- unchanged until an administrator edits them. Tenant overrides are stored as ScopeCode='Tenant'
-- rows and take precedence over these Platform defaults; the resolver falls back Platform → code.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE
	(
		SettingKey NVARCHAR(200) NOT NULL,
		SettingValue NVARCHAR(2000) NOT NULL,
		DataTypeCode NVARCHAR(50) NOT NULL,
		Description NVARCHAR(1000) NOT NULL,
		IsEncrypted BIT NOT NULL
	);

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description,IsEncrypted)
	VALUES
		(N'Intelligence.Epistemic.UseClaimAuthorityGate',N'true',N'Boolean',N'EA-1: gate decision authority on verified support strength for material claims.',0),
		(N'Intelligence.Epistemic.UseMaterialClaimVerification',N'true',N'Boolean',N'EA-3: verify material claims against projected support before authorizing.',0),
		(N'Intelligence.Epistemic.UseClaimDependencyPropagation',N'true',N'Boolean',N'EA-4: propagate verification outcomes across claim dependencies.',0),
		(N'Intelligence.Epistemic.UseClaimReadinessBlocking',N'true',N'Boolean',N'EA-5: evaluate decision readiness and block on unresolved essential claims.',0),
		(N'Intelligence.Epistemic.UseOutputClaimAudit',N'true',N'Boolean',N'EA-5: audit output claims that could surface in the answer.',0),
		(N'Intelligence.Epistemic.UseEpistemicDecisionBridge',N'true',N'Boolean',N'EA-6: master switch for the advisory bridge projecting the V2 graph into EA claims.',0),
		(N'Intelligence.Epistemic.OverrideMode',N'Advisory',N'String',N'EA-7: how the readiness verdict may affect the effective decision (Advisory, SoftGate, HardGate). Downgrade-only; never mutates the V2 verdict.',0),
		(N'Intelligence.Epistemic.MaxVerificationActionsPerRound',N'5',N'Integer',N'Maximum verification actions executed per prioritization round.',0),
		(N'Intelligence.Epistemic.MaxOutputRepairAttempts',N'1',N'Integer',N'Maximum output repair attempts during the audit loop.',0),
		(N'Intelligence.Epistemic.MinimumVerificationIV',N'0.15',N'Decimal',N'Minimum information value before a verification action is worth executing.',0),
		(N'Intelligence.Epistemic.MaterialityThreshold',N'0.5',N'Decimal',N'Materiality at/above which a claim is decision-relevant enough to gate authority/readiness.',0),
		(N'Intelligence.Epistemic.FullAuthorityStrengthThreshold',N'0.75',N'Decimal',N'Verified-support strength required for Full (vs Limited) decision authority.',0),
		(N'Intelligence.Epistemic.VerificationAdequacyThreshold',N'0.5',N'Decimal',N'Support/contradiction strength at/above which a side is treated as adequate.',0);

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
			target.IsEncrypted=source.IsEncrypted,
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
			source.SettingValue,source.DataTypeCode,source.Description,source.IsEncrypted,0,
			SYSUTCDATETIME(),0
		);
END;

COMMIT TRANSACTION;
