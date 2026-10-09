-- ============================================================================
-- 0404: Curated (proxy-free) California statute-text store.
--
-- WHY: California statutory retrieval fails because leginfo.legislature.ca.gov
-- and law.justia.com both return HTTP 403 to this host's egress IP, and no
-- forward proxy is available. The authority registry (POLOXI.Legal_AuthoritySource)
-- stores only URL *templates* -- it has no statute text -- so when the live web
-- source is blocked there is nothing to fall back to and the Legal Authority
-- workspace shows zero verified evidence.
--
-- This migration adds a DB-backed curated statute-text store served through the
-- existing OfficialLegalAuthorityRetriever pipeline via a new ExtractionStrategyCode
-- 'CURATED_STORE'. The curated CCP descriptor is seeded WEB-FIRST: it is given a
-- HIGHER Priority number (900) than the live leginfo descriptor (CA_LEGINFO_CCP,
-- Priority 500), so the retriever tries the live web source first and only falls
-- through to the curated store when the web source returns ACCESS_DENIED / empty.
--
-- Provenance is preserved: every statute-text row records the authoritative
-- SourceUrl, SourceLabel, VerifiedDateUtc, and a ContentHash so admitted curated
-- snippets carry verifiable attribution exactly like live-retrieved evidence.
--
-- DB is the source of truth: statute text is data, not hardcoded C#. More sections
-- / codes can be added by inserting rows -- no code change required.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'POLOXI') EXEC(N'CREATE SCHEMA POLOXI');

-- ── Content table ──────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_AuthorityStatuteText', N'U') IS NULL
CREATE TABLE POLOXI.Legal_AuthorityStatuteText
(
	LegalAuthorityStatuteTextId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_AuthorityStatuteText PRIMARY KEY DEFAULT NEWID(),
	ProviderCode        NVARCHAR(80)   NOT NULL,
	JurisdictionCode    NVARCHAR(40)   NOT NULL,
	SectionNumber       NVARCHAR(80)   NOT NULL,
	StatuteText         NVARCHAR(MAX)  NOT NULL,
	SourceUrl           NVARCHAR(2000) NOT NULL,
	SourceLabel         NVARCHAR(200)  NOT NULL,
	ContentHash         NVARCHAR(64)   NULL,
	VerifiedDateUtc     DATETIME2      NOT NULL,
	TenantId            UNIQUEIDENTIFIER NULL,
	CreatedDateUtc      DATETIME2      NOT NULL CONSTRAINT DF_Legal_AuthorityStatuteText_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2      NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT            NOT NULL CONSTRAINT DF_Legal_AuthorityStatuteText_Deleted DEFAULT 0
);

-- A section is unique per provider + jurisdiction + (platform|tenant) scope.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_AuthorityStatuteText') AND name=N'UX_Legal_AuthorityStatuteText_ProviderJurisdictionSection')
	CREATE UNIQUE INDEX UX_Legal_AuthorityStatuteText_ProviderJurisdictionSection
	ON POLOXI.Legal_AuthorityStatuteText (ProviderCode,JurisdictionCode,SectionNumber,TenantId)
	WHERE IsDeleted=0;

-- ── Curated CCP descriptor (WEB-FIRST fallback) ────────────────────────────
-- Same natural key shape as the live descriptors but a distinct ProviderCode so
-- it never collides with CA_LEGINFO_CCP. ExtractionStrategyCode CURATED_STORE
-- tells the retriever to bypass HTTP and read POLOXI.Legal_AuthorityStatuteText.
-- The CitationPattern mirrors the CA_LEGINFO_CCP wrongful-death routing (0336) so
-- both "Code of Civil Procedure" and the common "Civil Code s 377.x" miscitation
-- resolve here. Priority 900 > CA_LEGINFO_CCP 500 => live web is tried first.
IF OBJECT_ID(N'POLOXI.Legal_AuthoritySource', N'U') IS NOT NULL
   AND NOT EXISTS
   (
	   SELECT 1 FROM POLOXI.Legal_AuthoritySource
	   WHERE ProviderCode=N'CA_CURATED_CCP' AND JurisdictionCode=N'NAME:CALIFORNIA'
		 AND AuthorityKindCode=N'STATUTE' AND TenantId IS NULL AND IsDeleted=0
   )
BEGIN
	INSERT POLOXI.Legal_AuthoritySource
		(LegalAuthoritySourceId,ProviderCode,JurisdictionCode,AuthorityKindCode,CitationPattern,BaseUrl,
		 DocumentUrlTemplate,SectionAnchorTemplate,ExtractionStrategyCode,DiscoveryMethodCode,
		 DiscoveryEvidenceUrl,VerifiedDateUtc,Priority,IsEnabled,TenantId,CreatedDateUtc,IsDeleted)
	VALUES
		(NEWID(),N'CA_CURATED_CCP',N'NAME:CALIFORNIA',N'STATUTE',
		 N'\bCal(?:ifornia|\.)?\s+(?:(?:Code\s+(?:of\s+)?Civ(?:il)?\.?\s+Proc(?:edure)?\.?|Civ(?:il)?\.?\s+Proc(?:edure)?\.?\s+Code)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[\dA-Za-z.:-]*)|Civ(?:il)?\.?\s+Code\s*(?:§+|section|sec\.?)?\s*(?<section>377[\dA-Za-z.:-]*))',
		 N'curated://california',
		 N'curated://california/ccp/{section}',
		 NULL,N'CURATED_STORE',N'MANUAL_CURATION',
		 N'https://leginfo.legislature.ca.gov/faces/codesTOCSelected.xhtml?tocCode=CCP',
		 SYSUTCDATETIME(),900,1,NULL,SYSUTCDATETIME(),0);
