-- Legal evidence-verification signals for cached external knowledge snippets (deterministic, no LLM).
-- Separates three concerns that were previously collapsed:
--   1) AuthorityKind          - which legal source class the snippet was verified against.
--   2) AuthorityIdentityVerified - mandatory gate: did a retrieved source actually reference the
--                                  proposed authority? A snippet that fails this can never be rescued
--                                  by proposition scoring.
--   3) PropositionSupportScore / PropositionSupportStatus - deterministic overlap between the branch
--      claim and the snippet text, used to WEIGHT evidence contribution (not to declare legal truth).
--      Status is one of VERIFIED_SUPPORT | AUTHORITY_FOUND_BUT_SUPPORT_UNCLEAR | UNVERIFIED.
-- All nullable/defaulted so legacy rows and general-web snippets (no legal verification) remain valid.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.Legal_ExternalKnowledge',N'AuthorityKind') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_ExternalKnowledge ADD AuthorityKind NVARCHAR(20) NULL;
END;

IF COL_LENGTH(N'POLOXI.Legal_ExternalKnowledge',N'AuthorityIdentityVerified') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_ExternalKnowledge ADD AuthorityIdentityVerified BIT NOT NULL
		CONSTRAINT DF_Legal_PoloxiExternalKnowledge_AuthorityVerified DEFAULT 0;
END;

IF COL_LENGTH(N'POLOXI.Legal_ExternalKnowledge',N'PropositionSupportScore') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_ExternalKnowledge ADD PropositionSupportScore DECIMAL(5,4) NOT NULL
		CONSTRAINT DF_Legal_PoloxiExternalKnowledge_PropSupport DEFAULT 0;
END;

IF COL_LENGTH(N'POLOXI.Legal_ExternalKnowledge',N'PropositionSupportStatus') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_ExternalKnowledge ADD PropositionSupportStatus NVARCHAR(40) NULL;
END;

COMMIT TRANSACTION;
