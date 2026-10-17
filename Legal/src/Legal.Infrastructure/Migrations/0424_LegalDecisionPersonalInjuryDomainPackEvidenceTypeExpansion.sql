-- ============================================================================
-- 0424: Personal Injury Domain Pack — evidence-type gap seed.
--
-- PURPOSE:
--   The PERSONAL_INJURY domain pack evidence catalog was seeded across
--   migrations 0284 (baseline, 10 types) and 0405 (enterprise expansion, 28
--   types). A taxonomy review proposed additional evidence types; most were
--   already present by code OR by concept (e.g. proposed MEDICAL_RECORD ==
--   existing MEDICAL_RECORDS, IMAGING_REPORT == existing IMAGING,
--   SURVEILLANCE_VIDEO == existing SURVEILLANCE_VIDEO). This migration seeds
--   ONLY the evidence types that have no existing equivalent (Bucket A).
--
--   EvidenceTypeCode follows the established UPPER_SNAKE convention used by all
--   38 existing rows; the human-readable space form lives in the Name column.
--   Each row is mapped to the most appropriate DimensionCode (including the
--   new 0423 dimensions INCIDENT_FACTS / TREATMENT / EXPERT_SUPPORT /
--   RESOLUTION), slotted into its family SortOrder band (0405 convention), and
--   given an advisory PoloxiImportanceScore anchored to the 0407 rubric.
--
--   Deliberately NOT added:
--     * Codes already present by exact or concept match (ACCIDENT_REPORT,
--       SCENE_INSPECTION, MAINTENANCE_RECORDS, WEATHER_RECORDS,
--       SURVEILLANCE_VIDEO, EMERGENCY_CALL, EVENT_DATA_RECORDER,
--       MEDICAL_RECORDS, IMAGING, OPERATIVE_REPORTS, MEDICAL_NARRATIVE,
--       PRIOR_MEDICAL_HISTORY, PHARMACY_RECORDS, MENTAL_HEALTH_RECORDS,
--       IME_REPORT, LIFE_CARE_PLAN, WITNESS_STATEMENTS, DEPOSITION,
--       PARTY_INTERROGATORIES, ACCIDENT_RECONSTRUCTION, BIOMECHANICAL_REPORT,
--       VOCATIONAL_ASSESSMENT, ECONOMIST_REPORT, MEDICAL_BILLS, WAGE_RECORDS,
--       EMPLOYER_WAGE_VERIFICATION, TAX_RETURNS, OUT_OF_POCKET_RECEIPTS,
--       HOUSEHOLD_SERVICES_LOG, INSURANCE_POLICY, COVERAGE_CORRESPONDENCE,
--       LIEN_ASSERTIONS, DEMAND_OFFER, SETTLEMENT_AGREEMENT).
--     * Bucket C overlap candidates (ScenePhoto/InjuryPhoto, Dashcam/Bodycam,
--       Gps/Telematics, SiteMeasurement, InspectionHistory, MedicalExpertReport,
--       InsurerCorrespondence, BusinessIncomeRecord, DiscoveryResponse,
--       IncidentReport) — reviewer chose to keep the existing broader codes.
--
-- SCOPE / SAFETY:
--   * Advisory configuration only — POLOXI Core owns all decision reasoning.
--   * Global defaults (TenantId NULL); tenant rows may override later.
--   * Fully idempotent: keyed by EvidenceTypeCode and inserted only WHEN NOT
--     MATCHED BY TARGET, so re-running is a no-op. Existing rows are never
--     updated or deleted.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Resolve the global PERSONAL_INJURY pack (TenantId NULL) ──────────────────
DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

