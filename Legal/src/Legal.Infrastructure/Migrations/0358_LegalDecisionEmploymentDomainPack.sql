SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — EMPLOYMENT Domain Pack (companion to the PERSONAL_INJURY pack seeded in 0284/0289).
--
-- A Domain Pack supplies DOMAIN SEMANTICS only: terminology, decision-hierarchy dimensions, evidence
-- classifications, verification profiles, matter-type taxonomy, concepts, and concept relations. It does
-- NOT own ambiguity, hierarchy governance, Candidate × Branch competition, frontier, information value,
-- adaptive narrowing, recompetition, flip points, convergence, or readiness — those remain in POLOXI Core.
-- Domain concepts are advisory (never hard-coded conclusions).
--
-- This migration is data-only and idempotent by PackCode. The Domain Pack tables themselves are created
-- by 0284 (pack/dimension/evidence-type/verification-profile/matter-type) and 0289 (concept/relation),
-- both of which must have run first. Global (TenantId NULL) rows are seeded defaults; tenant rows override.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainPack', N'U') IS NULL
	THROW 50010, 'Legal_DecisionDomainPack must exist before migration 0358 (run 0284 first).', 1;
IF OBJECT_ID(N'POLOXI.Legal_DecisionDomainConcept', N'U') IS NULL
	THROW 50011, 'Legal_DecisionDomainConcept must exist before migration 0358 (run 0289 first).', 1;

GO

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Seed the Employment Domain Pack (global default, TenantId NULL). Idempotent by PackCode.
-- ────────────────────────────────────────────────────────────────────────────────────────────────
DECLARE @EmpPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
	 WHERE PackCode = N'EMPLOYMENT' AND TenantId IS NULL AND IsDeleted = 0);

IF @EmpPackId IS NULL
BEGIN
	SET @EmpPackId = NEWID();
	INSERT INTO POLOXI.Legal_DecisionDomainPack (DecisionDomainPackId, PackCode, PracticeAreaCode, Name, Description, IsDefault, IsActive, SortOrder, TenantId)
	VALUES (@EmpPackId, N'EMPLOYMENT', N'EMPLOYMENT', N'Employment',
		N'Domain semantics for Employment matters: employment relationship, protected activity, adverse action, causation/pretext, damages, evidence, employer defenses, wage & hour, restrictive covenants, and procedure. Advisory only — POLOXI Core owns all decision reasoning.',
		0, 1, 20, NULL);
END

-- ── Decision-hierarchy dimensions ──
MERGE POLOXI.Legal_DecisionDomainPackDimension AS target
USING (VALUES
	(N'RELATIONSHIP',   N'Employment Relationship', N'Existence, classification (employee vs contractor), and terms of the relationship.', 10),
	(N'PROTECTED',      N'Protected Activity',      N'Protected status or conduct (discrimination class, whistleblowing, leave, complaint).', 20),
	(N'ADVERSE_ACTION', N'Adverse Action',          N'Termination, demotion, discipline, or other materially adverse employment action.', 30),
	(N'CAUSATION',      N'Causation / Pretext',      N'Link between protected activity/status and the adverse action; pretext for legitimate reason.', 40),
	(N'DAMAGES',        N'Damages',                 N'Back pay, front pay, lost benefits, emotional distress, and statutory/liquidated damages.', 50),
	(N'EVIDENCE',       N'Evidence',                N'Personnel file, performance records, communications, comparators, witnesses, policies.', 60),
	(N'DEFENSES',       N'Employer Defenses',       N'Legitimate non-discriminatory reason, after-acquired evidence, failure to mitigate, at-will.', 70),
	(N'WAGE_HOUR',      N'Wage & Hour',             N'Minimum wage, overtime, misclassification, meal/rest, and unpaid-wage exposure.', 80),
	(N'COVENANTS',      N'Restrictive Covenants',    N'Non-compete, non-solicit, confidentiality, and trade-secret obligations/enforceability.', 90),
	(N'PROCEDURE',      N'Procedure',               N'Administrative exhaustion, limitations, arbitration, and trial posture.', 100)
) AS source (DimensionCode, Name, Description, SortOrder)
ON target.DecisionDomainPackId = @EmpPackId AND target.DimensionCode = source.DimensionCode AND target.TenantId IS NULL
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, DimensionCode, Name, Description, SortOrder, TenantId)
	VALUES (@EmpPackId, source.DimensionCode, source.Name, source.Description, source.SortOrder, NULL);

