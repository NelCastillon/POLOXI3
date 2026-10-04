-- ============================================================================
-- 0391: New York authority-retrieval correction (forward-only fix for 0390).
--
-- WHY: Migration 0390 originally seeded New York concept authorities with
-- ABBREVIATED Bluebook CitationText (e.g. "N.Y. C.P.L.R. § 214") and guessed
-- nysenate.gov source descriptors (NY_NYSENATE_*). California, which retrieves
-- and admits evidence correctly, instead uses:
--   * Spelled-out CitationText ("California Code of Civil Procedure § 998") so the
--     jurisdiction detector resolves the state, and
--   * ENABLED per-code Legal_AuthoritySource descriptors on Justia static-HTML
--     routes (CA_FINDLAW_*, migrations 0329/0331).
--
-- 0390 was edited in source to match California, but databases that already
-- recorded 0390 as applied will NOT pick up those edits. This forward-only
-- migration re-applies the corrections idempotently so every environment ends up
-- identical:
--   1. Rewrites New York Legal_LegalConceptAuthority CitationText to the fully
--      spelled-out form (matching the California convention).
--   2. Retires the guessed NY_NYSENATE_* and stale NY_CONS_LAWS source rows.
--   3. Seeds ENABLED NY_JUSTIA_* descriptors mirroring the working CA_FINDLAW_*
--      rows (Justia routes + dual spelled-out/abbreviated CitationPatterns).
--
-- DATA ONLY. Pipeline mechanics, admission gates, POLOXI Core scoring, and the
-- UI are UNCHANGED. All rows are platform-scope (TenantId NULL), idempotent.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'POLOXI') EXEC(N'CREATE SCHEMA POLOXI');

-- ───────────────────────────────────────────────────────────────────────────
-- 1. Correct New York concept-authority CitationText to the spelled-out form.
--    Keyed on the stable VerificationTokens (section number) + SourceLabel so a
--    re-run is a no-op once values already match. Only touches platform rows.
-- ───────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_LegalConceptAuthority', N'U') IS NOT NULL
BEGIN
	UPDATE t
		SET t.CitationText = s.CitationText,
			t.ModifiedDateUtc = SYSUTCDATETIME()
	FROM POLOXI.Legal_LegalConceptAuthority t
	INNER JOIN (VALUES
		(N'NY CPLR', N'214',    N'New York Civil Practice Law and Rules § 214'),
		(N'NY CPLR', N'214-a',  N'New York Civil Practice Law and Rules § 214-a'),
		(N'NY CPLR', N'217',    N'New York Civil Practice Law and Rules § 217'),
		(N'NY CPLR', N'1411',   N'New York Civil Practice Law and Rules § 1411'),
		(N'NY CPLR', N'1601',   N'New York Civil Practice Law and Rules § 1601'),
		(N'NY CPLR', N'3212',   N'New York Civil Practice Law and Rules § 3212'),
		(N'NY Ins',  N'5102',   N'New York Insurance Law § 5102'),
		(N'NY Ins',  N'5104',   N'New York Insurance Law § 5104'),
		(N'NY GOL',  N'15-108', N'New York General Obligations Law § 15-108'),
		(N'NY GOL',  N'11-101', N'New York General Obligations Law § 11-101'),
		(N'NY Labor',N'240',    N'New York Labor Law § 240'),
		(N'NY Labor',N'241',    N'New York Labor Law § 241'),
		(N'NY EPTL', N'5-4.1',  N'New York Estates, Powers and Trusts Law § 5-4.1')
	) AS s (SourceLabel, VerificationTokens, CitationText)
		ON t.SourceLabel = s.SourceLabel
	   AND t.VerificationTokens = s.VerificationTokens
	WHERE t.TenantId IS NULL
	  AND t.IsDeleted = 0
	  AND t.CitationText <> s.CitationText;
END;

