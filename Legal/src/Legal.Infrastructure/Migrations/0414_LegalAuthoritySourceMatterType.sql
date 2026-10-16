SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ─────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Matter-Type-aware Legal Authority Source registry.
-- Extends POLOXI.Legal_AuthoritySource (created in 0295) so authority sources can
-- be curated per Provider × Jurisdiction (State) × Authority Kind × Matter Type.
-- Closes the per-state STATUTE / REGULATION coverage gap that CourtListener +
-- GovInfo + Cornell LII do not fully cover, using DB-backed curated sources
-- (Justia state codes + eCFR) — no mock/hardcoded application data.
-- Idempotent: column add guarded, index rebuild guarded, seeds via MERGE.
-- Each GO-separated batch is self-contained (the migrator splits on GO and runs
-- batches on one connection without an outer transaction).
-- ─────────────────────────────────────────────────────────────────────────────

-- ── 1. Add MatterTypeCode column (NULL / '*' = applies to all matter types) ──
IF COL_LENGTH(N'POLOXI.Legal_AuthoritySource', N'MatterTypeCode') IS NULL
	ALTER TABLE POLOXI.Legal_AuthoritySource ADD MatterTypeCode NVARCHAR(40) NULL;
GO

-- ── 2. Recreate the unique index to include MatterTypeCode ──
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_AuthoritySource') AND name=N'UX_Legal_AuthoritySource_ProviderJurisdictionKind')
	DROP INDEX UX_Legal_AuthoritySource_ProviderJurisdictionKind ON POLOXI.Legal_AuthoritySource;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_AuthoritySource') AND name=N'UX_Legal_AuthoritySource_ProviderJurisdictionKindMatterType')
	CREATE UNIQUE INDEX UX_Legal_AuthoritySource_ProviderJurisdictionKindMatterType
	ON POLOXI.Legal_AuthoritySource (ProviderCode,JurisdictionCode,AuthorityKindCode,MatterTypeCode,TenantId)
	WHERE IsDeleted=0;
GO

-- ── 3. Seed per-state STATUTE sources (Justia state codes) for all 50 states + DC ──
-- MatterTypeCode NULL = applies to every matter type (statutes are matter-type-agnostic).
-- Platform defaults (TenantId NULL); JurisdictionCode uses the NAME:STATE convention
-- consumed by LegalJurisdictionScope / OfficialLegalAuthorityRetriever.
BEGIN TRANSACTION;