-- ── Evidence types ──
MERGE POLOXI.Legal_DecisionDomainPackEvidenceType AS target
USING (VALUES
	(N'PERSONNEL_FILE',      N'Personnel File',              N'RELATIONSHIP',   N'Official personnel and employment file.',                     10),
	(N'PERFORMANCE_REVIEW',  N'Performance Reviews',         N'DEFENSES',       N'Performance evaluations, PIPs, and disciplinary records.',     20),
	(N'EMPLOYMENT_CONTRACT', N'Employment Agreement',        N'RELATIONSHIP',   N'Offer letters, employment and arbitration agreements.',        30),
	(N'RESTRICTIVE_COVENANT',N'Restrictive Covenant',        N'COVENANTS',      N'Non-compete, non-solicit, and confidentiality agreements.',    40),
	(N'COMMUNICATIONS',      N'Emails / Messages',           N'CAUSATION',      N'Emails, chat, and internal communications.',                  50),
	(N'HR_COMPLAINT',        N'HR Complaint / Investigation', N'PROTECTED',      N'Internal complaints and investigation records.',              60),
	(N'COMPARATOR_DATA',     N'Comparator Evidence',         N'CAUSATION',      N'Treatment of similarly situated employees.',                  70),
	(N'WITNESS_STATEMENTS',  N'Witness Statements',          N'CAUSATION',      N'Coworker and manager accounts.',                              80),
	(N'PAY_TIME_RECORDS',    N'Pay / Time Records',          N'WAGE_HOUR',      N'Payroll, timekeeping, and classification records.',           90),
	(N'AGENCY_CHARGE',       N'Agency Charge / Right to Sue', N'PROCEDURE',      N'EEOC/state agency charge and right-to-sue documentation.',    100),
	(N'POLICY_HANDBOOK',     N'Policies / Handbook',         N'DEFENSES',       N'Employee handbook and applicable company policies.',           110),
	(N'SEPARATION_DOC',      N'Separation Documentation',    N'ADVERSE_ACTION', N'Termination notices, severance, and separation agreements.',  120)
) AS source (EvidenceTypeCode, Name, DimensionCode, Description, SortOrder)
ON target.DecisionDomainPackId = @EmpPackId AND target.EvidenceTypeCode = source.EvidenceTypeCode AND target.TenantId IS NULL
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, EvidenceTypeCode, Name, DimensionCode, Description, SortOrder, TenantId)
	VALUES (@EmpPackId, source.EvidenceTypeCode, source.Name, source.DimensionCode, source.Description, source.SortOrder, NULL);

-- ── Verification profiles ──
MERGE POLOXI.Legal_DecisionDomainPackVerificationProfile AS target
USING (VALUES
	(N'ADVERSE_ACTION_FACTS', N'Adverse Action Verification',   N'SEPARATION_DOC',      N'Verify the adverse action, its timing, and stated basis against separation and HR records.', 10),
	(N'PROTECTED_ACTIVITY',   N'Protected Activity Verification', N'HR_COMPLAINT',       N'Verify protected status or conduct and employer knowledge against complaints and communications.', 20),
	(N'CAUSATION_PRETEXT',    N'Causation / Pretext Verification', N'COMPARATOR_DATA',    N'Verify causal linkage and pretext against comparators, communications, and performance records.', 30),
	(N'DAMAGES_QUANTUM',      N'Damages Quantum Verification',   N'PAY_TIME_RECORDS',    N'Verify back/front pay and lost benefits against pay and time records.', 40),
	(N'COVENANT_ENFORCEABILITY', N'Covenant Enforceability Verification', N'RESTRICTIVE_COVENANT', N'Verify scope, consideration, and governing-law enforceability of restrictive covenants.', 50)
) AS source (ProfileCode, Name, EvidenceTypeCode, Description, SortOrder)
ON target.DecisionDomainPackId = @EmpPackId AND target.ProfileCode = source.ProfileCode AND target.TenantId IS NULL
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, ProfileCode, Name, EvidenceTypeCode, Description, SortOrder, TenantId)
	VALUES (@EmpPackId, source.ProfileCode, source.Name, source.EvidenceTypeCode, source.Description, source.SortOrder, NULL);

