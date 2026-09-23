SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — expand POLOXI.Legal_DecisionMatter from three legacy free-text metadata fields
-- (MatterTypeCode, Jurisdiction, Posture) into a fully structured, three-dimensional decision
-- contract model, per the decision-contract design:
--   Type        = What kind of dispute?         MatterTypeCode (legacy) + Subtype
--   Jurisdiction= Which legal system controls?  Jurisdiction (legacy) + CourtSystem, State,
--                                               CourtLevel, County, GoverningLaw
--   Posture     = What decision is being asked?  Posture (legacy) + MovingParty, RespondingParty,
--                                               MotionTarget, RequestedDisposition
-- The three legacy columns are PRESERVED for backward compatibility; the new columns are additive
-- and nullable. This migration is idempotent (column adds are guarded; option seeds use MERGE).
-- ─────────────────────────────────────────────────────────────────────────────────────────────

-- ── Add structured Type columns ────────────────────────────────────────────────────────────────
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'Subtype') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD Subtype NVARCHAR(120) NULL;
GO

-- ── Add structured Jurisdiction columns ────────────────────────────────────────────────────────
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'CourtSystem') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD CourtSystem NVARCHAR(120) NULL;
GO
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'State') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD State NVARCHAR(120) NULL;
GO
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'CourtLevel') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD CourtLevel NVARCHAR(120) NULL;
GO
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'County') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD County NVARCHAR(120) NULL;
GO
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'GoverningLaw') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD GoverningLaw NVARCHAR(120) NULL;
GO

-- ── Add structured Posture columns ─────────────────────────────────────────────────────────────
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'MovingParty') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD MovingParty NVARCHAR(200) NULL;
GO
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'RespondingParty') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD RespondingParty NVARCHAR(200) NULL;
GO
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'MotionTarget') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD MotionTarget NVARCHAR(300) NULL;
GO
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'RequestedDisposition') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD RequestedDisposition NVARCHAR(300) NULL;
GO

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- Seed DB-backed dropdown defaults for the new categorical fields (idempotent MERGE keyed on
-- FieldCode + Value). New FieldCodes: SUBTYPE, COURT_SYSTEM, STATE, COURT_LEVEL, GOVERNING_LAW.
-- (MovingParty / RespondingParty / MotionTarget / RequestedDisposition are free-text inputs.)
-- ─────────────────────────────────────────────────────────────────────────────────────────────
MERGE POLOXI.Legal_DecisionMatterOption AS target
USING (VALUES
	-- ── Subtypes ──
	(N'SUBTYPE', N'Contract / Commercial Dispute',   10),
	(N'SUBTYPE', N'Coverage Dispute',                20),
	(N'SUBTYPE', N'Bad Faith',                       30),
	(N'SUBTYPE', N'Tort / Negligence',               40),
	(N'SUBTYPE', N'Employment',                      50),
	(N'SUBTYPE', N'Intellectual Property',           60),
	(N'SUBTYPE', N'Trade Secret / Confidentiality',  70),
	(N'SUBTYPE', N'Real Property',                    80),
	(N'SUBTYPE', N'Regulatory / Compliance',         90),
	(N'SUBTYPE', N'Class Action',                    100),

	-- ── Court systems ──
	(N'COURT_SYSTEM', N'United States - Federal',     10),
	(N'COURT_SYSTEM', N'United States - State',       20),
	(N'COURT_SYSTEM', N'Tribal',                      30),
	(N'COURT_SYSTEM', N'Administrative',              40),
	(N'COURT_SYSTEM', N'Arbitration Forum',           50),

	-- ── States ──
	(N'STATE', N'California',                          10),
	(N'STATE', N'New York',                           20),
	(N'STATE', N'Texas',                              30),
	(N'STATE', N'Florida',                            40),
	(N'STATE', N'Illinois',                           50),
	(N'STATE', N'Delaware',                           60),
	(N'STATE', N'Washington',                         70),
	(N'STATE', N'Massachusetts',                      80),

	-- ── Court levels ──
	(N'COURT_LEVEL', N'Trial Court',                  10),
	(N'COURT_LEVEL', N'Superior Court',               20),
	(N'COURT_LEVEL', N'District Court',               30),
	(N'COURT_LEVEL', N'Court of Appeals',             40),
	(N'COURT_LEVEL', N'Supreme Court',                50),
	(N'COURT_LEVEL', N'Bankruptcy Court',             60),

	-- ── Governing law ──
	(N'GOVERNING_LAW', N'California',                  10),
	(N'GOVERNING_LAW', N'New York',                   20),
	(N'GOVERNING_LAW', N'Texas',                      30),
	(N'GOVERNING_LAW', N'Delaware',                   40),
	(N'GOVERNING_LAW', N'Federal',                    50)
) AS source (FieldCode, Value, SortOrder)
ON target.FieldCode = source.FieldCode AND target.Value = source.Value
WHEN NOT MATCHED BY TARGET THEN
	INSERT (FieldCode, Value, DisplayName, SortOrder)
	VALUES (source.FieldCode, source.Value, source.Value, source.SortOrder);

GO

COMMIT TRANSACTION;
