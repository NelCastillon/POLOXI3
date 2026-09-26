-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0332: Repoint the already-seeded California CA_FINDLAW_* authority rows to Justia static-HTML.
--
-- Background / root cause:
--   Migration 0331 seeds the 13 California statutory descriptors and was later edited in-place to point
--   at Justia (https://law.justia.com) instead of FindLaw. But 0331 is INSERT-only and guarded by a
--   WHERE NOT EXISTS (Provider,Jurisdiction,Kind) existence check. Once 0331 has run once, the rows are
--   present, so re-running 0331 (or F5-launching the API after editing 0331) is a no-op — the existing
--   rows keep whatever BaseUrl / DocumentUrlTemplate they were first inserted with (FindLaw), and the
--   Justia repoint never lands in the database. California retrieval therefore still hits the old target,
--   extraction stays empty, and Admitted Evidence remains 0.
--
-- Fix:
--   This forward-only migration UPDATEs the existing global (TenantId NULL) CA_FINDLAW_* rows to the
--   Justia BaseUrl and per-code section-page DocumentUrlTemplate. The section number is expanded through
--   the '{section:replace:.-}' operator so the dot in California section numbers (e.g. 377.61) is slugified
--   to a hyphen (377-61) — Justia's static section pages live at /codes/california/<code>/section-<n-hyphen>/,
--   so a bare '{section}' produced a dotted URL that 404s (PROVIDER_FAILURE) and admitted zero evidence.
--   CitationPattern, ExtractionStrategyCode, Priority, and every other column are left untouched — only the
--   retrieval target changes. Idempotent: safe to run repeatedly; only rows that still differ are touched.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	DECLARE @Repoint TABLE
	(
		ProviderCode        NVARCHAR(80)   NOT NULL,
		BaseUrl             NVARCHAR(1000) NOT NULL,
		DocumentUrlTemplate NVARCHAR(2000) NOT NULL
	);

	INSERT @Repoint VALUES
	(N'CA_FINDLAW_VEH', N'https://law.justia.com', N'codes/california/code-veh/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_CIV', N'https://law.justia.com', N'codes/california/code-civ/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_PEN', N'https://law.justia.com', N'codes/california/code-pen/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_PROB',N'https://law.justia.com', N'codes/california/code-prob/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_EVID',N'https://law.justia.com', N'codes/california/code-evid/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_BPC', N'https://law.justia.com', N'codes/california/code-bpc/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_CORP',N'https://law.justia.com', N'codes/california/code-corp/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_FAM', N'https://law.justia.com', N'codes/california/code-fam/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_GOV', N'https://law.justia.com', N'codes/california/code-gov/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_HSC', N'https://law.justia.com', N'codes/california/code-hsc/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_INS', N'https://law.justia.com', N'codes/california/code-ins/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_LAB', N'https://law.justia.com', N'codes/california/code-lab/section-{section:replace:.-}/'),
	(N'CA_FINDLAW_CCP', N'https://law.justia.com', N'codes/california/code-ccp/section-{section:replace:.-}/');

	UPDATE t
	SET t.BaseUrl             = r.BaseUrl,
		t.DocumentUrlTemplate = r.DocumentUrlTemplate,
		t.ModifiedDateUtc     = SYSUTCDATETIME()
	FROM POLOXI.Legal_AuthoritySource t
	INNER JOIN @Repoint r
		ON r.ProviderCode = t.ProviderCode
	WHERE t.JurisdictionCode = N'NAME:CALIFORNIA'
	  AND t.AuthorityKindCode = N'STATUTE'
	  AND t.TenantId IS NULL
	  AND t.IsDeleted = 0
	  AND (t.BaseUrl <> r.BaseUrl OR t.DocumentUrlTemplate <> r.DocumentUrlTemplate);
END

COMMIT TRANSACTION;