-- ── Matter-type taxonomy ──
MERGE POLOXI.Legal_DecisionDomainPackMatterType AS target
USING (VALUES
	(N'Wrongful Termination',        N'Wrongful Termination',        N'Termination in violation of law or public policy.',      10),
	(N'Discrimination',              N'Discrimination',              N'Adverse action based on a protected class.',             20),
	(N'Retaliation',                 N'Retaliation',                 N'Adverse action for protected activity.',                 30),
	(N'Harassment / Hostile Work Environment', N'Harassment / Hostile Work Environment', N'Severe or pervasive harassing conduct.', 40),
	(N'Wage & Hour Dispute',         N'Wage & Hour Dispute',         N'Unpaid wages, overtime, or misclassification.',          50),
	(N'Non-Compete Enforcement',     N'Non-Compete Enforcement',     N'Enforcement or challenge of restrictive covenants.',     60),
	(N'Trade Secret Dispute',        N'Trade Secret Dispute',        N'Misappropriation of confidential information.',          70),
	(N'Leave / Accommodation',       N'Leave / Accommodation',       N'FMLA/ADA leave and reasonable-accommodation disputes.',  80),
	(N'Whistleblower',               N'Whistleblower',               N'Retaliation for reporting unlawful conduct.',            90),
	(N'Other Employment',            N'Other Employment',            N'Employment matter not otherwise classified.',            100)
) AS source (MatterTypeCode, Name, Description, SortOrder)
ON target.DecisionDomainPackId = @EmpPackId AND target.MatterTypeCode = source.MatterTypeCode AND target.TenantId IS NULL
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, MatterTypeCode, Name, Description, SortOrder, TenantId)
	VALUES (@EmpPackId, source.MatterTypeCode, source.Name, source.Description, source.SortOrder, NULL);

GO

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Concepts + concept relations (semantic guardrails). Advisory only. Idempotent by ConceptCode / edge.
-- ────────────────────────────────────────────────────────────────────────────────────────────────
DECLARE @EmpPackId UNIQUEIDENTIFIER =
(
	SELECT TOP 1 DecisionDomainPackId
	FROM POLOXI.Legal_DecisionDomainPack
	WHERE PackCode = N'EMPLOYMENT' AND TenantId IS NULL AND IsDeleted = 0
	ORDER BY IsDefault DESC, SortOrder
);

IF @EmpPackId IS NULL
	THROW 50012, 'EMPLOYMENT domain pack must exist before seeding its concepts in migration 0358.', 1;

