-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- 0339: Validated Candidate Competition — DB-backed configuration (Table stage).
--
-- Milestone "Validated Candidate Competition" requires that every value validated BEFORE it can
-- influence candidate evaluation. This migration seeds the deterministic, database-backed configuration
-- the application layer reads to (a) classify a raw matter-field label and a proposition/factor label
-- into a semantic KIND, (b) decide whether a supplied source value is ADMISSIBLE to a proposition, and
-- (c) grade evidence admission on a fixed state ladder. No mock/hard-coded data lives in C#; the DB is
-- the source of truth. The optional LLM semantic-match confirmation is seeded DISABLED so the default
-- run stays deterministic, stateless, and adds no new paid model call.
--
-- Root cause it fixes (Aisha Patel run):
--   SettlementStatus = Disbursed was bound (by lexical token overlap) to unrelated propositions such as
--   "Liability established", "Confidentiality enforceable", "Damages documented", "Discovery sufficient";
--   DemandStatus = Responded was treated as demand acceptance/rejection. These are CATEGORY mismatches:
--   a payment/settlement-status field never establishes liability, damages, discovery, or confidentiality.
--   The admissibility rules below DENY exactly those field-kind → proposition-kind pairings so the value
--   is preserved as SUPPLIED with a verification obligation rather than promoted to an established fact.
--
-- Three GLOBAL (TenantId NULL) config tables, all carrying the standard base/audit fields, all seeded
-- idempotently via MERGE so re-running is a no-op:
--   • POLOXI.Legal_FactBindingKind          — keyword patterns that classify a label into a KIND.
--   • POLOXI.Legal_FactBindingRule          — ALLOW/DENY admissibility per (FieldKind, PropositionKind).
--   • POLOXI.Legal_EvidenceAdmissionState   — the graded evidence-admission state ladder.
-- Plus one disabled Legal_DecisionSetting flag gating the optional LLM confirmation.
-- ───────────────────────────────────────────────────────────────────────────────────────────────
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Kind classification ─────────────────────────────────────────────────────────────────────────
-- Each row maps a keyword pattern to a semantic KIND within a SCOPE (FIELD = a matter-data source field,
-- PROPOSITION = a factor/proposition being evaluated). Matching is deterministic significant-token
-- containment performed in the application layer; MatchPriority breaks ties (higher wins) so specific
-- patterns beat generic ones. UNKNOWN is the implicit fallback when nothing matches.
IF OBJECT_ID(N'POLOXI.Legal_FactBindingKind',N'U') IS NULL
CREATE TABLE POLOXI.Legal_FactBindingKind
(
	FactBindingKindId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_FactBindingKind PRIMARY KEY DEFAULT NEWID(),
	Scope               NVARCHAR(20) NOT NULL,           -- FIELD | PROPOSITION
	KindCode            NVARCHAR(60) NOT NULL,           -- e.g. SETTLEMENT_STATUS | LIABILITY | DAMAGES
	KeywordPattern      NVARCHAR(400) NOT NULL,          -- space/comma-separated significant terms
	MatchPriority       INT NOT NULL CONSTRAINT DF_Legal_FactBindingKind_Priority DEFAULT 100,
	IsActive            BIT NOT NULL CONSTRAINT DF_Legal_FactBindingKind_IsActive DEFAULT 1,
	Notes               NVARCHAR(1000) NULL,
	TenantId            UNIQUEIDENTIFIER NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Legal_FactBindingKind_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Legal_FactBindingKind_IsDeleted DEFAULT 0,
	CONSTRAINT UQ_Legal_FactBindingKind_Scope_Kind_Pattern UNIQUE (Scope, KindCode, KeywordPattern)
);

IF OBJECT_ID(N'IX_Legal_FactBindingKind_Scope',N'IX') IS NULL
	CREATE INDEX IX_Legal_FactBindingKind_Scope ON POLOXI.Legal_FactBindingKind (Scope, MatchPriority DESC) WHERE IsDeleted = 0;

GO

