-- ============================================================================
-- 0405: Personal Injury Domain Pack — Enterprise taxonomy expansion.
--
-- PURPOSE:
--   The PERSONAL_INJURY domain pack (migration 0284) seeded a baseline set of
--   dimensions, evidence types, verification profiles, and matter types. This
--   migration broadens that advisory taxonomy to an enterprise-grade catalog so
--   the Decision cockpit, intake wizard, and evidence→signal reasoning have the
--   full practice-area vocabulary available.
--
--   • Adds finer-grained decision dimensions (duty/breach split, damages facets,
--     comparative fault, liens/subrogation, jurisdiction/venue, limitations, …).
--   • Adds enterprise evidence types across medical, liability, testimony,
--     expert, damages/financial, and insurance/lien categories.
--   • Adds verification profiles covering preexisting-condition, wage-loss,
--     future-care, coverage, lien, limitations, authenticity, and reconstruction.
--   • Adds the full PI matter-type taxonomy (trucking, rideshare, nursing home,
--     toxic tort, dram shop, TBI, spinal, etc.).
--
-- SCOPE / SAFETY:
--   • Advisory configuration only — POLOXI Core owns all decision reasoning.
--   • Global defaults (TenantId NULL); tenant rows may override later.
--   • Does NOT touch Legal_DecisionDomainPackOutcomeCandidate — the canonical
--     C1–C9 outcomes (migrations 0396/0397) already cover the outcome space;
--     adding parallel outcome codes here would duplicate/conflict with them.
--   • Fully idempotent: every child row is keyed by its business code and
--     inserted only WHEN NOT MATCHED BY TARGET, so re-running is a no-op.
--     Existing rows are never updated or deleted.
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
	-- ── Additional decision-hierarchy dimensions ────────────────────────────
	MERGE POLOXI.Legal_DecisionDomainPackDimension AS target
	USING (VALUES
		(N'DUTY',                 N'Duty of Care',              N'Existence and scope of a legal duty, including special relationships and statutory duties.',          15),
		(N'BREACH',               N'Breach / Standard of Care', N'Negligence standard, statutory/regulatory violations (negligence per se), and breach of duty.',        16),
		(N'COMPARATIVE_FAULT',    N'Comparative / Contributory Fault', N'Plaintiff fault allocation and modified-comparative thresholds affecting recovery.',             45),
		(N'DAMAGES_ECONOMIC',     N'Economic Damages',          N'Medical expenses, lost earnings, out-of-pocket costs, and property damage.',                           41),
		(N'DAMAGES_NONECONOMIC',  N'Non-Economic Damages',      N'Pain and suffering, loss of consortium, disfigurement, and emotional distress.',                       42),
		(N'DAMAGES_FUTURE',       N'Future Damages',            N'Future medical care, life-care plans, and present-value discounting.',                                 43),
		(N'DAMAGES_PUNITIVE',     N'Punitive Damages',          N'Eligibility for punitive/exemplary damages based on malice or gross negligence.',                      44),
		(N'DAMAGES_MITIGATION',   N'Mitigation of Damages',     N'Treatment gaps and failure-to-mitigate reducing recoverable damages.',                                 46),
		(N'INSURANCE_COVERAGE',   N'Insurance & Coverage',      N'Policy limits, UM/UIM, MedPay, PIP, and excess/umbrella coverage analysis.',                           71),
		(N'LIENS_SUBROGATION',    N'Liens & Subrogation',       N'ERISA, Medicare/Medicaid, hospital, and workers'' compensation liens and subrogation interests.',       72),
		(N'JURISDICTION_VENUE',   N'Jurisdiction & Venue',      N'Subject-matter jurisdiction, personal jurisdiction, and proper venue.',                                82),
		(N'LIMITATIONS',          N'Statute of Limitations / Repose', N'Accrual, tolling, discovery rule, and notice-of-claim deadlines.',                               83),
		(N'PARTIES',              N'Parties & Capacity',        N'Minors, estates, guardians, and corporate/governmental defendants and their capacity.',                84),
		(N'SETTLEMENT_VALUATION', N'Settlement Valuation',      N'Demand ranges, verdict analogs, and structured-settlement valuation.',                                 91)
	) AS source (DimensionCode, Name, Description, SortOrder)
	ON target.DecisionDomainPackId = @PiPackId AND target.DimensionCode = source.DimensionCode AND target.TenantId IS NULL
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (DecisionDomainPackId, DimensionCode, Name, Description, SortOrder, TenantId)
		VALUES (@PiPackId, source.DimensionCode, source.Name, source.Description, source.SortOrder, NULL);

	-- ── Additional evidence types ───────────────────────────────────────────
	MERGE POLOXI.Legal_DecisionDomainPackEvidenceType AS target
	USING (VALUES
		-- Medical / Injury
		(N'OPERATIVE_REPORTS',        N'Operative / Surgical Reports',   N'INJURY',              N'Surgical and operative notes documenting treatment.',              110),
		(N'IME_REPORT',               N'Independent Medical Exam (IME)', N'CAUSATION',           N'Defense/independent medical examination findings.',                120),
		(N'MEDICAL_NARRATIVE',        N'Treating-Physician Narrative',   N'CAUSATION',           N'Treating-physician causation and prognosis narrative.',             130),
		(N'PRIOR_MEDICAL_HISTORY',    N'Prior Medical History',          N'DEFENSES',            N'Pre-incident records relevant to preexisting/aggravation.',         140),
		(N'PHARMACY_RECORDS',         N'Pharmacy Records',               N'INJURY',              N'Prescription and medication history.',                              150),
		(N'MENTAL_HEALTH_RECORDS',    N'Mental Health Records',          N'INJURY',              N'Psychological/psychiatric and PTSD treatment records.',             160),
		(N'LIFE_CARE_PLAN',           N'Life-Care Plan',                 N'DAMAGES_FUTURE',      N'Projected future-care needs and cost estimates.',                   170),
		-- Liability / Scene
		(N'SURVEILLANCE_VIDEO',       N'Surveillance / Dashcam Video',   N'LIABILITY',           N'CCTV, dashcam, or body-cam footage of the incident.',               210),
		(N'SCENE_INSPECTION',         N'Scene Inspection',               N'LIABILITY',           N'Site inspection notes, measurements, and diagrams.',                220),
		(N'EVENT_DATA_RECORDER',      N'Event Data Recorder / Telematics', N'CAUSATION',         N'Vehicle "black box" / telematics data.',                            230),
		(N'MAINTENANCE_RECORDS',      N'Maintenance Records',            N'BREACH',              N'Premises or vehicle maintenance and inspection history.',           240),
		(N'INCIDENT_INVESTIGATION',   N'Incident / OSHA Investigation',  N'BREACH',              N'Employer, OSHA, or internal investigation reports.',                250),
		(N'WEATHER_RECORDS',          N'Weather Records',                N'LIABILITY',           N'Documented conditions at the time of loss.',                        260),
		-- Testimony
		(N'PARTY_INTERROGATORIES',    N'Interrogatory Responses',        N'PROCEDURE',           N'Written discovery responses from the parties.',                     310),
		(N'EMERGENCY_CALL',           N'911 / Emergency Call',           N'LIABILITY',           N'Emergency call audio or transcript.',                               320),
		-- Expert
		(N'ACCIDENT_RECONSTRUCTION',  N'Accident Reconstruction',        N'CAUSATION',           N'Engineering accident-reconstruction analysis.',                     410),
		(N'BIOMECHANICAL_REPORT',     N'Biomechanical Report',           N'CAUSATION',           N'Injury-mechanism / biomechanical analysis.',                        420),
		(N'VOCATIONAL_ASSESSMENT',    N'Vocational Assessment',          N'DAMAGES_ECONOMIC',    N'Earning-capacity and vocational evaluation.',                       430),
		(N'ECONOMIST_REPORT',         N'Economist Report',               N'DAMAGES_FUTURE',      N'Present-value and lost-earnings economic analysis.',                440),
		-- Damages / Financial
		(N'TAX_RETURNS',              N'Tax Returns',                    N'DAMAGES_ECONOMIC',    N'Self-employed or business income substantiation.',                  510),
		(N'EMPLOYER_WAGE_VERIFICATION', N'Employer Wage Verification',   N'DAMAGES_ECONOMIC',    N'Employer wage and absence verification letter.',                    520),
		(N'OUT_OF_POCKET_RECEIPTS',   N'Out-of-Pocket Receipts',         N'DAMAGES_ECONOMIC',    N'Mileage, medical devices, and care-cost receipts.',                 530),
		(N'HOUSEHOLD_SERVICES_LOG',   N'Household Services Log',         N'DAMAGES_ECONOMIC',    N'Replacement-services valuation documentation.',                     540),
		-- Insurance / Liens / Settlement
		(N'INSURANCE_POLICY',         N'Insurance Policy / Declarations',N'INSURANCE_COVERAGE',  N'Policy declarations and coverage limits.',                          610),
		(N'COVERAGE_CORRESPONDENCE',  N'Coverage Correspondence',        N'INSURANCE_COVERAGE',  N'Reservation-of-rights and coverage-position letters.',              620),
		(N'LIEN_ASSERTIONS',          N'Lien Assertions',                N'LIENS_SUBROGATION',   N'Medicare/Medicaid, ERISA, and hospital lien notices.',              630),
		(N'SETTLEMENT_AGREEMENT',     N'Settlement Agreement / Release', N'SETTLEMENT_VALUATION',N'Executed release and settlement documentation.',                    640),
		(N'PRIOR_CLAIMS_HISTORY',     N'Prior Claims History',           N'DEFENSES',            N'ISO/claims-index and prior-claim history.',                         650)
	) AS source (EvidenceTypeCode, Name, DimensionCode, Description, SortOrder)
	ON target.DecisionDomainPackId = @PiPackId AND target.EvidenceTypeCode = source.EvidenceTypeCode AND target.TenantId IS NULL
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (DecisionDomainPackId, EvidenceTypeCode, Name, DimensionCode, Description, SortOrder, TenantId)
		VALUES (@PiPackId, source.EvidenceTypeCode, source.Name, source.DimensionCode, source.Description, source.SortOrder, NULL);

	-- ── Additional verification profiles ────────────────────────────────────
	MERGE POLOXI.Legal_DecisionDomainPackVerificationProfile AS target
	USING (VALUES
		(N'PREEXISTING_CONDITION',    N'Preexisting / Aggravation Check',     N'PRIOR_MEDICAL_HISTORY',  N'Verify whether the injury is new, aggravated, or preexisting.',         110),
		(N'WAGE_LOSS_VERIFICATION',   N'Wage-Loss Substantiation',            N'WAGE_RECORDS',           N'Verify claimed wage loss against employer and earnings records.',       120),
		(N'FUTURE_CARE_VERIFICATION', N'Future-Care Support',                 N'LIFE_CARE_PLAN',         N'Verify future-care damages against a life-care plan and economics.',    130),
		(N'COVERAGE_VERIFICATION',    N'Coverage & Limits Verification',      N'INSURANCE_POLICY',       N'Verify available policy limits and applicable coverages.',              140),
		(N'LIEN_VERIFICATION',        N'Lien / Subrogation Validation',       N'LIEN_ASSERTIONS',        N'Validate asserted liens and subrogation interests.',                    150),
		(N'SOL_VERIFICATION',         N'Limitations / Accrual Check',         N'ACCIDENT_REPORT',        N'Verify the limitations/repose deadline and accrual date.',              160),
		(N'AUTHENTICITY_SPOLIATION',  N'Media Authenticity / Chain of Custody', N'SURVEILLANCE_VIDEO',   N'Verify media authenticity and preservation/chain-of-custody.',          170),
		(N'RECONSTRUCTION_VALIDATION',N'Reconstruction Soundness',            N'ACCIDENT_RECONSTRUCTION',N'Verify the soundness and admissibility of reconstruction analysis.',    180)
	) AS source (ProfileCode, Name, EvidenceTypeCode, Description, SortOrder)
	ON target.DecisionDomainPackId = @PiPackId AND target.ProfileCode = source.ProfileCode AND target.TenantId IS NULL
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (DecisionDomainPackId, ProfileCode, Name, EvidenceTypeCode, Description, SortOrder, TenantId)
		VALUES (@PiPackId, source.ProfileCode, source.Name, source.EvidenceTypeCode, source.Description, source.SortOrder, NULL);

	-- ── Additional matter types ─────────────────────────────────────────────
	MERGE POLOXI.Legal_DecisionDomainPackMatterType AS target
	USING (VALUES
		(N'Trucking / Commercial Vehicle', N'Trucking / Commercial Vehicle', N'Collisions involving commercial trucks and fleet vehicles.',      90),
		(N'Motorcycle Accident',           N'Motorcycle Accident',           N'Motorcycle collision injuries.',                                  100),
		(N'Pedestrian / Bicycle Accident', N'Pedestrian / Bicycle Accident', N'Injuries to pedestrians or cyclists.',                            110),
		(N'Rideshare (TNC) Accident',      N'Rideshare (TNC) Accident',      N'Collisions involving transportation-network-company vehicles.',   120),
		(N'Negligent Security',            N'Negligent Security',            N'Injuries from inadequate premises security.',                     130),
		(N'Nursing Home / Elder Abuse',    N'Nursing Home / Elder Abuse',    N'Neglect or abuse injuries in care facilities.',                   140),
		(N'Construction / Workplace Injury', N'Construction / Workplace Injury', N'Injuries at construction sites or workplaces.',                150),
		(N'Toxic Tort / Exposure',         N'Toxic Tort / Exposure',         N'Injuries from toxic or hazardous exposure.',                      160),
		(N'Dram Shop / Liquor Liability',  N'Dram Shop / Liquor Liability',  N'Injuries tied to negligent service of alcohol.',                  170),
		(N'Aviation / Maritime',           N'Aviation / Maritime',           N'Aviation or maritime (including Jones Act) injuries.',             180),
		(N'Mass Tort / Pharmaceutical',    N'Mass Tort / Pharmaceutical',    N'Coordinated mass-tort or pharmaceutical injury claims.',          190),
		(N'Traumatic Brain Injury (TBI)',  N'Traumatic Brain Injury (TBI)',  N'Claims centered on traumatic brain injury.',                      200),
		(N'Spinal Cord Injury',            N'Spinal Cord Injury',            N'Claims centered on spinal cord injury.',                          210)
	) AS source (MatterTypeCode, Name, Description, SortOrder)
	ON target.DecisionDomainPackId = @PiPackId AND target.MatterTypeCode = source.MatterTypeCode AND target.TenantId IS NULL
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (DecisionDomainPackId, MatterTypeCode, Name, Description, SortOrder, TenantId)
		VALUES (@PiPackId, source.MatterTypeCode, source.Name, source.Description, source.SortOrder, NULL);
END

GO

COMMIT TRANSACTION;
GO
