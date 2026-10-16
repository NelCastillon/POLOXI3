SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ─────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Additional per-state STATUTE providers (redundancy / recovery).
-- Context: migration 0414 seeded ONE universal statutory provider per state
-- (Justia, Priority 60) plus the shared federal eCFR regulation row. California,
-- New York, Washington and Nevada additionally received dedicated provider rows
-- (Leginfo / abbreviated citations / RCW / NRS in 0325–0331), which is why those
-- states already expose more than two sources. This migration levels the field by
-- adding TWO more broadly-applicable, generically-templatable, free statutory
-- aggregators for EVERY state + DC so the Adaptive Retrieval Logic (ARL) has real
-- fallback depth per jurisdiction instead of a single provider.
--
-- The retriever (OfficialLegalAuthorityRetriever) is PROVIDER-AGNOSTIC: it fetches
-- any row generically via ProviderCode + BaseUrl + DocumentUrlTemplate +
-- ExtractionStrategyCode='HtmlDocument', substituting the {citation} token, and
-- tries candidates in ascending Priority order. No application code change is
-- required — these rows are consumed immediately as ranked fallbacks after Justia.
--
--   Justia   = Priority 60  (primary statutory source, seeded in 0414)
--   FindLaw  = Priority 66  (first statutory fallback)
--   LawServer= Priority 69  (second statutory fallback)
--
-- MatterTypeCode NULL = applies to every matter type (statutes are matter-agnostic).
-- Platform defaults (TenantId NULL). JurisdictionCode uses the NAME:STATE convention
-- consumed by LegalJurisdictionScope / OfficialLegalAuthorityRetriever.
-- Idempotent: seeds via MERGE guarded on the unique descriptor key.
-- ─────────────────────────────────────────────────────────────────────────────

BEGIN TRANSACTION;

