SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0461: Retire POLOXI.Legal_DecisionDomainPackMatterType.
--   Why: migration 0460 made POLOXI.Legal_MatterSubtype the canonical
--   MatterType -> Subtype catalog. The pack subtype table became a redundant
--   pack-scoped projection (audit confirmed: one pack per broad MatterType,
--   pack row counts equal canonical subtype counts, all 521 evidence junction
--   rows resolve to a canonical subtype, zero matter gaps).
--   This migration:
--     1) Adds MatterSubtypeId to Legal_DecisionDomainPackEvidenceTypeMatterType,
--        backfills it through the existing pack-row link, verifies full
--        coverage, adds FK/index, then drops the legacy FK/column.
--     2) Drops Legal_DecisionMatter.DecisionDomainPackMatterTypeId (canonical
--        MatterSubtypeId already backfilled by 0460).
--     3) Drops Legal_DecisionDomainPackMatterType.
--   Idempotent throughout; aborts (THROW) if any junction row cannot be rekeyed.
-- ============================================================================
BEGIN TRANSACTION;

-- ---------------------------------------------------------------------------
-- 1) Re-key the evidence junction onto the canonical subtype catalog.
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType', N'MatterSubtypeId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType ADD MatterSubtypeId UNIQUEIDENTIFIER NULL;

COMMIT TRANSACTION;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- Backfill from the legacy pack-row link (only while the legacy table/column still exist).
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackMatterType') IS NOT NULL
   AND COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType', N'DecisionDomainPackMatterTypeId') IS NOT NULL
BEGIN
	EXEC sp_executesql N'
		UPDATE j
		   SET j.MatterSubtypeId  = mt.MatterSubtypeId,
			   j.ModifiedDateUtc  = @Now,
			   j.ModifiedByUserId = @SystemUserId
		  FROM POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType j
		  JOIN POLOXI.Legal_DecisionDomainPackMatterType mt
			ON mt.DecisionDomainPackMatterTypeId = j.DecisionDomainPackMatterTypeId
		 WHERE j.MatterSubtypeId IS NULL
		   AND mt.MatterSubtypeId IS NOT NULL;',
		N'@Now DATETIME2, @SystemUserId UNIQUEIDENTIFIER', @Now = @Now, @SystemUserId = @SystemUserId;
END;

-- Safety gate: every active junction row must be rekeyed before we drop anything.
IF EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType WHERE IsDeleted = 0 AND MatterSubtypeId IS NULL)
BEGIN
	ROLLBACK TRANSACTION;
	THROW 50461, N'0461 aborted: active evidence junction rows without a canonical MatterSubtypeId remain. Investigate before retrying.', 1;
END;

-- FK + index on the new canonical key.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Legal_DDPEvidenceTypeMatterType_MatterSubtype')
	ALTER TABLE POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType
		ADD CONSTRAINT FK_Legal_DDPEvidenceTypeMatterType_MatterSubtype
		FOREIGN KEY (MatterSubtypeId) REFERENCES POLOXI.Legal_MatterSubtype (MatterSubtypeId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Legal_DDPEvidenceTypeMatterType_Subtype'
			   AND object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType'))
	CREATE INDEX IX_Legal_DDPEvidenceTypeMatterType_Subtype
		ON POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType (MatterSubtypeId) WHERE IsDeleted = 0;

-- Replacement uniqueness: one active link per (evidence type, canonical subtype).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Legal_DDPEvidenceTypeMatterType_Subtype'
			   AND object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType'))
	CREATE UNIQUE INDEX UX_Legal_DDPEvidenceTypeMatterType_Subtype
		ON POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType (DecisionDomainPackEvidenceTypeId, MatterSubtypeId) WHERE IsDeleted = 0;

-- ---------------------------------------------------------------------------
-- 2) Drop legacy junction FK/indexes/column.
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType', N'DecisionDomainPackMatterTypeId') IS NOT NULL
BEGIN
	-- Drop any FK on the legacy column.
	DECLARE @sql NVARCHAR(MAX) = N'';
	SELECT @sql = @sql + N'ALTER TABLE POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
	FROM sys.foreign_keys fk
	JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
	JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
	WHERE fk.parent_object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType')
	  AND c.name = N'DecisionDomainPackMatterTypeId';

	-- Drop any UNIQUE/CHECK-style constraints enforced via indexes on the legacy column.
	SELECT @sql = @sql + N'ALTER TABLE POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType DROP CONSTRAINT ' + QUOTENAME(i.name) + N';'
	FROM sys.indexes i
	JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
	JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
	WHERE i.object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType')
	  AND i.is_primary_key = 0 AND i.is_unique_constraint = 1
	  AND c.name = N'DecisionDomainPackMatterTypeId';

	-- Drop any plain index referencing the legacy column.
	SELECT @sql = @sql + N'DROP INDEX ' + QUOTENAME(i.name) + N' ON POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType;'
	FROM sys.indexes i
	JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
	JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
	WHERE i.object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType')
	  AND i.is_primary_key = 0 AND i.is_unique_constraint = 0 AND i.type > 0
	  AND c.name = N'DecisionDomainPackMatterTypeId';

	IF @sql <> N'' EXEC sp_executesql @sql;

	ALTER TABLE POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType DROP COLUMN DecisionDomainPackMatterTypeId;
