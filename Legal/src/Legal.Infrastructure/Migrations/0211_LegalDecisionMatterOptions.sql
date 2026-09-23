SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal Decision Matter Options (/legal/matters)
-- Provides DB-backed, pre-populated dropdown values for the New/Edit Matter dialog fields
-- (Matter type, Jurisdiction, Posture). Previously these dropdowns only reflected values already
-- stored on existing matters, so a fresh database/tenant rendered empty dropdowns. This table seeds
-- sensible enterprise defaults while remaining fully editable (users may still type new values,
-- which then appear via the distinct-value union in the repository). Object lives in the POLOXI
-- schema, is prefixed Legal_Decision*, and carries the standard base/audit fields. Global defaults
-- use TenantId NULL, matching the 0209 configuration tables.
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Matter option: seeded dropdown values grouped by field (MATTER_TYPE / JURISDICTION / POSTURE). ──
IF OBJECT_ID(N'POLOXI.Legal_DecisionMatterOption',N'U') IS NULL
CREATE TABLE POLOXI.Legal_DecisionMatterOption
(
	DecisionMatterOptionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionMatterOption PRIMARY KEY DEFAULT NEWID(),
	FieldCode              NVARCHAR(40) NOT NULL,          -- MATTER_TYPE | JURISDICTION | POSTURE
	Value                 NVARCHAR(200) NOT NULL,          -- the value stored on the matter (and shown)
	DisplayName           NVARCHAR(200) NULL,              -- optional friendly label (defaults to Value)
	SortOrder             INT NOT NULL CONSTRAINT DF_Legal_DecisionMatterOption_SortOrder DEFAULT 0,
	IsActive              BIT NOT NULL CONSTRAINT DF_Legal_DecisionMatterOption_IsActive DEFAULT 1,
	TenantId              UNIQUEIDENTIFIER NULL,
	CreatedDateUtc        DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionMatterOption_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId       UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc       DATETIME2 NULL,
	ModifiedByUserId      UNIQUEIDENTIFIER NULL,
	IsDeleted             BIT NOT NULL CONSTRAINT DF_Legal_DecisionMatterOption_IsDeleted DEFAULT 0,
	CONSTRAINT UQ_Legal_DecisionMatterOption_Field_Value UNIQUE (FieldCode, Value)
);

IF OBJECT_ID(N'IX_Legal_DecisionMatterOption_Field',N'IX') IS NULL
	CREATE INDEX IX_Legal_DecisionMatterOption_Field ON POLOXI.Legal_DecisionMatterOption (FieldCode, SortOrder, Value) WHERE IsDeleted = 0;

GO

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- Seed DB-backed dropdown defaults (idempotent MERGE keyed on FieldCode + Value).
-- ─────────────────────────────────────────────────────────────────────────────────────────────
MERGE POLOXI.Legal_DecisionMatterOption AS target
USING (VALUES
	-- ── Matter types ──
	(N'MATTER_TYPE', N'Summary Judgment',           10),
	(N'MATTER_TYPE', N'Motion to Dismiss',          20),
	(N'MATTER_TYPE', N'Coverage Dispute',           30),
	(N'MATTER_TYPE', N'Contract Dispute',           40),
	(N'MATTER_TYPE', N'Breach of Contract',         50),
	(N'MATTER_TYPE', N'Insurance Claim',            60),
	(N'MATTER_TYPE', N'Bad Faith',                  70),
	(N'MATTER_TYPE', N'Employment',                 80),
	(N'MATTER_TYPE', N'Personal Injury',            90),
	(N'MATTER_TYPE', N'Product Liability',          100),
	(N'MATTER_TYPE', N'Intellectual Property',      110),
	(N'MATTER_TYPE', N'Regulatory / Compliance',    120),
	(N'MATTER_TYPE', N'Class Action',               130),
	(N'MATTER_TYPE', N'Arbitration',                140),
	(N'MATTER_TYPE', N'Appeal',                     150),

	-- ── Jurisdictions ──
	(N'JURISDICTION', N'U.S. Supreme Court',        10),
	(N'JURISDICTION', N'9th Circuit',               20),
	(N'JURISDICTION', N'2nd Circuit',               30),
	(N'JURISDICTION', N'Federal Circuit',           40),
	(N'JURISDICTION', N'N.D. Cal.',                 50),
	(N'JURISDICTION', N'C.D. Cal.',                 60),
	(N'JURISDICTION', N'S.D.N.Y.',                  70),
	(N'JURISDICTION', N'E.D. Tex.',                 80),
	(N'JURISDICTION', N'N.D. Ill.',                 90),
	(N'JURISDICTION', N'D. Del.',                   100),
	(N'JURISDICTION', N'California',                 110),
	(N'JURISDICTION', N'New York',                  120),
	(N'JURISDICTION', N'Texas',                     130),
	(N'JURISDICTION', N'Florida',                   140),
	(N'JURISDICTION', N'Illinois',                  150),
	(N'JURISDICTION', N'Delaware',                  160),

	-- ── Postures ──
	(N'POSTURE', N'Pre-litigation',                 10),
	(N'POSTURE', N'Pre-trial motion',               20),
	(N'POSTURE', N'Discovery',                      30),
	(N'POSTURE', N'Dispositive motion',             40),
	(N'POSTURE', N'Trial',                          50),
	(N'POSTURE', N'Post-trial motion',              60),
	(N'POSTURE', N'Appeal',                         70),
	(N'POSTURE', N'Settlement',                     80),
	(N'POSTURE', N'Mediation',                      90),
	(N'POSTURE', N'Arbitration',                    100)
) AS source (FieldCode, Value, SortOrder)
ON target.FieldCode = source.FieldCode AND target.Value = source.Value
WHEN NOT MATCHED BY TARGET THEN
	INSERT (FieldCode, Value, DisplayName, SortOrder)
	VALUES (source.FieldCode, source.Value, source.Value, source.SortOrder);

GO

COMMIT TRANSACTION;
