-- V3.2.1 Clarification Gate Calibration: raise ClarificationConfidenceThreshold from 0.60 to 0.65.
-- Rationale: observed ENTITY_RANKING runs where the ranking was demonstrably unsettled
-- (winner stability 50%, top-3 stability 25%, information rounds gained 0 bits) but the gate
-- did not fire because decision confidence landed at 62% - 2 points above the 60% threshold.
-- The gate's ALL-conditions design is correct (a single low metric must never trigger a
-- question), but the confidence ceiling was calibrated too low: a 60-65% confidence ranking
-- with low stability and stalled retrieval is precisely the case where asking the user which
-- ranking concept they mean (e.g. revenue vs units vs popularity) beats committing.
-- Tenant-specific overrides are intentionally preserved; only the global default changes,
-- and only when it still holds the prior default value.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	UPDATE Core.ConfigurationSetting
	SET SettingValue=N'0.65',
		DefaultValue=N'0.65',
		Description=N'Clarification gate: decision confidence must be BELOW this for POLOXI to ask instead of answer (all gate conditions must hold). V3.2.1: raised from 0.60 to 0.65 so low-stability rankings with stalled retrieval ask the user instead of committing.',
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE SettingKey=N'Intelligence.SearchWide.ClarificationConfidenceThreshold'
		AND TenantId IS NULL
		AND IsDeleted=0
		AND SettingValue=N'0.60';
END;
