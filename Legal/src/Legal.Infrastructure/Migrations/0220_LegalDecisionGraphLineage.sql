SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal V2.1 — Dependency-graph LINEAGE + VERSION columns.
--
-- The V2.1 closed loop must translate a graph state change into the AUTHORITATIVE POLOXI branch /
-- candidate it affects using EXPLICIT lineage (never string matching). These columns record, on
-- each typed node and edge, which authoritative POLOXI object the node/edge was derived from:
--
--   SourceBranchId    → POLOXI.Legal_DecisionBranch
--   SourceCandidateId → POLOXI.Legal_DecisionCandidate
--   SourceEvidenceId  → POLOXI.Legal_DecisionEvidence
--   SourceAuthorityId → external authority reference (nullable; no FK)
--   MatterId          → POLOXI.Legal_DecisionMatter (denormalized for tenant/matter-scoped queries)
--   Version           → optimistic-concurrency token for idempotent propagation
--   PropagationPolicy → edge-level override for how invalidation flows (default DEPENDENCY)
--   AlternativePathAllowed → edge may be bypassed if another verified path establishes the target
--
-- All columns are nullable / defaulted so existing seeded graphs (0215) remain valid; the mapper
-- degrades gracefully when lineage is absent. Idempotent: guarded by COL_LENGTH checks.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @NodeTables TABLE (TableName SYSNAME, PkColumn SYSNAME);
INSERT @NodeTables (TableName, PkColumn) VALUES
	(N'Legal_DecisionFactProposition',   N'DecisionFactPropositionId'),
	(N'Legal_DecisionLegalProposition',  N'DecisionLegalPropositionId'),
	(N'Legal_DecisionLegalElement',      N'DecisionLegalElementId'),
	(N'Legal_DecisionReasoningStrategy', N'DecisionReasoningStrategyId'),
	(N'Legal_DecisionBurdenRule',        N'DecisionBurdenRuleId'),
	(N'Legal_DecisionProceduralConstraint', N'DecisionProceduralConstraintId');

DECLARE @tbl SYSNAME, @sql NVARCHAR(MAX);
DECLARE node_cur CURSOR LOCAL FAST_FORWARD FOR SELECT TableName FROM @NodeTables;
OPEN node_cur;
FETCH NEXT FROM node_cur INTO @tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
	IF OBJECT_ID(N'POLOXI.' + @tbl, N'U') IS NOT NULL
	BEGIN
		IF COL_LENGTH(N'POLOXI.' + @tbl, N'SourceBranchId') IS NULL
		BEGIN SET @sql = N'ALTER TABLE POLOXI.' + QUOTENAME(@tbl) + N' ADD SourceBranchId UNIQUEIDENTIFIER NULL;'; EXEC sp_executesql @sql; END
		IF COL_LENGTH(N'POLOXI.' + @tbl, N'SourceCandidateId') IS NULL
		BEGIN SET @sql = N'ALTER TABLE POLOXI.' + QUOTENAME(@tbl) + N' ADD SourceCandidateId UNIQUEIDENTIFIER NULL;'; EXEC sp_executesql @sql; END
		IF COL_LENGTH(N'POLOXI.' + @tbl, N'SourceEvidenceId') IS NULL
		BEGIN SET @sql = N'ALTER TABLE POLOXI.' + QUOTENAME(@tbl) + N' ADD SourceEvidenceId UNIQUEIDENTIFIER NULL;'; EXEC sp_executesql @sql; END
		IF COL_LENGTH(N'POLOXI.' + @tbl, N'SourceAuthorityId') IS NULL
		BEGIN SET @sql = N'ALTER TABLE POLOXI.' + QUOTENAME(@tbl) + N' ADD SourceAuthorityId NVARCHAR(500) NULL;'; EXEC sp_executesql @sql; END
		IF COL_LENGTH(N'POLOXI.' + @tbl, N'MatterId') IS NULL
		BEGIN SET @sql = N'ALTER TABLE POLOXI.' + QUOTENAME(@tbl) + N' ADD MatterId UNIQUEIDENTIFIER NULL;'; EXEC sp_executesql @sql; END
		IF COL_LENGTH(N'POLOXI.' + @tbl, N'RowVersionNo') IS NULL
		BEGIN SET @sql = N'ALTER TABLE POLOXI.' + QUOTENAME(@tbl) + N' ADD RowVersionNo INT NOT NULL CONSTRAINT DF_' + @tbl + N'_RowVer DEFAULT 1;'; EXEC sp_executesql @sql; END
	END
	FETCH NEXT FROM node_cur INTO @tbl;
END
CLOSE node_cur;
DEALLOCATE node_cur;

-- ── Edge: lineage + propagation policy + alternative-path flag + version. ─────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_DecisionGraphEdge', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.Legal_DecisionGraphEdge', N'SourceBranchId') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionGraphEdge ADD SourceBranchId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'POLOXI.Legal_DecisionGraphEdge', N'SourceCandidateId') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionGraphEdge ADD SourceCandidateId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'POLOXI.Legal_DecisionGraphEdge', N'SourceEvidenceId') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionGraphEdge ADD SourceEvidenceId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'POLOXI.Legal_DecisionGraphEdge', N'SourceAuthorityId') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionGraphEdge ADD SourceAuthorityId NVARCHAR(500) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_DecisionGraphEdge', N'MatterId') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionGraphEdge ADD MatterId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'POLOXI.Legal_DecisionGraphEdge', N'AlternativePathAllowed') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionGraphEdge ADD AlternativePathAllowed BIT NOT NULL CONSTRAINT DF_Legal_DecisionEdge_AltPath DEFAULT 0;
	IF COL_LENGTH(N'POLOXI.Legal_DecisionGraphEdge', N'PropagationPolicy') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionGraphEdge ADD PropagationPolicy NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_DecisionEdge_PropPolicy DEFAULT N'DEPENDENCY';
	IF COL_LENGTH(N'POLOXI.Legal_DecisionGraphEdge', N'RowVersionNo') IS NULL
		ALTER TABLE POLOXI.Legal_DecisionGraphEdge ADD RowVersionNo INT NOT NULL CONSTRAINT DF_Legal_DecisionEdge_RowVer DEFAULT 1;
END

COMMIT TRANSACTION;
