-- ============================================================================
-- 0426: Personal Injury Domain Pack — re-band legacy evidence types.
--
-- PURPOSE:
--   0425 introduced a per-dimension SortOrder band scheme (CLAIM_FRAMEWORK 800
--   … RESOLUTION 2260) for 275 new evidence types. The 65 legacy rows from
--   0284 / 0405 / 0424 still used the older family bands (10–770), so a
--   dimension-sorted catalog interleaved them inconsistently. This migration
--   moves every legacy row into its dimension's band, placed at the HEAD of
--   the band (odd offsets 1,2,3… below the 0425 rows which start at the band
--   base and step by 5), so the most established evidence types list first
--   within each dimension.
--
--   PoloxiImportanceScore is re-anchored to the 0407 rubric using the same
--   scale as 0425 so legacy and new rows are directly comparable:
--     prima-facie proof (liability/causation/injury/limitations) 0.74–0.88
--     damages                                                    0.54–0.80
--     coverage / liens / evidence-management / expert support   0.50–0.76
--     procedure / resolution                                     0.46–0.70
--
--   Band map (from 0425):
--     CLAIM_FRAMEWORK 800  LIABILITY 860  DUTY 940  BREACH 1000
--     INCIDENT_FACTS 1060  INJURY 1100  TREATMENT 1160  CAUSATION 1230
--     DAMAGES 1270  DAMAGES_ECONOMIC 1290  DAMAGES_NONECONOMIC 1370
--     DAMAGES_FUTURE 1420  DAMAGES_PUNITIVE 1470  DAMAGES_MITIGATION 1510
--     COMPARATIVE_FAULT 1550  DEFENSES 1610  EVIDENCE 1660  EXPERT_SUPPORT 1720
--     INSURANCE 1770  INSURANCE_COVERAGE 1790  LIENS_SUBROGATION 1850
--     JURISDICTION_VENUE 1920  LIMITATIONS 1960  PARTIES 2010  PROCEDURE 2070
--     SETTLEMENT 2150  SETTLEMENT_VALUATION 2170  RESOLUTION 2210
--
-- SCOPE / SAFETY:
--   * Updates ONLY SortOrder and PoloxiImportanceScore on global rows
--     (TenantId NULL) of the PERSONAL_INJURY pack. Codes, names, dimensions,
--     descriptions, and tenant overrides are untouched. No inserts/deletes.
--   * Advisory configuration only — POLOXI Core owns all decision reasoning.
--   * Idempotent: re-running sets the same values.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

