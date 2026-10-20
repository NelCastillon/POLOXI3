SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0443: Inclusion-first per-matter Evidence Type scope (HIGH IMPACT - the only L2->L3 membership gate).
--   0441 populated POLOXI.Legal_MatterEvidenceScope exclusion-first: every catalog target started IN_SCOPE
--   and was held back only when its blended relevance fell below 0.05 AND no protection applied. In practice
--   the catalog-importance prior alone reached 0.05 and the fuzzy token-match DocumentPresent flag protected
--   ~1/3 of the catalog, so a matter with 3 documented evidence types still sent all 353 targets to L3.
--   This migration flips the default: a target is IN_SCOPE only when a POSITIVE matter signal exists
--   (directly classified evidence item/document, PRIORITIZE rule fired, required coverage, mapped document
--   passage, matter-type match, or blended relevance >= MatterScopeOutThreshold). Everything else is
--   OUT_OF_SCOPE: kept in the table, visible in the Evidence Scope UI, attorney-reversible (USER decisions
--   stay authoritative). Deterministic runtime fallback when the setting row is absent is also inclusion-first.
--   Idempotent. DB remains the source of truth for both dials.
-- ============================================================================
BEGIN TRANSACTION;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200) NOT NULL,SettingValue NVARCHAR(2000) NOT NULL,DataTypeCode NVARCHAR(50) NOT NULL,Description NVARCHAR(1000) NOT NULL);
	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
	(N'Intelligence.SearchWide.MatterScopeInclusionFirst',N'true',N'Boolean',N'Per-matter Evidence Type scope gate mode. true (inclusion-first): a Domain Pack L2 target receives an L3 pass only when a positive matter signal exists (classified evidence item, PRIORITIZE rule, required coverage, mapped passage, matter-type match, or blended relevance >= MatterScopeOutThreshold). false (legacy exclusion-first): every target is IN_SCOPE unless blended relevance < threshold and unprotected. USER decisions are authoritative in both modes.');
	MERGE Core.ConfigurationSetting AS target
	USING @Settings AS source
	   ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey AND target.IsDeleted=0
	WHEN NOT MATCHED BY TARGET THEN
		INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
		VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,source.SettingValue,source.DataTypeCode,source.Description,0,0,SYSUTCDATETIME(),0);

	-- Raise the relevance dial from the inert 0.05 to 0.20 (raise-only; tenant overrides untouched).
	UPDATE Core.ConfigurationSetting
	   SET SettingValue=CASE WHEN TRY_CONVERT(DECIMAL(9,4),SettingValue)<0.20 THEN N'0.20' ELSE SettingValue END,
		   DefaultValue=N'0.20',
		   Description=N'Blended (deterministic + LLM) matter relevance at or above which an unprotected Domain Pack L2 target is IN_SCOPE for the matter (inclusion-first) or below which it is auto-marked OUT_OF_SCOPE (legacy). Targets with a classified evidence item, PRIORITIZE rule, required coverage, mapped passage, or matter-type match are IN_SCOPE regardless. USER decisions are never overwritten.',
		   ModifiedDateUtc=SYSUTCDATETIME()
	 WHERE TenantId IS NULL AND ScopeCode=N'Platform' AND SettingKey=N'Intelligence.SearchWide.MatterScopeOutThreshold' AND IsDeleted=0;
END;

COMMIT TRANSACTION;
