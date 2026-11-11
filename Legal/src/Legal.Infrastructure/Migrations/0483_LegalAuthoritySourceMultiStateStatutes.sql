-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- 0483: New York, Florida, and Texas statutory authority-source descriptors for the DB-backed legal
-- grounding registry (POLOXI.Legal_AuthoritySource).
--
-- Root cause this closes: the California statute-extraction fix (LegalCaliforniaStatuteCitationRegex)
-- and the Washington seed (migration 0325) only grounded CA + WA. Every other state's statutory
-- citation was extracted-but-unresolvable (OFFICIAL_AUTHORITY outcome=COVERAGE_GAP) because the
-- registry had no descriptor row, so GatherExternalKnowledgeAsync collected zero snippets. The paired
-- code change (LegalStateStatuteCitationRegex added to BOTH IntelligenceWide2Service.LegalAuthorities.cs
-- and IntelligenceWideService.LegalAuthorities.cs) now EXTRACTS non-California state citations; this
-- migration gives the three highest-volume PI jurisdictions an authoritative RETRIEVAL target so those
-- extracted citations resolve to official full-text pages.
--
-- Design decision — SCALE BY DATA, NOT CODE (same principle as 0325): adding any further state is a
-- single INSERT (or one admin-UI entry) — no code change, no redeploy. These three rows are the
-- reference pattern for every remaining state.
--
-- Fail-soft contract: ExtractionStrategyCode=FULL_PAGE_TEXT with a best-effort official deep-link
-- template. A wrong/edge-case URL simply yields no snippet (the mandatory identity gate then drops the
-- lead) — it can NEVER inject wrong evidence or inflate confidence. Templates are admin-refineable via
-- the Legal Authority Sources registry UI.
--
-- Global (TenantId NULL) seeds. Idempotent via the ProviderCode/JurisdictionCode/AuthorityKind
-- existence guard, matching the UX_Legal_AuthoritySource_ProviderJurisdictionKind uniqueness contract
-- from migration 0295. Schema, pipeline mechanics, admission gates, POLOXI Core scoring, and the Blazor
-- UI are UNCHANGED.
-- ────────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	DECLARE @SystemUserId UNIQUEIDENTIFIER=N'00000000-0000-0000-0000-000000000000';

	-- ── New York — Civil Practice Law & Rules (CPLR). Dominant NY PI citation (e.g. comparative fault
	--    "N.Y. C.P.L.R. § 1411", "CPLR 1411"). The official NY Senate host serves CPLR under the CVP law
	--    code: https://www.nysenate.gov/legislation/laws/CVP/1411. ────────────────────────────────────
	IF NOT EXISTS
	(
		SELECT 1 FROM POLOXI.Legal_AuthoritySource
		WHERE ProviderCode=N'NY_SENATE_CPLR'
		  AND JurisdictionCode=N'NAME:NEW YORK'
		  AND AuthorityKindCode=N'STATUTE'
		  AND TenantId IS NULL
		  AND IsDeleted=0
	)
		INSERT POLOXI.Legal_AuthoritySource
		(
			LegalAuthoritySourceId,ProviderCode,JurisdictionCode,AuthorityKindCode,CitationPattern,
			BaseUrl,DocumentUrlTemplate,SectionAnchorTemplate,ExtractionStrategyCode,
			DiscoveryMethodCode,Priority,IsEnabled,TenantId,CreatedByUserId
		)
		VALUES
		(
			NEWID(),
			N'NY_SENATE_CPLR',
			N'NAME:NEW YORK',
			N'STATUTE',
			-- Matches "N.Y. C.P.L.R. § 1411", "CPLR 1411", "New York Civil Practice Law and Rules 1411".
			N'\b(?:N\.?\s?Y\.?\s+)?(?:C\.?\s?P\.?\s?L\.?\s?R\.?|Civil\s+Practice\s+Law\s+(?:and|&)\s+Rules)\s*(?:§+\s*|section\s+|sec\.?\s+)?(?<section>\d[\dA-Za-z.\-]*)',
			N'https://www.nysenate.gov',
			N'legislation/laws/CVP/{section}',
			NULL,
			N'FULL_PAGE_TEXT',
			N'MANUAL_SEED',
			500,
			1,
			NULL,
			@SystemUserId
		);

	-- ── Florida — Florida Statutes. The official Online Sunshine host resolves any section under a single
	--    cite: https://www.flsenate.gov/Laws/Statutes/{section} (redirects to the current edition). ─────
	IF NOT EXISTS
	(
		SELECT 1 FROM POLOXI.Legal_AuthoritySource
		WHERE ProviderCode=N'FL_LEG_STATUTES'
		  AND JurisdictionCode=N'NAME:FLORIDA'
		  AND AuthorityKindCode=N'STATUTE'
		  AND TenantId IS NULL
		  AND IsDeleted=0
	)
		INSERT POLOXI.Legal_AuthoritySource
		(
			LegalAuthoritySourceId,ProviderCode,JurisdictionCode,AuthorityKindCode,CitationPattern,
			BaseUrl,DocumentUrlTemplate,SectionAnchorTemplate,ExtractionStrategyCode,
			DiscoveryMethodCode,Priority,IsEnabled,TenantId,CreatedByUserId
		)
		VALUES
		(
			NEWID(),
			N'FL_LEG_STATUTES',
			N'NAME:FLORIDA',
			N'STATUTE',
			-- Matches "Fla. Stat. § 768.81", "Florida Statutes 768.81", "Fla. Stat. Ann. § 95.11".
			N'\bFla(?:\.|orida)?\s+Stat(?:utes?)?\.?(?:\s+Ann(?:otated)?\.?)?\s*(?:§+\s*|section\s+|sec\.?\s+)?(?<section>\d[\dA-Za-z.\-]*)',
			N'https://www.flsenate.gov',
			N'Laws/Statutes/{section}',
			NULL,
			N'FULL_PAGE_TEXT',
			N'MANUAL_SEED',
			500,
			1,
			NULL,
			@SystemUserId
		);

	-- ── Texas — Civil Practice & Remedies Code (dominant TX PI citation, e.g. proportionate responsibility
	--    "Tex. Civ. Prac. & Rem. Code § 33.001"). The official Texas Statutes host serves each code chapter
	--    as a page: https://statutes.capitol.texas.gov/Docs/CP/htm/CP.{chapter}.htm, where {chapter} is the
	--    integer part of the section. The named <chapter> group captures that part for the URL template. ──
	IF NOT EXISTS
	(
		SELECT 1 FROM POLOXI.Legal_AuthoritySource
		WHERE ProviderCode=N'TX_LEG_CPRC'
		  AND JurisdictionCode=N'NAME:TEXAS'
		  AND AuthorityKindCode=N'STATUTE'
		  AND TenantId IS NULL
		  AND IsDeleted=0
	)
		INSERT POLOXI.Legal_AuthoritySource
		(
			LegalAuthoritySourceId,ProviderCode,JurisdictionCode,AuthorityKindCode,CitationPattern,
			BaseUrl,DocumentUrlTemplate,SectionAnchorTemplate,ExtractionStrategyCode,
			DiscoveryMethodCode,Priority,IsEnabled,TenantId,CreatedByUserId
		)
		VALUES
		(
			NEWID(),
			N'TX_LEG_CPRC',
			N'NAME:TEXAS',
			N'STATUTE',
			-- Matches "Tex. Civ. Prac. & Rem. Code § 33.001", "Texas Civil Practice and Remedies Code 33.001".
			N'\bTex(?:\.|as)?\s+Civ(?:il)?\.?\s+Prac(?:tice)?\.?\s+(?:and|&)\s+Rem(?:edies)?\.?\s+Code(?:\s+Ann(?:otated)?\.?)?\s*(?:§+\s*|section\s+|sec\.?\s+)?(?<chapter>\d+)(?:\.\d[\dA-Za-z.\-]*)?',
			N'https://statutes.capitol.texas.gov',
			N'Docs/CP/htm/CP.{chapter}.htm',
			NULL,
			N'FULL_PAGE_TEXT',
			N'MANUAL_SEED',
			500,
			1,
			NULL,
			@SystemUserId
		);
END

COMMIT TRANSACTION;
