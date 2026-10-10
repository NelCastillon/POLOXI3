SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ──────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI importance scoring for Domain Pack Dimensions and Evidence Types (idempotent).
--
-- Adds a DB-backed advisory importance signal (PoloxiImportanceScore, 0.0000–1.0000) to the
-- Personal Injury Domain Pack decision-hierarchy dimensions and evidence types. The score expresses
-- how decisive each node is to a PI recovery decision and is used, in Domain Pack (checked) mode
-- only, as the interpretation prior for the deterministic L1 Dimension / L2 Evidence branches.
--
-- Scale & rationale (0.00–1.00):
--   Dimensions   — liability/causation/injury are prima-facie elements (highest); damages quantum and
--                  threshold bars (limitations) are high; ancillary/procedural/valuation are lower.
--   EvidenceTypes— weighted by probative force and authority: objective medical and official incident
--                  records highest; corroborating lay/financial records mid; supplemental records lower.
--
-- POLOXI Core still owns all authoritative scoring, competition, and normalization. This value is an
-- advisory domain-configuration prior ONLY and never a conclusion or an exclusion.
-- ──────────────────────────────────────────────────────────────────────────────────────────────

-- ── Columns (idempotent) ────────────────────────────────────────────────────────────────────────
IF COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackDimension', N'PoloxiImportanceScore') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionDomainPackDimension ADD PoloxiImportanceScore DECIMAL(5,4) NULL;
GO

IF COL_LENGTH(N'POLOXI.Legal_DecisionDomainPackEvidenceType', N'PoloxiImportanceScore') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionDomainPackEvidenceType ADD PoloxiImportanceScore DECIMAL(5,4) NULL;
GO

-- ── Resolve the global PERSONAL_INJURY pack (TenantId NULL) ──────────────────────────────────────
DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

