SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ─────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Per-source API credentials for JSON/API authority sources.
-- Adds configurable API connection fields to POLOXI.Legal_AuthoritySource so an
-- authority source can be driven by a JSON API (host/endpoint override, account/
-- user identifier, and API key/token) instead of relying on a single hardcoded
-- global configuration key. All columns are nullable so existing HTML/curated
-- rows are unaffected. Idempotent.
--   ApiEndpoint  — optional host/endpoint base override (used instead of BaseUrl
--                  for the API call when present).
--   ApiAccountId — optional user id / account id (non-secret identifier).
--   ApiKey       — optional API key / token / token key (secret).
-- ─────────────────────────────────────────────────────────────────────────────

BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.Legal_AuthoritySource', N'ApiEndpoint') IS NULL
	ALTER TABLE POLOXI.Legal_AuthoritySource ADD ApiEndpoint NVARCHAR(1000) NULL;

IF COL_LENGTH(N'POLOXI.Legal_AuthoritySource', N'ApiAccountId') IS NULL
	ALTER TABLE POLOXI.Legal_AuthoritySource ADD ApiAccountId NVARCHAR(200) NULL;

IF COL_LENGTH(N'POLOXI.Legal_AuthoritySource', N'ApiKey') IS NULL
	ALTER TABLE POLOXI.Legal_AuthoritySource ADD ApiKey NVARCHAR(500) NULL;

COMMIT TRANSACTION;