-- ───────────────────────────────────────────────────────────────────────────
-- 2. New York authority SOURCE descriptors — THE RETRIEVAL FIX.
--    Mirrors the WORKING California CA_FINDLAW_* rows (0329/0331): Justia
--    static-HTML routes plus CitationPatterns that accept BOTH the spelled-out
--    ("New York Civil Practice Law and Rules §") and abbreviated ("N.Y. C.P.L.R.
--    §") citation forms via the same \bN(?:ew\s+York|\.?Y\.?) alternation prefix.
--    Global (TenantId NULL). Idempotent via the Provider/Jurisdiction/Kind guard.
-- ───────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource', N'U') IS NOT NULL
BEGIN
	DECLARE @NySystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

	-- Retire the stale generic NY_CONS_LAWS row AND the guessed nysenate rows from
	-- the first 0390 attempt so a broken/guessed match can never win.
	UPDATE POLOXI.Legal_AuthoritySource
		SET IsEnabled = 0, ModifiedDateUtc = SYSUTCDATETIME()
		WHERE JurisdictionCode = N'NAME:NEW YORK'
		  AND TenantId IS NULL AND IsDeleted = 0 AND IsEnabled = 1
		  AND ProviderCode IN (
			N'NY_CONS_LAWS',
			N'NY_NYSENATE_CVP', N'NY_NYSENATE_ISC', N'NY_NYSENATE_GOB',
			N'NY_NYSENATE_LAB', N'NY_NYSENATE_EPT');

	DECLARE @NySeed TABLE
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

	-- Justia NY law slugs: cvp (CPLR), isc (Insurance), gob (Gen. Oblig.),
	-- lab (Labor), ept (EPTL). Patterns mirror California's CA_FINDLAW_* style.
	INSERT @NySeed VALUES
	(N'NY_JUSTIA_CVP', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+(?:Civ(?:il)?\.?\s+Prac(?:tice)?\.?\s+Law\s*(?:and|&)?\s*Rules|C\.?P\.?L\.?R\.?)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://law.justia.com', N'codes/new-york/cvp/article-{section}/', NULL, N'FULL_PAGE_TEXT', 1),
	(N'NY_JUSTIA_ISC', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+Ins(?:urance)?\.?\s+Law\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://law.justia.com', N'codes/new-york/isc/section-{section}/', NULL, N'FULL_PAGE_TEXT', 1),
	(N'NY_JUSTIA_GOB', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+Gen(?:eral)?\.?\s+Oblig(?:ations)?\.?\s+Law\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://law.justia.com', N'codes/new-york/gob/section-{section}/', NULL, N'FULL_PAGE_TEXT', 1),
	(N'NY_JUSTIA_LAB', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+Lab(?:or)?\.?\s+Law\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://law.justia.com', N'codes/new-york/lab/section-{section}/', NULL, N'FULL_PAGE_TEXT', 1),
	(N'NY_JUSTIA_EPT', N'NAME:NEW YORK', N'STATUTE', N'\bN(?:ew\s+York|\.?Y\.?)\s+(?:Est(?:ates)?\.?,?\s+Pow(?:ers)?\.?\s*(?:and|&)?\s*Tr(?:usts)?\.?\s+Law|E\.?P\.?T\.?L\.?)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)', N'https://law.justia.com', N'codes/new-york/ept/section-{section}/', NULL, N'FULL_PAGE_TEXT', 1);

	-- Insert any missing descriptors (idempotent on Provider/Jurisdiction/Kind).
	INSERT POLOXI.Legal_AuthoritySource
	(
		LegalAuthoritySourceId, ProviderCode, JurisdictionCode, AuthorityKindCode, CitationPattern,
		BaseUrl, DocumentUrlTemplate, SectionAnchorTemplate, ExtractionStrategyCode,
		DiscoveryMethodCode, Priority, IsEnabled, TenantId, CreatedByUserId
	)
	SELECT
		NEWID(), s.ProviderCode, s.JurisdictionCode, s.AuthorityKindCode, s.CitationPattern,
		s.BaseUrl, s.DocumentUrlTemplate, s.SectionAnchorTemplate, s.ExtractionStrategyCode,
		N'MANUAL_SEED', 500, s.IsEnabled, NULL, @NySystemUserId
	FROM @NySeed s
	WHERE NOT EXISTS
	(
		SELECT 1 FROM POLOXI.Legal_AuthoritySource t
		WHERE t.ProviderCode = s.ProviderCode
		  AND t.JurisdictionCode = s.JurisdictionCode
		  AND t.AuthorityKindCode = s.AuthorityKindCode
		  AND t.TenantId IS NULL
		  AND t.IsDeleted = 0
	);

	-- Correct + (re-)enable any pre-existing NY_JUSTIA_* rows so re-runs converge.
	UPDATE t
		SET t.IsEnabled = 1,
			t.CitationPattern = s.CitationPattern,
			t.BaseUrl = s.BaseUrl,
			t.DocumentUrlTemplate = s.DocumentUrlTemplate,
			t.ExtractionStrategyCode = s.ExtractionStrategyCode,
			t.Priority = 500,
			t.ModifiedDateUtc = SYSUTCDATETIME()
	FROM POLOXI.Legal_AuthoritySource t
	INNER JOIN @NySeed s
		ON s.ProviderCode = t.ProviderCode
	   AND s.JurisdictionCode = t.JurisdictionCode
	   AND s.AuthorityKindCode = t.AuthorityKindCode
	WHERE t.TenantId IS NULL AND t.IsDeleted = 0;
END;

-- ───────────────────────────────────────────────────────────────────────────
-- 3. Configure the Sapini matter (0387) to run as New York.
--    The Sapini matter was seeded with a free-text venue string
--    ("Supreme Court of the State of New York, Westchester County") that does NOT
--    match the seeded JURISDICTION dropdown option. Align its Jurisdiction field
--    to the canonical seeded value so the matter's jurisdiction detector resolves
--    New York -> NAME:NEW YORK and the NY_JUSTIA_* descriptors engage, exactly
--    like California. Keyed on the deterministic Sapini matter id; idempotent.
-- ───────────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionMatter', N'U') IS NOT NULL
BEGIN
	DECLARE @SapiniMatter UNIQUEIDENTIFIER = N'A3000003-0000-0000-0000-000000000001';

	UPDATE POLOXI.Legal_DecisionMatter
		SET Jurisdiction = N'New York - Supreme Court, Westchester County',
			ModifiedDateUtc = SYSUTCDATETIME()
		WHERE DecisionMatterId = @SapiniMatter
		  AND IsDeleted = 0
		  AND Jurisdiction <> N'New York - Supreme Court, Westchester County';
END;

COMMIT TRANSACTION;
