SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal Decision — Matter Practice Area classification (/legal/personalinjury)
-- Personal Injury is modeled as a practice-area CLASSIFICATION on the existing Matter aggregate,
-- not as a separate engine and not as free-text only. This migration adds a PracticeAreaCode column
-- to POLOXI.Legal_DecisionMatter so matters can be scoped by practice area (e.g. PERSONAL_INJURY),
-- and seeds DB-backed dropdown values (practice areas, PI matter types, PI claim types) into the
-- existing POLOXI.Legal_DecisionMatterOption table (0211 pattern). Global defaults use TenantId NULL.
-- Table → API → UI: schema first; all objects live in the POLOXI schema with base/audit fields.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Practice-area classification on the Matter aggregate. ──
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'PracticeAreaCode') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD PracticeAreaCode NVARCHAR(60) NULL;

GO

-- ── Claim type on the Matter aggregate (PI: Negligence, Premises Liability, etc.). ──
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'ClaimTypeCode') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD ClaimTypeCode NVARCHAR(120) NULL;

GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionMatter') AND name = N'IX_Legal_DecisionMatter_PracticeArea')
   AND COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'PracticeAreaCode') IS NOT NULL
	CREATE INDEX IX_Legal_DecisionMatter_PracticeArea
		ON POLOXI.Legal_DecisionMatter (TenantId, PracticeAreaCode, ModifiedDateUtc DESC, CreatedDateUtc DESC)
		WHERE IsDeleted = 0;

GO

-- ── Seed practice-area + PI matter-type + PI claim-type dropdown options (idempotent MERGE). ──
MERGE POLOXI.Legal_DecisionMatterOption AS target
USING (VALUES
	-- ── Practice areas ──
	(N'PRACTICE_AREA', N'Personal Injury',        10),
	(N'PRACTICE_AREA', N'Employment',             20),
	(N'PRACTICE_AREA', N'Contract',               30),
	(N'PRACTICE_AREA', N'Insurance Coverage',     40),
	(N'PRACTICE_AREA', N'Commercial Litigation',  50),
	(N'PRACTICE_AREA', N'Intellectual Property',  60),

	-- ── Personal Injury matter types ──
	(N'PI_MATTER_TYPE', N'Motor Vehicle Accident', 10),
	(N'PI_MATTER_TYPE', N'Premises Liability',      20),
	(N'PI_MATTER_TYPE', N'Product Liability',       30),
	(N'PI_MATTER_TYPE', N'Medical Malpractice',     40),
	(N'PI_MATTER_TYPE', N'Wrongful Death',          50),
	(N'PI_MATTER_TYPE', N'Dog Bite / Animal Injury',60),
	(N'PI_MATTER_TYPE', N'Slip and Fall',           70),
	(N'PI_MATTER_TYPE', N'Other Negligence',        80),

	-- ── Personal Injury claim types ──
	(N'PI_CLAIM_TYPE', N'Negligence',           10),
	(N'PI_CLAIM_TYPE', N'Negligence Per Se',    20),
	(N'PI_CLAIM_TYPE', N'Premises Liability',   30),
	(N'PI_CLAIM_TYPE', N'Product Liability',    40),
	(N'PI_CLAIM_TYPE', N'Strict Liability',     50),
	(N'PI_CLAIM_TYPE', N'Wrongful Death',       60),
	(N'PI_CLAIM_TYPE', N'Survival Claim',       70),
	(N'PI_CLAIM_TYPE', N'Loss of Consortium',   80)
) AS source (FieldCode, Value, SortOrder)
ON target.FieldCode = source.FieldCode AND target.Value = source.Value
WHEN NOT MATCHED BY TARGET THEN
	INSERT (FieldCode, Value, DisplayName, SortOrder)
	VALUES (source.FieldCode, source.Value, source.Value, source.SortOrder);

GO

COMMIT TRANSACTION;
