-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0326: Nevada (NRS) statutory authority source descriptor for the DB-backed legal grounding registry.
--
-- Continues the "scale by DATA, not code" pattern established by migration 0325 (Washington RCW). A
-- Nevada matter (e.g. "NRS 41.130", "Nev. Rev. Stat. § 41.130", "Nevada Revised Statutes 41.130")
-- previously resolved to OFFICIAL_AUTHORITY outcome=COVERAGE_GAP because POLOXI.Legal_AuthoritySource
-- had no Nevada row, so ExternalEvidenceCount stayed 0 and the Decision Intelligence "Admitted evidence"
-- KPI stayed 0. This adds coverage as a single global registry row — no C# change, no redeploy.
--
-- Nevada URL nuance handled generically: the Nevada Legislature publishes each NRS chapter at
--   https://www.leg.state.nv.us/NRS/NRS-041.html#NRS041Sec130   (for NRS 41.130)
-- where the chapter is ZERO-PADDED to three digits (41 -> 041) in BOTH the file name and the anchor id.
-- To keep this a pure data row, the OfficialLegalAuthorityRetriever template engine was extended with a
-- generic {token:pad:N} left-zero-pad operation (companion to the existing {token:prefix:N} truncate).
-- That capability is reusable by every future state with a padded-chapter URL scheme; it is NOT
-- Nevada-specific. The citation regex captures <chapter> and <section> separately so the template can
-- pad the chapter while leaving the section intact.
--
-- Extraction: HTML_ID_SECTION isolates the exact section by its anchor id (NRS{chapter}Sec{section})
-- rather than returning the entire chapter page, matching how HTML_ID_SECTION is consumed in Extract().
--
-- Global (TenantId NULL) seed. Idempotent via the ProviderCode/JurisdictionCode/AuthorityKind existence
-- guard (UX_Legal_AuthoritySource_ProviderJurisdictionKind, migration 0295). Pipeline mechanics,
-- admission gates, POLOXI Core scoring, and the Blazor UI are UNCHANGED.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000000';

	-- Idempotent: only insert when no global Nevada statutory descriptor exists yet.
	IF NOT EXISTS
	(
		SELECT 1 FROM POLOXI.Legal_AuthoritySource
		WHERE ProviderCode=N'NV_LEG_NRS'
		  AND JurisdictionCode=N'NAME:NEVADA'
		  AND AuthorityKindCode=N'STATUTE'
		  AND TenantId IS NULL
		  AND IsDeleted=0
	)
	BEGIN
		INSERT POLOXI.Legal_AuthoritySource
		(
			LegalAuthoritySourceId,ProviderCode,JurisdictionCode,AuthorityKindCode,CitationPattern,
			BaseUrl,DocumentUrlTemplate,SectionAnchorTemplate,ExtractionStrategyCode,
			DiscoveryMethodCode,Priority,IsEnabled,TenantId,CreatedByUserId
		)
		VALUES
		(
			NEWID(),
			N'NV_LEG_NRS',
			N'NAME:NEVADA',
			N'STATUTE',
			-- Matches "NRS 41.130", "Nev. Rev. Stat. § 41.130", "Nevada Revised Statutes 41.130".
			-- <chapter> = digits before the dot, <section> = digits after; the template pads <chapter>.
			N'\b(?:NRS|Nev(?:ada)?\.?\s+Rev(?:ised)?\.?\s+Stat(?:utes)?\.?)\s*(?:§+\s*|section\s+|sec\.?\s+)?(?<chapter>\d+)\.(?<section>\d[\dA-Za-z]*)',
			N'https://www.leg.state.nv.us',
			-- Chapter zero-padded to 3 digits: NRS 41.130 -> NRS/NRS-041.html
			N'NRS/NRS-{chapter:pad:3}.html',
			-- Section anchor on the chapter page: #NRS041Sec130
			N'NRS{chapter:pad:3}Sec{section}',
			N'HTML_ID_SECTION',
			N'MANUAL_SEED',
			-- Same priority band as the Washington and built-in California seeds.
			500,
			1,
			NULL,
			@SystemUserId
		);
	END
END

COMMIT TRANSACTION;
