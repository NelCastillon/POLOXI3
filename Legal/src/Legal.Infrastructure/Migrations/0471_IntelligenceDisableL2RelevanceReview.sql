SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0471: Disable the optional L2 matter-relevance review (Intelligence.SearchWide.EnableL2RelevanceReview).
--
-- Scope: /legal/personalinjury_decision2 (Wide2 pipeline, IntelligenceWide2Service).
-- The L2 relevance review (WIDE_L2_RELEVANCE_REVIEW) scored every Domain Pack L2 catalog row with the
-- LLM, in dimension-grouped batches (0436/0440). For large packs (PERSONAL_INJURY ~341 evidence types)
-- this is ~100+ sequential LLM calls per run (observed: "batch 23/109", ~69k-char prompts) and was the
-- dominant latency source. Its only effect was blending an LLM score into PriorityScore to adjust Tier A/B
-- ORDER and context density - it is never an inclusion/exclusion gate and never adds/removes a factor.
--
-- With the atomic L3 propositions now generated directly on WIDE_INTENT v4.0 (0470) under the DB-backed
-- L1/L2 catalog, the review is redundant for generation. The Tier A/B ordering and the evidence-relevance
-- cutoff were subsequently REMOVED from the L3 path (0471 cleanup): every in-scope L2 parent is packed into
-- uniform budget-sized batches and sent to WIDE_INTENT. The deterministic relevance score still populates
-- the scope-ledger audit (PriorityScore/TierCode) but no longer affects batching or context density.
--
-- Runtime resolves the flag by COALESCE(SettingValue, DefaultValue); set BOTH to 'false' so the review is
-- off by platform default. Idempotent. Re-enable per-tenant by inserting a tenant SettingValue='true'.
-- ============================================================================
BEGIN TRANSACTION;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	   SET SettingValue=N'false',DefaultValue=N'false',ModifiedDateUtc=SYSUTCDATETIME()
	 WHERE TenantId IS NULL AND ScopeCode=N'Platform'
	   AND SettingKey=N'Intelligence.SearchWide.EnableL2RelevanceReview' AND IsDeleted=0;

	-- Ensure the platform row exists even if 0436 was not applied, so the default is authoritative.
	IF NOT EXISTS(SELECT 1 FROM Core.ConfigurationSetting
		WHERE TenantId IS NULL AND ScopeCode=N'Platform'
		  AND SettingKey=N'Intelligence.SearchWide.EnableL2RelevanceReview' AND IsDeleted=0)
		INSERT Core.ConfigurationSetting(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
		VALUES(NEWID(),NULL,N'Platform',N'Intelligence',N'Intelligence.SearchWide.EnableL2RelevanceReview',N'false',N'false',N'Boolean',
			N'Optional LLM matter-relevance review over the configured L2 catalog. Disabled (0471): atomic L3 is generated on WIDE_INTENT under the DB-backed L1/L2 catalog with uniform batching. Tier A/B ordering and the evidence-relevance cutoff were removed; deterministic relevance only populates scope-ledger audit metadata and is never an inclusion gate.',0,0,SYSUTCDATETIME(),0);
END;

COMMIT TRANSACTION;
GO
