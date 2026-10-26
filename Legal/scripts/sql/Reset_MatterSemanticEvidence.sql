-- =====================================================================================================
-- Reset_MatterSemanticEvidence.sql
--
-- Purpose : Remove LLM-generated semantic evidence for ONE matter so the corpus activation pass
--           re-runs semantic extraction (LEGAL_DOCUMENT_SEMANTIC_EXTRACTION) against the existing
--           passages. Documents, versions, passages, binaries, matter configuration, and any
--           proposition that is still supported by other evidence are preserved.
--
-- Scope   : Evidence-only reset. This is intentionally lighter than the API purge endpoint
--           (DELETE matters/{id}/corpus/documents), which also removes passages/versions/documents
--           and forces re-OCR.
--
-- Effect  : After running, every clean version with passages becomes eligible again in
--           LegalDocumentCorpusRepository.GetPendingCorpusVersionsAsync, and the DYNAMIC_LLM
--           short-circuit in SaveSemanticProposalAsync is cleared. Trigger re-activation via
--           POST matters/{matterId}/corpus/activate or by running a decision on the matter.
--
-- Usage   : Set @MatterId, run in one batch. Idempotent (safe to re-run).
-- =====================================================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @MatterId UNIQUEIDENTIFIER = '83EA6EF4-66CB-4DCB-BFA2-81A90F1C7A9B'; -- Sapini, Justin — MVA

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @MatterId)
BEGIN
	DECLARE @MatterIdText NVARCHAR(36) = CONVERT(NVARCHAR(36), @MatterId);
	RAISERROR('Matter %s not found.', 16, 1, @MatterIdText);
	RETURN;
END

BEGIN TRAN;

-- Versions belonging to the matter (non-deleted).
DECLARE @Versions TABLE (LegalDocumentVersionId UNIQUEIDENTIFIER PRIMARY KEY);
INSERT @Versions (LegalDocumentVersionId)
SELECT v.LegalDocumentVersionId
FROM POLOXI.Legal_MatterDocumentVersion v
INNER JOIN POLOXI.Legal_MatterDocument d ON d.LegalDocumentId = v.LegalDocumentId
WHERE d.DecisionMatterId = @MatterId AND v.IsDeleted = 0;

-- 1. Source assertions anchored to the matter's evidence items (FK child of Legal_MatterEvidenceItem).
DELETE s
FROM POLOXI.Legal_SourceAssertion s
INNER JOIN POLOXI.Legal_MatterEvidenceItem e ON e.LegalEvidenceItemId = s.LegalEvidenceItemId
WHERE e.DecisionMatterId = @MatterId;
DECLARE @SourceAssertions INT = @@ROWCOUNT;

-- 2. Proposition support edges from the matter's evidence items (FK child of Legal_MatterEvidenceItem).
DELETE p
FROM POLOXI.Legal_MatterPropositionSupport p
INNER JOIN POLOXI.Legal_MatterEvidenceItem e ON e.LegalEvidenceItemId = p.LegalEvidenceItemId
WHERE e.DecisionMatterId = @MatterId;
DECLARE @Supports INT = @@ROWCOUNT;

-- 3. The evidence items themselves.
DELETE FROM POLOXI.Legal_MatterEvidenceItem
WHERE DecisionMatterId = @MatterId;
DECLARE @Evidence INT = @@ROWCOUNT;

-- 4. LLM-originated fact propositions that are now orphaned (no remaining support edges).
--    Non-LLM (attorney/seed) propositions are never touched.
DELETE f
FROM POLOXI.Legal_MatterFactProposition f
WHERE f.DecisionMatterId = @MatterId
  AND f.GenerationOriginCode = N'DYNAMIC_LLM'
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterPropositionSupport s
				  WHERE s.LegalFactPropositionId = f.LegalFactPropositionId AND s.IsDeleted = 0);
DECLARE @OrphanLlmPropositions INT = @@ROWCOUNT;

-- 5. Clear the semantic-activation marker so the versions are eligible for re-activation.
UPDATE v
SET v.SemanticActivationStatusCode = NULL,
	v.SemanticActivatedDateUtc     = NULL,
	v.ModifiedDateUtc              = SYSUTCDATETIME()
FROM POLOXI.Legal_MatterDocumentVersion v
INNER JOIN @Versions x ON x.LegalDocumentVersionId = v.LegalDocumentVersionId;
DECLARE @VersionsReset INT = @@ROWCOUNT;

COMMIT;

-- Summary
SELECT
	@MatterId                AS MatterId,
	@SourceAssertions        AS SourceAssertionsRemoved,
	@Supports                AS PropositionSupportsRemoved,
	@Evidence                AS EvidenceItemsRemoved,
	@OrphanLlmPropositions   AS OrphanLlmPropositionsRemoved,
	@VersionsReset           AS VersionsReset,
	(SELECT COUNT(1) FROM POLOXI.Legal_DocumentPassage p
	 WHERE p.IsDeleted = 0 AND p.LegalDocumentVersionId IN (SELECT LegalDocumentVersionId FROM @Versions)) AS PassagesKept;
