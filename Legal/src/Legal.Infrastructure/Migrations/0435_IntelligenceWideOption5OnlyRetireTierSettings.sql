SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ============================================================================
-- 0435: Option 5 only - retire the optional Option 6 priority-tier settings. The tier never gated
-- inclusion (it only reordered batches); with L3TierOneCount=0 the pipeline is pure full-coverage
-- adaptive batching. Soft-delete so the rows remain auditable; the repository falls back to 0/defaults.
-- ============================================================================
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME()
	WHERE TenantId IS NULL AND ScopeCode=N'Platform' AND IsDeleted=0
	  AND SettingKey IN (N'Intelligence.SearchWide.L3TierOneCount',N'Intelligence.SearchWide.L3TierOneBatchSize',N'Intelligence.SearchWide.L3TierOneMaxPerDimension');
END;
GO