IF @PiPackId IS NOT NULL
BEGIN
	-- ── Dimension importance scores ─────────────────────────────────────────────────────────────
	;WITH DimensionScore (DimensionCode, Score) AS
	(
		SELECT * FROM (VALUES
			-- Prima-facie negligence elements — most decisive
			(N'LIABILITY',            CAST(0.9700 AS DECIMAL(5,4))),
			(N'CAUSATION',            CAST(0.9500 AS DECIMAL(5,4))),
			(N'INJURY',               CAST(0.9200 AS DECIMAL(5,4))),
			(N'DUTY',                 CAST(0.8800 AS DECIMAL(5,4))),
			(N'BREACH',               CAST(0.8800 AS DECIMAL(5,4))),
			-- Recovery quantum
			(N'DAMAGES',              CAST(0.9000 AS DECIMAL(5,4))),
			(N'DAMAGES_ECONOMIC',     CAST(0.8400 AS DECIMAL(5,4))),
			(N'DAMAGES_NONECONOMIC',  CAST(0.7200 AS DECIMAL(5,4))),
			(N'DAMAGES_FUTURE',       CAST(0.7000 AS DECIMAL(5,4))),
			(N'DAMAGES_PUNITIVE',     CAST(0.4800 AS DECIMAL(5,4))),
			(N'DAMAGES_MITIGATION',   CAST(0.5800 AS DECIMAL(5,4))),
			-- Threshold bars and reducers
			(N'LIMITATIONS',          CAST(0.8600 AS DECIMAL(5,4))),
			(N'COMPARATIVE_FAULT',    CAST(0.7800 AS DECIMAL(5,4))),
			(N'DEFENSES',             CAST(0.7600 AS DECIMAL(5,4))),
			-- Supporting proof axis
			(N'EVIDENCE',             CAST(0.8000 AS DECIMAL(5,4))),
			-- Coverage / collectability
			(N'INSURANCE_COVERAGE',   CAST(0.6600 AS DECIMAL(5,4))),
			(N'INSURANCE',            CAST(0.6200 AS DECIMAL(5,4))),
			(N'LIENS_SUBROGATION',    CAST(0.5200 AS DECIMAL(5,4))),
			-- Procedural / party / forum
			(N'PARTIES',              CAST(0.6000 AS DECIMAL(5,4))),
			(N'JURISDICTION_VENUE',   CAST(0.5600 AS DECIMAL(5,4))),
			(N'PROCEDURE',            CAST(0.5000 AS DECIMAL(5,4))),
			-- Resolution modelling
			(N'SETTLEMENT_VALUATION', CAST(0.6000 AS DECIMAL(5,4))),
			(N'SETTLEMENT',           CAST(0.5400 AS DECIMAL(5,4)))
		) AS v (DimensionCode, Score)
	)
	UPDATE d
		SET d.PoloxiImportanceScore = s.Score,
			d.ModifiedDateUtc = SYSUTCDATETIME()
	FROM POLOXI.Legal_DecisionDomainPackDimension d
	INNER JOIN DimensionScore s ON s.DimensionCode = d.DimensionCode
	WHERE d.DecisionDomainPackId = @PiPackId AND d.TenantId IS NULL;

	-- ── Evidence-type importance scores ─────────────────────────────────────────────────────────
	;WITH EvidenceScore (EvidenceTypeCode, Score) AS
	(
		SELECT * FROM (VALUES
			-- Objective medical / official incident proof — highest probative force
			(N'MEDICAL_RECORDS',            CAST(0.9500 AS DECIMAL(5,4))),
			(N'IMAGING',                    CAST(0.9000 AS DECIMAL(5,4))),
			(N'ACCIDENT_REPORT',            CAST(0.8800 AS DECIMAL(5,4))),
			(N'SURVEILLANCE_VIDEO',         CAST(0.8800 AS DECIMAL(5,4))),
			(N'OPERATIVE_REPORTS',          CAST(0.8600 AS DECIMAL(5,4))),
			(N'MEDICAL_NARRATIVE',          CAST(0.8500 AS DECIMAL(5,4))),
			(N'EXPERT_REPORTS',             CAST(0.8400 AS DECIMAL(5,4))),
			(N'EVENT_DATA_RECORDER',        CAST(0.8400 AS DECIMAL(5,4))),
			(N'ACCIDENT_RECONSTRUCTION',    CAST(0.8200 AS DECIMAL(5,4))),
			(N'MEDICAL_BILLS',              CAST(0.8000 AS DECIMAL(5,4))),
			(N'DEPOSITION',                 CAST(0.8000 AS DECIMAL(5,4))),
			(N'IME_REPORT',                 CAST(0.7800 AS DECIMAL(5,4))),
			(N'BIOMECHANICAL_REPORT',       CAST(0.7600 AS DECIMAL(5,4))),
			(N'EMERGENCY_CALL',             CAST(0.7600 AS DECIMAL(5,4))),
			-- Corroborating lay / scene proof
			(N'WITNESS_STATEMENTS',         CAST(0.7400 AS DECIMAL(5,4))),
			(N'PHOTOS_VIDEO',               CAST(0.7400 AS DECIMAL(5,4))),
			(N'SCENE_INSPECTION',           CAST(0.7200 AS DECIMAL(5,4))),
			(N'INCIDENT_INVESTIGATION',     CAST(0.7200 AS DECIMAL(5,4))),
			(N'WEATHER_RECORDS',            CAST(0.6000 AS DECIMAL(5,4))),
			-- Earnings / economic substantiation
			(N'WAGE_RECORDS',               CAST(0.7400 AS DECIMAL(5,4))),
			(N'EMPLOYER_WAGE_VERIFICATION', CAST(0.7200 AS DECIMAL(5,4))),
			(N'TAX_RETURNS',                CAST(0.7000 AS DECIMAL(5,4))),
			(N'ECONOMIST_REPORT',           CAST(0.7400 AS DECIMAL(5,4))),
			(N'VOCATIONAL_ASSESSMENT',      CAST(0.6800 AS DECIMAL(5,4))),
			(N'LIFE_CARE_PLAN',             CAST(0.7400 AS DECIMAL(5,4))),
			(N'OUT_OF_POCKET_RECEIPTS',     CAST(0.6000 AS DECIMAL(5,4))),
			(N'HOUSEHOLD_SERVICES_LOG',     CAST(0.5600 AS DECIMAL(5,4))),
			-- Supplemental medical / history
			(N'PHARMACY_RECORDS',           CAST(0.6600 AS DECIMAL(5,4))),
			(N'MENTAL_HEALTH_RECORDS',      CAST(0.6800 AS DECIMAL(5,4))),
			(N'PRIOR_MEDICAL_HISTORY',      CAST(0.7000 AS DECIMAL(5,4))),
			(N'MAINTENANCE_RECORDS',        CAST(0.6600 AS DECIMAL(5,4))),
			-- Insurance / liens / settlement / discovery
			(N'INSURANCE_POLICY',           CAST(0.6800 AS DECIMAL(5,4))),
			(N'COVERAGE_CORRESPONDENCE',    CAST(0.5800 AS DECIMAL(5,4))),
			(N'LIEN_ASSERTIONS',            CAST(0.5600 AS DECIMAL(5,4))),
			(N'PRIOR_CLAIMS_HISTORY',       CAST(0.6200 AS DECIMAL(5,4))),
			(N'SETTLEMENT_AGREEMENT',       CAST(0.6400 AS DECIMAL(5,4))),
			(N'DEMAND_OFFER',               CAST(0.6000 AS DECIMAL(5,4))),
			(N'PARTY_INTERROGATORIES',      CAST(0.6400 AS DECIMAL(5,4)))
		) AS v (EvidenceTypeCode, Score)
	)
	UPDATE e
		SET e.PoloxiImportanceScore = s.Score,
			e.ModifiedDateUtc = SYSUTCDATETIME()
	FROM POLOXI.Legal_DecisionDomainPackEvidenceType e
	INNER JOIN EvidenceScore s ON s.EvidenceTypeCode = e.EvidenceTypeCode
	WHERE e.DecisionDomainPackId = @PiPackId AND e.TenantId IS NULL;
END
GO

COMMIT TRANSACTION;
