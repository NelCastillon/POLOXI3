SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0463: Canonical Evidence Type binding for semantic extraction.
--   The LEGAL_DOCUMENT_SEMANTIC_EXTRACTION call now receives the Domain Pack Evidence Type vocabulary
--   (POLOXI.Legal_DecisionDomainPackEvidenceType) and its output is governed exact -> normalized ->
--   UNCLASSIFIED, so Legal_MatterEvidenceItem.EvidenceTypeCode matches the codes Wide2 scope/presence
--   gating keys on (previously free-form: ~100 ad-hoc codes per matter, DocumentPresent=0 everywhere).
--   RawEvidenceTypeCode preserves the model's original code whenever governance rewrote it, so residual
--   mappings remain reviewable. Additive and idempotent.
-- ============================================================================
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_MatterEvidenceItem',N'U') IS NOT NULL
   AND COL_LENGTH(N'POLOXI.Legal_MatterEvidenceItem',N'RawEvidenceTypeCode') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_MatterEvidenceItem ADD RawEvidenceTypeCode NVARCHAR(120) NULL;
END;

COMMIT TRANSACTION;
