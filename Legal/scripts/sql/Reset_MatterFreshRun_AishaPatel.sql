-- =====================================================================================================
-- Reset_MatterFreshRun_AishaPatel.sql
--
-- Purpose : FULL matter reset so the Aisha Patel case can be re-run as a brand-new Matter. Identical
--           strategy to Reset_MatterFreshRun.sql (Sapini): hard-deletes the entire document + evidence
--           corpus (documents, versions, pages, layout, passages, processing runs, evidence items,
--           propositions, supports, source assertions, lineage, upload batches, passage-pinned wide-run
--           children), clears the per-matter Evidence Type scope, and retires every prior decision/search
--           run so the cockpit starts from a clean slate.
--
-- Matter   : Resolved dynamically BY TITLE ('%Aisha%Patel%'), because this matter was created at runtime
--            and does not have a fixed seeded GUID. If more than one matter matches the title, the script
--            stops and lists the candidates so you can pin the exact @MatterId by hand.
--
-- Binaries : Document bytes live in external storage (Legal_MatterDocumentVersion.StorageReference), not
--            in SQL. Deleting the version rows detaches those references; re-uploading re-ingests/re-OCRs.
--
-- Strategy : Document/evidence corpus  -> HARD delete (FK-safe leaf-first order).
--            Prior decision/search runs -> SOFT delete (IsDeleted = 1) on the run roots.
--
-- Usage    : Review, run in one batch inside SSMS. Transactional and idempotent (safe to re-run).
-- =====================================================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ──────────────────────────────────────────────────────────────────────────────────────────────────
-- Resolve the Aisha Patel matter by title. (Set @MatterId directly if you already know the GUID.)
-- ──────────────────────────────────────────────────────────────────────────────────────────────────
DECLARE @MatterId UNIQUEIDENTIFIER = NULL;

IF @MatterId IS NULL
BEGIN
	DECLARE @MatchCount INT =
		(SELECT COUNT(*) FROM POLOXI.Legal_DecisionMatter
		 WHERE IsDeleted = 0 AND Title LIKE N'%Aisha%Patel%');

	IF @MatchCount = 0
	BEGIN
		RAISERROR('No matter found with a title matching "%%Aisha%%Patel%%". Set @MatterId manually.', 16, 1);
		RETURN;
	END

	IF @MatchCount > 1
	BEGIN
		SELECT DecisionMatterId, Title, MatterTypeCode, Jurisdiction, StatusCode, CreatedDateUtc
		FROM POLOXI.Legal_DecisionMatter
		WHERE IsDeleted = 0 AND Title LIKE N'%Aisha%Patel%'
		ORDER BY CreatedDateUtc DESC;
		RAISERROR('More than one matter matches "%%Aisha%%Patel%%". Pin the exact @MatterId from the list above.', 16, 1);
		RETURN;
	END

	SELECT @MatterId = DecisionMatterId
	FROM POLOXI.Legal_DecisionMatter
	WHERE IsDeleted = 0 AND Title LIKE N'%Aisha%Patel%';
END

IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @MatterId)
BEGIN
	DECLARE @MatterIdText NVARCHAR(36) = CONVERT(NVARCHAR(36), @MatterId);
	RAISERROR('Matter %s not found.', 16, 1, @MatterIdText);
	RETURN;
END

DECLARE @TenantId UNIQUEIDENTIFIER =
	(SELECT TOP (1) TenantId FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @MatterId);

PRINT 'Resetting matter: ' + CONVERT(NVARCHAR(36), @MatterId) + ' — '
	+ (SELECT Title FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId = @MatterId);

BEGIN TRAN;

-- ──────────────────────────────────────────────────────────────────────────────────────────────────
-- Resolve the matter's documents and versions.
-- ──────────────────────────────────────────────────────────────────────────────────────────────────
DECLARE @Documents TABLE (LegalDocumentId UNIQUEIDENTIFIER PRIMARY KEY);
INSERT @Documents (LegalDocumentId)
SELECT LegalDocumentId
FROM POLOXI.Legal_MatterDocument
WHERE DecisionMatterId = @MatterId;

DECLARE @Versions TABLE (LegalDocumentVersionId UNIQUEIDENTIFIER PRIMARY KEY);
INSERT @Versions (LegalDocumentVersionId)
SELECT v.LegalDocumentVersionId
FROM POLOXI.Legal_MatterDocumentVersion v
INNER JOIN @Documents d ON d.LegalDocumentId = v.LegalDocumentId;

