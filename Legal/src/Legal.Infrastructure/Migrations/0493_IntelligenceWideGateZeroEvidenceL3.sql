SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0493: Gate zero-evidence Evidence Types out of L3 atomic-proposition generation.
--   A recent Domain Pack run (Whitaker v. Pioneer Health) produced 796 L3 CHR nodes with only
--   ~5% evidence coverage: most nodes were speculative 0%-evidence "No X supplied" propositions
--   generated for EVD_ Evidence Types the matter has NO document, mapped passage, or indexed
--   passage for. The 0448 evidence-aware routing deferral channel existed but its computed
--   deferrals were being discarded before L3, so zero-evidence Evidence Types still received a
--   full L3 pass.
--   This migration adds the platform dial for the new deterministic gate (code half already wired):
--     * When true (default), Domain Pack L3 generation holds out EVD_ Evidence Types with zero
--       matter evidence (no classified document, no mapped passage, no indexed passage) and records
--       them as DEFERRED_ACQUISITION in the processing ledger.
--     * Concepts (CPT_), evidenced Evidence Types, and required-coverage Evidence Types are NEVER
--       gated. A safety guard prevents stripping every participating parent.
--   Idempotent and additive. Nothing is deleted; existing tenant overrides are untouched.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2=SYSUTCDATETIME();

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200) NOT NULL, Value NVARCHAR(100) NOT NULL, DataTypeCode NVARCHAR(40) NOT NULL, Description NVARCHAR(1000) NOT NULL);
	INSERT @Settings(SettingKey,Value,DataTypeCode,Description) VALUES
	(N'Intelligence.SearchWide.GateZeroEvidenceEvidenceTypesFromL3',N'true',N'Boolean',
	 N'When true, Domain Pack L3 holds out EVD_ Evidence Types with zero matter evidence (no classified document, mapped passage, indexed passage, or required coverage) as DEFERRED_ACQUISITION instead of generating speculative zero-evidence propositions. Concepts, evidenced and required-coverage Evidence Types are never gated. When false, every target gets an L3 pass.');

	UPDATE c SET c.DefaultValue=s.Value, c.Description=s.Description, c.ModifiedDateUtc=@Now
	  FROM Core.ConfigurationSetting c JOIN @Settings s ON s.SettingKey=c.SettingKey
	 WHERE c.TenantId IS NULL AND c.ScopeCode=N'Platform' AND c.IsDeleted=0;

	INSERT Core.ConfigurationSetting(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
	SELECT NEWID(),NULL,N'Platform',N'Intelligence',s.SettingKey,s.Value,s.Value,s.DataTypeCode,s.Description,0,0,@Now,0
	  FROM @Settings s
	 WHERE NOT EXISTS(SELECT 1 FROM Core.ConfigurationSetting c WHERE c.SettingKey=s.SettingKey AND c.TenantId IS NULL AND c.ScopeCode=N'Platform' AND c.IsDeleted=0);
END;

COMMIT TRANSACTION;
