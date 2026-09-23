-- Context-anchor gate for the Wide LEGAL concept->authority resolution map.
-- DEFECT: generic single-word doctrines (e.g. N'cure', N'waiver', N'good faith') were mapped to
-- specific UCC Article 2 commercial statutes. Because concept matching only required the keyword to
-- appear anywhere in the branch/query text, a non-commercial question (e.g. a child-custody / genetic
-- therapy scenario that repeatedly says "the cure...") falsely resolved to U.C.C. 2-508 (seller's cure
-- of improper tender). The citation is real, so the identity gate admitted it as live-grounded evidence
-- and it even surfaced as the outcome.
--
-- FIX (DB is the source of truth; no hardcoded arrays): add an optional ContextAnchors column. When a
-- concept row declares anchors, the concept only resolves if at least ONE anchor token also appears in
-- the text. Commercial doctrines therefore require a commercial context (goods, seller, buyer, tender,
-- delivery, sale, merchant, contract, shipment, warranty, payment). Rows with no anchors keep the
-- previous behavior (backward compatible). A wrong mapping still flows through the mandatory identity
-- gate, so this only NARROWS false positives; it never inflates confidence.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.Legal_LegalConceptAuthority',N'ContextAnchors') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_LegalConceptAuthority
		ADD ContextAnchors NVARCHAR(400) NOT NULL
			CONSTRAINT DF_Legal_LegalConceptAuthority_ContextAnchors DEFAULT N'';
END;

-- Commercial-context anchors: at least one of these must appear alongside the doctrine keyword before a
-- UCC / commercial statute can resolve. This keeps sale-of-goods statutes out of family, constitutional,
-- criminal, medical, and other non-commercial questions.
-- NOTE: the UPDATE is executed via EXEC so it is compiled AFTER the ALTER TABLE above. The migrator runs
-- the whole file as a single batch (no GO splitting), so a direct UPDATE referencing the just-added
-- ContextAnchors column would fail to compile with "Invalid column name 'ContextAnchors'".
EXEC(N'
UPDATE POLOXI.Legal_LegalConceptAuthority
	SET ContextAnchors = N''goods,seller,buyer,tender,delivery,sale,sold,merchant,contract,shipment,warranty,payment,purchase,vendor,commercial'',
		ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId IS NULL
	AND IsDeleted = 0
	AND CitationText LIKE N''U.C.C.%'';');

COMMIT TRANSACTION;