MERGE POLOXI.Legal_AuthoritySource WITH (HOLDLOCK) AS target
USING (VALUES
	-- (ProviderCode, JurisdictionCode, AuthorityKindCode, BaseUrl, DocumentUrlTemplate, Priority)

	-- ── FindLaw State Codes (codes.findlaw.com/{state-abbr}/) — Priority 66 ──
	(N'FindLaw', N'NAME:ALABAMA',              N'Statute', N'https://codes.findlaw.com/al/',              N'https://codes.findlaw.com/al/{citation}/',              66),
	(N'FindLaw', N'NAME:ALASKA',               N'Statute', N'https://codes.findlaw.com/ak/',              N'https://codes.findlaw.com/ak/{citation}/',              66),
	(N'FindLaw', N'NAME:ARIZONA',              N'Statute', N'https://codes.findlaw.com/az/',              N'https://codes.findlaw.com/az/{citation}/',              66),
	(N'FindLaw', N'NAME:ARKANSAS',             N'Statute', N'https://codes.findlaw.com/ar/',              N'https://codes.findlaw.com/ar/{citation}/',              66),
	(N'FindLaw', N'NAME:CALIFORNIA',           N'Statute', N'https://codes.findlaw.com/ca/',              N'https://codes.findlaw.com/ca/{citation}/',              66),
	(N'FindLaw', N'NAME:COLORADO',             N'Statute', N'https://codes.findlaw.com/co/',              N'https://codes.findlaw.com/co/{citation}/',              66),
	(N'FindLaw', N'NAME:CONNECTICUT',          N'Statute', N'https://codes.findlaw.com/ct/',              N'https://codes.findlaw.com/ct/{citation}/',              66),
	(N'FindLaw', N'NAME:DELAWARE',             N'Statute', N'https://codes.findlaw.com/de/',              N'https://codes.findlaw.com/de/{citation}/',              66),
	(N'FindLaw', N'NAME:DISTRICT OF COLUMBIA', N'Statute', N'https://codes.findlaw.com/dc/',              N'https://codes.findlaw.com/dc/{citation}/',              66),
	(N'FindLaw', N'NAME:FLORIDA',              N'Statute', N'https://codes.findlaw.com/fl/',              N'https://codes.findlaw.com/fl/{citation}/',              66),
	(N'FindLaw', N'NAME:GEORGIA',              N'Statute', N'https://codes.findlaw.com/ga/',              N'https://codes.findlaw.com/ga/{citation}/',              66),
	(N'FindLaw', N'NAME:HAWAII',               N'Statute', N'https://codes.findlaw.com/hi/',              N'https://codes.findlaw.com/hi/{citation}/',              66),
	(N'FindLaw', N'NAME:IDAHO',                N'Statute', N'https://codes.findlaw.com/id/',              N'https://codes.findlaw.com/id/{citation}/',              66),
	(N'FindLaw', N'NAME:ILLINOIS',             N'Statute', N'https://codes.findlaw.com/il/',              N'https://codes.findlaw.com/il/{citation}/',              66),
	(N'FindLaw', N'NAME:INDIANA',              N'Statute', N'https://codes.findlaw.com/in/',              N'https://codes.findlaw.com/in/{citation}/',              66),
	(N'FindLaw', N'NAME:IOWA',                 N'Statute', N'https://codes.findlaw.com/ia/',              N'https://codes.findlaw.com/ia/{citation}/',              66),
	(N'FindLaw', N'NAME:KANSAS',               N'Statute', N'https://codes.findlaw.com/ks/',              N'https://codes.findlaw.com/ks/{citation}/',              66),
	(N'FindLaw', N'NAME:KENTUCKY',             N'Statute', N'https://codes.findlaw.com/ky/',              N'https://codes.findlaw.com/ky/{citation}/',              66),
	(N'FindLaw', N'NAME:LOUISIANA',            N'Statute', N'https://codes.findlaw.com/la/',              N'https://codes.findlaw.com/la/{citation}/',              66),
	(N'FindLaw', N'NAME:MAINE',                N'Statute', N'https://codes.findlaw.com/me/',              N'https://codes.findlaw.com/me/{citation}/',              66),
	(N'FindLaw', N'NAME:MARYLAND',             N'Statute', N'https://codes.findlaw.com/md/',              N'https://codes.findlaw.com/md/{citation}/',              66),
	(N'FindLaw', N'NAME:MASSACHUSETTS',        N'Statute', N'https://codes.findlaw.com/ma/',              N'https://codes.findlaw.com/ma/{citation}/',              66),
	(N'FindLaw', N'NAME:MICHIGAN',             N'Statute', N'https://codes.findlaw.com/mi/',              N'https://codes.findlaw.com/mi/{citation}/',              66),
	(N'FindLaw', N'NAME:MINNESOTA',            N'Statute', N'https://codes.findlaw.com/mn/',              N'https://codes.findlaw.com/mn/{citation}/',              66),
	(N'FindLaw', N'NAME:MISSISSIPPI',          N'Statute', N'https://codes.findlaw.com/ms/',              N'https://codes.findlaw.com/ms/{citation}/',              66),
	(N'FindLaw', N'NAME:MISSOURI',             N'Statute', N'https://codes.findlaw.com/mo/',              N'https://codes.findlaw.com/mo/{citation}/',              66),
	(N'FindLaw', N'NAME:MONTANA',              N'Statute', N'https://codes.findlaw.com/mt/',              N'https://codes.findlaw.com/mt/{citation}/',              66),
	(N'FindLaw', N'NAME:NEBRASKA',             N'Statute', N'https://codes.findlaw.com/ne/',              N'https://codes.findlaw.com/ne/{citation}/',              66),
	(N'FindLaw', N'NAME:NEVADA',               N'Statute', N'https://codes.findlaw.com/nv/',              N'https://codes.findlaw.com/nv/{citation}/',              66),
	(N'FindLaw', N'NAME:NEW HAMPSHIRE',        N'Statute', N'https://codes.findlaw.com/nh/',              N'https://codes.findlaw.com/nh/{citation}/',              66),
	(N'FindLaw', N'NAME:NEW JERSEY',           N'Statute', N'https://codes.findlaw.com/nj/',              N'https://codes.findlaw.com/nj/{citation}/',              66),
	(N'FindLaw', N'NAME:NEW MEXICO',           N'Statute', N'https://codes.findlaw.com/nm/',              N'https://codes.findlaw.com/nm/{citation}/',              66),
	(N'FindLaw', N'NAME:NEW YORK',             N'Statute', N'https://codes.findlaw.com/ny/',              N'https://codes.findlaw.com/ny/{citation}/',              66),
	(N'FindLaw', N'NAME:NORTH CAROLINA',       N'Statute', N'https://codes.findlaw.com/nc/',              N'https://codes.findlaw.com/nc/{citation}/',              66),
	(N'FindLaw', N'NAME:NORTH DAKOTA',         N'Statute', N'https://codes.findlaw.com/nd/',              N'https://codes.findlaw.com/nd/{citation}/',              66),
	(N'FindLaw', N'NAME:OHIO',                 N'Statute', N'https://codes.findlaw.com/oh/',              N'https://codes.findlaw.com/oh/{citation}/',              66),
	(N'FindLaw', N'NAME:OKLAHOMA',             N'Statute', N'https://codes.findlaw.com/ok/',              N'https://codes.findlaw.com/ok/{citation}/',              66),
	(N'FindLaw', N'NAME:OREGON',               N'Statute', N'https://codes.findlaw.com/or/',              N'https://codes.findlaw.com/or/{citation}/',              66),
	(N'FindLaw', N'NAME:PENNSYLVANIA',         N'Statute', N'https://codes.findlaw.com/pa/',              N'https://codes.findlaw.com/pa/{citation}/',              66),
	(N'FindLaw', N'NAME:RHODE ISLAND',         N'Statute', N'https://codes.findlaw.com/ri/',              N'https://codes.findlaw.com/ri/{citation}/',              66),
	(N'FindLaw', N'NAME:SOUTH CAROLINA',       N'Statute', N'https://codes.findlaw.com/sc/',              N'https://codes.findlaw.com/sc/{citation}/',              66),
	(N'FindLaw', N'NAME:SOUTH DAKOTA',         N'Statute', N'https://codes.findlaw.com/sd/',              N'https://codes.findlaw.com/sd/{citation}/',              66),
	(N'FindLaw', N'NAME:TENNESSEE',            N'Statute', N'https://codes.findlaw.com/tn/',              N'https://codes.findlaw.com/tn/{citation}/',              66),
	(N'FindLaw', N'NAME:TEXAS',                N'Statute', N'https://codes.findlaw.com/tx/',              N'https://codes.findlaw.com/tx/{citation}/',              66),
	(N'FindLaw', N'NAME:UTAH',                 N'Statute', N'https://codes.findlaw.com/ut/',              N'https://codes.findlaw.com/ut/{citation}/',              66),
	(N'FindLaw', N'NAME:VERMONT',              N'Statute', N'https://codes.findlaw.com/vt/',              N'https://codes.findlaw.com/vt/{citation}/',              66),
	(N'FindLaw', N'NAME:VIRGINIA',             N'Statute', N'https://codes.findlaw.com/va/',              N'https://codes.findlaw.com/va/{citation}/',              66),
	(N'FindLaw', N'NAME:WASHINGTON',           N'Statute', N'https://codes.findlaw.com/wa/',              N'https://codes.findlaw.com/wa/{citation}/',              66),
	(N'FindLaw', N'NAME:WEST VIRGINIA',        N'Statute', N'https://codes.findlaw.com/wv/',              N'https://codes.findlaw.com/wv/{citation}/',              66),
	(N'FindLaw', N'NAME:WISCONSIN',            N'Statute', N'https://codes.findlaw.com/wi/',              N'https://codes.findlaw.com/wi/{citation}/',              66),
	(N'FindLaw', N'NAME:WYOMING',              N'Statute', N'https://codes.findlaw.com/wy/',              N'https://codes.findlaw.com/wy/{citation}/',              66),

	-- ── LawServer (lawserver.com/law/state/{state}/) — Priority 69 ──
	(N'LawServer', N'NAME:ALABAMA',              N'Statute', N'https://www.lawserver.com/law/state/alabama/',              N'https://www.lawserver.com/law/state/alabama/{citation}',              69),
	(N'LawServer', N'NAME:ALASKA',               N'Statute', N'https://www.lawserver.com/law/state/alaska/',               N'https://www.lawserver.com/law/state/alaska/{citation}',               69),
	(N'LawServer', N'NAME:ARIZONA',              N'Statute', N'https://www.lawserver.com/law/state/arizona/',              N'https://www.lawserver.com/law/state/arizona/{citation}',              69),
	(N'LawServer', N'NAME:ARKANSAS',             N'Statute', N'https://www.lawserver.com/law/state/arkansas/',             N'https://www.lawserver.com/law/state/arkansas/{citation}',             69),
	(N'LawServer', N'NAME:CALIFORNIA',           N'Statute', N'https://www.lawserver.com/law/state/california/',           N'https://www.lawserver.com/law/state/california/{citation}',           69),
	(N'LawServer', N'NAME:COLORADO',             N'Statute', N'https://www.lawserver.com/law/state/colorado/',             N'https://www.lawserver.com/law/state/colorado/{citation}',             69),
	(N'LawServer', N'NAME:CONNECTICUT',          N'Statute', N'https://www.lawserver.com/law/state/connecticut/',          N'https://www.lawserver.com/law/state/connecticut/{citation}',          69),
	(N'LawServer', N'NAME:DELAWARE',             N'Statute', N'https://www.lawserver.com/law/state/delaware/',             N'https://www.lawserver.com/law/state/delaware/{citation}',             69),
	(N'LawServer', N'NAME:DISTRICT OF COLUMBIA', N'Statute', N'https://www.lawserver.com/law/state/district-of-columbia/', N'https://www.lawserver.com/law/state/district-of-columbia/{citation}', 69),
	(N'LawServer', N'NAME:FLORIDA',              N'Statute', N'https://www.lawserver.com/law/state/florida/',              N'https://www.lawserver.com/law/state/florida/{citation}',              69),
	(N'LawServer', N'NAME:GEORGIA',              N'Statute', N'https://www.lawserver.com/law/state/georgia/',              N'https://www.lawserver.com/law/state/georgia/{citation}',              69),
	(N'LawServer', N'NAME:HAWAII',               N'Statute', N'https://www.lawserver.com/law/state/hawaii/',               N'https://www.lawserver.com/law/state/hawaii/{citation}',               69),
	(N'LawServer', N'NAME:IDAHO',                N'Statute', N'https://www.lawserver.com/law/state/idaho/',                N'https://www.lawserver.com/law/state/idaho/{citation}',                69),
	(N'LawServer', N'NAME:ILLINOIS',             N'Statute', N'https://www.lawserver.com/law/state/illinois/',             N'https://www.lawserver.com/law/state/illinois/{citation}',             69),
	(N'LawServer', N'NAME:INDIANA',              N'Statute', N'https://www.lawserver.com/law/state/indiana/',              N'https://www.lawserver.com/law/state/indiana/{citation}',              69),
	(N'LawServer', N'NAME:IOWA',                 N'Statute', N'https://www.lawserver.com/law/state/iowa/',                 N'https://www.lawserver.com/law/state/iowa/{citation}',                 69),
	(N'LawServer', N'NAME:KANSAS',               N'Statute', N'https://www.lawserver.com/law/state/kansas/',               N'https://www.lawserver.com/law/state/kansas/{citation}',               69),
	(N'LawServer', N'NAME:KENTUCKY',             N'Statute', N'https://www.lawserver.com/law/state/kentucky/',             N'https://www.lawserver.com/law/state/kentucky/{citation}',             69),
	(N'LawServer', N'NAME:LOUISIANA',            N'Statute', N'https://www.lawserver.com/law/state/louisiana/',            N'https://www.lawserver.com/law/state/louisiana/{citation}',            69),
	(N'LawServer', N'NAME:MAINE',                N'Statute', N'https://www.lawserver.com/law/state/maine/',                N'https://www.lawserver.com/law/state/maine/{citation}',                69),
	(N'LawServer', N'NAME:MARYLAND',             N'Statute', N'https://www.lawserver.com/law/state/maryland/',             N'https://www.lawserver.com/law/state/maryland/{citation}',             69),
	(N'LawServer', N'NAME:MASSACHUSETTS',        N'Statute', N'https://www.lawserver.com/law/state/massachusetts/',        N'https://www.lawserver.com/law/state/massachusetts/{citation}',        69),
	(N'LawServer', N'NAME:MICHIGAN',             N'Statute', N'https://www.lawserver.com/law/state/michigan/',             N'https://www.lawserver.com/law/state/michigan/{citation}',             69),
	(N'LawServer', N'NAME:MINNESOTA',            N'Statute', N'https://www.lawserver.com/law/state/minnesota/',            N'https://www.lawserver.com/law/state/minnesota/{citation}',            69),
	(N'LawServer', N'NAME:MISSISSIPPI',          N'Statute', N'https://www.lawserver.com/law/state/mississippi/',          N'https://www.lawserver.com/law/state/mississippi/{citation}',          69),
	(N'LawServer', N'NAME:MISSOURI',             N'Statute', N'https://www.lawserver.com/law/state/missouri/',             N'https://www.lawserver.com/law/state/missouri/{citation}',             69),
	(N'LawServer', N'NAME:MONTANA',              N'Statute', N'https://www.lawserver.com/law/state/montana/',              N'https://www.lawserver.com/law/state/montana/{citation}',              69),
	(N'LawServer', N'NAME:NEBRASKA',             N'Statute', N'https://www.lawserver.com/law/state/nebraska/',             N'https://www.lawserver.com/law/state/nebraska/{citation}',             69),
	(N'LawServer', N'NAME:NEVADA',               N'Statute', N'https://www.lawserver.com/law/state/nevada/',               N'https://www.lawserver.com/law/state/nevada/{citation}',               69),
	(N'LawServer', N'NAME:NEW HAMPSHIRE',        N'Statute', N'https://www.lawserver.com/law/state/new-hampshire/',        N'https://www.lawserver.com/law/state/new-hampshire/{citation}',        69),
	(N'LawServer', N'NAME:NEW JERSEY',           N'Statute', N'https://www.lawserver.com/law/state/new-jersey/',           N'https://www.lawserver.com/law/state/new-jersey/{citation}',           69),
	(N'LawServer', N'NAME:NEW MEXICO',           N'Statute', N'https://www.lawserver.com/law/state/new-mexico/',           N'https://www.lawserver.com/law/state/new-mexico/{citation}',           69),
	(N'LawServer', N'NAME:NEW YORK',             N'Statute', N'https://www.lawserver.com/law/state/new-york/',             N'https://www.lawserver.com/law/state/new-york/{citation}',             69),
	(N'LawServer', N'NAME:NORTH CAROLINA',       N'Statute', N'https://www.lawserver.com/law/state/north-carolina/',       N'https://www.lawserver.com/law/state/north-carolina/{citation}',       69),
	(N'LawServer', N'NAME:NORTH DAKOTA',         N'Statute', N'https://www.lawserver.com/law/state/north-dakota/',         N'https://www.lawserver.com/law/state/north-dakota/{citation}',         69),
	(N'LawServer', N'NAME:OHIO',                 N'Statute', N'https://www.lawserver.com/law/state/ohio/',                 N'https://www.lawserver.com/law/state/ohio/{citation}',                 69),
	(N'LawServer', N'NAME:OKLAHOMA',             N'Statute', N'https://www.lawserver.com/law/state/oklahoma/',             N'https://www.lawserver.com/law/state/oklahoma/{citation}',             69),
	(N'LawServer', N'NAME:OREGON',               N'Statute', N'https://www.lawserver.com/law/state/oregon/',               N'https://www.lawserver.com/law/state/oregon/{citation}',               69),
	(N'LawServer', N'NAME:PENNSYLVANIA',         N'Statute', N'https://www.lawserver.com/law/state/pennsylvania/',         N'https://www.lawserver.com/law/state/pennsylvania/{citation}',         69),
	(N'LawServer', N'NAME:RHODE ISLAND',         N'Statute', N'https://www.lawserver.com/law/state/rhode-island/',         N'https://www.lawserver.com/law/state/rhode-island/{citation}',         69),
	(N'LawServer', N'NAME:SOUTH CAROLINA',       N'Statute', N'https://www.lawserver.com/law/state/south-carolina/',       N'https://www.lawserver.com/law/state/south-carolina/{citation}',       69),
	(N'LawServer', N'NAME:SOUTH DAKOTA',         N'Statute', N'https://www.lawserver.com/law/state/south-dakota/',         N'https://www.lawserver.com/law/state/south-dakota/{citation}',         69),
	(N'LawServer', N'NAME:TENNESSEE',            N'Statute', N'https://www.lawserver.com/law/state/tennessee/',            N'https://www.lawserver.com/law/state/tennessee/{citation}',            69),
	(N'LawServer', N'NAME:TEXAS',                N'Statute', N'https://www.lawserver.com/law/state/texas/',                N'https://www.lawserver.com/law/state/texas/{citation}',                69),
	(N'LawServer', N'NAME:UTAH',                 N'Statute', N'https://www.lawserver.com/law/state/utah/',                 N'https://www.lawserver.com/law/state/utah/{citation}',                 69),
	(N'LawServer', N'NAME:VERMONT',              N'Statute', N'https://www.lawserver.com/law/state/vermont/',              N'https://www.lawserver.com/law/state/vermont/{citation}',              69),
	(N'LawServer', N'NAME:VIRGINIA',             N'Statute', N'https://www.lawserver.com/law/state/virginia/',             N'https://www.lawserver.com/law/state/virginia/{citation}',             69),
	(N'LawServer', N'NAME:WASHINGTON',           N'Statute', N'https://www.lawserver.com/law/state/washington/',           N'https://www.lawserver.com/law/state/washington/{citation}',           69),
	(N'LawServer', N'NAME:WEST VIRGINIA',        N'Statute', N'https://www.lawserver.com/law/state/west-virginia/',        N'https://www.lawserver.com/law/state/west-virginia/{citation}',        69),
	(N'LawServer', N'NAME:WISCONSIN',            N'Statute', N'https://www.lawserver.com/law/state/wisconsin/',            N'https://www.lawserver.com/law/state/wisconsin/{citation}',            69),
	(N'LawServer', N'NAME:WYOMING',              N'Statute', N'https://www.lawserver.com/law/state/wyoming/',              N'https://www.lawserver.com/law/state/wyoming/{citation}',              69)
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
