-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0325: Washington (RCW) statutory authority source descriptor for the DB-backed legal grounding registry.
--
-- Root cause this closes: the JudzRetrievalDiagnostic LEGAL-TRACE run proved that legal grounding is
-- ENABLED and healthy (California Vehicle Code § 22350 returns RESULTS_FOUND via the built-in
-- CA_LEGINFO_* seeds), but any NON-California statutory matter — for example the Carlos Mendoza v.
-- Northwind Trucking (Washington premises-liability / negligence-per-se) matter — resolves to
-- OFFICIAL_AUTHORITY outcome=COVERAGE_GAP because POLOXI.Legal_AuthoritySource has no Washington row.
-- GatherExternalKnowledgeAsync then collects zero snippets, ExternalEvidenceCount=0, and the Decision
-- Intelligence "Admitted evidence" KPI stays at 0. This is a coverage-data gap, NOT a UI, admission-gate,
-- or hierarchy defect (the Wide2 hierarchy correctly produces 23 atomic factors).
--
-- Design decision — SCALE BY DATA, NOT CODE: the California coverage was bolted in as hardcoded C#
-- (OfficialLegalAuthorityRetriever.BuildCaliforniaSources). That does not scale — every new state would
-- require new code + a new build. This migration instead seeds the AUTHORITATIVE, DB-backed registry
-- table POLOXI.Legal_AuthoritySource that OfficialLegalAuthorityRetriever already consults FIRST
-- (registry descriptors take precedence over the built-in California seeds). Consequence for "what
-- happens when the next case is another state?": adding Texas, Florida, New York, etc. becomes a single
-- INSERT of one descriptor row (or one admin-UI entry) — no code change, no redeploy. This row is the
-- reference pattern for every future state.
--
-- Why ONE row covers all of Washington: unlike California's leginfo (which needs a distinct lawCode per
-- code, hence 13 CA_LEGINFO_* descriptors), the Washington State Legislature publishes every RCW title
-- under a single cite parameter — https://app.leg.wa.gov/RCW/default.aspx?cite=<Title.Chapter.Section> —
-- so one descriptor with cite={section} resolves any RCW citation (e.g. "RCW 5.40.050",
-- "Wash. Rev. Code § 4.24.630", "Revised Code of Washington 9A.36.021").
--
-- Global (TenantId NULL) seed. Idempotent via the ProviderCode/JurisdictionCode/AuthorityKind existence
-- guard, matching the UX_Legal_AuthoritySource_ProviderJurisdictionKind uniqueness contract from
-- migration 0295. Schema, pipeline mechanics, admission gates, POLOXI Core scoring, and the Blazor UI
-- are UNCHANGED.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000000';

	-- Idempotent: only insert when no global Washington statutory descriptor exists yet.
	IF NOT EXISTS
	(
		SELECT 1 FROM POLOXI.Legal_AuthoritySource
		WHERE ProviderCode=N'WA_LEG_RCW'
		  AND JurisdictionCode=N'NAME:WASHINGTON'
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
			N'WA_LEG_RCW',
			N'NAME:WASHINGTON',
			N'STATUTE',
			-- Matches "RCW 5.40.050", "Wash. Rev. Code § 4.24.630",
			-- "Washington Revised Code 9A.36.021", "Revised Code of Washington § 7.70.040".
			-- The named group <section> captures the Title.Chapter.Section cite used by the URL template.
			N'\b(?:RCW|Wash(?:ington)?\.?\s+Rev(?:ised)?\.?\s+Code|Revised\s+Code\s+of\s+Washington)\s*(?:§+\s*|section\s+|sec\.?\s+)?(?<section>\d[\dA-Za-z.]*)',
			N'https://app.leg.wa.gov',
			-- Single cite parameter resolves any RCW Title.Chapter.Section on the authoritative host.
			N'RCW/default.aspx?cite={section}',
			NULL,
			N'FULL_PAGE_TEXT',
			N'MANUAL_SEED',
			-- Lower Priority wins; keep in the same band as the built-in California seeds (500).
			500,
			1,
			NULL,
			@SystemUserId
		);
	END
END

COMMIT TRANSACTION;
