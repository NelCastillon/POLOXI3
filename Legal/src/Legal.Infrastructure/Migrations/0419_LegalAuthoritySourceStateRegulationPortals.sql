SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ─────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Per-state REGULATION (administrative code) portals.
-- Closes the regulation coverage gap: before this migration the ONLY Regulation
-- row in POLOXI.Legal_AuthoritySource was the FEDERAL eCFR source (Priority 70,
-- seeded in 0414). State administrative/regulatory citations therefore resolved to
-- COVERAGE_GAP. This migration seeds each state's official administrative-code
-- portal as a Regulation source for all 50 states + DC.
--
-- Provider-agnostic consumption: ProviderCode + BaseUrl + DocumentUrlTemplate +
-- ExtractionStrategyCode='HtmlDocument', {citation} token substituted. No code change.
-- The jurisdiction detector routes state admin-code citations to the matching
-- NAME:STATE + AuthorityKindCode='Regulation' descriptor.
--
-- Priority 72 keeps these just AFTER the federal eCFR row (70) so a federal C.F.R.
-- citation still prefers eCFR, while a state regulatory citation (scoped to its own
-- NAME:STATE jurisdiction) matches its own state portal.
--
-- MatterTypeCode NULL = all matter types. Platform defaults (TenantId NULL).
-- JurisdictionCode uses the NAME:STATE convention. Idempotent via MERGE on the
-- unique descriptor key.
-- ─────────────────────────────────────────────────────────────────────────────

BEGIN TRANSACTION;

