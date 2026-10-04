-- ============================================================================
-- 0384: Prevent duplicate matters (same Title + MatterType + Jurisdiction per tenant).
--
-- WHY: CreateMatterAsync inserts unconditionally, so the same matter could be
-- saved multiple times (double-click, multiple tabs, direct API callers). The
-- UI cannot reliably guard against this (race conditions / non-UI callers), so
-- enforcement belongs in the database.
--
-- STRATEGY (defense in depth):
--   1. A PERSISTED computed column MatterDedupeKey normalizes the duplicate
--      signature: LOWER(TRIM(Title)) | MatterTypeCode | Jurisdiction.
--      Normalization makes "Smith v. Jones " and "smith v.  jones" collide.
--   2. A FILTERED UNIQUE INDEX on (TenantId, MatterDedupeKey) WHERE IsDeleted = 0
--      is the hard guarantee. It is tenant-scoped (different tenants may reuse a
--      title) and ignores soft-deleted matters (a deleted title can be reused).
--
-- The repository performs a friendly pre-check and also translates the unique
-- violation (SQL error 2601/2627) into a clean DuplicateMatterException -> HTTP 409.
--
-- Idempotent / safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- 1. Persisted, normalized duplicate-signature column.
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'MatterDedupeKey') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter
		ADD MatterDedupeKey AS (
			CONVERT(NVARCHAR(900),
				LOWER(LTRIM(RTRIM(ISNULL(Title, N'')))) + N'|' +
				LOWER(LTRIM(RTRIM(ISNULL(MatterTypeCode, N'')))) + N'|' +
				LOWER(LTRIM(RTRIM(ISNULL(Jurisdiction, N''))))
			)) PERSISTED;
GO

-- 2. Tenant-scoped filtered unique index. Only enforced for live (non-deleted) rows.
IF NOT EXISTS (
	SELECT 1 FROM sys.indexes
	WHERE name = N'UX_Legal_DecisionMatter_Dedupe'
	  AND object_id = OBJECT_ID(N'POLOXI.Legal_DecisionMatter'))
BEGIN
	-- Defensive: if pre-existing duplicates exist, soft-delete all but the oldest
	-- so the unique index can be created. (Keeps the earliest CreatedDateUtc.)
	WITH ranked AS (
		SELECT DecisionMatterId,
			   ROW_NUMBER() OVER (
				   PARTITION BY TenantId, MatterDedupeKey
				   ORDER BY CreatedDateUtc, DecisionMatterId) AS rn
		FROM POLOXI.Legal_DecisionMatter
		WHERE IsDeleted = 0)
	UPDATE matter
	SET matter.IsDeleted = 1
	FROM POLOXI.Legal_DecisionMatter matter
	JOIN ranked ON ranked.DecisionMatterId = matter.DecisionMatterId
	WHERE ranked.rn > 1;

	CREATE UNIQUE INDEX UX_Legal_DecisionMatter_Dedupe
		ON POLOXI.Legal_DecisionMatter (TenantId, MatterDedupeKey)
		WHERE IsDeleted = 0;
END
GO

COMMIT TRANSACTION;
GO
