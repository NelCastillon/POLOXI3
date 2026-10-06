-- ============================================================================
-- 0400: California jurisdiction concept->authority seed data.
--
-- WHY: The concept->authority map (POLOXI.Legal_LegalConceptAuthority) shipped
-- federal/UCC rows (0184) and later New-York-specific rows (0390/0391). A matter
-- venued in California therefore resolved its generic doctrines (for example
-- "comparative fault", "wrongful death", "statute limitations personal injury")
-- to NEW YORK authorities (CPLR / EPTL), which the post-retrieval jurisdiction
-- scope gate then correctly dropped as SCOPE_INELIGIBLE -- leaving California
-- personal-injury branches with ZERO legal evidence.
--
-- This migration seeds, into the existing DB-backed reference table (DB is the
-- source of truth; no hardcoded arrays), the California statutory authorities the
-- LEGAL grounding pipeline resolves concepts against. Paired with the application
-- jurisdiction gate in ResolveConceptAuthorities, California matters now resolve
-- California citations instead of an out-of-state seed.
--
-- Citation identities follow the corrected California routing (0336-0338):
-- wrongful death / survival are in the Code of Civil Procedure (CCP s 377.60 /
-- s 377.30), NOT the stale "Civil Code s 377.60" alias. CitationText spells out
-- the state + full code name (like the working NY rows) so the jurisdiction
-- detector resolves "California" and VerificationTokens is the distinctive
-- section number a retrieved source must contain to pass identity verification.
--
-- All rows are platform-scope (TenantId NULL) and tenant-overridable. The MERGE
-- keys on the natural business key (TenantId + ConceptKeywords + CitationText),
-- matching UX_Legal_LegalConceptAuthority_TenantConceptCitation, so it is
-- idempotent / re-runnable.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'POLOXI') EXEC(N'CREATE SCHEMA POLOXI');

IF OBJECT_ID(N'POLOXI.Legal_LegalConceptAuthority', N'U') IS NOT NULL
BEGIN
	MERGE POLOXI.Legal_LegalConceptAuthority AS target
	USING (VALUES
		-- Statutes of limitation (CCP)
		(N'statute limitations personal injury',    N'California Code of Civil Procedure § 335.1',  N'Statute', N'335.1',  N'CA CCP', N'Two-year limitation for personal injury / wrongful death actions'),
		(N'statute limitations negligence',         N'California Code of Civil Procedure § 335.1',  N'Statute', N'335.1',  N'CA CCP', N'Two-year limitation for negligence-based personal injury actions'),
		(N'statute limitations wrongful death',      N'California Code of Civil Procedure § 335.1',  N'Statute', N'335.1',  N'CA CCP', N'Two-year limitation for wrongful death actions'),
		(N'statute limitations medical malpractice', N'California Code of Civil Procedure § 340.5',  N'Statute', N'340.5',  N'CA CCP', N'Three-year / one-year limitation for professional (medical) negligence'),
		-- Wrongful death & survival (CCP -- corrected identity per 0336-0338)
		(N'wrongful death',                          N'California Code of Civil Procedure § 377.60', N'Statute', N'377.60', N'CA CCP', N'Persons entitled to bring a wrongful death action'),
		(N'wrongful death standing',                 N'California Code of Civil Procedure § 377.60', N'Statute', N'377.60', N'CA CCP', N'Standing / proper plaintiffs for wrongful death'),
		(N'survival action',                         N'California Code of Civil Procedure § 377.30', N'Statute', N'377.30', N'CA CCP', N'Survival of a cause of action to the decedent''s successor in interest'),
		(N'survival damages',                        N'California Code of Civil Procedure § 377.34', N'Statute', N'377.34', N'CA CCP', N'Damages recoverable in a survival action'),
		-- Negligence / fault (Civil Code)
		(N'negligence duty care',                    N'California Civil Code § 1714',                N'Statute', N'1714',   N'CA Civ', N'General duty of ordinary care; responsibility for want of ordinary care'),
		(N'comparative negligence',                  N'California Civil Code § 1714',                N'Statute', N'1714',   N'CA Civ', N'Liability for want of ordinary care; comparative fault reduces recovery'),
		(N'comparative fault',                       N'California Civil Code § 1431.2',              N'Statute', N'1431.2', N'CA Civ', N'Several liability for non-economic damages in proportion to fault (Prop 51)'),
		(N'joint several liability',                 N'California Civil Code § 1431.2',              N'Statute', N'1431.2', N'CA Civ', N'Several liability for non-economic damages allocated by percentage of fault'),
		-- Motor vehicle liability (Vehicle Code)
		(N'negligence per se vehicle',               N'California Vehicle Code § 17150',             N'Statute', N'17150',  N'CA Veh', N'Owner liability for negligent operation of a motor vehicle'),
		(N'motor vehicle liability owner',           N'California Vehicle Code § 17150',             N'Statute', N'17150',  N'CA Veh', N'Liability of a vehicle owner for the negligence of a permissive user')
	) AS source (ConceptKeywords, CitationText, AuthorityKindCode, VerificationTokens, SourceLabel, DisplayName)
	ON target.TenantId IS NULL
		AND target.ConceptKeywords = source.ConceptKeywords
		AND target.CitationText = source.CitationText
		AND target.IsDeleted = 0
	WHEN MATCHED THEN
		UPDATE SET
			target.AuthorityKindCode = source.AuthorityKindCode,
			target.VerificationTokens = source.VerificationTokens,
			target.SourceLabel = source.SourceLabel,
			target.DisplayName = source.DisplayName,
			target.IsActive = 1,
			target.ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (LegalConceptAuthorityId, TenantId, ConceptKeywords, CitationText, AuthorityKindCode, VerificationTokens, SourceLabel, DisplayName, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
		VALUES (NEWID(), NULL, source.ConceptKeywords, source.CitationText, source.AuthorityKindCode, source.VerificationTokens, source.SourceLabel, source.DisplayName, 1, 0, SYSUTCDATETIME(), 0);
END;

COMMIT TRANSACTION;
