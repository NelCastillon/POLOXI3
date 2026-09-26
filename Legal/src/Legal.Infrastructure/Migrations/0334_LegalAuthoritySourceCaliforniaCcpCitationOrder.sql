-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0334: Broaden the California Code of Civil Procedure citation pattern so both Bluebook orderings resolve.
--
-- Background / root cause:
--   The CCP descriptors (CA_FINDLAW_CCP static-HTML/Justia and CA_LEGINFO_CCP leginfo) carry a
--   CitationPattern that only accepts the "code word first" ordering, e.g.
--     Cal. Code Civ. Proc. § 377.32   /   California Code of Civil Procedure § 377.32
--   A very common real-world form places "Code" LAST:
--     Cal. Civ. Proc. Code 377.32
--   OfficialLegalAuthorityRetriever.MatchDescriptors runs each descriptor's CitationPattern verbatim
--   against the query. The "Code last" form matched no descriptor, so the retriever fell through to
--   LegalJurisdictionDetector, whose NamedCodeCitation regex greedily captured "Civ. Proc." as the
--   jurisdiction NAME. That produced stage=3-authority-bootstrap outcome=NO_TRUSTED_SOURCE
--   jurisdiction=NAME:CAL. CIV. PROC. and stage=2-exact-authority-empty outcome=NO_CITATIONS_RESOLVED,
--   so the CCP statute (e.g. Cal. Civ. Proc. Code 377.32) was never retrieved.
--
--   The seed rows already exist in every deployed database (0328/0329/0331), and those migrations are
--   guarded by idempotency checks and recorded in dbo._LegalMigrations, so editing them in place is a
--   no-op. This separate forward-only migration lands the corrected pattern.
--
-- Fix:
--   Rewrite the CitationPattern for both CCP descriptors to accept BOTH orderings via an alternation:
--     (?:Code\s+(?:of\s+)?Civ(?:il)?\.?\s+Proc(?:edure)?\.?|Civ(?:il)?\.?\s+Proc(?:edure)?\.?\s+Code)
--   The section token, jurisdiction, kind, URL templates, priority and every other column are left
--   untouched. Idempotent: only rows that still differ are touched.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource',N'U') IS NOT NULL
BEGIN
	DECLARE @Ccp TABLE
	(
		ProviderCode    NVARCHAR(80)   NOT NULL,
		CitationPattern NVARCHAR(2000) NOT NULL
	);

	INSERT @Ccp VALUES
	(N'CA_FINDLAW_CCP', N'\bCal(?:ifornia|\.)?\s+(?:Code\s+(?:of\s+)?Civ(?:il)?\.?\s+Proc(?:edure)?\.?|Civ(?:il)?\.?\s+Proc(?:edure)?\.?\s+Code)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)'),
	(N'CA_LEGINFO_CCP', N'\bCal(?:ifornia|\.)?\s+(?:Code\s+(?:of\s+)?Civ(?:il)?\.?\s+Proc(?:edure)?\.?|Civ(?:il)?\.?\s+Proc(?:edure)?\.?\s+Code)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)');

	UPDATE t
	SET t.CitationPattern = c.CitationPattern,
		t.ModifiedDateUtc  = SYSUTCDATETIME()
	FROM POLOXI.Legal_AuthoritySource t
	INNER JOIN @Ccp c
		ON c.ProviderCode = t.ProviderCode
	WHERE t.JurisdictionCode = N'NAME:CALIFORNIA'
	  AND t.AuthorityKindCode = N'STATUTE'
	  AND t.TenantId IS NULL
	  AND t.IsDeleted = 0
	  AND t.CitationPattern <> c.CitationPattern;
END

COMMIT TRANSACTION;
