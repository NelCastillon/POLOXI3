SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Enables live external web grounding (Tavily) for /legal/search Wide interpretive results.
-- Migration 0144 seeded the global Platform-scoped rows with Enabled='false' and an empty ApiKey,
-- so under the GENERAL (non-LEGAL) context the router (IntelligenceWideService ~L2243) skips the web
-- provider and falls back to legal grounding (CourtListener/GovInfo) - producing no Tavily evidence.
-- This migration turns the provider on and supplies the API key on the global Platform rows.
-- A tenant-scoped row (TenantId = specific tenant) still overrides these values when present.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	-- IMPORTANT: replace the placeholder below with your real Tavily API key before running.
	DECLARE @TavilyApiKey NVARCHAR(2000) = N'REPLACE_WITH_TAVILY_API_KEY';

	UPDATE Core.ConfigurationSetting
	SET SettingValue=N'true',
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE TenantId IS NULL
	  AND ScopeCode=N'Platform'
	  AND SettingKey=N'Intelligence.SearchWide.ExternalGrounding.Enabled'
	  AND IsDeleted=0;

	UPDATE Core.ConfigurationSetting
	SET SettingValue=@TavilyApiKey,
		ModifiedDateUtc=SYSUTCDATETIME()
	WHERE TenantId IS NULL
	  AND ScopeCode=N'Platform'
	  AND SettingKey=N'Intelligence.SearchWide.ExternalGrounding.ApiKey'
	  AND IsDeleted=0;
END;

COMMIT TRANSACTION;