MERGE POLOXI.Legal_DecisionDomainConcept AS target
USING (VALUES
	(N'EMPLOYMENT_RELATIONSHIP', N'RELATIONSHIP', N'Employment relationship', N'Whether an employment relationship (vs independent contractor) existed and its governing terms.', N'MIXED', N'MATTER_DOCUMENT', N'ADVERSE_ACTION_FACTS', NULL, NULL, 1, 1, 10),
	(N'PROTECTED_ACTIVITY', N'PROTECTED', N'Protected status or activity', N'Whether the employee engaged in protected activity or held protected status of which the employer was aware.', N'MIXED', N'MATTER_DOCUMENT', N'PROTECTED_ACTIVITY', NULL, NULL, 1, 1, 20),
	(N'ADVERSE_ACTION', N'ADVERSE_ACTION', N'Materially adverse action', N'Whether the employer took a materially adverse employment action.', N'MIXED', N'MATTER_DOCUMENT', N'ADVERSE_ACTION_FACTS', NULL, NULL, 1, 1, 30),
	(N'CAUSAL_LINK', N'CAUSATION', N'Causation / pretext', N'Whether the protected activity or status was a legally sufficient cause of the adverse action and whether the stated reason is pretextual.', N'MIXED', N'MATTER_DOCUMENT', N'CAUSATION_PRETEXT', NULL, NULL, 1, 1, 40),
	(N'CAUSATION_STANDARD', N'CAUSATION', N'Governing causation standard', N'The governing causation standard (e.g., motivating-factor vs but-for) for the asserted claim and jurisdiction.', N'LEGAL_RULE', N'LEGAL_AUTHORITY', N'CAUSATION_PRETEXT', NULL, NULL, 0, 1, 50),
	(N'COMPARATOR_EVIDENCE', N'EVIDENCE', N'Comparator evidence', N'Matter evidence comparing treatment of similarly situated employees outside the protected class/activity.', N'MATTER_EVIDENCE', N'MATTER_DOCUMENT', N'CAUSATION_PRETEXT', NULL, NULL, 0, 1, 60),
	(N'LEGITIMATE_REASON', N'DEFENSES', N'Legitimate non-discriminatory reason', N'Whether the employer has articulated a legitimate, non-discriminatory reason for the action.', N'MIXED', N'MATTER_DOCUMENT', N'ADVERSE_ACTION_FACTS', NULL, NULL, 0, 1, 70),
	(N'DAMAGES_QUANTUM', N'DAMAGES', N'Damages quantum', N'The existence, amount, and recoverability of back pay, front pay, lost benefits, and statutory damages.', N'MIXED', N'MATTER_DOCUMENT', N'DAMAGES_QUANTUM', NULL, NULL, 1, 1, 80),
	(N'MITIGATION', N'DEFENSES', N'Failure to mitigate', N'Whether the employee reasonably mitigated damages through comparable employment efforts.', N'MIXED', N'MATTER_DOCUMENT', N'DAMAGES_QUANTUM', NULL, NULL, 0, 1, 90),
	(N'WAGE_HOUR_COMPLIANCE', N'WAGE_HOUR', N'Wage & hour compliance', N'Whether minimum-wage, overtime, classification, and meal/rest obligations were satisfied.', N'MIXED', N'MATTER_DOCUMENT', N'DAMAGES_QUANTUM', NULL, NULL, 0, 1, 100),
	(N'COVENANT_ENFORCEABILITY', N'COVENANTS', N'Restrictive-covenant enforceability', N'Whether a restrictive covenant is enforceable given scope, consideration, and governing law.', N'MIXED', N'LEGAL_AUTHORITY', N'COVENANT_ENFORCEABILITY', NULL, NULL, 0, 1, 110),
	(N'EXHAUSTION_TIMELINESS', N'PROCEDURE', N'Administrative exhaustion and timeliness', N'Whether required agency exhaustion and limitations/filing deadlines were satisfied.', N'PROCEDURAL_STANDARD', N'LEGAL_AUTHORITY', NULL, NULL, NULL, 1, 1, 120),
	(N'BURDEN_STANDARD', N'PROCEDURE', N'Burden and decision standard', N'The governing burden, standard, and record sufficiency for the requested decision.', N'PROCEDURAL_STANDARD', N'LEGAL_AUTHORITY', NULL, NULL, NULL, 1, 1, 130)
) AS source (ConceptCode, DimensionCode, Name, Description, ConceptKindCode, SourceClassCode, VerificationProfileCode, JurisdictionCode, MatterTypeCode, IsRequiredCoverage, IsFallbackEligible, SortOrder)
ON target.DecisionDomainPackId = @EmpPackId AND target.ConceptCode = source.ConceptCode AND target.TenantId IS NULL
WHEN MATCHED THEN UPDATE SET
	DimensionCode = source.DimensionCode, Name = source.Name, Description = source.Description,
	ConceptKindCode = source.ConceptKindCode, SourceClassCode = source.SourceClassCode,
	VerificationProfileCode = source.VerificationProfileCode, JurisdictionCode = source.JurisdictionCode,
	MatterTypeCode = source.MatterTypeCode, IsRequiredCoverage = source.IsRequiredCoverage,
	IsFallbackEligible = source.IsFallbackEligible, SortOrder = source.SortOrder, IsActive = 1,
	IsDeleted = 0,
	ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, ConceptCode, DimensionCode, Name, Description, ConceptKindCode, SourceClassCode, VerificationProfileCode, JurisdictionCode, MatterTypeCode, IsRequiredCoverage, IsFallbackEligible, SortOrder, TenantId)
	VALUES (@EmpPackId, source.ConceptCode, source.DimensionCode, source.Name, source.Description, source.ConceptKindCode, source.SourceClassCode, source.VerificationProfileCode, source.JurisdictionCode, source.MatterTypeCode, source.IsRequiredCoverage, source.IsFallbackEligible, source.SortOrder, NULL);