-- ── Admissibility rules ─────────────────────────────────────────────────────────────────────────
-- ALLOW/DENY whether a source value of FieldKind may establish a proposition of PropositionKind. DENY
-- rows are the corrective heart of this milestone. A binding with no explicit rule defaults (application
-- side) to REQUIRE-VERIFICATION (never silently promoted), so the table only needs to encode the
-- affirmatively-ALLOWED and affirmatively-DENIED pairings.
IF OBJECT_ID(N'POLOXI.Legal_FactBindingRule',N'U') IS NULL
CREATE TABLE POLOXI.Legal_FactBindingRule
(
	FactBindingRuleId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_FactBindingRule PRIMARY KEY DEFAULT NEWID(),
	FieldKindCode       NVARCHAR(60) NOT NULL,
	PropositionKindCode NVARCHAR(60) NOT NULL,
	Admissibility       NVARCHAR(20) NOT NULL,           -- ALLOW | DENY
	Rationale           NVARCHAR(1000) NULL,
	IsActive            BIT NOT NULL CONSTRAINT DF_Legal_FactBindingRule_IsActive DEFAULT 1,
	TenantId            UNIQUEIDENTIFIER NULL,
	CreatedDateUtc      DATETIME2 NOT NULL CONSTRAINT DF_Legal_FactBindingRule_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId     UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc     DATETIME2 NULL,
	ModifiedByUserId    UNIQUEIDENTIFIER NULL,
	IsDeleted           BIT NOT NULL CONSTRAINT DF_Legal_FactBindingRule_IsDeleted DEFAULT 0,
	CONSTRAINT UQ_Legal_FactBindingRule_Field_Prop UNIQUE (FieldKindCode, PropositionKindCode)
);

IF OBJECT_ID(N'IX_Legal_FactBindingRule_Field',N'IX') IS NULL
	CREATE INDEX IX_Legal_FactBindingRule_Field ON POLOXI.Legal_FactBindingRule (FieldKindCode, PropositionKindCode) WHERE IsDeleted = 0;

GO

-- ── Evidence-admission state ladder ─────────────────────────────────────────────────────────────
-- The fixed, ordered vocabulary that grades how far a proposition's support has progressed. Only states
-- flagged CountsTowardEvidenceScore may influence the evidence-backed component of candidate evaluation;
-- IsTerminalNegative marks refutation. StateRank orders the ladder (low → high confidence).
IF OBJECT_ID(N'POLOXI.Legal_EvidenceAdmissionState',N'U') IS NULL
CREATE TABLE POLOXI.Legal_EvidenceAdmissionState
(
	EvidenceAdmissionStateId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_EvidenceAdmissionState PRIMARY KEY DEFAULT NEWID(),
	StateCode                 NVARCHAR(40) NOT NULL,     -- SUPPLIED | SOURCE_AVAILABLE | EXTRACTED | SUPPORTED | VERIFIED | CONTRADICTED | UNRESOLVED
	DisplayName               NVARCHAR(120) NOT NULL,
	StateRank                 INT NOT NULL,
	CountsTowardEvidenceScore BIT NOT NULL CONSTRAINT DF_Legal_EvidenceAdmissionState_Counts DEFAULT 0,
	IsTerminalNegative        BIT NOT NULL CONSTRAINT DF_Legal_EvidenceAdmissionState_Neg DEFAULT 0,
	Description               NVARCHAR(1000) NULL,
	IsActive                  BIT NOT NULL CONSTRAINT DF_Legal_EvidenceAdmissionState_IsActive DEFAULT 1,
	TenantId                  UNIQUEIDENTIFIER NULL,
	CreatedDateUtc            DATETIME2 NOT NULL CONSTRAINT DF_Legal_EvidenceAdmissionState_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId           UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc           DATETIME2 NULL,
	ModifiedByUserId          UNIQUEIDENTIFIER NULL,
	IsDeleted                 BIT NOT NULL CONSTRAINT DF_Legal_EvidenceAdmissionState_IsDeleted DEFAULT 0,
	CONSTRAINT UQ_Legal_EvidenceAdmissionState_Code UNIQUE (StateCode)
);

GO

-- ── Seed: FIELD kinds (matter-data source fields observed in the PI/decision matter model) ────────
MERGE POLOXI.Legal_FactBindingKind AS target
USING (VALUES
	(N'FIELD', N'SETTLEMENT_STATUS', N'settlement status disbursed disbursement settled payout paid',        200),
	(N'FIELD', N'DEMAND_STATUS',     N'demand policy-limits policy limits demand status responded response',  200),
	(N'FIELD', N'PAYMENT_STATUS',    N'payment paid disbursed remittance funds transferred',                  190),
	(N'FIELD', N'LIABILITY_FINDING', N'liability fault negligence adjudicated found admitted',                190),
	(N'FIELD', N'DAMAGES_RECORD',    N'damages medical special economic noneconomic wage loss',               180),
	(N'FIELD', N'DISCOVERY_STATUS',  N'discovery deposition interrogatory disclosure production',             180),
	(N'FIELD', N'CONFIDENTIALITY',   N'confidentiality confidential nondisclosure nda sealed',                180),
	(N'FIELD', N'COVERAGE_STATUS',   N'coverage policy insurer carrier limits endorsement',                   170),
	(N'FIELD', N'DECEDENT_IDENTITY', N'decedent deceased death wrongful survivor heir',                       170)
) AS source (Scope, KindCode, KeywordPattern, MatchPriority)
ON  target.Scope = source.Scope
AND target.KindCode = source.KindCode
AND target.KeywordPattern = source.KeywordPattern
WHEN NOT MATCHED BY TARGET THEN
	INSERT (Scope, KindCode, KeywordPattern, MatchPriority)
	VALUES (source.Scope, source.KindCode, source.KeywordPattern, source.MatchPriority);

