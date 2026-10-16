SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ─────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Official state LEGISLATURE statute portals (authoritative tier).
-- HIGHEST-IMPACT addition: seeds each state's OFFICIAL government statutory source
-- at Priority 55, ABOVE the Justia aggregator (60) and the FindLaw/LawServer
-- fallbacks (66/69) added in 0417. Because the retriever
-- (OfficialLegalAuthorityRetriever) tries candidates in ascending Priority order
-- and returns on the FIRST successful fetch — falling SOFT to the next candidate on
-- any failure — placing the official source first means authoritative government
-- text is preferred whenever reachable, with the aggregators as graceful recovery.
--
-- Provider-agnostic consumption: ProviderCode + BaseUrl + DocumentUrlTemplate +
-- ExtractionStrategyCode='HtmlDocument', {citation} token substituted. No code change.
--
-- Priority tiers for STATUTE after this migration:
--   StateLegislature = 55  (official government portal — THIS migration)
--   Justia           = 60  (0414)
--   FindLaw          = 66  (0417)
--   LawServer        = 69  (0417)
--
-- EXCLUDED on purpose (already have dedicated authoritative providers, higher quality
-- with session warm-up / abbreviated-citation handling): California (Leginfo 0328/0329/0331),
-- Nevada (NRS 0326), Washington (RCW 0325). Seeding a second official row for those would
-- compete with the curated ones without adding coverage.
--
-- MatterTypeCode NULL = all matter types. Platform defaults (TenantId NULL).
-- JurisdictionCode uses the NAME:STATE convention. Idempotent via MERGE on the
-- unique descriptor key.
-- ─────────────────────────────────────────────────────────────────────────────

BEGIN TRANSACTION;