-- ──────────────────────────────────────────────────────────────────────────────────────────────────
-- HARD DELETE: document + evidence corpus (FK-safe, leaf provenance first).
-- ──────────────────────────────────────────────────────────────────────────────────────────────────

-- 1. Source assertions anchored to the matter's evidence items.
DELETE s
FROM POLOXI.Legal_SourceAssertion s
INNER JOIN POLOXI.Legal_MatterEvidenceItem e ON e.LegalEvidenceItemId = s.LegalEvidenceItemId
WHERE e.DecisionMatterId = @MatterId;
DECLARE @SourceAssertions INT = @@ROWCOUNT;

-- 2. Proposition support edges from the matter's evidence items.
DELETE p
FROM POLOXI.Legal_MatterPropositionSupport p
INNER JOIN POLOXI.Legal_MatterEvidenceItem e ON e.LegalEvidenceItemId = p.LegalEvidenceItemId
WHERE e.DecisionMatterId = @MatterId;
DECLARE @Supports INT = @@ROWCOUNT;

-- 3. The evidence items themselves.
DELETE FROM POLOXI.Legal_MatterEvidenceItem
WHERE DecisionMatterId = @MatterId;
DECLARE @Evidence INT = @@ROWCOUNT;

-- 4. All of the matter's fact propositions (full reset — attorney/seed and LLM alike).
DELETE FROM POLOXI.Legal_MatterFactProposition
WHERE DecisionMatterId = @MatterId;
DECLARE @Propositions INT = @@ROWCOUNT;

-- 5. Evidence lineage members (via occurrences for these documents), then empty lineage groups.
DELETE member
FROM POLOXI.Legal_EvidenceLineageMember member
INNER JOIN POLOXI.Legal_EvidenceOccurrence occurrence
	ON occurrence.LegalEvidenceOccurrenceId = member.LegalEvidenceOccurrenceId
INNER JOIN @Documents d ON d.LegalDocumentId = occurrence.LegalDocumentId;

DELETE grp
FROM POLOXI.Legal_EvidenceLineageGroup grp
WHERE grp.DecisionMatterId = @MatterId
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_EvidenceLineageMember member
				  WHERE member.LegalEvidenceLineageGroupId = grp.LegalEvidenceLineageGroupId);

-- 6. Processing operations whose input/result entity is one of these versions/documents.
DELETE po
FROM POLOXI.Legal_ProcessingOperation po
WHERE po.DecisionMatterId = @MatterId
  AND (po.InputEntityId  IN (SELECT LegalDocumentVersionId FROM @Versions)
	OR po.ResultEntityId IN (SELECT LegalDocumentVersionId FROM @Versions)
	OR po.InputEntityId  IN (SELECT LegalDocumentId FROM @Documents)
	OR po.ResultEntityId IN (SELECT LegalDocumentId FROM @Documents));

-- 7. Provenance occurrences (children first via the ParentOccurrenceId self-reference).
DELETE o
FROM POLOXI.Legal_EvidenceOccurrence o
INNER JOIN @Documents d ON d.LegalDocumentId = o.LegalDocumentId
WHERE o.ParentOccurrenceId IS NOT NULL;
DELETE o
FROM POLOXI.Legal_EvidenceOccurrence o
INNER JOIN @Documents d ON d.LegalDocumentId = o.LegalDocumentId;

-- 8. Search-projection outbox rows (FK to version) must go before versions.
DELETE ob
FROM POLOXI.Legal_DocumentSearchProjectionOutbox ob
INNER JOIN @Versions v ON v.LegalDocumentVersionId = ob.LegalDocumentVersionId;

-- 9. Wide-execution audit rows that FK-reference the matter's passages. These run artifacts physically
--    point at Legal_DocumentPassage rows, so they MUST be hard-removed before the passages are deleted.
DELETE map
FROM POLOXI.Legal_WideBranchPassageMap map
INNER JOIN POLOXI.Legal_DocumentPassage pa ON pa.LegalDocumentPassageId = map.LegalDocumentPassageId
INNER JOIN @Versions v ON v.LegalDocumentVersionId = pa.LegalDocumentVersionId;
DECLARE @WideBranchPassageMaps INT = @@ROWCOUNT;

DELETE anchor
FROM POLOXI.Legal_WidePropositionAnchor anchor
INNER JOIN POLOXI.Legal_DocumentPassage pa ON pa.LegalDocumentPassageId = anchor.LegalDocumentPassageId
INNER JOIN @Versions v ON v.LegalDocumentVersionId = pa.LegalDocumentVersionId;
DECLARE @WidePropositionAnchors INT = @@ROWCOUNT;