MERGE POLOXI.Legal_AuthoritySource WITH (HOLDLOCK) AS target
USING (VALUES
	-- (ProviderCode, JurisdictionCode, AuthorityKindCode, BaseUrl, DocumentUrlTemplate, Priority)
	(N'StateAdminCode', N'NAME:ALABAMA',              N'Regulation', N'https://admincode.legislature.state.al.us/',        N'https://admincode.legislature.state.al.us/divisions/{citation}',   72),
	(N'StateAdminCode', N'NAME:ALASKA',               N'Regulation', N'https://www.akleg.gov/basis/aac.asp',               N'https://www.akleg.gov/basis/aac.asp#{citation}',                  72),
	(N'StateAdminCode', N'NAME:ARIZONA',              N'Regulation', N'https://apps.azsos.gov/public_services/Title_{citation}', N'https://apps.azsos.gov/public_services/Title_{citation}',    72),
	(N'StateAdminCode', N'NAME:ARKANSAS',             N'Regulation', N'https://www.sos.arkansas.gov/rules-and-regulations/', N'https://www.sos.arkansas.gov/rules-and-regulations/{citation}',   72),
	(N'StateAdminCode', N'NAME:CALIFORNIA',           N'Regulation', N'https://govt.westlaw.com/calregs/',                 N'https://govt.westlaw.com/calregs/Document/{citation}',            72),
	(N'StateAdminCode', N'NAME:COLORADO',             N'Regulation', N'https://www.sos.state.co.us/CCR/',                  N'https://www.sos.state.co.us/CCR/{citation}',                      72),
	(N'StateAdminCode', N'NAME:CONNECTICUT',          N'Regulation', N'https://eregulations.ct.gov/eRegsPortal/',          N'https://eregulations.ct.gov/eRegsPortal/Browse/RCSA/{citation}',  72),
	(N'StateAdminCode', N'NAME:DELAWARE',             N'Regulation', N'https://regulations.delaware.gov/',                 N'https://regulations.delaware.gov/AdminCode/title{citation}/',     72),
	(N'StateAdminCode', N'NAME:DISTRICT OF COLUMBIA', N'Regulation', N'https://www.dcregs.dc.gov/',                        N'https://www.dcregs.dc.gov/Common/DCMR/{citation}',               72),
	(N'StateAdminCode', N'NAME:FLORIDA',              N'Regulation', N'https://www.flrules.org/',                          N'https://www.flrules.org/gateway/ChapterHome.asp?Chapter={citation}', 72),
	(N'StateAdminCode', N'NAME:GEORGIA',              N'Regulation', N'https://rules.sos.ga.gov/',                         N'https://rules.sos.ga.gov/GAC/{citation}',                        72),
	(N'StateAdminCode', N'NAME:HAWAII',               N'Regulation', N'https://ltgov.hawaii.gov/the-office/administrative-rules/', N'https://law.justia.com/codes/hawaii/{citation}/',         72),
	(N'StateAdminCode', N'NAME:IDAHO',                N'Regulation', N'https://adminrules.idaho.gov/',                     N'https://adminrules.idaho.gov/rules/current/{citation}/',          72),
	(N'StateAdminCode', N'NAME:ILLINOIS',             N'Regulation', N'https://www.ilsos.gov/jcar/admincode/',             N'https://www.ilsos.gov/jcar/admincode/{citation}',               72),
	(N'StateAdminCode', N'NAME:INDIANA',              N'Regulation', N'https://www.in.gov/legislative/iac/',               N'https://www.in.gov/legislative/iac/{citation}',                 72),
	(N'StateAdminCode', N'NAME:IOWA',                 N'Regulation', N'https://www.legis.iowa.gov/law/administrativeRules', N'https://www.legis.iowa.gov/law/administrativeRules/rules?agency={citation}', 72),
	(N'StateAdminCode', N'NAME:KANSAS',               N'Regulation', N'https://www.sos.ks.gov/pubs/pubs_kar.aspx',          N'https://www.sos.ks.gov/pubs/kar/{citation}',                     72),
	(N'StateAdminCode', N'NAME:KENTUCKY',             N'Regulation', N'https://apps.legislature.ky.gov/law/kar/',          N'https://apps.legislature.ky.gov/law/kar/titles/{citation}/',     72),
	(N'StateAdminCode', N'NAME:LOUISIANA',            N'Regulation', N'https://www.doa.la.gov/doa/osr/lac/',               N'https://www.doa.la.gov/doa/osr/lac/{citation}',                 72),
	(N'StateAdminCode', N'NAME:MAINE',                N'Regulation', N'https://www.maine.gov/sos/cec/rules/',              N'https://www.maine.gov/sos/cec/rules/{citation}',                72),
	(N'StateAdminCode', N'NAME:MARYLAND',             N'Regulation', N'https://dsd.maryland.gov/Pages/COMARHome.aspx',     N'https://dsd.maryland.gov/regulations/Pages/{citation}.aspx',    72),
	(N'StateAdminCode', N'NAME:MASSACHUSETTS',        N'Regulation', N'https://www.mass.gov/regulations',                  N'https://www.mass.gov/regulations/{citation}',                   72),
	(N'StateAdminCode', N'NAME:MICHIGAN',             N'Regulation', N'https://www.michigan.gov/lara/bureau-list/moahr/admin-rules', N'https://ars.apps.lara.state.mi.us/AdminCode/DeptBureauAdminCode?Department={citation}', 72),
	(N'StateAdminCode', N'NAME:MINNESOTA',            N'Regulation', N'https://www.revisor.mn.gov/rules/',                 N'https://www.revisor.mn.gov/rules/{citation}/',                  72),
	(N'StateAdminCode', N'NAME:MISSISSIPPI',          N'Regulation', N'https://www.sos.ms.gov/adminsearch/',               N'https://www.sos.ms.gov/adminsearch/{citation}',                 72),
	(N'StateAdminCode', N'NAME:MISSOURI',             N'Regulation', N'https://www.sos.mo.gov/adrules/csr/csr',            N'https://www.sos.mo.gov/cmsimages/adrules/csr/current/{citation}.pdf', 72),
	(N'StateAdminCode', N'NAME:MONTANA',              N'Regulation', N'https://rules.mt.gov/',                             N'https://rules.mt.gov/gateway/ChapterHome.asp?Chapter={citation}', 72),
	(N'StateAdminCode', N'NAME:NEBRASKA',             N'Regulation', N'https://www.nebraska.gov/rules-and-regs/regsearch/', N'https://www.nebraska.gov/rules-and-regs/regsearch/Rules/{citation}', 72),
	(N'StateAdminCode', N'NAME:NEVADA',               N'Regulation', N'https://www.leg.state.nv.us/nac/',                  N'https://www.leg.state.nv.us/nac/NAC-{citation}.html',           72),
	(N'StateAdminCode', N'NAME:NEW HAMPSHIRE',        N'Regulation', N'https://www.gencourt.state.nh.us/rules/',           N'https://www.gencourt.state.nh.us/rules/state_agencies/{citation}.html', 72),
	(N'StateAdminCode', N'NAME:NEW JERSEY',           N'Regulation', N'https://www.njoal.gov/',                            N'https://www.njoal.gov/{citation}',                             72),
	(N'StateAdminCode', N'NAME:NEW MEXICO',           N'Regulation', N'https://www.srca.nm.gov/nmac-home/',                N'https://www.srca.nm.gov/nmac/{citation}',                       72),
	(N'StateAdminCode', N'NAME:NEW YORK',             N'Regulation', N'https://govt.westlaw.com/nycrr/',                   N'https://govt.westlaw.com/nycrr/Document/{citation}',            72),
	(N'StateAdminCode', N'NAME:NORTH CAROLINA',       N'Regulation', N'https://www.oah.nc.gov/rules-division/administrative-code', N'http://reports.oah.state.nc.us/ncac/title%20{citation}/', 72),
	(N'StateAdminCode', N'NAME:NORTH DAKOTA',         N'Regulation', N'https://www.legis.nd.gov/agency-rules',             N'https://www.legis.nd.gov/information/acdata/pdf/{citation}.pdf', 72),
	(N'StateAdminCode', N'NAME:OHIO',                 N'Regulation', N'https://codes.ohio.gov/ohio-administrative-code',   N'https://codes.ohio.gov/ohio-administrative-code/{citation}',    72),
	(N'StateAdminCode', N'NAME:OKLAHOMA',             N'Regulation', N'https://oklahoma.gov/oar/oklahoma-administrative-code.html', N'https://law.justia.com/codes/oklahoma/{citation}/',    72),
	(N'StateAdminCode', N'NAME:OREGON',               N'Regulation', N'https://secure.sos.state.or.us/oard/',              N'https://secure.sos.state.or.us/oard/view.action?ruleNumber={citation}', 72),
	(N'StateAdminCode', N'NAME:PENNSYLVANIA',         N'Regulation', N'https://www.pacodeandbulletin.gov/',                N'https://www.pacodeandbulletin.gov/Display/pacode?file={citation}', 72),
	(N'StateAdminCode', N'NAME:RHODE ISLAND',         N'Regulation', N'https://rules.sos.ri.gov/',                         N'https://rules.sos.ri.gov/regulations/{citation}',              72),
	(N'StateAdminCode', N'NAME:SOUTH CAROLINA',       N'Regulation', N'https://www.scstatehouse.gov/coderegs/statmast.php', N'https://www.scstatehouse.gov/coderegs/{citation}.php',         72),
	(N'StateAdminCode', N'NAME:SOUTH DAKOTA',         N'Regulation', N'https://sdlegislature.gov/Rules',                   N'https://sdlegislature.gov/Rules/Administrative/{citation}',     72),
	(N'StateAdminCode', N'NAME:TENNESSEE',            N'Regulation', N'https://publications.tnsosfiles.com/rules/',        N'https://publications.tnsosfiles.com/rules/{citation}',          72),
	(N'StateAdminCode', N'NAME:TEXAS',                N'Regulation', N'https://texreg.sos.state.tx.us/public/readtac$ext.ViewTAC', N'https://texreg.sos.state.tx.us/public/readtac$ext.TacPage?tac={citation}', 72),
	(N'StateAdminCode', N'NAME:UTAH',                 N'Regulation', N'https://adminrules.utah.gov/',                      N'https://adminrules.utah.gov/public/rule/{citation}/Current%20Rules', 72),
	(N'StateAdminCode', N'NAME:VERMONT',              N'Regulation', N'https://sos.vermont.gov/administrative-rules/',     N'https://sos.vermont.gov/administrative-rules/{citation}',       72),
	(N'StateAdminCode', N'NAME:VIRGINIA',             N'Regulation', N'https://law.lis.virginia.gov/admincode/',           N'https://law.lis.virginia.gov/admincode/{citation}/',           72),
	(N'StateAdminCode', N'NAME:WASHINGTON',           N'Regulation', N'https://app.leg.wa.gov/wac/',                       N'https://app.leg.wa.gov/wac/default.aspx?cite={citation}',       72),
	(N'StateAdminCode', N'NAME:WEST VIRGINIA',        N'Regulation', N'https://apps.sos.wv.gov/adlaw/csr/',                N'https://apps.sos.wv.gov/adlaw/csr/rule.aspx?document={citation}', 72),
	(N'StateAdminCode', N'NAME:WISCONSIN',            N'Regulation', N'https://docs.legis.wisconsin.gov/code/admin_code',  N'https://docs.legis.wisconsin.gov/code/admin_code/{citation}',   72),
	(N'StateAdminCode', N'NAME:WYOMING',              N'Regulation', N'https://rules.wyo.gov/',                            N'https://rules.wyo.gov/Search.aspx?mode=1&agency={citation}',    72)
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
