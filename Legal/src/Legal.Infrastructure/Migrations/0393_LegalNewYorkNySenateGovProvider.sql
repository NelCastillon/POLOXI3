-- ============================================================================
-- 0393: Give New York a GOVERNMENT-HOST authority provider (California parity).
--
-- WHY: Investigation of the Sapini (New York) zero-evidence failure proved that
-- the CA-vs-NY difference is NOT a Justia URL-slug bug. Direct probes showed
-- law.justia.com returns HTTP 403 for BOTH California AND New York equally, so
-- Justia is not the reason California retrieves and New York does not.
--
-- The real difference: California has TWO provider families seeded --
--   * CA_LEGINFO_*  -> leginfo.legislature.ca.gov  (GOVERNMENT host, 0328), and
--   * CA_FINDLAW_*  -> law.justia.com               (0331-0333),
-- while migration 0391 DISABLED New York's only non-Justia option
-- (NY_NYSENATE_*) and left New York depending solely on the blocked Justia host.
-- When Justia 403s, New York has no working provider left and admits 0 evidence.
--
-- This forward-only migration restores California parity by seeding ENABLED
-- per-code NY_NYSENATE_* descriptors on the New York State Senate Open
-- Legislation government host (www.nysenate.gov), at a HIGHER priority (lower
-- number = tried first) than the NY_JUSTIA_* fallback. It also adds the missing
-- Vehicle & Traffic Law (VAT) coverage that the Sapini motor-vehicle matter
-- cites. Justia rows are left in place as a secondary fallback.
--
-- nysenate.gov statute URL shape: /legislation/laws/{LAWID}/{section}
--   e.g. https://www.nysenate.gov/legislation/laws/CVP/214   (CPLR 214)
--        https://www.nysenate.gov/legislation/laws/ISC/5102  (Insurance 5102)
--        https://www.nysenate.gov/legislation/laws/VAT/1192   (VTL 1192)
-- Law ids: CVP=CPLR, ISC=Insurance, GOB=General Obligations, LAB=Labor,
--          EPT=Estates Powers & Trusts, VAT=Vehicle & Traffic.
--
-- DATA ONLY. No schema, pipeline, scoring, or UI changes. Platform-scope
-- (TenantId NULL). Idempotent via the Provider/Jurisdiction/Kind existence guard.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'POLOXI') EXEC(N'CREATE SCHEMA POLOXI');

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource', N'U') IS NOT NULL
BEGIN
	DECLARE @NySystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

	-- ─────────────────────────────────────────────────────────────────────────
	-- Government-host (nysenate.gov) descriptors, mirroring the enabled
	-- California leginfo provider. CitationPatterns match BOTH the spelled-out
	-- ("New York Civil Practice Law and Rules § 214") and abbreviated
	-- ("N.Y. C.P.L.R. § 214") forms via the same \bN(?:ew\s+York|\.?Y\.?) prefix
	-- used by the NY_JUSTIA_* rows. DocumentUrlTemplate expands the captured
	-- section into the nysenate /legislation/laws/{LAWID}/{section} route.
	-- ─────────────────────────────────────────────────────────────────────────
	DECLARE @NySenate TABLE
	(
		ProviderCode        NVARCHAR(80)   NOT NULL,
		JurisdictionCode    NVARCHAR(40)   NOT NULL,
		AuthorityKindCode   NVARCHAR(40)   NOT NULL,
		CitationPattern     NVARCHAR(1000) NOT NULL,
		BaseUrl             NVARCHAR(1000) NOT NULL,
		DocumentUrlTemplate NVARCHAR(2000) NOT NULL,
		SectionAnchorTemplate NVARCHAR(400) NULL,
		ExtractionStrategyCode NVARCHAR(40) NOT NULL,
		IsEnabled           BIT            NOT NULL
	);

	INSERT @NySenate VALUES
	(N'NY_NYSENATE_CVP', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+(?:Civ(?:il)?\.?\s+Prac(?:tice)?\.?\s+Law\s*(?:and|&)?\s*Rules|C\.?P\.?L\.?R\.?)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://www.nysenate.gov', N'legislation/laws/CVP/{section}', NULL, N'FULL_PAGE_TEXT', 1),
	(N'NY_NYSENATE_ISC', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+Ins(?:urance)?\.?\s+Law\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://www.nysenate.gov', N'legislation/laws/ISC/{section}', NULL, N'FULL_PAGE_TEXT', 1),
	(N'NY_NYSENATE_GOB', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+Gen(?:eral)?\.?\s+Oblig(?:ations)?\.?\s+Law\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://www.nysenate.gov', N'legislation/laws/GOB/{section}', NULL, N'FULL_PAGE_TEXT', 1),
	(N'NY_NYSENATE_LAB', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+Lab(?:or)?\.?\s+Law\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://www.nysenate.gov', N'legislation/laws/LAB/{section}', NULL, N'FULL_PAGE_TEXT', 1),
	(N'NY_NYSENATE_EPT', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+(?:Est(?:ates)?\.?,?\s+Pow(?:ers)?\.?\s*(?:and|&)?\s*Tr(?:usts)?\.?\s+Law|E\.?P\.?T\.?L\.?)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://www.nysenate.gov', N'legislation/laws/EPT/{section}', NULL, N'FULL_PAGE_TEXT', 1),
	(N'NY_NYSENATE_VAT', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+(?:Veh(?:icle)?\.?\s*(?:and|&)?\s*Traf(?:fic)?\.?\s+Law|V\.?T\.?L\.?)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://www.nysenate.gov', N'legislation/laws/VAT/{section}', NULL, N'FULL_PAGE_TEXT', 1);

	-- Insert any missing nysenate descriptors (idempotent on Provider/Jurisdiction/Kind).
	-- Priority 400 => tried BEFORE the NY_JUSTIA_* fallback (Priority 500).
	INSERT POLOXI.Legal_AuthoritySource
	(
		LegalAuthoritySourceId, ProviderCode, JurisdictionCode, AuthorityKindCode, CitationPattern,
		BaseUrl, DocumentUrlTemplate, SectionAnchorTemplate, ExtractionStrategyCode,
		DiscoveryMethodCode, Priority, IsEnabled, TenantId, CreatedByUserId
	)
	SELECT
		NEWID(), s.ProviderCode, s.JurisdictionCode, s.AuthorityKindCode, s.CitationPattern,
		s.BaseUrl, s.DocumentUrlTemplate, s.SectionAnchorTemplate, s.ExtractionStrategyCode,
		N'MANUAL_SEED', 400, s.IsEnabled, NULL, @NySystemUserId
	FROM @NySenate s
	WHERE NOT EXISTS
	(
		SELECT 1 FROM POLOXI.Legal_AuthoritySource t
		WHERE t.ProviderCode = s.ProviderCode
		  AND t.JurisdictionCode = s.JurisdictionCode
		  AND t.AuthorityKindCode = s.AuthorityKindCode
		  AND t.TenantId IS NULL
		  AND t.IsDeleted = 0
	);

	-- Correct + (re-)ENABLE any pre-existing NY_NYSENATE_* rows (0391 disabled
	-- them) so re-runs converge to the government-host route at Priority 400.
	UPDATE t
		SET t.IsEnabled = 1,
			t.CitationPattern = s.CitationPattern,
			t.BaseUrl = s.BaseUrl,
			t.DocumentUrlTemplate = s.DocumentUrlTemplate,
			t.ExtractionStrategyCode = s.ExtractionStrategyCode,
			t.Priority = 400,
			t.ModifiedDateUtc = SYSUTCDATETIME()
	FROM POLOXI.Legal_AuthoritySource t
	INNER JOIN @NySenate s
		ON s.ProviderCode = t.ProviderCode
	   AND s.JurisdictionCode = t.JurisdictionCode
	   AND s.AuthorityKindCode = t.AuthorityKindCode
	WHERE t.TenantId IS NULL AND t.IsDeleted = 0;

	-- ─────────────────────────────────────────────────────────────────────────
	-- Add the missing Vehicle & Traffic Law (VAT) Justia fallback descriptor so
	-- the Sapini motor-vehicle citation also has a secondary provider, mirroring
	-- the existing NY_JUSTIA_* rows seeded in 0390/0391. Priority 500 (fallback).
	-- ─────────────────────────────────────────────────────────────────────────
	IF NOT EXISTS
	(
		SELECT 1 FROM POLOXI.Legal_AuthoritySource
		WHERE ProviderCode = N'NY_JUSTIA_VAT' AND JurisdictionCode = N'NAME:NEW YORK'
		  AND AuthorityKindCode = N'STATUTE' AND TenantId IS NULL AND IsDeleted = 0
	)
	BEGIN
		INSERT POLOXI.Legal_AuthoritySource
		(
			LegalAuthoritySourceId, ProviderCode, JurisdictionCode, AuthorityKindCode, CitationPattern,
			BaseUrl, DocumentUrlTemplate, SectionAnchorTemplate, ExtractionStrategyCode,
			DiscoveryMethodCode, Priority, IsEnabled, TenantId, CreatedByUserId
		)
		VALUES
		(
			NEWID(), N'NY_JUSTIA_VAT', N'NAME:NEW YORK', N'STATUTE',
			N'\bN(?:ew\s+York|\.?Y\.?)\s+(?:Veh(?:icle)?\.?\s*(?:and|&)?\s*Traf(?:fic)?\.?\s+Law|V\.?T\.?L\.?)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)',
			N'https://law.justia.com', N'codes/new-york/vat/section-{section:replace:.-}/', NULL, N'FULL_PAGE_TEXT',
			N'MANUAL_SEED', 500, 1, NULL, @NySystemUserId
		);
	END;
END;

COMMIT TRANSACTION;