IF @PiPackId IS NOT NULL
BEGIN
	-- ── Approved gap evidence types ─────────────────────────────────────────
	-- Columns: EvidenceTypeCode, Name (space form), DimensionCode, Description,
	--          SortOrder (family band per 0405), PoloxiImportanceScore (0407 rubric).
	MERGE POLOXI.Legal_DecisionDomainPackEvidenceType AS target
	USING (VALUES
		-- Medical / Treatment
		(N'IMAGING_SOURCE',             N'Imaging Source',              N'INJURY',             N'Native imaging study files underlying an imaging report.',               155, CAST(0.8800 AS DECIMAL(5,4))),
		(N'SUBSEQUENT_MEDICAL_RECORD',  N'Subsequent Medical Record',   N'TREATMENT',          N'Post-incident ongoing and follow-up treatment records.',                 180, CAST(0.7400 AS DECIMAL(5,4))),
		(N'THERAPY_RECORD',             N'Therapy Record',              N'TREATMENT',          N'Physical/occupational therapy attendance and progress records.',         185, CAST(0.6600 AS DECIMAL(5,4))),
		(N'FUNCTIONAL_ASSESSMENT',      N'Functional Assessment',       N'INJURY',             N'Objective functional-capacity and impairment evaluation.',               190, CAST(0.7400 AS DECIMAL(5,4))),
		(N'PROVIDER_CORRESPONDENCE',    N'Provider Correspondence',     N'TREATMENT',          N'Treating-provider letters and care communications.',                     195, CAST(0.5600 AS DECIMAL(5,4))),
		-- Incident records
		(N'SUPPLEMENTAL_REPORT',        N'Supplemental Report',         N'INCIDENT_FACTS',     N'Follow-up or amended incident report supplementing the original.',       270, CAST(0.7400 AS DECIMAL(5,4))),
		(N'PRIOR_INCIDENT_RECORD',      N'Prior Incident Record',       N'DEFENSES',           N'Prior similar incidents relevant to notice or pattern.',                 275, CAST(0.6600 AS DECIMAL(5,4))),
		-- Media / machine data
		(N'AUDIO_RECORDING',            N'Audio Recording',             N'INCIDENT_FACTS',     N'Audio capture of the incident or related statements.',                   280, CAST(0.7000 AS DECIMAL(5,4))),
		(N'DEVICE_SYSTEM_LOG',          N'Device System Log',           N'CAUSATION',          N'Machine, app, or system telemetry/event logs.',                          285, CAST(0.7600 AS DECIMAL(5,4))),
		-- Testimony / communications
		(N'PARTY_STATEMENT',            N'Party Statement',             N'LIABILITY',          N'Recorded or written statement from a party to the incident.',            330, CAST(0.7400 AS DECIMAL(5,4))),
		(N'ADMISSION_RESPONSE',         N'Admission Response',          N'PROCEDURE',          N'Requests-for-admission discovery responses.',                            340, CAST(0.6600 AS DECIMAL(5,4))),
		(N'EMAIL',                      N'Email',                       N'EVIDENCE',           N'Relevant email correspondence.',                                         350, CAST(0.5800 AS DECIMAL(5,4))),
		(N'MESSAGE',                    N'Message',                     N'EVIDENCE',           N'Text/chat message communications.',                                      360, CAST(0.5800 AS DECIMAL(5,4))),
		-- Expert
		(N'ENGINEERING_REPORT',         N'Engineering Report',          N'EXPERT_SUPPORT',     N'Engineering analysis of product, premises, or mechanism.',               450, CAST(0.7600 AS DECIMAL(5,4))),
		-- Financial
		(N'BILLING_LEDGER',             N'Billing Ledger',              N'DAMAGES_ECONOMIC',   N'Itemized provider billing ledger of charges.',                           550, CAST(0.7400 AS DECIMAL(5,4))),
		(N'PAYMENT_ADJUSTMENT_RECORD',  N'Payment Adjustment Record',   N'DAMAGES_ECONOMIC',   N'Billed-vs-paid adjustments, write-offs, and payments.',                  560, CAST(0.6600 AS DECIMAL(5,4))),
		-- Coverage / liens
		(N'POLICY_FULL_TEXT',           N'Policy Full Text',            N'INSURANCE_COVERAGE', N'Complete policy wording beyond the declarations page.',                  660, CAST(0.6600 AS DECIMAL(5,4))),
		(N'POLICY_ENDORSEMENT',         N'Policy Endorsement',          N'INSURANCE_COVERAGE', N'Endorsements modifying policy coverage or terms.',                       670, CAST(0.6200 AS DECIMAL(5,4))),
		(N'REIMBURSEMENT_STATEMENT',    N'Reimbursement Statement',     N'LIENS_SUBROGATION',  N'Lienholder/subrogation reimbursement statement of amounts claimed.',     680, CAST(0.5600 AS DECIMAL(5,4))),
		(N'LIEN_RESOLUTION_RECORD',     N'Lien Resolution Record',      N'LIENS_SUBROGATION',  N'Negotiated lien reduction, resolution, and payoff documentation.',       690, CAST(0.5600 AS DECIMAL(5,4))),
		-- Procedure / resolution
		(N'PLEADING',                   N'Pleading',                    N'PROCEDURE',          N'Complaints, answers, and other filed pleadings.',                        710, CAST(0.6000 AS DECIMAL(5,4))),
		(N'COURT_ORDER',                N'Court Order',                 N'PROCEDURE',          N'Judicial orders and rulings in the litigation.',                         720, CAST(0.6400 AS DECIMAL(5,4))),
		(N'EXPERT_DISCLOSURE',          N'Expert Disclosure',           N'EXPERT_SUPPORT',     N'Expert designation and disclosure filings.',                             730, CAST(0.6800 AS DECIMAL(5,4))),
		(N'APPROVAL_ORDER',             N'Approval Order',              N'RESOLUTION',         N'Minor/estate or court settlement-approval orders.',                      740, CAST(0.5600 AS DECIMAL(5,4))),
		(N'JUDGMENT',                   N'Judgment',                    N'RESOLUTION',         N'Entered judgment resolving the matter.',                                 750, CAST(0.6200 AS DECIMAL(5,4))),
		(N'PAYMENT_DISBURSEMENT_RECORD',N'Payment Disbursement Record', N'RESOLUTION',         N'Settlement/judgment disbursement and distribution records.',             760, CAST(0.5400 AS DECIMAL(5,4))),
		(N'SATISFACTION_RECORD',        N'Satisfaction Record',         N'RESOLUTION',         N'Satisfaction of judgment or final close-out documentation.',             770, CAST(0.5400 AS DECIMAL(5,4)))
	) AS source (EvidenceTypeCode, Name, DimensionCode, Description, SortOrder, PoloxiImportanceScore)
	ON target.DecisionDomainPackId = @PiPackId AND target.EvidenceTypeCode = source.EvidenceTypeCode AND target.TenantId IS NULL
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (DecisionDomainPackId, EvidenceTypeCode, Name, DimensionCode, Description, SortOrder, PoloxiImportanceScore, TenantId)
		VALUES (@PiPackId, source.EvidenceTypeCode, source.Name, source.DimensionCode, source.Description, source.SortOrder, source.PoloxiImportanceScore, NULL);
END

COMMIT TRANSACTION;