IF @PiPackId IS NOT NULL
BEGIN
	UPDATE t
	   SET t.SortOrder             = s.SortOrder,
		   t.PoloxiImportanceScore = s.PoloxiImportanceScore,
		   t.ModifiedDateUtc       = SYSUTCDATETIME()
	  FROM POLOXI.Legal_DecisionDomainPackEvidenceType AS t
	  JOIN (VALUES
		-- ── LIABILITY (860) ──────────────────────────────────────────────────
		(N'ACCIDENT_REPORT',             861, CAST(0.8600 AS DECIMAL(5,4))),
		(N'PHOTOS_VIDEO',                862, CAST(0.8200 AS DECIMAL(5,4))),
		(N'WITNESS_STATEMENTS',          863, CAST(0.8000 AS DECIMAL(5,4))),
		(N'DEPOSITION',                  864, CAST(0.8000 AS DECIMAL(5,4))),
		(N'SURVEILLANCE_VIDEO',          866, CAST(0.8400 AS DECIMAL(5,4))),
		(N'SCENE_INSPECTION',            867, CAST(0.7800 AS DECIMAL(5,4))),
		(N'WEATHER_RECORDS',             868, CAST(0.6600 AS DECIMAL(5,4))),
		(N'EMERGENCY_CALL',              869, CAST(0.7400 AS DECIMAL(5,4))),
		(N'PARTY_STATEMENT',             871, CAST(0.7600 AS DECIMAL(5,4))),
		-- ── BREACH (1000) ────────────────────────────────────────────────────
		(N'MAINTENANCE_RECORDS',        1001, CAST(0.7600 AS DECIMAL(5,4))),
		(N'INCIDENT_INVESTIGATION',     1002, CAST(0.7600 AS DECIMAL(5,4))),
		-- ── INCIDENT_FACTS (1060) ────────────────────────────────────────────
		(N'SUPPLEMENTAL_REPORT',        1061, CAST(0.7400 AS DECIMAL(5,4))),
		(N'AUDIO_RECORDING',            1062, CAST(0.7000 AS DECIMAL(5,4))),
		-- ── INJURY (1100) ────────────────────────────────────────────────────
		(N'MEDICAL_RECORDS',            1101, CAST(0.8800 AS DECIMAL(5,4))),
		(N'IMAGING',                    1102, CAST(0.8600 AS DECIMAL(5,4))),
		(N'IMAGING_SOURCE',             1103, CAST(0.8200 AS DECIMAL(5,4))),
		(N'OPERATIVE_REPORTS',          1104, CAST(0.8400 AS DECIMAL(5,4))),
		(N'PHARMACY_RECORDS',           1106, CAST(0.6800 AS DECIMAL(5,4))),
		(N'MENTAL_HEALTH_RECORDS',      1107, CAST(0.7200 AS DECIMAL(5,4))),
		(N'FUNCTIONAL_ASSESSMENT',      1108, CAST(0.7600 AS DECIMAL(5,4))),
		-- ── TREATMENT (1160) ─────────────────────────────────────────────────
		(N'SUBSEQUENT_MEDICAL_RECORD',  1161, CAST(0.7600 AS DECIMAL(5,4))),
		(N'THERAPY_RECORD',             1162, CAST(0.6800 AS DECIMAL(5,4))),
		(N'PROVIDER_CORRESPONDENCE',    1163, CAST(0.5800 AS DECIMAL(5,4))),
		-- ── CAUSATION (1230) ─────────────────────────────────────────────────
		(N'EXPERT_REPORTS',             1231, CAST(0.8600 AS DECIMAL(5,4))),
		(N'IME_REPORT',                 1232, CAST(0.8000 AS DECIMAL(5,4))),
		(N'MEDICAL_NARRATIVE',          1233, CAST(0.8400 AS DECIMAL(5,4))),
		(N'EVENT_DATA_RECORDER',        1234, CAST(0.8200 AS DECIMAL(5,4))),
		(N'ACCIDENT_RECONSTRUCTION',    1236, CAST(0.8200 AS DECIMAL(5,4))),
		(N'BIOMECHANICAL_REPORT',       1237, CAST(0.7800 AS DECIMAL(5,4))),
		(N'DEVICE_SYSTEM_LOG',          1238, CAST(0.7600 AS DECIMAL(5,4))),
		-- ── DAMAGES (1270) ───────────────────────────────────────────────────
		(N'MEDICAL_BILLS',              1271, CAST(0.8000 AS DECIMAL(5,4))),
		(N'WAGE_RECORDS',               1272, CAST(0.7800 AS DECIMAL(5,4))),
		-- ── DAMAGES_ECONOMIC (1290) ──────────────────────────────────────────
		(N'TAX_RETURNS',                1291, CAST(0.7600 AS DECIMAL(5,4))),
		(N'EMPLOYER_WAGE_VERIFICATION', 1292, CAST(0.7800 AS DECIMAL(5,4))),
		(N'OUT_OF_POCKET_RECEIPTS',     1293, CAST(0.6400 AS DECIMAL(5,4))),
		(N'HOUSEHOLD_SERVICES_LOG',     1294, CAST(0.6000 AS DECIMAL(5,4))),
		(N'VOCATIONAL_ASSESSMENT',      1296, CAST(0.7400 AS DECIMAL(5,4))),
		(N'BILLING_LEDGER',             1297, CAST(0.7400 AS DECIMAL(5,4))),
		(N'PAYMENT_ADJUSTMENT_RECORD',  1298, CAST(0.6800 AS DECIMAL(5,4))),
		-- ── DAMAGES_FUTURE (1420) ────────────────────────────────────────────
		(N'LIFE_CARE_PLAN',             1421, CAST(0.8000 AS DECIMAL(5,4))),
		(N'ECONOMIST_REPORT',           1422, CAST(0.7800 AS DECIMAL(5,4))),
		-- ── DEFENSES (1610) ──────────────────────────────────────────────────
		(N'PRIOR_MEDICAL_HISTORY',      1611, CAST(0.7800 AS DECIMAL(5,4))),
		(N'PRIOR_CLAIMS_HISTORY',       1612, CAST(0.7000 AS DECIMAL(5,4))),
		(N'PRIOR_INCIDENT_RECORD',      1613, CAST(0.6800 AS DECIMAL(5,4))),
		-- ── EVIDENCE (1660) ──────────────────────────────────────────────────
		(N'EMAIL',                      1661, CAST(0.6000 AS DECIMAL(5,4))),
		(N'MESSAGE',                    1662, CAST(0.6000 AS DECIMAL(5,4))),
		-- ── EXPERT_SUPPORT (1720) ────────────────────────────────────────────
		(N'ENGINEERING_REPORT',         1721, CAST(0.7600 AS DECIMAL(5,4))),
		(N'EXPERT_DISCLOSURE',          1722, CAST(0.6800 AS DECIMAL(5,4))),
		-- ── INSURANCE_COVERAGE (1790) ────────────────────────────────────────
		(N'INSURANCE_POLICY',           1791, CAST(0.7600 AS DECIMAL(5,4))),
		(N'COVERAGE_CORRESPONDENCE',    1792, CAST(0.7000 AS DECIMAL(5,4))),
		(N'POLICY_FULL_TEXT',           1793, CAST(0.6800 AS DECIMAL(5,4))),
		(N'POLICY_ENDORSEMENT',         1794, CAST(0.6400 AS DECIMAL(5,4))),
		-- ── LIENS_SUBROGATION (1850) ─────────────────────────────────────────
		(N'LIEN_ASSERTIONS',            1851, CAST(0.7000 AS DECIMAL(5,4))),
		(N'REIMBURSEMENT_STATEMENT',    1852, CAST(0.6200 AS DECIMAL(5,4))),
		(N'LIEN_RESOLUTION_RECORD',     1853, CAST(0.6000 AS DECIMAL(5,4))),
		-- ── PROCEDURE (2070) ─────────────────────────────────────────────────
		(N'PLEADING',                   2071, CAST(0.6600 AS DECIMAL(5,4))),
		(N'COURT_ORDER',                2072, CAST(0.6600 AS DECIMAL(5,4))),
		(N'PARTY_INTERROGATORIES',      2073, CAST(0.6400 AS DECIMAL(5,4))),
		(N'ADMISSION_RESPONSE',         2074, CAST(0.6600 AS DECIMAL(5,4))),
		-- ── SETTLEMENT (2150) ────────────────────────────────────────────────
		(N'DEMAND_OFFER',               2151, CAST(0.7000 AS DECIMAL(5,4))),
		-- ── SETTLEMENT_VALUATION (2170) ──────────────────────────────────────
		(N'SETTLEMENT_AGREEMENT',       2171, CAST(0.6800 AS DECIMAL(5,4))),
		-- ── RESOLUTION (2210) ────────────────────────────────────────────────
		(N'JUDGMENT',                   2211, CAST(0.6600 AS DECIMAL(5,4))),
		(N'APPROVAL_ORDER',             2212, CAST(0.5800 AS DECIMAL(5,4))),
		(N'PAYMENT_DISBURSEMENT_RECORD', 2213, CAST(0.5200 AS DECIMAL(5,4))),
		(N'SATISFACTION_RECORD',        2214, CAST(0.5400 AS DECIMAL(5,4)))
	) AS s (EvidenceTypeCode, SortOrder, PoloxiImportanceScore)
		ON s.EvidenceTypeCode = t.EvidenceTypeCode
	 WHERE t.DecisionDomainPackId = @PiPackId
	   AND t.TenantId IS NULL
	   AND t.IsDeleted = 0;
END

COMMIT TRANSACTION;
