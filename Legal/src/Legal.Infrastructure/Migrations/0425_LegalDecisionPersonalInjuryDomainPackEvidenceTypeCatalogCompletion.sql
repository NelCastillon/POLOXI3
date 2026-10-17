-- ============================================================================
-- 0425: Personal Injury Domain Pack — evidence-type catalog completion.
--
-- PURPOSE:
--   Completes the PERSONAL_INJURY evidence catalog across all 28 seeded
--   dimensions (0284 baseline, 0405 enterprise, 0423 gap). Prior catalog:
--   0284 (10) + 0405 (28) + 0424 (27) = 65 evidence types. A full taxonomy
--   sweep per dimension produced ~380 candidates; after concept-level
--   normalization against the 65 existing rows this migration seeds ONLY the
--   candidates with no existing equivalent.
--
--   Normalized OUT (existing code covers the concept):
--     Scene Photo / Injury Photo / Before-After Photos    -> PHOTOS_VIDEO
--     Dashcam Bodycam / Traffic Camera Footage            -> SURVEILLANCE_VIDEO
--     GPS Telematics / Crash Data Download                -> EVENT_DATA_RECORDER
--     Site Measurement / Scene Diagram / Skid Marks       -> SCENE_INSPECTION
--     Medical Expert Report / Causation Opinion Letter    -> EXPERT_REPORTS / MEDICAL_NARRATIVE
--     Recorded Statement To Insurer                       -> PARTY_STATEMENT
--     Explanation Of Benefits / Provider Lien Ledger      -> PAYMENT_ADJUSTMENT_RECORD / BILLING_LEDGER
--     Hospital Admission / Nursing Notes / Lab Results    -> MEDICAL_RECORDS
--     Pre/Post Imaging Comparison                         -> IMAGING
--     Work Order History / Repair Invoice / Inspection Checklist -> MAINTENANCE_RECORDS
--     Weather Radar Archive / Act Of God Weather          -> WEATHER_RECORDS
--     ISO ClaimSearch Report                              -> PRIOR_CLAIMS_HISTORY
--     Standard Of Care Expert Report / Human Factors / Life Care Planner / Forensic Accountant
--                                                         -> EXPERT_REPORTS / LIFE_CARE_PLAN / ECONOMIST_REPORT
--     Reservation Of Rights / Coverage Opinion Letter     -> COVERAGE_CORRESPONDENCE
--     Policy Limits Demand / Time Limited Demand          -> DEMAND_OFFER
--     Release Executed                                    -> SETTLEMENT_AGREEMENT
--     Lien Reduction Request / Lien Payoff Confirmation   -> LIEN_RESOLUTION_RECORD
--     Mediation Brief / Settlement Conference Statement   -> (seeded once as MEDIATION_BRIEF)
--     Request For Production Response                     -> PARTY_INTERROGATORIES (written discovery)
--     Damages Timeline / Timeline Chronology              -> (seeded once as TIMELINE_CHRONOLOGY)
--     Appointment Attendance Log / Missed Appointment     -> (seeded once as MISSED_APPOINTMENT_RECORD)
--     Spouse Testimony                                    -> LOSS_OF_CONSORTIUM_DECLARATION
--     Prior Lawsuit Record                                -> PRIOR_CLAIMS_HISTORY
--     Treatment Delay Record                              -> TREATMENT_GAP_ANALYSIS
--
--   Conventions (unchanged from 0405/0424):
--     * EvidenceTypeCode UPPER_SNAKE (<= 60 chars); Name is Title Case.
--     * SortOrder in per-dimension bands starting at 800 so nothing collides
--       with the existing 10-770 range; existing rows are never re-ordered.
--     * PoloxiImportanceScore anchored to the 0407 rubric: prima-facie proof
--       (liability/causation/injury) 0.74-0.88; damages/limitations 0.62-0.80;
--       evidence-management/coverage 0.54-0.70; procedure/resolution 0.46-0.64.
--
-- SCOPE / SAFETY:
--   * Advisory configuration only — POLOXI Core owns all decision reasoning.
--   * Global defaults (TenantId NULL); tenant rows may override later.
--   * Fully idempotent: keyed by EvidenceTypeCode and inserted only WHEN NOT
--     MATCHED BY TARGET. Existing rows are never updated or deleted.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0);