-- ── Seed: PROPOSITION kinds (factor/proposition semantics being evaluated) ────────────────────────
MERGE POLOXI.Legal_FactBindingKind AS target
USING (VALUES
	(N'PROPOSITION', N'LIABILITY',        N'liability established fault negligence duty breach causation',   200),
	(N'PROPOSITION', N'DAMAGES',          N'damages documented quantified proven medical economic',          200),
	(N'PROPOSITION', N'DISCOVERY',        N'discovery sufficient complete adequate conducted',               200),
	(N'PROPOSITION', N'CONFIDENTIALITY',  N'confidentiality enforceable agreement provision nondisclosure',  200),
	(N'PROPOSITION', N'SETTLEMENT_TERMS', N'settlement terms accepted executed agreement release',           200),
	(N'PROPOSITION', N'DEMAND_ACCEPTED',  N'demand accepted acceptance agreed',                              200),
	(N'PROPOSITION', N'DEMAND_REJECTED',  N'demand rejected declined refused',                               200),
	(N'PROPOSITION', N'TRIAL_READINESS',  N'trial ready readiness prepared',                                 190),
	(N'PROPOSITION', N'COVERAGE',         N'coverage available applies policy limits insurer',               190),
	(N'PROPOSITION', N'CLAIM_VALIDITY',   N'claim valid viable meritorious cognizable',                      190),
	(N'PROPOSITION', N'DECEDENT_DEATH',   N'death decedent deceased wrongful died',                          190)
) AS source (Scope, KindCode, KeywordPattern, MatchPriority)
ON  target.Scope = source.Scope
AND target.KindCode = source.KindCode
AND target.KeywordPattern = source.KeywordPattern
WHEN NOT MATCHED BY TARGET THEN
	INSERT (Scope, KindCode, KeywordPattern, MatchPriority)
	VALUES (source.Scope, source.KindCode, source.KeywordPattern, source.MatchPriority);

GO

