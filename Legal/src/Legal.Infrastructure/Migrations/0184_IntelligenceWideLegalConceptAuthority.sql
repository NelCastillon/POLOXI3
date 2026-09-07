-- Concept -> legal authority resolution map for the Wide LEGAL grounding pipeline.
-- Concept-only legal questions (e.g. "cure of delay", "implied waiver", "perfect tender")
-- never produce an explicit citation, so the citation-driven Cornell LII / GovInfo retrievers
-- never fire and zero grounding evidence is admitted. This DB-backed map (DB is the source of
-- truth; no hardcoded arrays) deterministically resolves decisive doctrinal concepts to concrete
-- UCC / U.S. Code citations. The resolved citation flows through the existing retrieval path and
-- the MANDATORY identity gate, so a wrong mapping simply fails verification and is dropped.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'POLOXI') EXEC(N'CREATE SCHEMA POLOXI');

IF OBJECT_ID(N'POLOXI.Legal_LegalConceptAuthority',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_LegalConceptAuthority
	(
		LegalConceptAuthorityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_LegalConceptAuthority PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NULL,
		-- Space-delimited keyword phrase that must ALL appear in the branch text to match this concept
		-- (deterministic AND-match; e.g. "perfect tender" or "cure").
		ConceptKeywords NVARCHAR(200) NOT NULL,
		-- The canonical citation text handed to the retriever (e.g. N'U.C.C. § 2-508').
		CitationText NVARCHAR(120) NOT NULL,
		-- Legal authority kind routed by the retriever: Statute | Regulation | Case | Any.
		AuthorityKindCode NVARCHAR(20) NOT NULL CONSTRAINT DF_Legal_LegalConceptAuthority_Kind DEFAULT N'Statute',
		-- Comma-delimited distinctive tokens a retrieved source must contain to verify identity
		-- (e.g. N'2,508'). Empty falls back to numeric extraction from CitationText.
		VerificationTokens NVARCHAR(200) NOT NULL CONSTRAINT DF_Legal_LegalConceptAuthority_Tokens DEFAULT N'',
		SourceLabel NVARCHAR(120) NULL,
		DisplayName NVARCHAR(200) NULL,
		IsActive BIT NOT NULL CONSTRAINT DF_Legal_LegalConceptAuthority_IsActive DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_Legal_LegalConceptAuthority_SortOrder DEFAULT 0,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Legal_LegalConceptAuthority_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_LegalConceptAuthority_Deleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_Legal_LegalConceptAuthority_TenantConceptCitation
		ON POLOXI.Legal_LegalConceptAuthority(TenantId,ConceptKeywords,CitationText) WHERE IsDeleted=0;
END;

-- Seed core UCC Article 2 (sale of goods) doctrines plus common U.S. Code references. These are the
-- decisive doctrines behind typical contract-termination / breach / waiver questions that otherwise
-- ground to nothing. All rows are platform-scope (TenantId NULL) and tenant-overridable.
MERGE POLOXI.Legal_LegalConceptAuthority AS target
USING (VALUES
	-- UCC Article 2 - Sales
	(N'perfect tender',        N'U.C.C. § 2-601', N'Statute', N'2,601', N'Cornell LII', N'Perfect tender rule (buyer''s rights on improper delivery)'),
	(N'cure',                  N'U.C.C. § 2-508', N'Statute', N'2,508', N'Cornell LII', N'Seller''s right to cure improper tender or delivery'),
	(N'installment contract',  N'U.C.C. § 2-612', N'Statute', N'2,612', N'Cornell LII', N'Installment contracts; substantial impairment; breach'),
	(N'substantial impairment',N'U.C.C. § 2-612', N'Statute', N'2,612', N'Cornell LII', N'Substantial impairment of an installment or the whole contract'),
	(N'modification waiver',   N'U.C.C. § 2-209', N'Statute', N'2,209', N'Cornell LII', N'Modification, rescission and waiver'),
	(N'waiver',                N'U.C.C. § 2-209', N'Statute', N'2,209', N'Cornell LII', N'Waiver of contract terms (modification, rescission and waiver)'),
	(N'course performance',    N'U.C.C. § 2-208', N'Statute', N'2,208', N'Cornell LII', N'Course of performance or practical construction'),
	(N'rejection goods',       N'U.C.C. § 2-602', N'Statute', N'2,602', N'Cornell LII', N'Manner and effect of rightful rejection'),
	(N'acceptance goods',      N'U.C.C. § 2-606', N'Statute', N'2,606', N'Cornell LII', N'What constitutes acceptance of goods'),
	(N'revocation acceptance', N'U.C.C. § 2-608', N'Statute', N'2,608', N'Cornell LII', N'Revocation of acceptance in whole or in part'),
	(N'anticipatory repudiation',N'U.C.C. § 2-610',N'Statute', N'2,610', N'Cornell LII', N'Anticipatory repudiation'),
	(N'adequate assurance',    N'U.C.C. § 2-609', N'Statute', N'2,609', N'Cornell LII', N'Right to adequate assurance of performance'),
	(N'breach remedies buyer', N'U.C.C. § 2-711', N'Statute', N'2,711', N'Cornell LII', N'Buyer''s remedies in general on seller''s breach'),
	(N'breach remedies seller',N'U.C.C. § 2-703', N'Statute', N'2,703', N'Cornell LII', N'Seller''s remedies in general on buyer''s breach'),
	(N'statute frauds',        N'U.C.C. § 2-201', N'Statute', N'2,201', N'Cornell LII', N'Formal requirements; statute of frauds'),
	(N'good faith',            N'U.C.C. § 1-304', N'Statute', N'1,304', N'Cornell LII', N'Obligation of good faith'),
	(N'unconscionable',        N'U.C.C. § 2-302', N'Statute', N'2,302', N'Cornell LII', N'Unconscionable contract or clause'),
	(N'implied warranty merchantability',N'U.C.C. § 2-314',N'Statute',N'2,314',N'Cornell LII',N'Implied warranty: merchantability; usage of trade'),
	(N'implied warranty fitness',N'U.C.C. § 2-315',N'Statute',N'2,315', N'Cornell LII', N'Implied warranty: fitness for particular purpose'),
	(N'liquidated damages',    N'U.C.C. § 2-718', N'Statute', N'2,718', N'Cornell LII', N'Liquidation or limitation of damages'),
	-- U.S. Code (common federal references)
	(N'copyright infringement',N'17 U.S.C. § 501',N'Statute', N'17,501',N'Cornell LII', N'Copyright infringement'),
	(N'fair use',              N'17 U.S.C. § 107',N'Statute', N'17,107',N'Cornell LII', N'Limitations on exclusive rights: fair use'),
	(N'trademark infringement',N'15 U.S.C. § 1114',N'Statute',N'15,1114',N'Cornell LII',N'Remedies; infringement (Lanham Act)'),
	(N'bankruptcy automatic stay',N'11 U.S.C. § 362',N'Statute',N'11,362',N'Cornell LII',N'Automatic stay')
) AS source(ConceptKeywords,CitationText,AuthorityKindCode,VerificationTokens,SourceLabel,DisplayName)
   ON target.TenantId IS NULL AND target.ConceptKeywords=source.ConceptKeywords AND target.CitationText=source.CitationText AND target.IsDeleted=0
WHEN MATCHED THEN
	UPDATE SET
		target.AuthorityKindCode=source.AuthorityKindCode,
		target.VerificationTokens=source.VerificationTokens,
		target.SourceLabel=source.SourceLabel,
		target.DisplayName=source.DisplayName,
		target.IsActive=1,
		target.ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN
	INSERT(LegalConceptAuthorityId,TenantId,ConceptKeywords,CitationText,AuthorityKindCode,VerificationTokens,SourceLabel,DisplayName,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
	VALUES(NEWID(),NULL,source.ConceptKeywords,source.CitationText,source.AuthorityKindCode,source.VerificationTokens,source.SourceLabel,source.DisplayName,1,0,SYSUTCDATETIME(),0);

COMMIT TRANSACTION;