IF @PiPackId IS NOT NULL
BEGIN
	MERGE POLOXI.Legal_DecisionDomainPackEvidenceType AS target
	USING (VALUES
		-- ── CLAIM_FRAMEWORK (band 800) ───────────────────────────────────────
		(N'GOVERNING_LAW_MEMO',               N'Governing Law Memo',                N'CLAIM_FRAMEWORK',     N'Analysis of governing jurisdiction, applicable law, and claim theory.',            800, CAST(0.7200 AS DECIMAL(5,4))),
		(N'STATUTORY_CITATION_RECORD',        N'Statutory Citation Record',         N'CLAIM_FRAMEWORK',     N'Statutes and regulations cited as the basis of the claim.',                        805, CAST(0.7000 AS DECIMAL(5,4))),
		(N'NOTICE_OF_CLAIM',                  N'Notice Of Claim',                   N'CLAIM_FRAMEWORK',     N'Pre-suit government or statutory notice of claim filing.',                         810, CAST(0.7800 AS DECIMAL(5,4))),
		(N'RETAINER_AGREEMENT',               N'Retainer Agreement',                N'CLAIM_FRAMEWORK',     N'Client engagement and fee agreement.',                                             815, CAST(0.5000 AS DECIMAL(5,4))),
		(N'CLAIM_INTAKE_FORM',                N'Claim Intake Form',                 N'CLAIM_FRAMEWORK',     N'Initial client intake questionnaire and incident summary.',                        820, CAST(0.5800 AS DECIMAL(5,4))),
		(N'CHOICE_OF_LAW_CLAUSE',             N'Choice Of Law Clause',              N'CLAIM_FRAMEWORK',     N'Contractual choice-of-law provision affecting the governing standard.',            825, CAST(0.6200 AS DECIMAL(5,4))),
		(N'THEORY_OF_LIABILITY_MEMO',         N'Theory Of Liability Memo',          N'CLAIM_FRAMEWORK',     N'Internal memo framing negligence, strict liability, or statutory theories.',       830, CAST(0.6600 AS DECIMAL(5,4))),
		(N'STANDARD_OF_PROOF_MEMO',           N'Standard Of Proof Memo',            N'CLAIM_FRAMEWORK',     N'Analysis of burden and standard of proof for each element.',                       835, CAST(0.6200 AS DECIMAL(5,4))),
		(N'SPECIAL_CLAIM_PREREQUISITE',       N'Special Claim Prerequisite',        N'CLAIM_FRAMEWORK',     N'Certificate of merit, pre-suit notice, or other statutory prerequisite.',          840, CAST(0.7400 AS DECIMAL(5,4))),
		(N'ARBITRATION_AGREEMENT',            N'Arbitration Agreement',             N'CLAIM_FRAMEWORK',     N'Agreement compelling arbitration or alternative forum.',                           845, CAST(0.6600 AS DECIMAL(5,4))),
		(N'MASS_TORT_DESIGNATION',            N'Mass Tort Designation',             N'CLAIM_FRAMEWORK',     N'Class, MDL, or coordinated-proceeding designation records.',                      850, CAST(0.5400 AS DECIMAL(5,4))),
		-- ── LIABILITY (band 860) ─────────────────────────────────────────────
		(N'TRAFFIC_CITATION',                 N'Traffic Citation',                  N'LIABILITY',           N'Traffic ticket or citation issued at the incident.',                               860, CAST(0.7800 AS DECIMAL(5,4))),
		(N'CRIMINAL_CASE_RECORD',             N'Criminal Case Record',              N'LIABILITY',           N'DUI, reckless-driving, or related criminal disposition and plea.',                 865, CAST(0.8000 AS DECIMAL(5,4))),
		(N'TOXICOLOGY_REPORT',                N'Toxicology Report',                 N'LIABILITY',           N'Blood-alcohol or drug toxicology results for a party.',                            870, CAST(0.8200 AS DECIMAL(5,4))),
		(N'CELL_PHONE_RECORDS',               N'Cell Phone Records',                N'LIABILITY',           N'Call, text, and data usage records bearing on distraction.',                       875, CAST(0.7400 AS DECIMAL(5,4))),
		(N'DRIVER_LOGS_HOS',                  N'Driver Logs HOS',                   N'LIABILITY',           N'Commercial driver hours-of-service logs and ELD data.',                            880, CAST(0.7600 AS DECIMAL(5,4))),
		(N'PREMISES_INCIDENT_LOG',            N'Premises Incident Log',             N'LIABILITY',           N'Owner/operator incident log for the premises.',                                    885, CAST(0.7200 AS DECIMAL(5,4))),
		(N'SWEEP_INSPECTION_LOG',             N'Sweep Inspection Log',              N'LIABILITY',           N'Floor sweep or periodic premises inspection log.',                                 890, CAST(0.7400 AS DECIMAL(5,4))),
		(N'LEASE_CONTROL_AGREEMENT',          N'Lease Control Agreement',           N'LIABILITY',           N'Lease or agreement allocating control of the premises.',                           895, CAST(0.6800 AS DECIMAL(5,4))),
		(N'TRAFFIC_SIGNAL_TIMING_RECORD',     N'Traffic Signal Timing Record',      N'LIABILITY',           N'Signal phase and timing records for the intersection.',                            900, CAST(0.7000 AS DECIMAL(5,4))),
		(N'ROAD_DESIGN_PLAN',                 N'Road Design Plan',                  N'LIABILITY',           N'Roadway design plans and as-built drawings.',                                      905, CAST(0.6400 AS DECIMAL(5,4))),
		(N'CONSTRUCTION_ZONE_PLAN',           N'Construction Zone Plan',            N'LIABILITY',           N'Traffic-control and work-zone plans in effect.',                                   910, CAST(0.6600 AS DECIMAL(5,4))),
		(N'LIGHTING_SURVEY',                  N'Lighting Survey',                   N'LIABILITY',           N'Illumination measurement of the incident location.',                              915, CAST(0.6200 AS DECIMAL(5,4))),
		(N'SECURITY_STAFFING_RECORD',         N'Security Staffing Record',          N'LIABILITY',           N'Security post orders and staffing at the time of loss.',                           920, CAST(0.6400 AS DECIMAL(5,4))),
		(N'ANIMAL_BITE_HISTORY_RECORD',       N'Animal Bite History Record',        N'LIABILITY',           N'Prior bite or dangerous-animal history and control records.',                      925, CAST(0.7000 AS DECIMAL(5,4))),
		(N'RIDESHARE_TRIP_LOG',               N'Rideshare Trip Log',                N'LIABILITY',           N'Rideshare/delivery platform trip status and period data.',                         930, CAST(0.7200 AS DECIMAL(5,4))),
		-- ── DUTY (band 940) ──────────────────────────────────────────────────
		(N'PROPERTY_OWNERSHIP_RECORD',        N'Property Ownership Record',         N'DUTY',                N'Deed, title, or assessor record establishing ownership.',                          940, CAST(0.7600 AS DECIMAL(5,4))),
		(N'CONTRACT_SCOPE_OF_WORK',           N'Contract Scope Of Work',            N'DUTY',                N'Contract defining the duties undertaken by a party.',                              945, CAST(0.7000 AS DECIMAL(5,4))),
		(N'EMPLOYMENT_RELATIONSHIP_RECORD',   N'Employment Relationship Record',    N'DUTY',                N'Records establishing employment and scope for vicarious liability.',               950, CAST(0.7400 AS DECIMAL(5,4))),
		(N'LICENSE_PERMIT',                   N'License Permit',                    N'DUTY',                N'Professional or operating license and permit records.',                            955, CAST(0.6200 AS DECIMAL(5,4))),
		(N'BUILDING_CODE_EXCERPT',            N'Building Code Excerpt',             N'DUTY',                N'Applicable building or fire code provisions.',                                     960, CAST(0.6800 AS DECIMAL(5,4))),
		(N'INDUSTRY_STANDARD',                N'Industry Standard',                 N'DUTY',                N'ANSI, ASTM, or other industry standard defining expected care.',                   965, CAST(0.7000 AS DECIMAL(5,4))),
		(N'SAFETY_POLICY_MANUAL',             N'Safety Policy Manual',              N'DUTY',                N'Defendant safety policies and procedures manual.',                                 970, CAST(0.7200 AS DECIMAL(5,4))),
		(N'FRANCHISE_AGREEMENT',              N'Franchise Agreement',               N'DUTY',                N'Franchise agreement bearing on control and duty.',                                 975, CAST(0.6000 AS DECIMAL(5,4))),
		(N'PROPERTY_MANAGEMENT_AGREEMENT',    N'Property Management Agreement',     N'DUTY',                N'Management agreement allocating maintenance responsibility.',                      980, CAST(0.6600 AS DECIMAL(5,4))),
		(N'COMMON_CARRIER_STATUS_RECORD',     N'Common Carrier Status Record',      N'DUTY',                N'Records establishing heightened common-carrier duty.',                             985, CAST(0.6600 AS DECIMAL(5,4))),
		(N'HOA_COVENANTS',                    N'HOA Covenants',                     N'DUTY',                N'Association covenants and maintenance obligations.',                               990, CAST(0.5600 AS DECIMAL(5,4))),
		(N'DESIGN_IMMUNITY_RECORD',           N'Design Immunity Record',            N'DUTY',                N'Governmental design approval supporting or defeating design immunity.',            995, CAST(0.6400 AS DECIMAL(5,4))),
		-- ── BREACH (band 1000) ───────────────────────────────────────────────
		(N'TRAINING_RECORDS',                 N'Training Records',                  N'BREACH',              N'Employee training and certification records.',                                   1000, CAST(0.7200 AS DECIMAL(5,4))),
		(N'SAFETY_AUDIT_REPORT',              N'Safety Audit Report',               N'BREACH',              N'Internal or third-party safety audit findings.',                                  1005, CAST(0.7400 AS DECIMAL(5,4))),
		(N'PRIOR_COMPLAINT_LOG',              N'Prior Complaint Log',               N'BREACH',              N'Customer or tenant complaints about the hazard.',                                 1010, CAST(0.7400 AS DECIMAL(5,4))),
		(N'CODE_VIOLATION_NOTICE',            N'Code Violation Notice',             N'BREACH',              N'Government citation for code or regulatory violation.',                           1015, CAST(0.8000 AS DECIMAL(5,4))),
		(N'RECALL_NOTICE',                    N'Recall Notice',                     N'BREACH',              N'Product recall or safety bulletin.',                                              1020, CAST(0.7800 AS DECIMAL(5,4))),
		(N'WARNING_LABEL_INSTRUCTIONS',       N'Warning Label Instructions',        N'BREACH',              N'Product warnings, labels, and instructions for use.',                             1025, CAST(0.7200 AS DECIMAL(5,4))),
		(N'DESIGN_SPECIFICATION',             N'Design Specification',              N'BREACH',              N'Product or premises design specifications and drawings.',                         1030, CAST(0.6800 AS DECIMAL(5,4))),
		(N'POLICY_DEVIATION_RECORD',          N'Policy Deviation Record',           N'BREACH',              N'Evidence that conduct departed from written policy.',                             1035, CAST(0.7600 AS DECIMAL(5,4))),
		(N'STAFFING_SCHEDULE',                N'Staffing Schedule',                 N'BREACH',              N'Shift and staffing schedules at the time of loss.',                               1040, CAST(0.6200 AS DECIMAL(5,4))),
		(N'DOT_VEHICLE_INSPECTION_REPORT',    N'DOT Vehicle Inspection Report',     N'BREACH',              N'Commercial vehicle DOT inspection report.',                                       1045, CAST(0.7200 AS DECIMAL(5,4))),
		(N'EQUIPMENT_CERTIFICATION',          N'Equipment Certification',           N'BREACH',              N'Equipment inspection and certification records.',                                1050, CAST(0.6400 AS DECIMAL(5,4))),
		-- ── INCIDENT_FACTS (band 1060) ───────────────────────────────────────
		(N'TIMELINE_CHRONOLOGY',              N'Timeline Chronology',               N'INCIDENT_FACTS',      N'Compiled chronology of incident and post-incident events.',                       1060, CAST(0.7000 AS DECIMAL(5,4))),
		(N'SOCIAL_MEDIA_POST',                N'Social Media Post',                 N'INCIDENT_FACTS',      N'Party or witness social media posts about the incident.',                         1065, CAST(0.6800 AS DECIMAL(5,4))),
		(N'VEHICLE_DAMAGE_APPRAISAL',         N'Vehicle Damage Appraisal',          N'INCIDENT_FACTS',      N'Appraisal and photographs of vehicle damage.',                                    1070, CAST(0.7200 AS DECIMAL(5,4))),
		(N'TOW_SALVAGE_RECORD',               N'Tow Salvage Record',                N'INCIDENT_FACTS',      N'Tow, storage, and salvage disposition records.',                                  1075, CAST(0.5600 AS DECIMAL(5,4))),
		(N'DRONE_AERIAL_IMAGERY',             N'Drone Aerial Imagery',              N'INCIDENT_FACTS',      N'Aerial imagery of the scene.',                                                    1080, CAST(0.6400 AS DECIMAL(5,4))),
		(N'LASER_SCAN_3D',                    N'3D Laser Scan',                     N'INCIDENT_FACTS',      N'Three-dimensional scan of the scene or vehicles.',                                1085, CAST(0.7000 AS DECIMAL(5,4))),
		(N'WITNESS_LOCATION_MAP',             N'Witness Location Map',              N'INCIDENT_FACTS',      N'Diagram of witness positions and sight lines.',                                   1090, CAST(0.6200 AS DECIMAL(5,4))),
		-- ── INJURY (band 1100) ───────────────────────────────────────────────
		(N'EMS_RUN_SHEET',                    N'EMS Run Sheet',                     N'INJURY',              N'Ambulance patient-care report.',                                                  1100, CAST(0.8400 AS DECIMAL(5,4))),
		(N'ER_TRIAGE_RECORD',                 N'ER Triage Record',                  N'INJURY',              N'Emergency department triage and initial assessment.',                            1105, CAST(0.8400 AS DECIMAL(5,4))),
		(N'PAIN_JOURNAL',                     N'Pain Journal',                      N'INJURY',              N'Plaintiff contemporaneous pain and symptom diary.',                               1110, CAST(0.6000 AS DECIMAL(5,4))),
		(N'IMPAIRMENT_RATING',                N'Impairment Rating',                 N'INJURY',              N'AMA Guides or statutory permanent impairment rating.',                            1115, CAST(0.8000 AS DECIMAL(5,4))),
		(N'DENTAL_RECORDS',                   N'Dental Records',                    N'INJURY',              N'Dental and oral-surgery treatment records.',                                      1120, CAST(0.7000 AS DECIMAL(5,4))),
		(N'AUTOPSY_REPORT',                   N'Autopsy Report',                    N'INJURY',              N'Medical examiner autopsy findings.',                                              1125, CAST(0.8600 AS DECIMAL(5,4))),
		(N'DEATH_CERTIFICATE',                N'Death Certificate',                 N'INJURY',              N'Certified death certificate with cause of death.',                                1130, CAST(0.8400 AS DECIMAL(5,4))),
		(N'NEUROPSYCHOLOGICAL_EVALUATION',    N'Neuropsychological Evaluation',     N'INJURY',              N'Cognitive and neuropsychological testing results.',                               1135, CAST(0.8000 AS DECIMAL(5,4))),
		(N'VISION_HEARING_TEST',              N'Vision Hearing Test',               N'INJURY',              N'Audiology or ophthalmology test results.',                                        1140, CAST(0.6600 AS DECIMAL(5,4))),
		(N'WOUND_CARE_RECORD',                N'Wound Care Record',                 N'INJURY',              N'Wound care and burn treatment records.',                                          1145, CAST(0.6800 AS DECIMAL(5,4))),
		(N'PROSTHETIC_ORTHOTIC_RECORD',       N'Prosthetic Orthotic Record',        N'INJURY',              N'Prosthetic or orthotic fitting and device records.',                              1150, CAST(0.7000 AS DECIMAL(5,4))),
		-- ── TREATMENT (band 1160) ────────────────────────────────────────────
		(N'TREATMENT_PLAN',                   N'Treatment Plan',                    N'TREATMENT',           N'Provider treatment plan and goals.',                                              1160, CAST(0.7200 AS DECIMAL(5,4))),
		(N'REFERRAL_RECORD',                  N'Referral Record',                   N'TREATMENT',           N'Specialist referral orders and acceptance.',                                      1165, CAST(0.6200 AS DECIMAL(5,4))),
		(N'DISCHARGE_SUMMARY',                N'Discharge Summary',                 N'TREATMENT',           N'Hospital or facility discharge summary.',                                         1170, CAST(0.7600 AS DECIMAL(5,4))),
		(N'MMI_DETERMINATION',                N'MMI Determination',                 N'TREATMENT',           N'Maximum medical improvement determination.',                                      1175, CAST(0.7800 AS DECIMAL(5,4))),
		(N'TREATMENT_GAP_ANALYSIS',           N'Treatment Gap Analysis',            N'TREATMENT',           N'Analysis of gaps or delays in treatment.',                                        1180, CAST(0.7000 AS DECIMAL(5,4))),
		(N'CHIROPRACTIC_RECORDS',             N'Chiropractic Records',              N'TREATMENT',           N'Chiropractic treatment notes and plans.',                                         1185, CAST(0.6400 AS DECIMAL(5,4))),
		(N'PAIN_MANAGEMENT_RECORDS',          N'Pain Management Records',           N'TREATMENT',           N'Pain management and injection therapy records.',                                  1190, CAST(0.7000 AS DECIMAL(5,4))),
		(N'HOME_HEALTH_RECORDS',              N'Home Health Records',               N'TREATMENT',           N'Home health and attendant care records.',                                         1195, CAST(0.6600 AS DECIMAL(5,4))),
		(N'DME_PRESCRIPTION',                 N'DME Prescription',                  N'TREATMENT',           N'Durable medical equipment prescriptions and invoices.',                           1200, CAST(0.6000 AS DECIMAL(5,4))),
		(N'SURGICAL_CONSULTATION',            N'Surgical Consultation',             N'TREATMENT',           N'Surgical consult and recommendation note.',                                       1205, CAST(0.7600 AS DECIMAL(5,4))),
		(N'SECOND_OPINION_RECORD',            N'Second Opinion Record',             N'TREATMENT',           N'Second-opinion evaluation record.',                                               1210, CAST(0.6400 AS DECIMAL(5,4))),
		(N'TELEHEALTH_VISIT_RECORD',          N'Telehealth Visit Record',           N'TREATMENT',           N'Telemedicine visit documentation.',                                               1215, CAST(0.5800 AS DECIMAL(5,4))),
		(N'WORK_RESTRICTION_NOTE',            N'Work Restriction Note',             N'TREATMENT',           N'Provider work status and restriction notes.',                                     1220, CAST(0.7200 AS DECIMAL(5,4))),
		-- ── CAUSATION (band 1230) ────────────────────────────────────────────
		(N'DIFFERENTIAL_DIAGNOSIS_RECORD',    N'Differential Diagnosis Record',     N'CAUSATION',           N'Provider differential diagnosis ruling alternative causes in or out.',           1230, CAST(0.8200 AS DECIMAL(5,4))),
		(N'INTERVENING_EVENT_RECORD',         N'Intervening Event Record',          N'CAUSATION',           N'Records of intervening events or subsequent trauma.',                            1235, CAST(0.7600 AS DECIMAL(5,4))),
		(N'DELTA_V_ANALYSIS',                 N'Delta V Analysis',                  N'CAUSATION',           N'Change-in-velocity and crash severity analysis.',                                1240, CAST(0.7600 AS DECIMAL(5,4))),
		(N'MEDICAL_LITERATURE',               N'Medical Literature',                N'CAUSATION',           N'Peer-reviewed literature supporting the causal mechanism.',                      1245, CAST(0.6600 AS DECIMAL(5,4))),
		(N'UTILIZATION_REVIEW',               N'Utilization Review',                N'CAUSATION',           N'Peer or utilization review of treatment necessity.',                             1250, CAST(0.6600 AS DECIMAL(5,4))),
		(N'TEMPORAL_ONSET_RECORD',            N'Temporal Onset Record',             N'CAUSATION',           N'Evidence of symptom onset timing relative to the incident.',                     1255, CAST(0.7800 AS DECIMAL(5,4))),
		(N'PRODUCT_FAILURE_ANALYSIS',         N'Product Failure Analysis',          N'CAUSATION',           N'Laboratory failure analysis of the product or component.',                       1260, CAST(0.7800 AS DECIMAL(5,4))),
		-- ── DAMAGES umbrella (band 1270) ─────────────────────────────────────
		(N'DAMAGES_SUMMARY_SCHEDULE',         N'Damages Summary Schedule',          N'DAMAGES',             N'Consolidated schedule of all claimed damages.',                                   1270, CAST(0.7400 AS DECIMAL(5,4))),
		(N'SPECIAL_DAMAGES_WORKSHEET',        N'Special Damages Worksheet',         N'DAMAGES',             N'Itemized special damages computation.',                                           1275, CAST(0.7400 AS DECIMAL(5,4))),
		(N'COLLATERAL_SOURCE_RECORD',         N'Collateral Source Record',          N'DAMAGES',             N'Payments from collateral sources and offset analysis.',                           1280, CAST(0.6600 AS DECIMAL(5,4))),
		-- ── DAMAGES_ECONOMIC (band 1290) ─────────────────────────────────────
		(N'PAY_STUBS',                        N'Pay Stubs',                         N'DAMAGES_ECONOMIC',    N'Pre- and post-incident pay stubs.',                                               1290, CAST(0.7600 AS DECIMAL(5,4))),
		(N'W2_1099_FORMS',                    N'W2 1099 Forms',                     N'DAMAGES_ECONOMIC',    N'Annual wage and income statements.',                                              1295, CAST(0.7600 AS DECIMAL(5,4))),
		(N'BUSINESS_INCOME_RECORD',           N'Business Income Record',            N'DAMAGES_ECONOMIC',    N'Profit-and-loss and business income records.',                                    1300, CAST(0.7000 AS DECIMAL(5,4))),
		(N'PROPERTY_DAMAGE_ESTIMATE',         N'Property Damage Estimate',          N'DAMAGES_ECONOMIC',    N'Repair estimate or property damage valuation.',                                   1305, CAST(0.6600 AS DECIMAL(5,4))),
		(N'RENTAL_CAR_RECEIPT',               N'Rental Car Receipt',                N'DAMAGES_ECONOMIC',    N'Loss-of-use rental receipts.',                                                    1310, CAST(0.5400 AS DECIMAL(5,4))),
		(N'MILEAGE_LOG',                      N'Mileage Log',                       N'DAMAGES_ECONOMIC',    N'Medical travel mileage log.',                                                     1315, CAST(0.5400 AS DECIMAL(5,4))),
		(N'CHILDCARE_RECEIPTS',               N'Childcare Receipts',                N'DAMAGES_ECONOMIC',    N'Incident-related childcare expense receipts.',                                    1320, CAST(0.5600 AS DECIMAL(5,4))),
		(N'HOME_MODIFICATION_ESTIMATE',       N'Home Modification Estimate',        N'DAMAGES_ECONOMIC',    N'Accessibility modification estimates and invoices.',                              1325, CAST(0.6600 AS DECIMAL(5,4))),
		(N'MEDICAL_CODING_AUDIT',             N'Medical Coding Audit',              N'DAMAGES_ECONOMIC',    N'CPT/HCPCS coding audit of billed charges.',                                       1330, CAST(0.6600 AS DECIMAL(5,4))),
		(N'REASONABLE_VALUE_ANALYSIS',        N'Reasonable Value Analysis',         N'DAMAGES_ECONOMIC',    N'Analysis of the reasonable value of medical services.',                           1335, CAST(0.7400 AS DECIMAL(5,4))),
		(N'SOCIAL_SECURITY_EARNINGS_STATEMENT', N'Social Security Earnings Statement', N'DAMAGES_ECONOMIC', N'SSA lifetime earnings record.',                                                   1340, CAST(0.7000 AS DECIMAL(5,4))),
		(N'BENEFITS_LOSS_STATEMENT',          N'Benefits Loss Statement',           N'DAMAGES_ECONOMIC',    N'Lost employment benefits valuation.',                                             1345, CAST(0.6600 AS DECIMAL(5,4))),
		(N'PENSION_IMPACT_ANALYSIS',          N'Pension Impact Analysis',           N'DAMAGES_ECONOMIC',    N'Retirement and pension loss analysis.',                                           1350, CAST(0.6400 AS DECIMAL(5,4))),
		(N'DIMINISHED_VALUE_APPRAISAL',       N'Diminished Value Appraisal',        N'DAMAGES_ECONOMIC',    N'Post-repair diminished value appraisal.',                                         1355, CAST(0.5600 AS DECIMAL(5,4))),
		(N'TOTAL_LOSS_VALUATION',             N'Total Loss Valuation',              N'DAMAGES_ECONOMIC',    N'Actual cash value total-loss valuation.',                                         1360, CAST(0.6000 AS DECIMAL(5,4))),
		-- ── DAMAGES_NONECONOMIC (band 1370) ──────────────────────────────────
		(N'DAY_IN_THE_LIFE_VIDEO',            N'Day In The Life Video',             N'DAMAGES_NONECONOMIC', N'Documentary video of daily living impact.',                                       1370, CAST(0.7400 AS DECIMAL(5,4))),
		(N'FAMILY_FRIEND_DECLARATION',        N'Family Friend Declaration',         N'DAMAGES_NONECONOMIC', N'Lay declarations on changes in the plaintiff.',                                   1375, CAST(0.6800 AS DECIMAL(5,4))),
		(N'LOSS_OF_CONSORTIUM_DECLARATION',   N'Loss Of Consortium Declaration',    N'DAMAGES_NONECONOMIC', N'Spouse or partner consortium-loss statement.',                                    1380, CAST(0.7000 AS DECIMAL(5,4))),
		(N'ACTIVITY_LIMITATION_LOG',          N'Activity Limitation Log',           N'DAMAGES_NONECONOMIC', N'Log of activities lost or limited after injury.',                                 1385, CAST(0.6400 AS DECIMAL(5,4))),
		(N'SCARRING_DISFIGUREMENT_PHOTO',     N'Scarring Disfigurement Photo',      N'DAMAGES_NONECONOMIC', N'Photographs documenting scarring or disfigurement.',                              1390, CAST(0.7600 AS DECIMAL(5,4))),
		(N'PSYCHOLOGICAL_EVALUATION',         N'Psychological Evaluation',          N'DAMAGES_NONECONOMIC', N'Forensic psychological evaluation of emotional distress.',                        1395, CAST(0.7600 AS DECIMAL(5,4))),
		(N'QUALITY_OF_LIFE_ASSESSMENT',       N'Quality Of Life Assessment',        N'DAMAGES_NONECONOMIC', N'Standardized quality-of-life instrument results.',                                1400, CAST(0.6600 AS DECIMAL(5,4))),
		(N'GRIEF_COUNSELING_RECORD',          N'Grief Counseling Record',           N'DAMAGES_NONECONOMIC', N'Bereavement counseling records in wrongful death.',                               1405, CAST(0.6600 AS DECIMAL(5,4))),
		(N'HOBBY_ACTIVITY_EVIDENCE',          N'Hobby Activity Evidence',           N'DAMAGES_NONECONOMIC', N'Evidence of pre-injury hobbies and activities.',                                  1410, CAST(0.5800 AS DECIMAL(5,4))),
		(N'SLEEP_DISTURBANCE_RECORD',         N'Sleep Disturbance Record',          N'DAMAGES_NONECONOMIC', N'Sleep study or documented sleep disruption.',                                     1415, CAST(0.5800 AS DECIMAL(5,4))),
		-- ── DAMAGES_FUTURE (band 1420) ───────────────────────────────────────
		(N'FUTURE_SURGERY_RECOMMENDATION',    N'Future Surgery Recommendation',     N'DAMAGES_FUTURE',      N'Provider recommendation for future surgical intervention.',                      1420, CAST(0.8000 AS DECIMAL(5,4))),
		(N'LIFE_EXPECTANCY_TABLE',            N'Life Expectancy Table',             N'DAMAGES_FUTURE',      N'Actuarial life expectancy reference.',                                            1425, CAST(0.6400 AS DECIMAL(5,4))),
		(N'WORKLIFE_EXPECTANCY_ANALYSIS',     N'Worklife Expectancy Analysis',      N'DAMAGES_FUTURE',      N'Remaining worklife expectancy analysis.',                                         1430, CAST(0.7000 AS DECIMAL(5,4))),
		(N'PRESENT_VALUE_CALCULATION',        N'Present Value Calculation',         N'DAMAGES_FUTURE',      N'Present-value discounting of future losses.',                                     1435, CAST(0.7000 AS DECIMAL(5,4))),
		(N'MEDICARE_SET_ASIDE_ALLOCATION',    N'Medicare Set Aside Allocation',     N'DAMAGES_FUTURE',      N'MSA allocation report for future Medicare-covered care.',                        1440, CAST(0.6800 AS DECIMAL(5,4))),
		(N'ANNUITY_QUOTE',                    N'Annuity Quote',                     N'DAMAGES_FUTURE',      N'Structured settlement annuity quote.',                                            1445, CAST(0.5600 AS DECIMAL(5,4))),
		(N'FUTURE_MEDICAL_COST_PROJECTION',   N'Future Medical Cost Projection',    N'DAMAGES_FUTURE',      N'Provider or expert projection of future medical costs.',                         1450, CAST(0.7800 AS DECIMAL(5,4))),
		(N'VOCATIONAL_REHABILITATION_PLAN',   N'Vocational Rehabilitation Plan',    N'DAMAGES_FUTURE',      N'Retraining and vocational rehabilitation plan.',                                  1455, CAST(0.6600 AS DECIMAL(5,4))),
		(N'ATTENDANT_CARE_COST_ESTIMATE',     N'Attendant Care Cost Estimate',      N'DAMAGES_FUTURE',      N'Estimate of future attendant care costs.',                                        1460, CAST(0.7000 AS DECIMAL(5,4))),
		-- ── DAMAGES_PUNITIVE (band 1470) ─────────────────────────────────────
		(N'PRIOR_NOTICE_EVIDENCE',            N'Prior Notice Evidence',             N'DAMAGES_PUNITIVE',    N'Evidence defendant knew of the hazard before the incident.',                     1470, CAST(0.7800 AS DECIMAL(5,4))),
		(N'INTERNAL_COMMUNICATIONS',          N'Internal Communications',           N'DAMAGES_PUNITIVE',    N'Internal emails or memos showing knowledge or disregard.',                       1475, CAST(0.7800 AS DECIMAL(5,4))),
		(N'DEFENDANT_FINANCIAL_STATEMENT',    N'Defendant Financial Statement',     N'DAMAGES_PUNITIVE',    N'Net worth evidence for punitive damages.',                                        1480, CAST(0.6600 AS DECIMAL(5,4))),
		(N'REGULATORY_SANCTION_RECORD',       N'Regulatory Sanction Record',        N'DAMAGES_PUNITIVE',    N'Agency fines, sanctions, or consent orders.',                                     1485, CAST(0.7200 AS DECIMAL(5,4))),
		(N'PATTERN_OF_CONDUCT_RECORD',        N'Pattern Of Conduct Record',         N'DAMAGES_PUNITIVE',    N'Evidence of repeated similar misconduct.',                                        1490, CAST(0.7400 AS DECIMAL(5,4))),
		(N'WHISTLEBLOWER_REPORT',             N'Whistleblower Report',              N'DAMAGES_PUNITIVE',    N'Internal or regulatory whistleblower complaint.',                                 1495, CAST(0.7000 AS DECIMAL(5,4))),
		(N'COVER_UP_EVIDENCE',                N'Cover Up Evidence',                 N'DAMAGES_PUNITIVE',    N'Evidence of concealment or destruction after the incident.',                     1500, CAST(0.8000 AS DECIMAL(5,4))),
		-- ── DAMAGES_MITIGATION (band 1510) ───────────────────────────────────
		(N'MISSED_APPOINTMENT_RECORD',        N'Missed Appointment Record',         N'DAMAGES_MITIGATION',  N'No-show and cancelled appointment records.',                                      1510, CAST(0.6600 AS DECIMAL(5,4))),
		(N'NON_COMPLIANCE_NOTE',              N'Non Compliance Note',               N'DAMAGES_MITIGATION',  N'Provider notes of non-compliance with treatment.',                                1515, CAST(0.7000 AS DECIMAL(5,4))),
		(N'RETURN_TO_WORK_RELEASE',           N'Return To Work Release',            N'DAMAGES_MITIGATION',  N'Provider release to return to work.',                                             1520, CAST(0.7200 AS DECIMAL(5,4))),
		(N'JOB_SEARCH_LOG',                   N'Job Search Log',                    N'DAMAGES_MITIGATION',  N'Documentation of efforts to obtain replacement work.',                            1525, CAST(0.6000 AS DECIMAL(5,4))),
		(N'DECLINED_TREATMENT_RECORD',        N'Declined Treatment Record',         N'DAMAGES_MITIGATION',  N'Record of recommended treatment declined by plaintiff.',                          1530, CAST(0.7000 AS DECIMAL(5,4))),
		(N'AMA_DISCHARGE_RECORD',             N'AMA Discharge Record',              N'DAMAGES_MITIGATION',  N'Discharge against medical advice documentation.',                                 1535, CAST(0.6800 AS DECIMAL(5,4))),
		(N'ALTERNATIVE_EMPLOYMENT_OFFER',     N'Alternative Employment Offer',      N'DAMAGES_MITIGATION',  N'Offer of modified or alternative employment.',                                    1540, CAST(0.6200 AS DECIMAL(5,4))),
		(N'HOME_EXERCISE_COMPLIANCE_LOG',     N'Home Exercise Compliance Log',      N'DAMAGES_MITIGATION',  N'Home exercise program compliance records.',                                       1545, CAST(0.5400 AS DECIMAL(5,4))),
		-- ── COMPARATIVE_FAULT (band 1550) ────────────────────────────────────
		(N'PLAINTIFF_CONDUCT_EVIDENCE',       N'Plaintiff Conduct Evidence',        N'COMPARATIVE_FAULT',   N'Evidence of plaintiff conduct contributing to the incident.',                    1550, CAST(0.7800 AS DECIMAL(5,4))),
		(N'SEATBELT_HELMET_USE_RECORD',       N'Seatbelt Helmet Use Record',        N'COMPARATIVE_FAULT',   N'Evidence of restraint or protective-equipment use.',                             1555, CAST(0.7400 AS DECIMAL(5,4))),
		(N'PLAINTIFF_TOXICOLOGY',             N'Plaintiff Toxicology',              N'COMPARATIVE_FAULT',   N'Plaintiff alcohol or drug toxicology results.',                                  1560, CAST(0.7800 AS DECIMAL(5,4))),
		(N'PLAINTIFF_CELL_RECORDS',           N'Plaintiff Cell Records',            N'COMPARATIVE_FAULT',   N'Plaintiff phone usage at the time of incident.',                                 1565, CAST(0.7200 AS DECIMAL(5,4))),
		(N'FOOTWEAR_ATTIRE_PHOTO',            N'Footwear Attire Photo',             N'COMPARATIVE_FAULT',   N'Photographs of plaintiff footwear and attire.',                                  1570, CAST(0.6000 AS DECIMAL(5,4))),
		(N'WARNING_SIGN_PHOTO',               N'Warning Sign Photo',                N'COMPARATIVE_FAULT',   N'Photographs of posted warnings at the scene.',                                   1575, CAST(0.7000 AS DECIMAL(5,4))),
		(N'FAULT_ALLOCATION_ANALYSIS',        N'Fault Allocation Analysis',         N'COMPARATIVE_FAULT',   N'Expert or internal apportionment of fault.',                                     1580, CAST(0.7200 AS DECIMAL(5,4))),
		(N'PLAINTIFF_SPEED_EVIDENCE',         N'Plaintiff Speed Evidence',          N'COMPARATIVE_FAULT',   N'Evidence of plaintiff vehicle speed.',                                           1585, CAST(0.7200 AS DECIMAL(5,4))),
		(N'PLAINTIFF_PRIOR_CITATION_RECORD',  N'Plaintiff Prior Citation Record',   N'COMPARATIVE_FAULT',   N'Plaintiff driving record and prior citations.',                                  1590, CAST(0.5400 AS DECIMAL(5,4))),
		(N'PEDESTRIAN_SIGNAL_RECORD',         N'Pedestrian Signal Record',          N'COMPARATIVE_FAULT',   N'Crosswalk and pedestrian signal status evidence.',                               1595, CAST(0.6800 AS DECIMAL(5,4))),
		(N'THIRD_PARTY_FAULT_EVIDENCE',       N'Third Party Fault Evidence',        N'COMPARATIVE_FAULT',   N'Evidence attributing fault to a non-party.',                                     1600, CAST(0.7200 AS DECIMAL(5,4))),
		-- ── DEFENSES (band 1610) ─────────────────────────────────────────────
		(N'SOCIAL_MEDIA_SURVEILLANCE',        N'Social Media Surveillance',         N'DEFENSES',            N'Defense capture of plaintiff social media activity.',                            1610, CAST(0.7000 AS DECIMAL(5,4))),
		(N'SUB_ROSA_SURVEILLANCE',            N'Sub Rosa Surveillance',             N'DEFENSES',            N'Defense investigator surveillance footage and report.',                          1615, CAST(0.7600 AS DECIMAL(5,4))),
		(N'ASSUMPTION_OF_RISK_WAIVER',        N'Assumption Of Risk Waiver',         N'DEFENSES',            N'Signed waiver or assumption-of-risk acknowledgment.',                            1620, CAST(0.8000 AS DECIMAL(5,4))),
		(N'OPEN_OBVIOUS_PHOTO',               N'Open Obvious Photo',                N'DEFENSES',            N'Photographs supporting an open-and-obvious defense.',                            1625, CAST(0.6800 AS DECIMAL(5,4))),
		(N'SUBSEQUENT_ACCIDENT_RECORD',       N'Subsequent Accident Record',        N'DEFENSES',            N'Records of later accidents or injuries.',                                        1630, CAST(0.7400 AS DECIMAL(5,4))),
		(N'IMMUNITY_DOCUMENTATION',           N'Immunity Documentation',            N'DEFENSES',            N'Sovereign, charitable, or statutory immunity records.',                          1635, CAST(0.7600 AS DECIMAL(5,4))),
		(N'WORKERS_COMP_EXCLUSIVITY_RECORD',  N'Workers Comp Exclusivity Record',   N'DEFENSES',            N'Workers compensation coverage supporting exclusive remedy.',                     1640, CAST(0.7600 AS DECIMAL(5,4))),
		(N'INDEPENDENT_CONTRACTOR_AGREEMENT', N'Independent Contractor Agreement',  N'DEFENSES',            N'Agreement disputing employment and vicarious liability.',                        1645, CAST(0.7000 AS DECIMAL(5,4))),
		(N'SUDDEN_EMERGENCY_EVIDENCE',        N'Sudden Emergency Evidence',         N'DEFENSES',            N'Evidence supporting a sudden-emergency defense.',                                1650, CAST(0.6400 AS DECIMAL(5,4))),
		(N'FRAUD_INDICATOR_REPORT',           N'Fraud Indicator Report',            N'DEFENSES',            N'SIU or fraud indicator analysis.',                                               1655, CAST(0.6600 AS DECIMAL(5,4))),
		-- ── EVIDENCE (band 1660) ─────────────────────────────────────────────
		(N'PRESERVATION_LETTER',              N'Preservation Letter',               N'EVIDENCE',            N'Litigation hold or evidence preservation demand.',                               1660, CAST(0.6800 AS DECIMAL(5,4))),
		(N'SPOLIATION_NOTICE',                N'Spoliation Notice',                 N'EVIDENCE',            N'Notice or motion concerning destroyed evidence.',                                1665, CAST(0.7000 AS DECIMAL(5,4))),
		(N'CHAIN_OF_CUSTODY_LOG',             N'Chain Of Custody Log',              N'EVIDENCE',            N'Custody log for physical or digital evidence.',                                  1670, CAST(0.6600 AS DECIMAL(5,4))),
		(N'AUTHENTICATION_AFFIDAVIT',         N'Authentication Affidavit',          N'EVIDENCE',            N'Affidavit authenticating a document or recording.',                              1675, CAST(0.6200 AS DECIMAL(5,4))),
		(N'RECORDS_CUSTODIAN_CERTIFICATION',  N'Records Custodian Certification',   N'EVIDENCE',            N'Custodian certification for business or medical records.',                      1680, CAST(0.6200 AS DECIMAL(5,4))),
		(N'SUBPOENA_DUCES_TECUM',             N'Subpoena Duces Tecum',              N'EVIDENCE',            N'Records subpoena and return.',                                                   1685, CAST(0.5600 AS DECIMAL(5,4))),
		(N'METADATA_FORENSIC_REPORT',         N'Metadata Forensic Report',          N'EVIDENCE',            N'Forensic analysis of file metadata and authenticity.',                           1690, CAST(0.6600 AS DECIMAL(5,4))),
		(N'PRIVILEGE_LOG',                    N'Privilege Log',                     N'EVIDENCE',            N'Log of withheld privileged documents.',                                          1695, CAST(0.5000 AS DECIMAL(5,4))),
		(N'EVIDENCE_INVENTORY',               N'Evidence Inventory',                N'EVIDENCE',            N'Master inventory of collected evidence.',                                        1700, CAST(0.5600 AS DECIMAL(5,4))),
		(N'TRANSLATION_CERTIFICATION',        N'Translation Certification',         N'EVIDENCE',            N'Certified translation of foreign-language evidence.',                            1705, CAST(0.5400 AS DECIMAL(5,4))),
		(N'DIGITAL_FORENSIC_IMAGE',           N'Digital Forensic Image',            N'EVIDENCE',            N'Forensic image of a device or storage medium.',                                  1710, CAST(0.6400 AS DECIMAL(5,4))),
		-- ── EXPERT_SUPPORT (band 1720) ───────────────────────────────────────
		(N'EXPERT_CV',                        N'Expert CV',                         N'EXPERT_SUPPORT',      N'Expert qualifications and curriculum vitae.',                                    1720, CAST(0.6400 AS DECIMAL(5,4))),
		(N'EXPERT_FEE_AGREEMENT',             N'Expert Fee Agreement',              N'EXPERT_SUPPORT',      N'Expert retention and fee agreement.',                                            1725, CAST(0.5000 AS DECIMAL(5,4))),
		(N'EXPERT_MATERIALS_CONSIDERED',      N'Expert Materials Considered',       N'EXPERT_SUPPORT',      N'List of materials reviewed by the expert.',                                      1730, CAST(0.6600 AS DECIMAL(5,4))),
		(N'EXPERT_DEPOSITION',                N'Expert Deposition',                 N'EXPERT_SUPPORT',      N'Expert witness deposition transcript.',                                          1735, CAST(0.7600 AS DECIMAL(5,4))),
		(N'REBUTTAL_EXPERT_REPORT',           N'Rebuttal Expert Report',            N'EXPERT_SUPPORT',      N'Rebuttal report responding to opposing expert.',                                 1740, CAST(0.7400 AS DECIMAL(5,4))),
		(N'DAUBERT_FRYE_MOTION',              N'Daubert Frye Motion',               N'EXPERT_SUPPORT',      N'Motion challenging expert admissibility.',                                       1745, CAST(0.7000 AS DECIMAL(5,4))),
		(N'PRIOR_TESTIMONY_LIST',             N'Prior Testimony List',              N'EXPERT_SUPPORT',      N'Expert prior testimony disclosure.',                                             1750, CAST(0.5600 AS DECIMAL(5,4))),
		(N'SUPPLEMENTAL_EXPERT_REPORT',       N'Supplemental Expert Report',        N'EXPERT_SUPPORT',      N'Supplemental or amended expert report.',                                         1755, CAST(0.7200 AS DECIMAL(5,4))),
		(N'EXPERT_METHODOLOGY_MEMO',          N'Expert Methodology Memo',           N'EXPERT_SUPPORT',      N'Explanation of expert methodology and assumptions.',                             1760, CAST(0.6800 AS DECIMAL(5,4))),
		-- ── INSURANCE umbrella (band 1770) ───────────────────────────────────
		(N'INSURANCE_CLAIM_FILE',             N'Insurance Claim File',              N'INSURANCE',           N'Carrier claim file and activity log.',                                           1770, CAST(0.7000 AS DECIMAL(5,4))),
		(N'ADJUSTER_NOTES',                   N'Adjuster Notes',                    N'INSURANCE',           N'Claims adjuster notes and evaluations.',                                         1775, CAST(0.6800 AS DECIMAL(5,4))),
		(N'CLAIM_ACKNOWLEDGMENT',             N'Claim Acknowledgment',              N'INSURANCE',           N'Carrier claim number assignment and acknowledgment.',                            1780, CAST(0.5200 AS DECIMAL(5,4))),
		-- ── INSURANCE_COVERAGE (band 1790) ───────────────────────────────────
		(N'UM_UIM_SELECTION_FORM',            N'UM UIM Selection Form',             N'INSURANCE_COVERAGE',  N'Uninsured/underinsured motorist selection or rejection form.',                  1790, CAST(0.7400 AS DECIMAL(5,4))),
		(N'MEDPAY_PIP_LOG',                   N'MedPay PIP Log',                    N'INSURANCE_COVERAGE',  N'Medical payments or PIP benefits payment log.',                                 1795, CAST(0.6600 AS DECIMAL(5,4))),
		(N'EXCESS_UMBRELLA_POLICY',           N'Excess Umbrella Policy',            N'INSURANCE_COVERAGE',  N'Excess or umbrella policy declarations.',                                        1800, CAST(0.7200 AS DECIMAL(5,4))),
		(N'POLICY_LIMITS_DISCLOSURE',         N'Policy Limits Disclosure',          N'INSURANCE_COVERAGE',  N'Statutory or voluntary policy limits disclosure.',                              1805, CAST(0.7600 AS DECIMAL(5,4))),
		(N'COVERAGE_DENIAL_LETTER',           N'Coverage Denial Letter',            N'INSURANCE_COVERAGE',  N'Carrier declination or denial of coverage.',                                    1810, CAST(0.7400 AS DECIMAL(5,4))),
		(N'BAD_FAITH_CORRESPONDENCE',         N'Bad Faith Correspondence',          N'INSURANCE_COVERAGE',  N'Correspondence supporting a bad-faith claim.',                                  1815, CAST(0.7000 AS DECIMAL(5,4))),
		(N'DMV_ACCIDENT_REPORT_SR1',          N'DMV Accident Report SR1',           N'INSURANCE_COVERAGE',  N'DMV financial-responsibility accident report.',                                 1820, CAST(0.5800 AS DECIMAL(5,4))),
		(N'CERTIFICATE_OF_INSURANCE',         N'Certificate Of Insurance',          N'INSURANCE_COVERAGE',  N'Certificate evidencing coverage for a party.',                                  1825, CAST(0.6200 AS DECIMAL(5,4))),
		(N'POLICY_APPLICATION',               N'Policy Application',                N'INSURANCE_COVERAGE',  N'Insurance application and representations.',                                   1830, CAST(0.5800 AS DECIMAL(5,4))),
		(N'PREMIUM_PAYMENT_RECORD',           N'Premium Payment Record',            N'INSURANCE_COVERAGE',  N'Premium payment history bearing on policy in force.',                           1835, CAST(0.5600 AS DECIMAL(5,4))),
		(N'POLICY_CANCELLATION_NOTICE',       N'Policy Cancellation Notice',        N'INSURANCE_COVERAGE',  N'Notice of policy cancellation or lapse.',                                       1840, CAST(0.6600 AS DECIMAL(5,4))),
		(N'SELF_INSURED_RETENTION_RECORD',    N'Self Insured Retention Record',     N'INSURANCE_COVERAGE',  N'Self-insurance or retention documentation.',                                    1845, CAST(0.6000 AS DECIMAL(5,4))),
		-- ── LIENS_SUBROGATION (band 1850) ────────────────────────────────────
		(N'MEDICARE_CONDITIONAL_PAYMENT_LETTER', N'Medicare Conditional Payment Letter', N'LIENS_SUBROGATION', N'Medicare conditional payment summary.',                                        1850, CAST(0.7000 AS DECIMAL(5,4))),
		(N'MEDICARE_FINAL_DEMAND',            N'Medicare Final Demand',             N'LIENS_SUBROGATION',   N'Medicare final demand letter.',                                                  1855, CAST(0.7200 AS DECIMAL(5,4))),
		(N'MEDICAID_LIEN_NOTICE',             N'Medicaid Lien Notice',              N'LIENS_SUBROGATION',   N'State Medicaid recovery notice.',                                                1860, CAST(0.6800 AS DECIMAL(5,4))),
		(N'ERISA_PLAN_DOCUMENT',              N'ERISA Plan Document',               N'LIENS_SUBROGATION',   N'ERISA plan document and reimbursement provisions.',                              1865, CAST(0.7000 AS DECIMAL(5,4))),
		(N'HOSPITAL_LIEN_FILING',             N'Hospital Lien Filing',              N'LIENS_SUBROGATION',   N'Recorded hospital lien.',                                                        1870, CAST(0.6600 AS DECIMAL(5,4))),
		(N'WORKERS_COMP_LIEN_NOTICE',         N'Workers Comp Lien Notice',          N'LIENS_SUBROGATION',   N'Workers compensation carrier lien notice.',                                      1875, CAST(0.6800 AS DECIMAL(5,4))),
		(N'LETTER_OF_PROTECTION',             N'Letter Of Protection',              N'LIENS_SUBROGATION',   N'Attorney letter of protection to a provider.',                                   1880, CAST(0.6200 AS DECIMAL(5,4))),
		(N'MEDICAL_FUNDING_AGREEMENT',        N'Medical Funding Agreement',         N'LIENS_SUBROGATION',   N'Third-party medical funding or factoring agreement.',                            1885, CAST(0.6200 AS DECIMAL(5,4))),
		(N'CHILD_SUPPORT_LIEN',               N'Child Support Lien',                N'LIENS_SUBROGATION',   N'Child support enforcement lien on recovery.',                                    1890, CAST(0.5600 AS DECIMAL(5,4))),
		(N'VA_TRICARE_LIEN_NOTICE',           N'VA TRICARE Lien Notice',            N'LIENS_SUBROGATION',   N'Federal VA or TRICARE recovery notice.',                                         1895, CAST(0.6400 AS DECIMAL(5,4))),
		(N'HEALTH_INSURER_SUBROGATION_NOTICE', N'Health Insurer Subrogation Notice', N'LIENS_SUBROGATION',  N'Private health insurer subrogation notice.',                                     1900, CAST(0.6600 AS DECIMAL(5,4))),
		(N'ATTORNEY_LIEN_NOTICE',             N'Attorney Lien Notice',              N'LIENS_SUBROGATION',   N'Prior counsel attorney lien notice.',                                            1905, CAST(0.5400 AS DECIMAL(5,4))),
		(N'LIEN_WAIVER',                      N'Lien Waiver',                       N'LIENS_SUBROGATION',   N'Executed waiver or release of lien.',                                            1910, CAST(0.5800 AS DECIMAL(5,4))),
		-- ── JURISDICTION_VENUE (band 1920) ───────────────────────────────────
		(N'PROOF_OF_SERVICE',                 N'Proof Of Service',                  N'JURISDICTION_VENUE',  N'Proof of service of process.',                                                   1920, CAST(0.6600 AS DECIMAL(5,4))),
		(N'CORPORATE_REGISTRATION_RECORD',    N'Corporate Registration Record',     N'JURISDICTION_VENUE',  N'Secretary of State registration and agent record.',                             1925, CAST(0.6200 AS DECIMAL(5,4))),
		(N'RESIDENCY_DOCUMENTATION',          N'Residency Documentation',           N'JURISDICTION_VENUE',  N'Evidence of party domicile and residency.',                                      1930, CAST(0.6200 AS DECIMAL(5,4))),
		(N'FORUM_SELECTION_CLAUSE',           N'Forum Selection Clause',            N'JURISDICTION_VENUE',  N'Contractual forum selection provision.',                                         1935, CAST(0.6400 AS DECIMAL(5,4))),
		(N'REMOVAL_REMAND_FILING',            N'Removal Remand Filing',             N'JURISDICTION_VENUE',  N'Notice of removal or motion to remand.',                                         1940, CAST(0.6000 AS DECIMAL(5,4))),
		(N'VENUE_TRANSFER_MOTION',            N'Venue Transfer Motion',             N'JURISDICTION_VENUE',  N'Motion to transfer or change venue.',                                            1945, CAST(0.5800 AS DECIMAL(5,4))),
		(N'MINIMUM_CONTACTS_EVIDENCE',        N'Minimum Contacts Evidence',         N'JURISDICTION_VENUE',  N'Evidence of defendant forum contacts.',                                          1950, CAST(0.6400 AS DECIMAL(5,4))),
		(N'DIVERSITY_JURISDICTION_AFFIDAVIT', N'Diversity Jurisdiction Affidavit',  N'JURISDICTION_VENUE',  N'Citizenship affidavit for diversity jurisdiction.',                             1955, CAST(0.5800 AS DECIMAL(5,4))),
		-- ── LIMITATIONS (band 1960) ──────────────────────────────────────────
		(N'ACCRUAL_DATE_RECORD',              N'Accrual Date Record',               N'LIMITATIONS',         N'Evidence establishing the claim accrual date.',                                  1960, CAST(0.8000 AS DECIMAL(5,4))),
		(N'TOLLING_AGREEMENT',                N'Tolling Agreement',                 N'LIMITATIONS',         N'Agreement tolling the limitations period.',                                      1965, CAST(0.7800 AS DECIMAL(5,4))),
		(N'DISCOVERY_RULE_EVIDENCE',          N'Discovery Rule Evidence',           N'LIMITATIONS',         N'Evidence of when the injury was or should have been discovered.',               1970, CAST(0.7600 AS DECIMAL(5,4))),
		(N'MINORITY_INCAPACITY_PROOF',        N'Minority Incapacity Proof',         N'LIMITATIONS',         N'Proof of minority or incapacity supporting tolling.',                           1975, CAST(0.7200 AS DECIMAL(5,4))),
		(N'GOVERNMENT_CLAIM_DENIAL',          N'Government Claim Denial',           N'LIMITATIONS',         N'Rejection of government claim triggering suit deadline.',                       1980, CAST(0.7800 AS DECIMAL(5,4))),
		(N'FILE_STAMPED_COMPLAINT',           N'File Stamped Complaint',            N'LIMITATIONS',         N'Court-stamped complaint establishing filing date.',                             1985, CAST(0.8000 AS DECIMAL(5,4))),
		(N'CONTINUOUS_TREATMENT_RECORD',      N'Continuous Treatment Record',       N'LIMITATIONS',         N'Records supporting continuous-treatment tolling.',                              1990, CAST(0.6600 AS DECIMAL(5,4))),
		(N'FRAUDULENT_CONCEALMENT_EVIDENCE',  N'Fraudulent Concealment Evidence',   N'LIMITATIONS',         N'Evidence of concealment supporting equitable tolling.',                         1995, CAST(0.7000 AS DECIMAL(5,4))),
		(N'BANKRUPTCY_STAY_RECORD',           N'Bankruptcy Stay Record',            N'LIMITATIONS',         N'Bankruptcy filing and automatic stay record.',                                  2000, CAST(0.6200 AS DECIMAL(5,4))),
		(N'MILITARY_SERVICE_TOLLING_RECORD',  N'Military Service Tolling Record',   N'LIMITATIONS',         N'Active-duty service record supporting SCRA tolling.',                           2005, CAST(0.6000 AS DECIMAL(5,4))),
		-- ── PARTIES (band 2010) ──────────────────────────────────────────────
		(N'BIRTH_CERTIFICATE',                N'Birth Certificate',                 N'PARTIES',             N'Birth certificate establishing minority or relationship.',                      2010, CAST(0.6200 AS DECIMAL(5,4))),
		(N'GUARDIANSHIP_ORDER',               N'Guardianship Order',                N'PARTIES',             N'Court order appointing guardian or guardian ad litem.',                         2015, CAST(0.7000 AS DECIMAL(5,4))),
		(N'LETTERS_OF_ADMINISTRATION',        N'Letters Of Administration',         N'PARTIES',             N'Letters appointing estate representative.',                                     2020, CAST(0.7200 AS DECIMAL(5,4))),
		(N'POWER_OF_ATTORNEY',                N'Power Of Attorney',                 N'PARTIES',             N'Power of attorney for an incapacitated party.',                                 2025, CAST(0.6200 AS DECIMAL(5,4))),
		(N'CORPORATE_STRUCTURE_RECORD',       N'Corporate Structure Record',        N'PARTIES',             N'Corporate ownership and affiliate structure.',                                  2030, CAST(0.6400 AS DECIMAL(5,4))),
		(N'HEIRSHIP_AFFIDAVIT',               N'Heirship Affidavit',                N'PARTIES',             N'Affidavit of heirship in wrongful death.',                                      2035, CAST(0.6800 AS DECIMAL(5,4))),
		(N'FICTITIOUS_NAME_FILING',           N'Fictitious Name Filing',            N'PARTIES',             N'DBA or fictitious business name filing.',                                       2040, CAST(0.5400 AS DECIMAL(5,4))),
		(N'MARRIAGE_CERTIFICATE',             N'Marriage Certificate',              N'PARTIES',             N'Marriage certificate supporting consortium or heirship standing.',              2045, CAST(0.6200 AS DECIMAL(5,4))),
		(N'CONSERVATORSHIP_ORDER',            N'Conservatorship Order',             N'PARTIES',             N'Court order appointing conservator.',                                           2050, CAST(0.6600 AS DECIMAL(5,4))),
		(N'SUCCESSOR_IN_INTEREST_DECLARATION', N'Successor In Interest Declaration', N'PARTIES',            N'Declaration establishing successor-in-interest status.',                        2055, CAST(0.6800 AS DECIMAL(5,4))),
		(N'DOE_DEFENDANT_AMENDMENT',          N'Doe Defendant Amendment',           N'PARTIES',             N'Amendment substituting a named defendant for a Doe.',                           2060, CAST(0.6000 AS DECIMAL(5,4))),
		-- ── PROCEDURE (band 2070) ────────────────────────────────────────────
		(N'DEPOSITION_NOTICE',                N'Deposition Notice',                 N'PROCEDURE',           N'Notice of deposition and subpoena.',                                            2070, CAST(0.5000 AS DECIMAL(5,4))),
		(N'MOTION_BRIEF',                     N'Motion Brief',                      N'PROCEDURE',           N'Motion papers and supporting brief.',                                           2075, CAST(0.6200 AS DECIMAL(5,4))),
		(N'SCHEDULING_ORDER',                 N'Scheduling Order',                  N'PROCEDURE',           N'Case scheduling and trial-setting order.',                                      2080, CAST(0.5600 AS DECIMAL(5,4))),
		(N'CASE_MANAGEMENT_STATEMENT',        N'Case Management Statement',         N'PROCEDURE',           N'Case management conference statement.',                                         2085, CAST(0.5200 AS DECIMAL(5,4))),
		(N'MEDIATION_ORDER',                  N'Mediation Order',                   N'PROCEDURE',           N'Order referring the matter to mediation or ADR.',                               2090, CAST(0.5200 AS DECIMAL(5,4))),
		(N'TRIAL_EXHIBIT_LIST',               N'Trial Exhibit List',                N'PROCEDURE',           N'Exchanged trial exhibit list.',                                                 2095, CAST(0.5800 AS DECIMAL(5,4))),
		(N'JURY_INSTRUCTIONS',                N'Jury Instructions',                 N'PROCEDURE',           N'Proposed and given jury instructions.',                                         2100, CAST(0.6400 AS DECIMAL(5,4))),
		(N'VERDICT_FORM',                     N'Verdict Form',                      N'PROCEDURE',           N'Special or general verdict form.',                                              2105, CAST(0.6600 AS DECIMAL(5,4))),
		(N'SANCTIONS_ORDER',                  N'Sanctions Order',                   N'PROCEDURE',           N'Order imposing discovery or litigation sanctions.',                             2110, CAST(0.6000 AS DECIMAL(5,4))),
		(N'MEET_AND_CONFER_LETTER',           N'Meet And Confer Letter',            N'PROCEDURE',           N'Discovery meet-and-confer correspondence.',                                     2115, CAST(0.4600 AS DECIMAL(5,4))),
		(N'PROTECTIVE_ORDER',                 N'Protective Order',                  N'PROCEDURE',           N'Stipulated or contested protective order.',                                     2120, CAST(0.5200 AS DECIMAL(5,4))),
		(N'SUMMARY_JUDGMENT_MOTION',          N'Summary Judgment Motion',           N'PROCEDURE',           N'Motion for summary judgment or adjudication.',                                  2125, CAST(0.7000 AS DECIMAL(5,4))),
		(N'MOTION_IN_LIMINE',                 N'Motion In Limine',                  N'PROCEDURE',           N'Pretrial motion to exclude evidence.',                                          2130, CAST(0.6200 AS DECIMAL(5,4))),
		(N'WITNESS_LIST',                     N'Witness List',                      N'PROCEDURE',           N'Exchanged trial witness list.',                                                 2135, CAST(0.5600 AS DECIMAL(5,4))),
		(N'TRIAL_BRIEF',                      N'Trial Brief',                       N'PROCEDURE',           N'Pretrial trial brief.',                                                         2140, CAST(0.6000 AS DECIMAL(5,4))),
		(N'DOCKET_SHEET',                     N'Docket Sheet',                      N'PROCEDURE',           N'Court docket and register of actions.',                                         2145, CAST(0.5000 AS DECIMAL(5,4))),
		-- ── SETTLEMENT umbrella (band 2150) ──────────────────────────────────
		(N'SETTLEMENT_AUTHORITY_MEMO',        N'Settlement Authority Memo',         N'SETTLEMENT',          N'Client or carrier settlement authority documentation.',                         2150, CAST(0.6400 AS DECIMAL(5,4))),
		(N'CLIENT_SETTLEMENT_INSTRUCTION',    N'Client Settlement Instruction',     N'SETTLEMENT',          N'Written client instruction to accept or reject.',                               2155, CAST(0.6600 AS DECIMAL(5,4))),
		(N'OFFER_OF_JUDGMENT',                N'Offer Of Judgment',                 N'SETTLEMENT',          N'Statutory offer of judgment (e.g., CCP 998 / Rule 68).',                        2160, CAST(0.6800 AS DECIMAL(5,4))),
		-- ── SETTLEMENT_VALUATION (band 2170) ─────────────────────────────────
		(N'VERDICT_ANALOG_REPORT',            N'Verdict Analog Report',             N'SETTLEMENT_VALUATION', N'Comparable verdict and settlement research report.',                           2170, CAST(0.7000 AS DECIMAL(5,4))),
		(N'MEDIATION_BRIEF',                  N'Mediation Brief',                   N'SETTLEMENT_VALUATION', N'Mediation or settlement conference brief.',                                    2175, CAST(0.6400 AS DECIMAL(5,4))),
		(N'VALUATION_WORKSHEET',              N'Valuation Worksheet',               N'SETTLEMENT_VALUATION', N'Internal case valuation worksheet.',                                           2180, CAST(0.6600 AS DECIMAL(5,4))),
		(N'STRUCTURED_SETTLEMENT_PROPOSAL',   N'Structured Settlement Proposal',    N'SETTLEMENT_VALUATION', N'Structured settlement proposal and schedule.',                                 2185, CAST(0.5800 AS DECIMAL(5,4))),
		(N'COLLECTABILITY_ASSET_SEARCH',      N'Collectability Asset Search',       N'SETTLEMENT_VALUATION', N'Defendant asset search and collectability analysis.',                          2190, CAST(0.6600 AS DECIMAL(5,4))),
		(N'LITIGATION_COST_PROJECTION',       N'Litigation Cost Projection',        N'SETTLEMENT_VALUATION', N'Projected cost and timing of litigation through trial.',                       2195, CAST(0.6000 AS DECIMAL(5,4))),
		(N'RISK_ADJUSTED_VALUE_MODEL',        N'Risk Adjusted Value Model',         N'SETTLEMENT_VALUATION', N'Probability-weighted case value model.',                                       2200, CAST(0.6400 AS DECIMAL(5,4))),
		(N'MEDIATOR_PROPOSAL',                N'Mediator Proposal',                 N'SETTLEMENT_VALUATION', N'Mediator settlement proposal.',                                                2205, CAST(0.6200 AS DECIMAL(5,4))),
		-- ── RESOLUTION (band 2210) ───────────────────────────────────────────
		(N'CLOSING_STATEMENT',                N'Closing Statement',                 N'RESOLUTION',          N'Client settlement closing and distribution statement.',                         2210, CAST(0.5800 AS DECIMAL(5,4))),
		(N'DISMISSAL_FILING',                 N'Dismissal Filing',                  N'RESOLUTION',          N'Request for dismissal or stipulated dismissal.',                                2215, CAST(0.6000 AS DECIMAL(5,4))),
		(N'MINOR_COMPROMISE_PETITION',        N'Minor Compromise Petition',         N'RESOLUTION',          N'Petition to approve minor or incompetent compromise.',                          2220, CAST(0.6200 AS DECIMAL(5,4))),
		(N'TRUST_ACCOUNT_LEDGER',             N'Trust Account Ledger',              N'RESOLUTION',          N'Client trust account ledger for the matter.',                                   2225, CAST(0.5000 AS DECIMAL(5,4))),
		(N'FEE_COST_STATEMENT',               N'Fee Cost Statement',                N'RESOLUTION',          N'Attorney fee and cost itemization.',                                            2230, CAST(0.5200 AS DECIMAL(5,4))),
		(N'APPEAL_NOTICE',                    N'Appeal Notice',                     N'RESOLUTION',          N'Notice of appeal or cross-appeal.',                                             2235, CAST(0.6200 AS DECIMAL(5,4))),
		(N'SETTLEMENT_CHECK_COPY',            N'Settlement Check Copy',             N'RESOLUTION',          N'Copy of settlement draft received.',                                            2240, CAST(0.4800 AS DECIMAL(5,4))),
		(N'TAX_REPORTING_FORM',               N'Tax Reporting Form',                N'RESOLUTION',          N'W-9 and 1099 settlement tax reporting forms.',                                  2245, CAST(0.4600 AS DECIMAL(5,4))),
		(N'CLIENT_DISBURSEMENT_ACKNOWLEDGMENT', N'Client Disbursement Acknowledgment', N'RESOLUTION',       N'Client acknowledgment of receipt and disbursement.',                            2250, CAST(0.4800 AS DECIMAL(5,4))),
		(N'POST_JUDGMENT_INTEREST_CALCULATION', N'Post Judgment Interest Calculation', N'RESOLUTION',       N'Calculation of post-judgment interest.',                                        2255, CAST(0.5400 AS DECIMAL(5,4))),
		(N'WRIT_OF_EXECUTION',                N'Writ Of Execution',                 N'RESOLUTION',          N'Writ to enforce an unsatisfied judgment.',                                      2260, CAST(0.5600 AS DECIMAL(5,4)))
	) AS source (EvidenceTypeCode, Name, DimensionCode, Description, SortOrder, PoloxiImportanceScore)
	ON target.DecisionDomainPackId = @PiPackId AND target.EvidenceTypeCode = source.EvidenceTypeCode AND target.TenantId IS NULL
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (DecisionDomainPackId, EvidenceTypeCode, Name, DimensionCode, Description, SortOrder, PoloxiImportanceScore, TenantId)
		VALUES (@PiPackId, source.EvidenceTypeCode, source.Name, source.DimensionCode, source.Description, source.SortOrder, source.PoloxiImportanceScore, NULL);
END

COMMIT TRANSACTION;