MERGE POLOXI.Legal_DecisionDomainConceptRelation AS target
USING
(
	SELECT sourceConcept.DecisionDomainConceptId AS SourceDecisionDomainConceptId,
		targetConcept.DecisionDomainConceptId AS TargetDecisionDomainConceptId, relation.*
	FROM (VALUES
	(N'CAUSAL_LINK', N'CAUSATION_STANDARD', N'REQUIRES', N'CAUSATION_STANDARD_REQUIRED', N'A causation finding must identify the governing causation standard for the claim and jurisdiction.', NULL, NULL, 1, 10),
	(N'CAUSAL_LINK', N'COMPARATOR_EVIDENCE', N'REQUIRES', N'PRETEXT_EVIDENCE_REQUIRED', N'A pretext/causation inference requires matter evidence such as comparators or communications.', NULL, NULL, 1, 20),
	(N'ADVERSE_ACTION', N'PROTECTED_ACTIVITY', N'DISTINCT_FROM', N'ACTION_ACTIVITY_SEPARATION', N'The adverse action and the protected activity/status must remain semantically distinct facts.', NULL, NULL, 0, 30),
	(N'CAUSAL_LINK', N'LEGITIMATE_REASON', N'MAY_ACTIVATE', N'PRETEXT_BURDEN_SHIFT', N'A legitimate stated reason may activate a pretext analysis rather than direct causation.', NULL, NULL, 0, 40),
	(N'DAMAGES_QUANTUM', N'MITIGATION', N'REQUIRES', N'MITIGATION_CONSIDERED', N'Damages quantum must account for the employee''s mitigation efforts.', NULL, NULL, 0, 50),
	(N'COVENANT_ENFORCEABILITY', N'EMPLOYMENT_RELATIONSHIP', N'REQUIRES', N'RELATIONSHIP_REQUIRED', N'Covenant enforceability depends on the underlying employment relationship and consideration.', NULL, NULL, 1, 60),
	(N'EXHAUSTION_TIMELINESS', N'BURDEN_STANDARD', N'REQUIRES', N'PROCEDURAL_STANDARD_REQUIRED', N'A procedural disposition must identify the governing burden and decision standard.', NULL, NULL, 1, 70)
	) AS relation (SourceConceptCode, TargetConceptCode, RelationTypeCode, ConstraintCode, Description, JurisdictionCode, MatterTypeCode, IsHardConstraint, SortOrder)
	INNER JOIN POLOXI.Legal_DecisionDomainConcept sourceConcept
		ON sourceConcept.DecisionDomainPackId = @EmpPackId AND sourceConcept.ConceptCode = relation.SourceConceptCode
		AND sourceConcept.TenantId IS NULL AND sourceConcept.IsDeleted = 0
	INNER JOIN POLOXI.Legal_DecisionDomainConcept targetConcept
		ON targetConcept.DecisionDomainPackId = @EmpPackId AND targetConcept.ConceptCode = relation.TargetConceptCode
		AND targetConcept.TenantId IS NULL AND targetConcept.IsDeleted = 0
) AS source
ON target.DecisionDomainPackId = @EmpPackId
	AND target.SourceConceptCode = source.SourceConceptCode
	AND target.TargetConceptCode = source.TargetConceptCode
	AND target.RelationTypeCode = source.RelationTypeCode
	AND target.TenantId IS NULL
WHEN MATCHED THEN UPDATE SET
	SourceDecisionDomainConceptId = source.SourceDecisionDomainConceptId,
	TargetDecisionDomainConceptId = source.TargetDecisionDomainConceptId,
	ConstraintCode = source.ConstraintCode, Description = source.Description,
	JurisdictionCode = source.JurisdictionCode, MatterTypeCode = source.MatterTypeCode,
	IsHardConstraint = source.IsHardConstraint, SortOrder = source.SortOrder, IsActive = 1, IsDeleted = 0,
	ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, SourceDecisionDomainConceptId, TargetDecisionDomainConceptId, SourceConceptCode, TargetConceptCode, RelationTypeCode, ConstraintCode, Description, JurisdictionCode, MatterTypeCode, IsHardConstraint, SortOrder, TenantId)
	VALUES (@EmpPackId, source.SourceDecisionDomainConceptId, source.TargetDecisionDomainConceptId, source.SourceConceptCode, source.TargetConceptCode, source.RelationTypeCode, source.ConstraintCode, source.Description, source.JurisdictionCode, source.MatterTypeCode, source.IsHardConstraint, source.SortOrder, NULL);

COMMIT TRANSACTION;