-- 10. Normalized layout artifacts + pages, then passages + processing runs (all by version).
DELETE la
FROM POLOXI.Legal_DocumentLayoutArtifact la
INNER JOIN @Versions v ON v.LegalDocumentVersionId = la.LegalDocumentVersionId;

DELETE pg
FROM POLOXI.Legal_DocumentPage pg
INNER JOIN @Versions v ON v.LegalDocumentVersionId = pg.LegalDocumentVersionId;

DELETE pa
FROM POLOXI.Legal_DocumentPassage pa
INNER JOIN @Versions v ON v.LegalDocumentVersionId = pa.LegalDocumentVersionId;
DECLARE @Passages INT = @@ROWCOUNT;

DELETE pr
FROM POLOXI.Legal_DocumentProcessingRun pr
INNER JOIN @Versions v ON v.LegalDocumentVersionId = pr.LegalDocumentVersionId;

-- 11. Versions, then documents.
DELETE dv
FROM POLOXI.Legal_MatterDocumentVersion dv
INNER JOIN @Versions v ON v.LegalDocumentVersionId = dv.LegalDocumentVersionId;
DECLARE @VersionsRemoved INT = @@ROWCOUNT;

DELETE FROM POLOXI.Legal_MatterDocument
WHERE DecisionMatterId = @MatterId;
DECLARE @DocumentsRemoved INT = @@ROWCOUNT;

-- 12. Matter-level upload batch provenance (full clean slate).
DELETE FROM POLOXI.Legal_UploadBatch
WHERE DecisionMatterId = @MatterId;
DECLARE @UploadBatches INT = @@ROWCOUNT;

-- 13. Per-matter Evidence Type scope selections (regenerated on next activation).
DELETE FROM POLOXI.Legal_MatterEvidenceScope
WHERE DecisionMatterId = @MatterId;
DECLARE @EvidenceScope INT = @@ROWCOUNT;

-- ──────────────────────────────────────────────────────────────────────────────────────────────────
-- SOFT DELETE: retire prior decision / search runs so the cockpit shows a clean slate. Audit rows and
-- their FK children are preserved; every run-history query filters IsDeleted = 0.
-- ──────────────────────────────────────────────────────────────────────────────────────────────────

-- Decision2 analysis-run ledger (keyed by MatterId).
UPDATE POLOXI.Legal_AnalysisRun
SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME()
WHERE MatterId = @MatterId AND IsDeleted = 0;
DECLARE @AnalysisRuns INT = @@ROWCOUNT;

-- Captured wide-run results for the matter (keyed by MatterId).
UPDATE POLOXI.Legal_WideRunResult
SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME()
WHERE MatterId = @MatterId AND IsDeleted = 0;
DECLARE @WideRunResults INT = @@ROWCOUNT;

-- POLOXI wide executions linked to the matter (keyed by MatterId; column added in migration 0372).
UPDATE POLOXI.Legal_WideExecution
SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME()
WHERE MatterId = @MatterId AND IsDeleted = 0;
DECLARE @WideExecutions INT = @@ROWCOUNT;

-- Guarded current-assessment pointer (hard-remove so the matter has no "current" run).
DELETE FROM POLOXI.Legal_AnalysisRunCurrentAssessment
WHERE MatterId = @MatterId;
DECLARE @CurrentAssessments INT = @@ROWCOUNT;

COMMIT;

-- ──────────────────────────────────────────────────────────────────────────────────────────────────
-- Summary
-- ──────────────────────────────────────────────────────────────────────────────────────────────────
SELECT
	@MatterId            AS MatterId,
	@TenantId            AS TenantId,
	@DocumentsRemoved    AS DocumentsRemoved,
	@VersionsRemoved     AS VersionsRemoved,
	@Passages            AS PassagesRemoved,
	@WideBranchPassageMaps  AS WideBranchPassageMapsRemoved,
	@WidePropositionAnchors AS WidePropositionAnchorsRemoved,
	@Evidence            AS EvidenceItemsRemoved,
	@Propositions        AS PropositionsRemoved,
	@Supports            AS PropositionSupportsRemoved,
	@SourceAssertions    AS SourceAssertionsRemoved,
	@UploadBatches       AS UploadBatchesRemoved,
	@EvidenceScope       AS EvidenceScopeRowsRemoved,
	@AnalysisRuns        AS AnalysisRunsRetired,
	@WideRunResults      AS WideRunResultsRetired,
	@WideExecutions      AS WideExecutionsRetired,
	@CurrentAssessments  AS CurrentAssessmentPointersRemoved;
