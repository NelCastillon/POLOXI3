SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ──────────────────────────────────────────────────────────────────────────────────────────────
-- Phase 3 — Immutable source anchoring (Legal_SourceAssertion).
--
-- Every evidence item and proposition-support claim in POLOXI is derived from text that lives in a
-- specific passage (POLOXI.Legal_DocumentPassage) of a specific, hash-sealed document version
-- (POLOXI.Legal_MatterDocumentVersion.Sha256Hash). Retrieval, embeddings, and LLM reasoning can all
-- drift; the source text cannot. This migration adds an append-only anchoring table that pins each
-- claim to an EXACT character span within a passage and records the content hash of the quoted span
-- so the anchor can be independently re-verified at any later time.
--
-- Design rules honoured here:
--   • Append-only / immutable: once written, an assertion row is never mutated. Corrections are
--     modelled by superseding (SupersededBySourceAssertionId) and inserting a new row. IsDeleted is
--     kept only for the standard soft-delete base contract; it is never used for revisions.
--   • Exact provenance: StartOffset/EndOffset are 0-based character offsets into the parent passage
--     PassageText; QuotedText is the literal quoted substring; QuotedTextHash is SHA-256 of QuotedText
--     so a verifier can confirm the quote matches the sealed source without re-fetching the document.
--   • Anchors point at either an evidence item OR a proposition-support edge (or both), always through
--     the passage that owns the immutable text. Tenant isolation is preserved on every row.
--
-- This is schema-only (Table stage of Table/API/UI). No repository/service/UI wiring is changed here.
-- ──────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_SourceAssertion', N'U') IS NULL
CREATE TABLE POLOXI.Legal_SourceAssertion
(
	LegalSourceAssertionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_SourceAssertion PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_SourceAssertion_Matter REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	LegalDocumentVersionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_SourceAssertion_Version REFERENCES POLOXI.Legal_MatterDocumentVersion (LegalDocumentVersionId),
	LegalDocumentPassageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_SourceAssertion_Passage REFERENCES POLOXI.Legal_DocumentPassage (LegalDocumentPassageId),
	-- Optional links to the claim(s) this span anchors. At least one is required (see CK below).
	LegalEvidenceItemId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_SourceAssertion_Evidence REFERENCES POLOXI.Legal_MatterEvidenceItem (LegalEvidenceItemId),
	LegalPropositionSupportId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_SourceAssertion_Support REFERENCES POLOXI.Legal_MatterPropositionSupport (LegalPropositionSupportId),
	-- Exact, immutable span within the parent passage text.
	StartOffset INT NOT NULL,
	EndOffset INT NOT NULL,
	QuotedText NVARCHAR(MAX) NOT NULL,
	QuotedTextHash CHAR(64) NOT NULL,
	-- Hash of the sealed source version this anchor was captured against (copied from the document
	-- version at capture time) so drift between capture and verification is detectable.
	SourceVersionHash CHAR(64) NOT NULL,
	AnchorMethodCode NVARCHAR(60) NOT NULL CONSTRAINT DF_Legal_SourceAssertion_Method DEFAULT N'EXACT_SPAN',
	VerificationStateCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_SourceAssertion_Verify DEFAULT N'UNVERIFIED',
	VerifiedDateUtc DATETIME2 NULL,
	-- Append-only revision chain: a new row supersedes an older one; older rows are never edited.
	SupersededBySourceAssertionId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_SourceAssertion_Superseded REFERENCES POLOXI.Legal_SourceAssertion (LegalSourceAssertionId),
	GenerationOriginCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_SourceAssertion_Origin DEFAULT N'DYNAMIC_LLM',
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_SourceAssertion_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_SourceAssertion_Deleted DEFAULT 0,
	CONSTRAINT CK_Legal_SourceAssertion_Offsets CHECK (StartOffset >= 0 AND EndOffset > StartOffset),
	CONSTRAINT CK_Legal_SourceAssertion_Anchor CHECK (LegalEvidenceItemId IS NOT NULL OR LegalPropositionSupportId IS NOT NULL)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_SourceAssertion') AND name=N'IX_Legal_SourceAssertion_Passage')
	CREATE INDEX IX_Legal_SourceAssertion_Passage ON POLOXI.Legal_SourceAssertion (TenantId, LegalDocumentPassageId, StartOffset, EndOffset) INCLUDE (VerificationStateCode, QuotedTextHash, SourceVersionHash) WHERE IsDeleted=0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_SourceAssertion') AND name=N'IX_Legal_SourceAssertion_Evidence')
	CREATE INDEX IX_Legal_SourceAssertion_Evidence ON POLOXI.Legal_SourceAssertion (TenantId, LegalEvidenceItemId) INCLUDE (LegalDocumentPassageId, VerificationStateCode) WHERE IsDeleted=0 AND LegalEvidenceItemId IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_SourceAssertion') AND name=N'IX_Legal_SourceAssertion_Support')
	CREATE INDEX IX_Legal_SourceAssertion_Support ON POLOXI.Legal_SourceAssertion (TenantId, LegalPropositionSupportId) INCLUDE (LegalDocumentPassageId, VerificationStateCode) WHERE IsDeleted=0 AND LegalPropositionSupportId IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.Legal_SourceAssertion') AND name=N'IX_Legal_SourceAssertion_Matter')
	CREATE INDEX IX_Legal_SourceAssertion_Matter ON POLOXI.Legal_SourceAssertion (TenantId, DecisionMatterId, VerificationStateCode) INCLUDE (LegalDocumentVersionId, LegalDocumentPassageId) WHERE IsDeleted=0;
GO

COMMIT TRANSACTION;
