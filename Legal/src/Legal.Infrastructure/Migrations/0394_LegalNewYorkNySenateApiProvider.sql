-- ============================================================================
-- 0394: Repoint New York NY_NYSENATE_* descriptors from the BLOCKED nysenate.gov
--       HTML host to the authenticated NY Senate Open Legislation JSON API.
--
-- WHY: 0393 enabled NY_NYSENATE_* against https://www.nysenate.gov/legislation/
-- laws/{LAWID}/{section} with FULL_PAGE_TEXT. Runtime diagnostics proved that
-- HTML host returns HTTP 403/ACCESS_DENIED from the deployment network (same as
-- Justia). Direct probes confirmed the government JSON API host
-- https://legislation.nysenate.gov/api/3/laws/{LAWID}/{section}?key={API_KEY}
-- returns HTTP 200 with substantive statute text for both CVP/214 (CPLR 214) and
-- VAT/1192 (DWI), which the Sapini New York motor-vehicle matter cites.
--
-- WHAT: This forward-only migration updates the existing enabled NY_NYSENATE_*
-- rows to:
--   * BaseUrl              = https://legislation.nysenate.gov
--   * DocumentUrlTemplate  = api/3/laws/{LAWID}/{section}
--   * ExtractionStrategyCode = JSON_NYSENATE   (parsed by OfficialLegalAuthority-
--                             Retriever; result.title + result.text)
-- Priority 400 (tried before NY_JUSTIA_* fallback) is preserved. The API key is
-- NOT stored here; it is injected at request time from configuration key
-- Legal:OfficialAuthority:NySenateApiKey (env Legal__OfficialAuthority__NySenateApiKey).
--
-- DATA ONLY. No schema, pipeline, scoring, or UI changes. Platform-scope
-- (TenantId NULL). Idempotent: updates converge to the API route on every run.
-- 0393 is left immutable as historical record.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource', N'U') IS NOT NULL
BEGIN
	-- Provider -> NY Senate LAWID map (suffix == LAWID for these families).
	DECLARE @NyApi TABLE
	(
		ProviderCode NVARCHAR(80) NOT NULL,
		LawId        NVARCHAR(16) NOT NULL
	);

	INSERT @NyApi VALUES
	(N'NY_NYSENATE_CVP', N'CVP'),
	(N'NY_NYSENATE_ISC', N'ISC'),
	(N'NY_NYSENATE_GOB', N'GOB'),
	(N'NY_NYSENATE_LAB', N'LAB'),
	(N'NY_NYSENATE_EPT', N'EPT'),
	(N'NY_NYSENATE_VAT', N'VAT');

	UPDATE t
		SET t.BaseUrl               = N'https://legislation.nysenate.gov',
			t.DocumentUrlTemplate   = N'api/3/laws/' + a.LawId + N'/{section}',
			t.ExtractionStrategyCode = N'JSON_NYSENATE',
			t.IsEnabled             = 1,
			t.Priority              = 400,
			t.ModifiedDateUtc       = SYSUTCDATETIME()
	FROM POLOXI.Legal_AuthoritySource t
	INNER JOIN @NyApi a
		ON a.ProviderCode = t.ProviderCode
	WHERE t.JurisdictionCode = N'NAME:NEW YORK'
	  AND t.AuthorityKindCode = N'STATUTE'
	  AND t.TenantId IS NULL
	  AND t.IsDeleted = 0;
END;

COMMIT TRANSACTION;