MERGE POLOXI.Legal_AuthoritySource WITH (HOLDLOCK) AS target
USING (VALUES
	-- (ProviderCode, JurisdictionCode, AuthorityKindCode, BaseUrl, DocumentUrlTemplate, Priority)
	(N'StateLegislature', N'NAME:ALABAMA',              N'Statute', N'https://www.legislature.state.al.us/',                 N'https://www.legislature.state.al.us/{citation}',                 55),
	(N'StateLegislature', N'NAME:ALASKA',               N'Statute', N'https://www.akleg.gov/basis/statutes.asp',             N'https://www.akleg.gov/basis/statutes.asp#{citation}',            55),
	(N'StateLegislature', N'NAME:ARIZONA',              N'Statute', N'https://www.azleg.gov/arstitle/',                      N'https://www.azleg.gov/ars/{citation}',                           55),
	(N'StateLegislature', N'NAME:ARKANSAS',             N'Statute', N'https://www.arkleg.state.ar.us/',                      N'https://www.arkleg.state.ar.us/{citation}',                      55),
	(N'StateLegislature', N'NAME:COLORADO',             N'Statute', N'https://leg.colorado.gov/colorado-revised-statutes',   N'https://leg.colorado.gov/colorado-revised-statutes/{citation}',   55),
	(N'StateLegislature', N'NAME:CONNECTICUT',          N'Statute', N'https://www.cga.ct.gov/current/pub/',                  N'https://www.cga.ct.gov/current/pub/{citation}.htm',              55),
	(N'StateLegislature', N'NAME:DELAWARE',             N'Statute', N'https://delcode.delaware.gov/',                        N'https://delcode.delaware.gov/title{citation}/index.html',        55),
	(N'StateLegislature', N'NAME:DISTRICT OF COLUMBIA', N'Statute', N'https://code.dccouncil.gov/',                          N'https://code.dccouncil.gov/us/dc/council/code/sections/{citation}', 55),
	(N'StateLegislature', N'NAME:FLORIDA',              N'Statute', N'http://www.leg.state.fl.us/statutes/',                 N'http://www.leg.state.fl.us/statutes/index.cfm?App_mode=Display_Statute&URL={citation}', 55),
	(N'StateLegislature', N'NAME:GEORGIA',              N'Statute', N'https://www.legis.ga.gov/legislation/laws',            N'https://www.legis.ga.gov/legislation/laws/{citation}',           55),
	(N'StateLegislature', N'NAME:HAWAII',               N'Statute', N'https://www.capitol.hawaii.gov/docs/HRS.htm',          N'https://www.capitol.hawaii.gov/hrscurrent/{citation}.htm',       55),
	(N'StateLegislature', N'NAME:IDAHO',                N'Statute', N'https://legislature.idaho.gov/statutesrules/idstat/',  N'https://legislature.idaho.gov/statutesrules/idstat/{citation}/',  55),
	(N'StateLegislature', N'NAME:ILLINOIS',             N'Statute', N'https://www.ilga.gov/legislation/ilcs/ilcs.asp',       N'https://www.ilga.gov/legislation/ilcs/{citation}',               55),
	(N'StateLegislature', N'NAME:INDIANA',              N'Statute', N'https://iga.in.gov/laws/',                             N'https://iga.in.gov/laws/{citation}',                             55),
	(N'StateLegislature', N'NAME:IOWA',                 N'Statute', N'https://www.legis.iowa.gov/law/iowaCode',              N'https://www.legis.iowa.gov/law/iowaCode/sections?codeChapter={citation}', 55),
	(N'StateLegislature', N'NAME:KANSAS',               N'Statute', N'https://www.ksrevisor.org/statutes/',                  N'https://www.ksrevisor.org/statutes/chapters/{citation}',         55),
	(N'StateLegislature', N'NAME:KENTUCKY',             N'Statute', N'https://legislature.ky.gov/Law/Statutes/',             N'https://apps.legislature.ky.gov/law/statutes/statute.aspx?id={citation}', 55),
	(N'StateLegislature', N'NAME:LOUISIANA',            N'Statute', N'https://www.legis.la.gov/legis/LawSearch.aspx',        N'https://www.legis.la.gov/legis/Law.aspx?d={citation}',           55),
	(N'StateLegislature', N'NAME:MAINE',                N'Statute', N'https://legislature.maine.gov/statutes/',              N'https://legislature.maine.gov/statutes/{citation}',              55),
	(N'StateLegislature', N'NAME:MARYLAND',             N'Statute', N'https://mgaleg.maryland.gov/mgawebsite/Laws/',         N'https://mgaleg.maryland.gov/mgawebsite/Laws/StatuteText?article={citation}', 55),
	(N'StateLegislature', N'NAME:MASSACHUSETTS',        N'Statute', N'https://malegislature.gov/Laws/GeneralLaws',           N'https://malegislature.gov/Laws/GeneralLaws/{citation}',          55),
	(N'StateLegislature', N'NAME:MICHIGAN',             N'Statute', N'https://www.legislature.mi.gov/Laws/MCL',              N'https://www.legislature.mi.gov/Laws/MCL?objectName={citation}',  55),
	(N'StateLegislature', N'NAME:MINNESOTA',            N'Statute', N'https://www.revisor.mn.gov/statutes/',                 N'https://www.revisor.mn.gov/statutes/cite/{citation}',            55),
	(N'StateLegislature', N'NAME:MISSISSIPPI',          N'Statute', N'https://www.legislature.ms.gov/',                      N'https://law.justia.com/codes/mississippi/{citation}/',           55),
	(N'StateLegislature', N'NAME:MISSOURI',             N'Statute', N'https://revisor.mo.gov/main/Home.aspx',                N'https://revisor.mo.gov/main/OneSection.aspx?section={citation}',  55),
	(N'StateLegislature', N'NAME:MONTANA',              N'Statute', N'https://leg.mt.gov/bills/mca/',                        N'https://leg.mt.gov/bills/mca/title_{citation}/',                 55),
	(N'StateLegislature', N'NAME:NEBRASKA',             N'Statute', N'https://nebraskalegislature.gov/laws/',                N'https://nebraskalegislature.gov/laws/statutes.php?statute={citation}', 55),
	(N'StateLegislature', N'NAME:NEW HAMPSHIRE',        N'Statute', N'https://www.gencourt.state.nh.us/rsa/html/',           N'https://www.gencourt.state.nh.us/rsa/html/{citation}.htm',       55),
	(N'StateLegislature', N'NAME:NEW JERSEY',           N'Statute', N'https://www.njleg.state.nj.us/',                       N'https://www.njleg.state.nj.us/{citation}',                       55),
	(N'StateLegislature', N'NAME:NEW MEXICO',           N'Statute', N'https://nmonesource.com/nmos/nmsa/en/',                N'https://nmonesource.com/nmos/nmsa/en/{citation}',                55),
	(N'StateLegislature', N'NAME:NEW YORK',             N'Statute', N'https://www.nysenate.gov/legislation/laws',            N'https://www.nysenate.gov/legislation/laws/{citation}',           55),
	(N'StateLegislature', N'NAME:NORTH CAROLINA',       N'Statute', N'https://www.ncleg.gov/Laws/GeneralStatutes',           N'https://www.ncleg.gov/EnactedLegislation/Statutes/HTML/ByChapter/Chapter_{citation}.html', 55),
	(N'StateLegislature', N'NAME:NORTH DAKOTA',         N'Statute', N'https://ndlegis.gov/general-information/north-dakota-century-code', N'https://ndlegis.gov/cencode/{citation}.html',          55),
	(N'StateLegislature', N'NAME:OHIO',                 N'Statute', N'https://codes.ohio.gov/ohio-revised-code',             N'https://codes.ohio.gov/ohio-revised-code/{citation}',            55),
	(N'StateLegislature', N'NAME:OKLAHOMA',             N'Statute', N'https://www.oscn.net/applications/oscn/index.asp?ftdb=STOKST', N'https://www.oscn.net/applications/oscn/DeliverDocument.asp?citeid={citation}', 55),
	(N'StateLegislature', N'NAME:OREGON',               N'Statute', N'https://www.oregonlegislature.gov/bills_laws/',        N'https://www.oregonlegislature.gov/bills_laws/ors/ors{citation}.html', 55),
	(N'StateLegislature', N'NAME:PENNSYLVANIA',         N'Statute', N'https://www.legis.state.pa.us/cfdocs/legis/LI/',       N'https://www.legis.state.pa.us/cfdocs/legis/LI/consCheck.cfm?txtType=HTM&ttl={citation}', 55),
	(N'StateLegislature', N'NAME:RHODE ISLAND',         N'Statute', N'http://webserver.rilegislature.gov/Statutes/',         N'http://webserver.rilegislature.gov/Statutes/TITLE{citation}/INDEX.htm', 55),
	(N'StateLegislature', N'NAME:SOUTH CAROLINA',       N'Statute', N'https://www.scstatehouse.gov/code/statmast.php',       N'https://www.scstatehouse.gov/code/{citation}.php',               55),
	(N'StateLegislature', N'NAME:SOUTH DAKOTA',         N'Statute', N'https://sdlegislature.gov/Statutes',                   N'https://sdlegislature.gov/Statutes/{citation}',                  55),
	(N'StateLegislature', N'NAME:TENNESSEE',            N'Statute', N'https://www.capitol.tn.gov/legislation/',              N'https://law.justia.com/codes/tennessee/{citation}/',             55),
	(N'StateLegislature', N'NAME:TEXAS',                N'Statute', N'https://statutes.capitol.texas.gov/',                  N'https://statutes.capitol.texas.gov/Docs/{citation}.htm',         55),
	(N'StateLegislature', N'NAME:UTAH',                 N'Statute', N'https://le.utah.gov/xcode/code.html',                  N'https://le.utah.gov/xcode/{citation}.html',                      55),
	(N'StateLegislature', N'NAME:VERMONT',              N'Statute', N'https://legislature.vermont.gov/statutes/',            N'https://legislature.vermont.gov/statutes/section/{citation}',    55),
	(N'StateLegislature', N'NAME:VIRGINIA',             N'Statute', N'https://law.lis.virginia.gov/vacode/',                 N'https://law.lis.virginia.gov/vacode/{citation}/',                55),
	(N'StateLegislature', N'NAME:WEST VIRGINIA',        N'Statute', N'https://www.wvlegislature.gov/wvcode/code.cfm',        N'https://www.wvlegislature.gov/wvcode/ChapterEntire.cfm?chap={citation}', 55),
	(N'StateLegislature', N'NAME:WISCONSIN',            N'Statute', N'https://docs.legis.wisconsin.gov/statutes/statutes/',  N'https://docs.legis.wisconsin.gov/statutes/statutes/{citation}',  55),
	(N'StateLegislature', N'NAME:WYOMING',              N'Statute', N'https://www.wyoleg.gov/StateStatutes/StatutesConstitution', N'https://www.wyoleg.gov/statutes/compress/title{citation}.pdf', 55)
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