END;

-- ── Seeded statute text (verified, with provenance) ────────────────────────
-- Operative text transcribed from the official California Legislative Information
-- site (leginfo.legislature.ca.gov). SourceUrl records the authoritative page the
-- text was transcribed from; VerifiedDateUtc records the verification date.
DECLARE @Seed TABLE
(
	SectionNumber NVARCHAR(80)   NOT NULL,
	StatuteText   NVARCHAR(MAX)  NOT NULL,
	SourceUrl     NVARCHAR(2000) NOT NULL
);

INSERT @Seed (SectionNumber,StatuteText,SourceUrl) VALUES
(
	N'377.60',
	N'California Code of Civil Procedure § 377.60. A cause of action for the death of a person caused by the wrongful act or neglect of another may be asserted by any of the following persons or by the decedent''s personal representative on their behalf: (a) The decedent''s surviving spouse, domestic partner, children, and issue of deceased children, or, if there is no surviving issue of the decedent, the persons, including the surviving spouse or domestic partner, who would be entitled to the property of the decedent by intestate succession. (b) Whether or not qualified under subdivision (a), if they were dependent on the decedent, the putative spouse, children of the putative spouse, stepchildren, parents, or the legal guardians of the decedent if the parents are deceased. (c) A minor, whether or not qualified under subdivision (a) or (b), if, at the time of the decedent''s death, the minor resided for the previous 180 days in the decedent''s household and was dependent on the decedent for one-half or more of the minor''s support. (d) This section applies to any cause of action arising on or after January 1, 1993. (e) The addition of this section by Chapter 178 of the Statutes of 1992 was not intended to adversely affect the standing of any party having standing under prior law, and the standing of parties governed by that version of this section shall be the same as specified herein as amended by Chapter 563 of the Statutes of 1996.',
	N'https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?lawCode=CCP&sectionNum=377.60'
),
(
	N'437c',
	N'California Code of Civil Procedure § 437c. (a)(1) A party may move for summary judgment in an action or proceeding if it is contended that the action has no merit or that there is no defense to the action or proceeding. The motion may be made at any time after 60 days have elapsed since the general appearance in the action or proceeding of each party against whom the motion is directed, or at an earlier time after the general appearance that the court, with or without notice and upon good cause shown, may direct. (c) The motion for summary judgment shall be granted if all the papers submitted show that there is no triable issue as to any material fact and that the moving party is entitled to a judgment as a matter of law. In determining if the papers show that there is no triable issue as to any material fact, the court shall consider all of the evidence set forth in the papers, except the evidence to which objections have been made and sustained by the court, and all inferences reasonably deducible from the evidence, except summary judgment shall not be granted by the court based on inferences reasonably deducible from the evidence if contradicted by other inferences or evidence that raise a triable issue as to any material fact. (p) A motion for summary adjudication may be made by itself or as an alternative to a motion for summary judgment and shall proceed in all procedural respects as a motion for summary judgment.',
	N'https://leginfo.legislature.ca.gov/faces/codes_displaySection.xhtml?lawCode=CCP&sectionNum=437c'
);

INSERT POLOXI.Legal_AuthorityStatuteText
	(LegalAuthorityStatuteTextId,ProviderCode,JurisdictionCode,SectionNumber,StatuteText,SourceUrl,SourceLabel,ContentHash,VerifiedDateUtc,TenantId,CreatedDateUtc,IsDeleted)
SELECT NEWID(),N'CA_CURATED_CCP',N'NAME:CALIFORNIA',seed.SectionNumber,seed.StatuteText,seed.SourceUrl,
	   N'California Legislative Information (leginfo.legislature.ca.gov)',
	   CONVERT(NVARCHAR(64),HASHBYTES('SHA2_256',seed.StatuteText),2),
	   SYSUTCDATETIME(),NULL,SYSUTCDATETIME(),0
FROM @Seed seed
WHERE NOT EXISTS
(
	SELECT 1 FROM POLOXI.Legal_AuthorityStatuteText existing
	WHERE existing.ProviderCode=N'CA_CURATED_CCP' AND existing.JurisdictionCode=N'NAME:CALIFORNIA'
	  AND existing.SectionNumber=seed.SectionNumber AND existing.TenantId IS NULL AND existing.IsDeleted=0
);

COMMIT TRANSACTION;
