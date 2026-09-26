-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0333: Slugify the California Justia section number so the retrieval URL resolves instead of 404ing.
--
-- Background / root cause:
--   Migration 0332 repointed the 13 California CA_FINDLAW_* statute rows to Justia with a
--   DocumentUrlTemplate of 'codes/california/code-<code>/section-{section}/'. The bare '{section}'
--   token expands the citation section number verbatim, so 'Cal. Civ. Code § 377.61' produced
--   https://law.justia.com/codes/california/code-civ/section-377.61/ — but Justia's static section
--   pages slugify the dot to a hyphen (…/section-377-61/). The dotted URL 404s, so
--   OfficialLegalAuthorityRetriever recorded PROVIDER_FAILURE and returned 0 snippets. LegalRetriever
--   then fell through to the federal GovInfo/eCFR fan-out, which returned irrelevant U.S.-Code results
--   that all failed the identity gate (identityMatched=0). Net effect: Admitted Evidence stayed 0.
--
--   0332 is guarded by its own idempotency check AND is recorded in dbo._LegalMigrations after it first
--   runs, so editing 0332 in place is a no-op on any database where it already executed. This separate
--   forward-only migration is required to land the corrected template.
--
-- Fix:
--   Rewrite the DocumentUrlTemplate to expand the section through the '{section:replace:.-}' operator
--   (OfficialLegalAuthorityRetriever.ExpandTemplate), turning '377.61' into '377-61'. A same-host
--   redirect to a year-qualified path (…/2024/code-civ/…) is permitted by the retriever's redirect gate,
--   so the slug fix alone is sufficient. BaseUrl, CitationPattern, ExtractionStrategyCode, Priority, and
--   every other column are left untouched. Idempotent: only rows that still differ are touched.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	DECLARE @Slug TABLE
	(
		ProviderCode        NVARCHAR(80)   NOT NULL,
		DocumentUrlTemplate NVARCHAR(2000) NOT NULL
	);

	INSERT @Slug VALUES
	(N'CA_FINDLAW_VEH', N'codes/california/code-veh/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_CIV', N'codes/california/code-civ/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_PEN', N'codes/california/code-pen/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_PROB',N'codes/california/code-prob/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_EVID',N'codes/california/code-evid/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_BPC', N'codes/california/code-bpc/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_CORP',N'codes/california/code-corp/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_FAM', N'codes/california/code-fam/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_GOV', N'codes/california/code-gov/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_HSC', N'codes/california/code-hsc/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_INS', N'codes/california/code-ins/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_LAB', N'codes/california/code-lab/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_CCP', N'codes/california/code-ccp/section-{section:replace:.-}/');

	UPDATE t
	SET t.DocumentUrlTemplate = s.DocumentUrlTemplate,
		t.ModifiedDateUtc     = SYSUTCDATETIME()
	FROM POLOXI.Legal_AuthoritySource t
	INNER JOIN @Slug s
		ON s.ProviderCode = t.ProviderCode
	WHERE t.JurisdictionCode = N'NAME:CALIFORNIA'
	  AND t.AuthorityKindCode = N'STATUTE'
	  AND t.TenantId IS NULL
	  AND t.IsDeleted = 0
	  AND t.DocumentUrlTemplate <> s.DocumentUrlTemplate;
END

COMMIT TRANSACTION;