-- ── Seed: admissibility rules ─────────────────────────────────────────────────────────────────────
-- DENY rows encode the exact category mismatches from the Aisha Patel factor inventory. ALLOW rows keep
-- legitimate same-kind bindings (e.g. a settlement-status field DOES establish operative settlement
-- status, and a payment field DOES establish disbursement) so the guardrail never over-rejects.
MERGE POLOXI.Legal_FactBindingRule AS target
USING (VALUES
	-- Settlement/payment status must NOT establish substantive merits propositions.
	(N'SETTLEMENT_STATUS', N'LIABILITY',       N'DENY',  N'A settlement/payment status does not establish liability.'),
	(N'SETTLEMENT_STATUS', N'DAMAGES',         N'DENY',  N'A settlement/payment status does not document or quantify damages.'),
	(N'SETTLEMENT_STATUS', N'DISCOVERY',       N'DENY',  N'A settlement/payment status does not establish discovery sufficiency.'),
	(N'SETTLEMENT_STATUS', N'CONFIDENTIALITY', N'DENY',  N'A settlement/payment status does not establish an enforceable confidentiality provision.'),
	(N'SETTLEMENT_STATUS', N'SETTLEMENT_TERMS',N'DENY',  N'A disbursement status does not establish specific settlement terms/partial-settlement terms.'),
	(N'PAYMENT_STATUS',    N'LIABILITY',       N'DENY',  N'Payment status does not establish liability.'),
	(N'PAYMENT_STATUS',    N'DAMAGES',         N'DENY',  N'Payment status does not document damages.'),
	-- Demand status must NOT establish acceptance/rejection/trial readiness.
	(N'DEMAND_STATUS',     N'DEMAND_ACCEPTED', N'DENY',  N'A demand response does not establish demand acceptance.'),
	(N'DEMAND_STATUS',     N'DEMAND_REJECTED', N'DENY',  N'A demand response does not establish demand rejection.'),
	(N'DEMAND_STATUS',     N'TRIAL_READINESS', N'DENY',  N'A demand response does not establish trial readiness.'),
	-- Legitimate same-kind bindings remain ALLOWED.
	(N'LIABILITY_FINDING', N'LIABILITY',       N'ALLOW', N'An adjudicated/admitted liability finding may establish liability.'),
	(N'DAMAGES_RECORD',    N'DAMAGES',         N'ALLOW', N'A damages record may document damages.'),
	(N'DISCOVERY_STATUS',  N'DISCOVERY',       N'ALLOW', N'A discovery status may establish discovery progress.'),
	(N'CONFIDENTIALITY',   N'CONFIDENTIALITY', N'ALLOW', N'A confidentiality/NDA field may bear on confidentiality enforceability.'),
	(N'COVERAGE_STATUS',   N'COVERAGE',        N'ALLOW', N'A coverage status may bear on coverage availability.'),
	(N'DECEDENT_IDENTITY', N'DECEDENT_DEATH',  N'ALLOW', N'A decedent-identity field may bear on the death element.')
) AS source (FieldKindCode, PropositionKindCode, Admissibility, Rationale)
ON  target.FieldKindCode = source.FieldKindCode
AND target.PropositionKindCode = source.PropositionKindCode
WHEN NOT MATCHED BY TARGET THEN
	INSERT (FieldKindCode, PropositionKindCode, Admissibility, Rationale)
	VALUES (source.FieldKindCode, source.PropositionKindCode, source.Admissibility, source.Rationale);

GO

-- ── Seed: evidence-admission state ladder ─────────────────────────────────────────────────────────
MERGE POLOXI.Legal_EvidenceAdmissionState AS target
USING (VALUES
	(N'SUPPLIED',         N'Supplied',          10, 0, 0, N'A value was supplied (matter data / user), not yet corroborated by a source.'),
	(N'SOURCE_AVAILABLE', N'Source available',  20, 0, 0, N'A source that could support the proposition is reachable, but not yet extracted.'),
	(N'EXTRACTED',        N'Extracted',         30, 0, 0, N'A candidate passage/value was extracted from a source, pending support evaluation.'),
	(N'SUPPORTED',        N'Supported',         40, 1, 0, N'Extracted material affirmatively supports the proposition (counts toward evidence score).'),
	(N'VERIFIED',         N'Verified',          50, 1, 0, N'Independently verified against an authoritative source (counts toward evidence score).'),
	(N'CONTRADICTED',     N'Contradicted',      60, 0, 1, N'A source contradicts the proposition (terminal negative).'),
	(N'UNRESOLVED',       N'Unresolved',         5, 0, 0, N'A verification obligation exists and is not yet resolved.')
) AS source (StateCode, DisplayName, StateRank, CountsTowardEvidenceScore, IsTerminalNegative, Description)
ON target.StateCode = source.StateCode
WHEN NOT MATCHED BY TARGET THEN
	INSERT (StateCode, DisplayName, StateRank, CountsTowardEvidenceScore, IsTerminalNegative, Description)
	VALUES (source.StateCode, source.DisplayName, source.StateRank, source.CountsTowardEvidenceScore, source.IsTerminalNegative, source.Description);

GO

-- ── Seed: optional LLM confirmation flag (DISABLED by default) ─────────────────────────────────────
-- Keeps the default run deterministic/stateless with no new paid model call. When enabled, the
-- application runs a fail-soft LLM semantic-match confirmation only for bindings that survive the
-- deterministic guardrails but remain ambiguous (no explicit ALLOW/DENY rule).
MERGE POLOXI.Legal_DecisionSetting AS target
USING (VALUES
	(N'FactBinding.LlmSemanticMatch.Enabled', N'false', N'Boolean',
	 N'Optional fail-soft LLM semantic-match confirmation for ambiguous proposition fact bindings. Off by default; deterministic guardrails always run.')
) AS source (SettingKey, SettingValue, DataTypeCode, Description)
ON target.SettingKey = source.SettingKey
WHEN NOT MATCHED BY TARGET THEN
	INSERT (SettingKey, SettingValue, DataTypeCode, Description)
	VALUES (source.SettingKey, source.SettingValue, source.DataTypeCode, source.Description);

COMMIT TRANSACTION;