END;

-- ---------------------------------------------------------------------------
-- 3) Drop the legacy matter binding column (canonical MatterSubtypeId stays).
-- ---------------------------------------------------------------------------
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'DecisionDomainPackMatterTypeId') IS NOT NULL
BEGIN
	DECLARE @sql2 NVARCHAR(MAX) = N'';
	SELECT @sql2 = @sql2 + N'ALTER TABLE POLOXI.Legal_DecisionMatter DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
	FROM sys.foreign_keys fk
	JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
	JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
	WHERE fk.parent_object_id = OBJECT_ID(N'POLOXI.Legal_DecisionMatter')
	  AND c.name = N'DecisionDomainPackMatterTypeId';

	SELECT @sql2 = @sql2 + N'DROP INDEX ' + QUOTENAME(i.name) + N' ON POLOXI.Legal_DecisionMatter;'
	FROM sys.indexes i
	JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
	JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
	WHERE i.object_id = OBJECT_ID(N'POLOXI.Legal_DecisionMatter')
	  AND i.is_primary_key = 0 AND i.type > 0
	  AND c.name = N'DecisionDomainPackMatterTypeId';

	IF @sql2 <> N'' EXEC sp_executesql @sql2;

	ALTER TABLE POLOXI.Legal_DecisionMatter DROP COLUMN DecisionDomainPackMatterTypeId;
END;

-- ---------------------------------------------------------------------------
-- 4) Drop the retired pack subtype table (no remaining referencing FKs).
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackMatterType') IS NOT NULL
BEGIN
	IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE referenced_object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackMatterType'))
	BEGIN
		ROLLBACK TRANSACTION;
		THROW 50462, N'0461 aborted: foreign keys still reference Legal_DecisionDomainPackMatterType.', 1;
	END;
	DROP TABLE POLOXI.Legal_DecisionDomainPackMatterType;
END;

COMMIT TRANSACTION;
GO

-- Verification.
SELECT 'JunctionRekeyed' AS Chk, COUNT(*) AS TotalRows,
	   SUM(CASE WHEN MatterSubtypeId IS NOT NULL THEN 1 ELSE 0 END) AS WithSubtype
FROM POLOXI.Legal_DecisionDomainPackEvidenceTypeMatterType WHERE IsDeleted = 0;
SELECT 'LegacyTableGone' AS Chk, CASE WHEN OBJECT_ID(N'POLOXI.Legal_DecisionDomainPackMatterType') IS NULL THEN 1 ELSE 0 END AS Gone;
SELECT 'MattersWithSubtype' AS Chk, COUNT(*) AS Cnt FROM POLOXI.Legal_DecisionMatter WHERE IsDeleted = 0 AND MatterSubtypeId IS NOT NULL;
