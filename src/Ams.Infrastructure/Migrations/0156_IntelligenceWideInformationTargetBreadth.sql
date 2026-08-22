-- V3.1 Information Target Breadth: raise the per-round information-target cap from 2 to 3.
-- Rationale: with 3-5 branch hierarchies a cap of 2 structurally limits per-round branch
-- coverage, suppressing decision evidence coverage and final confidence even when the
-- ranking is correct. Selecting a third qualifying target adds one concurrent retrieval
-- and ZERO extra LLM calls (the information-value estimate is a single batched call).
-- MinimumInformationValue stays at 0.55: the floor is the precision filter and is working.
-- Tenant-specific overrides are intentionally preserved; only the global default changes.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET SettingValue=N'3',
		Description=N'Maximum evidence targets retrieved (concurrently) per information round. V3.1: raised from 2 to 3 to improve per-round branch coverage; targets must still pass MinimumInformationValue.',
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE SettingKey=N'Intelligence.SearchWide.MaximumInformationTargetsPerRound'
		AND TenantId IS NULL
		AND IsDeleted=0
		AND SettingValue=N'2';
END;
