-- Explicit branch attribution for cached external knowledge snippets.
-- Evidence attribution previously relied on the implicit convention that the retrieval query text
-- contained the branch display name (snippet.Query.Contains(branch.DisplayName)). That is brittle:
-- branch names can change, overlap, or accidentally match unrelated query text. These nullable
-- columns give each snippet a stable, deterministic link to the branch it was retrieved for.
-- BranchDisplayName is denormalized for readability/auditing; BranchId is the authoritative key.
-- Nullable so legacy rows and general-web snippets (no branch link) remain valid and fall back to
-- the legacy query-based attribution.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.Legal_ExternalKnowledge',N'BranchId') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_ExternalKnowledge ADD BranchId UNIQUEIDENTIFIER NULL;
END;

IF COL_LENGTH(N'POLOXI.Legal_ExternalKnowledge',N'BranchDisplayName') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_ExternalKnowledge ADD BranchDisplayName NVARCHAR(400) NULL;
END;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_Legal_PoloxiExternalKnowledge_Branch' AND object_id=OBJECT_ID(N'POLOXI.Legal_ExternalKnowledge'))
BEGIN
	EXEC(N'CREATE INDEX IX_Legal_PoloxiExternalKnowledge_Branch ON POLOXI.Legal_ExternalKnowledge(BranchId) WHERE IsDeleted=0 AND BranchId IS NOT NULL');
END;

COMMIT TRANSACTION;