MERGE POLOXI.Legal_AuthoritySource WITH (HOLDLOCK) AS target
USING (VALUES
	-- (ProviderCode, JurisdictionCode, AuthorityKindCode, BaseUrl, DocumentUrlTemplate, Priority)
	(N'Justia', N'NAME:ALABAMA',              N'Statute', N'https://law.justia.com/codes/alabama/',              N'https://law.justia.com/codes/alabama/{citation}/',              60),
	(N'Justia', N'NAME:ALASKA',               N'Statute', N'https://law.justia.com/codes/alaska/',               N'https://law.justia.com/codes/alaska/{citation}/',               60),
	(N'Justia', N'NAME:ARIZONA',              N'Statute', N'https://law.justia.com/codes/arizona/',              N'https://law.justia.com/codes/arizona/{citation}/',              60),
	(N'Justia', N'NAME:ARKANSAS',             N'Statute', N'https://law.justia.com/codes/arkansas/',             N'https://law.justia.com/codes/arkansas/{citation}/',             60),
	(N'Justia', N'NAME:CALIFORNIA',           N'Statute', N'https://law.justia.com/codes/california/',           N'https://law.justia.com/codes/california/{citation}/',           60),
	(N'Justia', N'NAME:COLORADO',             N'Statute', N'https://law.justia.com/codes/colorado/',             N'https://law.justia.com/codes/colorado/{citation}/',             60),
	(N'Justia', N'NAME:CONNECTICUT',          N'Statute', N'https://law.justia.com/codes/connecticut/',          N'https://law.justia.com/codes/connecticut/{citation}/',          60),
	(N'Justia', N'NAME:DELAWARE',             N'Statute', N'https://law.justia.com/codes/delaware/',             N'https://law.justia.com/codes/delaware/{citation}/',             60),
	(N'Justia', N'NAME:DISTRICT OF COLUMBIA', N'Statute', N'https://law.justia.com/codes/district-of-columbia/', N'https://law.justia.com/codes/district-of-columbia/{citation}/', 60),
	(N'Justia', N'NAME:FLORIDA',              N'Statute', N'https://law.justia.com/codes/florida/',              N'https://law.justia.com/codes/florida/{citation}/',              60),
	(N'Justia', N'NAME:GEORGIA',              N'Statute', N'https://law.justia.com/codes/georgia/',              N'https://law.justia.com/codes/georgia/{citation}/',              60),
	(N'Justia', N'NAME:HAWAII',               N'Statute', N'https://law.justia.com/codes/hawaii/',               N'https://law.justia.com/codes/hawaii/{citation}/',               60),
	(N'Justia', N'NAME:IDAHO',                N'Statute', N'https://law.justia.com/codes/idaho/',                N'https://law.justia.com/codes/idaho/{citation}/',                60),
	(N'Justia', N'NAME:ILLINOIS',             N'Statute', N'https://law.justia.com/codes/illinois/',             N'https://law.justia.com/codes/illinois/{citation}/',             60),
	(N'Justia', N'NAME:INDIANA',              N'Statute', N'https://law.justia.com/codes/indiana/',              N'https://law.justia.com/codes/indiana/{citation}/',              60),
	(N'Justia', N'NAME:IOWA',                 N'Statute', N'https://law.justia.com/codes/iowa/',                 N'https://law.justia.com/codes/iowa/{citation}/',                 60),
	(N'Justia', N'NAME:KANSAS',               N'Statute', N'https://law.justia.com/codes/kansas/',               N'https://law.justia.com/codes/kansas/{citation}/',               60),
	(N'Justia', N'NAME:KENTUCKY',             N'Statute', N'https://law.justia.com/codes/kentucky/',             N'https://law.justia.com/codes/kentucky/{citation}/',             60),
	(N'Justia', N'NAME:LOUISIANA',            N'Statute', N'https://law.justia.com/codes/louisiana/',            N'https://law.justia.com/codes/louisiana/{citation}/',            60),
	(N'Justia', N'NAME:MAINE',                N'Statute', N'https://law.justia.com/codes/maine/',                N'https://law.justia.com/codes/maine/{citation}/',                60),
	(N'Justia', N'NAME:MARYLAND',             N'Statute', N'https://law.justia.com/codes/maryland/',             N'https://law.justia.com/codes/maryland/{citation}/',             60),
	(N'Justia', N'NAME:MASSACHUSETTS',        N'Statute', N'https://law.justia.com/codes/massachusetts/',        N'https://law.justia.com/codes/massachusetts/{citation}/',        60),
	(N'Justia', N'NAME:MICHIGAN',             N'Statute', N'https://law.justia.com/codes/michigan/',             N'https://law.justia.com/codes/michigan/{citation}/',             60),
	(N'Justia', N'NAME:MINNESOTA',            N'Statute', N'https://law.justia.com/codes/minnesota/',            N'https://law.justia.com/codes/minnesota/{citation}/',            60),
	(N'Justia', N'NAME:MISSISSIPPI',          N'Statute', N'https://law.justia.com/codes/mississippi/',          N'https://law.justia.com/codes/mississippi/{citation}/',          60),
	(N'Justia', N'NAME:MISSOURI',             N'Statute', N'https://law.justia.com/codes/missouri/',             N'https://law.justia.com/codes/missouri/{citation}/',             60),
	(N'Justia', N'NAME:MONTANA',              N'Statute', N'https://law.justia.com/codes/montana/',              N'https://law.justia.com/codes/montana/{citation}/',              60),
	(N'Justia', N'NAME:NEBRASKA',             N'Statute', N'https://law.justia.com/codes/nebraska/',             N'https://law.justia.com/codes/nebraska/{citation}/',             60),
	(N'Justia', N'NAME:NEVADA',               N'Statute', N'https://law.justia.com/codes/nevada/',               N'https://law.justia.com/codes/nevada/{citation}/',               60),
	(N'Justia', N'NAME:NEW HAMPSHIRE',        N'Statute', N'https://law.justia.com/codes/new-hampshire/',        N'https://law.justia.com/codes/new-hampshire/{citation}/',        60),
	(N'Justia', N'NAME:NEW JERSEY',           N'Statute', N'https://law.justia.com/codes/new-jersey/',           N'https://law.justia.com/codes/new-jersey/{citation}/',           60),
	(N'Justia', N'NAME:NEW MEXICO',           N'Statute', N'https://law.justia.com/codes/new-mexico/',           N'https://law.justia.com/codes/new-mexico/{citation}/',           60),
	(N'Justia', N'NAME:NEW YORK',             N'Statute', N'https://law.justia.com/codes/new-york/',             N'https://law.justia.com/codes/new-york/{citation}/',             60),
	(N'Justia', N'NAME:NORTH CAROLINA',       N'Statute', N'https://law.justia.com/codes/north-carolina/',       N'https://law.justia.com/codes/north-carolina/{citation}/',       60),
	(N'Justia', N'NAME:NORTH DAKOTA',         N'Statute', N'https://law.justia.com/codes/north-dakota/',         N'https://law.justia.com/codes/north-dakota/{citation}/',         60),
	(N'Justia', N'NAME:OHIO',                 N'Statute', N'https://law.justia.com/codes/ohio/',                 N'https://law.justia.com/codes/ohio/{citation}/',                 60),
	(N'Justia', N'NAME:OKLAHOMA',             N'Statute', N'https://law.justia.com/codes/oklahoma/',             N'https://law.justia.com/codes/oklahoma/{citation}/',             60),
	(N'Justia', N'NAME:OREGON',               N'Statute', N'https://law.justia.com/codes/oregon/',               N'https://law.justia.com/codes/oregon/{citation}/',               60),
	(N'Justia', N'NAME:PENNSYLVANIA',         N'Statute', N'https://law.justia.com/codes/pennsylvania/',         N'https://law.justia.com/codes/pennsylvania/{citation}/',         60),
	(N'Justia', N'NAME:RHODE ISLAND',         N'Statute', N'https://law.justia.com/codes/rhode-island/',         N'https://law.justia.com/codes/rhode-island/{citation}/',         60),
	(N'Justia', N'NAME:SOUTH CAROLINA',       N'Statute', N'https://law.justia.com/codes/south-carolina/',       N'https://law.justia.com/codes/south-carolina/{citation}/',       60),
	(N'Justia', N'NAME:SOUTH DAKOTA',         N'Statute', N'https://law.justia.com/codes/south-dakota/',         N'https://law.justia.com/codes/south-dakota/{citation}/',         60),
	(N'Justia', N'NAME:TENNESSEE',            N'Statute', N'https://law.justia.com/codes/tennessee/',            N'https://law.justia.com/codes/tennessee/{citation}/',            60),
	(N'Justia', N'NAME:TEXAS',                N'Statute', N'https://law.justia.com/codes/texas/',                N'https://law.justia.com/codes/texas/{citation}/',                60),
	(N'Justia', N'NAME:UTAH',                 N'Statute', N'https://law.justia.com/codes/utah/',                 N'https://law.justia.com/codes/utah/{citation}/',                 60),
	(N'Justia', N'NAME:VERMONT',              N'Statute', N'https://law.justia.com/codes/vermont/',              N'https://law.justia.com/codes/vermont/{citation}/',              60),
	(N'Justia', N'NAME:VIRGINIA',             N'Statute', N'https://law.justia.com/codes/virginia/',             N'https://law.justia.com/codes/virginia/{citation}/',             60),
	(N'Justia', N'NAME:WASHINGTON',           N'Statute', N'https://law.justia.com/codes/washington/',           N'https://law.justia.com/codes/washington/{citation}/',           60),
	(N'Justia', N'NAME:WEST VIRGINIA',        N'Statute', N'https://law.justia.com/codes/west-virginia/',        N'https://law.justia.com/codes/west-virginia/{citation}/',        60),
	(N'Justia', N'NAME:WISCONSIN',            N'Statute', N'https://law.justia.com/codes/wisconsin/',            N'https://law.justia.com/codes/wisconsin/{citation}/',            60),
	(N'Justia', N'NAME:WYOMING',              N'Statute', N'https://law.justia.com/codes/wyoming/',              N'https://law.justia.com/codes/wyoming/{citation}/',              60),
	-- Federal regulations via eCFR (Code of Federal Regulations)
	(N'eCFR',   N'FEDERAL',                   N'Regulation', N'https://www.ecfr.gov/',                          N'https://www.ecfr.gov/current/title-{citation}',                 70)
) AS source (ProviderCode,JurisdictionCode,AuthorityKindCode,BaseUrl,DocumentUrlTemplate,Priority)
ON  target.ProviderCode=source.ProviderCode
AND target.JurisdictionCode=source.JurisdictionCode
AND target.AuthorityKindCode=source.AuthorityKindCode
AND target.MatterTypeCode IS NULL
AND target.TenantId IS NULL
AND target.IsDeleted=0
WHEN NOT MATCHED THEN INSERT
	(LegalAuthoritySourceId,ProviderCode,JurisdictionCode,AuthorityKindCode,MatterTypeCode,
	 CitationPattern,BaseUrl,DocumentUrlTemplate,SectionAnchorTemplate,ExtractionStrategyCode,
	 DiscoveryMethodCode,DiscoveryEvidenceUrl,VerifiedDateUtc,Priority,IsEnabled,TenantId,CreatedDateUtc,IsDeleted)
VALUES
	(NEWID(),source.ProviderCode,source.JurisdictionCode,source.AuthorityKindCode,NULL,
	 N'',source.BaseUrl,source.DocumentUrlTemplate,NULL,N'HtmlDocument',
	 N'Curated',source.BaseUrl,SYSUTCDATETIME(),source.Priority,1,NULL,SYSUTCDATETIME(),0);

COMMIT TRANSACTION;
GO
