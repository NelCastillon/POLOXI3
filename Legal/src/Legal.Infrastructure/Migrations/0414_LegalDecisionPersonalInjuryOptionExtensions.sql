SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal Decision Intelligence — additional Personal Injury DB-backed dropdown option lists.
-- Extends POLOXI.Legal_DecisionMatterOption (0286 pattern) with the categorical values that were
-- previously free-text in the Matter Profile Add/Edit modals so every selectable PI field is
-- database-backed (no hardcoded C#/Razor lists). Fully idempotent: keyed on (FieldCode, Value)
-- via MERGE; safe to re-run.
-- ───────────────────────────────────────────────────────────────────────────────────────────────

MERGE POLOXI.Legal_DecisionMatterOption AS target
USING (VALUES
	-- ── Deadline types ──
	(N'PI_DEADLINE_TYPE', N'Statute of Limitations',    10),
	(N'PI_DEADLINE_TYPE', N'Government Claim',           20),
	(N'PI_DEADLINE_TYPE', N'Notice of Claim',            30),
	(N'PI_DEADLINE_TYPE', N'Complaint Filing',           40),
	(N'PI_DEADLINE_TYPE', N'Service of Process',         50),
	(N'PI_DEADLINE_TYPE', N'Discovery Cutoff',           60),
	(N'PI_DEADLINE_TYPE', N'Expert Disclosure',          70),
	(N'PI_DEADLINE_TYPE', N'Motion Deadline',            80),
	(N'PI_DEADLINE_TYPE', N'Mediation',                  90),
	(N'PI_DEADLINE_TYPE', N'Trial Date',                100),
	(N'PI_DEADLINE_TYPE', N'Appeal Deadline',           110),
	(N'PI_DEADLINE_TYPE', N'Settlement Disbursement',   120),
	(N'PI_DEADLINE_TYPE', N'Other',                     130),

	-- ── Vehicle impact types ──
	(N'PI_IMPACT_TYPE', N'Rear-end',            10),
	(N'PI_IMPACT_TYPE', N'Head-on',             20),
	(N'PI_IMPACT_TYPE', N'Side-impact / T-bone',30),
	(N'PI_IMPACT_TYPE', N'Sideswipe',           40),
	(N'PI_IMPACT_TYPE', N'Rollover',            50),
	(N'PI_IMPACT_TYPE', N'Angle',               60),
	(N'PI_IMPACT_TYPE', N'Chain Reaction',      70),
	(N'PI_IMPACT_TYPE', N'Single Vehicle',      80),
	(N'PI_IMPACT_TYPE', N'Other',               90),

	-- ── Treatment provider specialties ──
	(N'PI_TREATMENT_SPECIALTY', N'Emergency Medicine',       10),
	(N'PI_TREATMENT_SPECIALTY', N'Primary Care',             20),
	(N'PI_TREATMENT_SPECIALTY', N'Orthopedics',              30),
	(N'PI_TREATMENT_SPECIALTY', N'Neurology',                40),
	(N'PI_TREATMENT_SPECIALTY', N'Chiropractic',             50),
	(N'PI_TREATMENT_SPECIALTY', N'Physical Therapy',         60),
	(N'PI_TREATMENT_SPECIALTY', N'Pain Management',          70),
	(N'PI_TREATMENT_SPECIALTY', N'Radiology / Imaging',      80),
	(N'PI_TREATMENT_SPECIALTY', N'Surgery',                  90),
	(N'PI_TREATMENT_SPECIALTY', N'Psychology / Psychiatry', 100),
	(N'PI_TREATMENT_SPECIALTY', N'Dental / Oral',           110),
	(N'PI_TREATMENT_SPECIALTY', N'Other',                   120),

	-- ── Negotiation event source (who made the offer/demand) ──
	(N'PI_NEGOTIATION_SOURCE', N'Plaintiff',           10),
	(N'PI_NEGOTIATION_SOURCE', N'Defendant',           20),
	(N'PI_NEGOTIATION_SOURCE', N'Insurer / Adjuster',  30),
	(N'PI_NEGOTIATION_SOURCE', N'Defense Counsel',     40),
	(N'PI_NEGOTIATION_SOURCE', N'Mediator',            50),
	(N'PI_NEGOTIATION_SOURCE', N'Other',               60),

	-- ── Insurance limits source (how limits were established) ──
	(N'PI_LIMITS_SOURCE', N'Declarations Page',          10),
	(N'PI_LIMITS_SOURCE', N'Adjuster Disclosure Letter', 20),
	(N'PI_LIMITS_SOURCE', N'Policy Document',            30),
	(N'PI_LIMITS_SOURCE', N'Coverage Verification Form', 40),
	(N'PI_LIMITS_SOURCE', N'Carrier Representation',     50),
	(N'PI_LIMITS_SOURCE', N'Estimated',                 60),
	(N'PI_LIMITS_SOURCE', N'Other',                     70),

	-- ── Medical record / bill request status ──
	(N'PI_REQUEST_STATUS', N'Not Requested',    10),
	(N'PI_REQUEST_STATUS', N'Requested',        20),
	(N'PI_REQUEST_STATUS', N'Follow-up Sent',   30),
	(N'PI_REQUEST_STATUS', N'Partial Received', 40),
	(N'PI_REQUEST_STATUS', N'Received',         50),
	(N'PI_REQUEST_STATUS', N'Overdue',          60),
	(N'PI_REQUEST_STATUS', N'Not Applicable',   70)
) AS source (FieldCode, Value, SortOrder)
ON target.FieldCode = source.FieldCode AND target.Value = source.Value
WHEN NOT MATCHED BY TARGET THEN
	INSERT (FieldCode, Value, DisplayName, SortOrder)
	VALUES (source.FieldCode, source.Value, source.Value, source.SortOrder);

GO

COMMIT TRANSACTION;
