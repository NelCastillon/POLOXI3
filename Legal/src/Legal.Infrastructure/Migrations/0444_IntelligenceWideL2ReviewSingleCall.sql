SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0444: L2 relevance review runs as ONE LLM call for the whole Domain Pack Evidence Type catalog.
--   0440 introduced Intelligence.SearchWide.L2ReviewBatchSize=40, but batches were also split per L1 dimension,
--   so a 28-dimension pack produced 28 calls (~12 targets each). Review batching is now driven solely by this
--   dial (dimension is sort order only) and review lines are compact, so the full catalog (353 targets today)
--   fits one prompt. Raise the dial to 500 (raise-only; tenant overrides untouched). Lower it per tenant only
--   if a pack outgrows the prompt budget.
-- ============================================================================
BEGIN TRANSACTION;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	   SET SettingValue=CASE WHEN TRY_CONVERT(int,SettingValue)<500 THEN N'500' ELSE SettingValue END,
		   DefaultValue=N'500',
		   Description=N'Maximum Domain Pack L2 targets (Evidence Types + Concepts) per L2 relevance-review LLM call. Set at or above the active pack catalog size so the whole catalog is reviewed in ONE call linked to the matter factors; lower only if a pack outgrows the prompt budget. Review lines are compact (code, name, matter types, deterministic signals).',
		   ModifiedDateUtc=SYSUTCDATETIME()
	 WHERE TenantId IS NULL AND ScopeCode=N'Platform' AND SettingKey=N'Intelligence.SearchWide.L2ReviewBatchSize' AND IsDeleted=0;

	IF @@ROWCOUNT=0
		INSERT Core.ConfigurationSetting(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
		VALUES(NEWID(),NULL,N'Platform',N'Intelligence',N'Intelligence.SearchWide.L2ReviewBatchSize',N'500',N'500',N'Integer',
			N'Maximum Domain Pack L2 targets (Evidence Types + Concepts) per L2 relevance-review LLM call. Set at or above the active pack catalog size so the whole catalog is reviewed in ONE call linked to the matter factors; lower only if a pack outgrows the prompt budget. Review lines are compact (code, name, matter types, deterministic signals).',
			0,0,SYSUTCDATETIME(),0);
END;

COMMIT TRANSACTION;
