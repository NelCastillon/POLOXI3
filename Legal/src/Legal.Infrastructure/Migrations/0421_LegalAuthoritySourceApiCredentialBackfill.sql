SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0421: Back-fill per-source API credential columns (added in 0420) for the
--       existing JSON-API authority sources — the NY Senate Open Legislation
--       JSON API descriptors (NY_NYSENATE_*, ExtractionStrategyCode='JSON_NYSENATE').
--
-- WHY: 0394 repointed the NY_NYSENATE_* rows to the authenticated NY Senate Open
-- Legislation JSON API (https://legislation.nysenate.gov/api/3/laws/{LAWID}/{section})
-- but, at that time, the registry had no per-source credential columns, so the
-- host lived in BaseUrl and the API key was resolved only from the global config
-- key Legal:OfficialAuthority:NySenateApiKey. 0420 added ApiEndpoint / ApiAccountId
-- / ApiKey. This migration populates ApiEndpoint on those rows so the API host is
-- explicit per-source and the generic retriever path (which prefers ApiEndpoint
-- over BaseUrl) resolves identically.
--
-- WHAT: For every enabled, platform-scope (TenantId NULL) NY_NYSENATE_* STATUTE
-- row whose ExtractionStrategyCode is JSON_NYSENATE, set:
--   * ApiEndpoint = https://legislation.nysenate.gov   (same host as BaseUrl)
-- The secret ApiKey is deliberately left NULL: the NY Senate key continues to be
-- injected at request time from configuration (Legal:OfficialAuthority:NySenateApiKey),
-- so no secret is persisted in the database. ApiAccountId is not required by this API.
--
-- DATA ONLY. Idempotent: converges to the same ApiEndpoint on every run and only
-- touches rows that still lack the value. Platform-scope (TenantId NULL).
-- ============================================================================

BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource', N'U') IS NOT NULL
   AND COL_LENGTH(N'POLOXI.Legal_AuthoritySource', N'ApiEndpoint') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_AuthoritySource
		SET ApiEndpoint     = N'https://legislation.nysenate.gov',
			ModifiedDateUtc  = SYSUTCDATETIME()
	WHERE JurisdictionCode     = N'NAME:NEW YORK'
	  AND AuthorityKindCode     = N'STATUTE'
	  AND ExtractionStrategyCode = N'JSON_NYSENATE'
	  AND ProviderCode LIKE N'NY_NYSENATE[_]%'
	  AND TenantId IS NULL
	  AND IsDeleted = 0
	  AND (ApiEndpoint IS NULL OR ApiEndpoint <> N'https://legislation.nysenate.gov');
END;

COMMIT TRANSACTION;
