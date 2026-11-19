SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================================
-- 0497: Atomic-proposition bridge — typed propositions + reviewable application inference.
--
--   The matter graph already models passages (Legal_DocumentPassage), evidence items
--   (Legal_MatterEvidenceItem), factual propositions (Legal_MatterFactProposition),
--   passage/evidence -> proposition support (Legal_MatterPropositionSupport),
--   proposition -> factor/node placement (Legal_PropositionNodeLink), and verified legal
--   authority (Legal_VerifiedLegalProposition / Legal_NormalizedAuthority).
--
--   What the graph did NOT model is the single most important design distinction for Judz:
--   keeping FACTUAL support ("what happened") separate from LEGAL support ("what rule governs")
--   and from the APPLICATION inference ("whether the facts satisfy the rule"). This migration:
--
--     1. Adds PropositionKindCode to Legal_MatterFactProposition so a proposition is explicitly
--        FACTUAL | LEGAL | APPLICATION. Existing rows backfill to FACTUAL (prior behavior).
--     2. Creates Legal_MatterApplicationInference: a first-class, reviewable record stating whether
--        a factor's legal requirement appears satisfied, with its own confidence/review state kept
--        separate from classification confidence and passage-support strength.
--     3. Creates Legal_MatterApplicationInferenceInput: the typed edges feeding an application
--        inference (FACTUAL_PROPOSITION | LEGAL_PROPOSITION | EVIDENCE | AUTHORITY), so every
--        application links back to both the facts and the governing law it relied on.
--
--   Additive and idempotent. No existing read/write path changes semantics: the new column is
--   defaulted and the new tables are independent.
-- ============================================================================

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');
GO

-- ── 1. Typed propositions ───────────────────────────────────────────────────────────────────────
IF OBJECT_ID(N'POLOXI.Legal_MatterFactProposition', N'U') IS NOT NULL
   AND COL_LENGTH(N'POLOXI.Legal_MatterFactProposition', N'PropositionKindCode') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_MatterFactProposition
		ADD PropositionKindCode NVARCHAR(40) NOT NULL
			CONSTRAINT DF_Legal_MatterFactProposition_Kind DEFAULT N'FACTUAL';
END;
GO

-- Backfill any pre-existing rows that predate the default (defensive; default already covers inserts).
IF OBJECT_ID(N'POLOXI.Legal_MatterFactProposition', N'U') IS NOT NULL
   AND COL_LENGTH(N'POLOXI.Legal_MatterFactProposition', N'PropositionKindCode') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_MatterFactProposition
	SET PropositionKindCode = N'FACTUAL'
	WHERE PropositionKindCode IS NULL OR LTRIM(RTRIM(PropositionKindCode)) = N'';
END;
GO

IF OBJECT_ID(N'POLOXI.Legal_MatterFactProposition', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Legal_MatterFactProposition_Kind')
BEGIN
	ALTER TABLE POLOXI.Legal_MatterFactProposition WITH CHECK
		ADD CONSTRAINT CK_Legal_MatterFactProposition_Kind
			CHECK (PropositionKindCode IN (N'FACTUAL', N'LEGAL', N'APPLICATION'));
END;
GO

IF OBJECT_ID(N'POLOXI.Legal_MatterFactProposition', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_MatterFactProposition') AND name = N'IX_Legal_MatterFactProposition_Kind')
	CREATE INDEX IX_Legal_MatterFactProposition_Kind
		ON POLOXI.Legal_MatterFactProposition (TenantId, DecisionMatterId, PropositionKindCode, FactStateCode)
		INCLUDE (Confidence, IsDecisionAuthoritative) WHERE IsDeleted = 0;
GO

-- ── 2. Application inference (facts + rule -> application) ───────────────────────────────────────
-- A separate, reviewable inference about whether a factor's legal requirement appears satisfied.
-- Its confidence and review state are deliberately distinct from classification confidence and from
-- passage-support strength so one score never hides different kinds of uncertainty.
IF OBJECT_ID(N'POLOXI.Legal_MatterApplicationInference', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterApplicationInference
(
	LegalApplicationInferenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterApplicationInference PRIMARY KEY DEFAULT NEWID(),
	DecisionMatterId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterApplicationInference_Matter REFERENCES POLOXI.Legal_DecisionMatter (DecisionMatterId),
	-- The factor this application resolves: Domain Pack dimension + optional hierarchy/outcome node
	-- (TargetNodeId mirrors Legal_PropositionNodeLink: a node id without a hard FK, so the factor may
	-- live in any of the hierarchy layers without coupling this table to one of them).
	DimensionCode NVARCHAR(60) NOT NULL,
	FactorCode NVARCHAR(80) NULL,
	TargetNodeId UNIQUEIDENTIFIER NULL,
	-- The application proposition itself may optionally be persisted as an APPLICATION-kind row in
	-- Legal_MatterFactProposition; this nullable link ties the inference to that proposition when present.
	LegalApplicationPropositionId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_MatterApplicationInference_Proposition REFERENCES POLOXI.Legal_MatterFactProposition (LegalFactPropositionId),
	ConclusionText NVARCHAR(3000) NOT NULL,
	-- SATISFIED | NOT_SATISFIED | PARTIALLY_SATISFIED | INDETERMINATE
	DispositionCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_MatterApplicationInference_Disposition DEFAULT N'INDETERMINATE',
	-- PROPOSED | UNDER_REVIEW | ACCEPTED | REJECTED | SUPERSEDED (reviewable; separate from fact state)
	ReviewStateCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_MatterApplicationInference_Review DEFAULT N'PROPOSED',
	-- Application confidence ONLY — never merged with classification or passage-support confidence.
	Confidence DECIMAL(5,4) NULL,
	Rationale NVARCHAR(MAX) NULL,
	GenerationOriginCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_MatterApplicationInference_Origin DEFAULT N'DYNAMIC_LLM',
	-- Append-only correction trail: a revised inference supersedes an older row rather than mutating it.
	SupersededByApplicationInferenceId UNIQUEIDENTIFIER NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterApplicationInference_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_MatterApplicationInference_Deleted DEFAULT 0,
	CONSTRAINT CK_Legal_MatterApplicationInference_Confidence CHECK (Confidence IS NULL OR Confidence BETWEEN 0 AND 1),
	CONSTRAINT CK_Legal_MatterApplicationInference_Disposition CHECK (DispositionCode IN (N'SATISFIED', N'NOT_SATISFIED', N'PARTIALLY_SATISFIED', N'INDETERMINATE')),
	CONSTRAINT CK_Legal_MatterApplicationInference_Review CHECK (ReviewStateCode IN (N'PROPOSED', N'UNDER_REVIEW', N'ACCEPTED', N'REJECTED', N'SUPERSEDED'))
);
GO

IF OBJECT_ID(N'POLOXI.Legal_MatterApplicationInference', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_MatterApplicationInference') AND name = N'IX_Legal_MatterApplicationInference_Factor')
	CREATE INDEX IX_Legal_MatterApplicationInference_Factor
		ON POLOXI.Legal_MatterApplicationInference (TenantId, DecisionMatterId, DimensionCode, FactorCode, ReviewStateCode)
		INCLUDE (DispositionCode, Confidence, TargetNodeId) WHERE IsDeleted = 0;
GO

-- ── 3. Application inference inputs (typed edges back to facts and governing law) ────────────────
-- Each input edge records WHICH piece of the graph the application relied on and in WHAT role, so the
-- factual chain (evidence -> factual proposition) and the legal chain (authority -> legal proposition)
-- remain visibly separate even where they feed the same conclusion.
IF OBJECT_ID(N'POLOXI.Legal_MatterApplicationInferenceInput', N'U') IS NULL
CREATE TABLE POLOXI.Legal_MatterApplicationInferenceInput
(
	LegalApplicationInferenceInputId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterApplicationInferenceInput PRIMARY KEY DEFAULT NEWID(),
	LegalApplicationInferenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_MatterApplicationInferenceInput_Inference REFERENCES POLOXI.Legal_MatterApplicationInference (LegalApplicationInferenceId),
	-- FACTUAL_PROPOSITION | LEGAL_PROPOSITION | EVIDENCE | AUTHORITY
	InputRoleCode NVARCHAR(40) NOT NULL,
	-- Exactly one of the following target columns is populated per row, matching InputRoleCode.
	LegalFactPropositionId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_MatterApplicationInferenceInput_Proposition REFERENCES POLOXI.Legal_MatterFactProposition (LegalFactPropositionId),
	LegalEvidenceItemId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_MatterApplicationInferenceInput_Evidence REFERENCES POLOXI.Legal_MatterEvidenceItem (LegalEvidenceItemId),
	VerifiedLegalPropositionId UNIQUEIDENTIFIER NULL CONSTRAINT FK_Legal_MatterApplicationInferenceInput_Authority REFERENCES POLOXI.Legal_VerifiedLegalProposition (VerifiedLegalPropositionId),
	-- SUPPORTS | CONTRADICTS | QUALIFIES | CONTEXT_ONLY — qualitative only; CONTEXT_ONLY adds no weight.
	RelationshipCode NVARCHAR(40) NOT NULL CONSTRAINT DF_Legal_MatterApplicationInferenceInput_Rel DEFAULT N'SUPPORTS',
	Rationale NVARCHAR(2000) NULL,
	TenantId UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterApplicationInferenceInput_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_MatterApplicationInferenceInput_Deleted DEFAULT 0,
	CONSTRAINT CK_Legal_MatterApplicationInferenceInput_Role CHECK (InputRoleCode IN (N'FACTUAL_PROPOSITION', N'LEGAL_PROPOSITION', N'EVIDENCE', N'AUTHORITY')),
	CONSTRAINT CK_Legal_MatterApplicationInferenceInput_Rel CHECK (RelationshipCode IN (N'SUPPORTS', N'CONTRADICTS', N'QUALIFIES', N'CONTEXT_ONLY')),
	-- The populated target column must match the declared role (one claim per edge).
	CONSTRAINT CK_Legal_MatterApplicationInferenceInput_Target CHECK (
		(InputRoleCode IN (N'FACTUAL_PROPOSITION', N'LEGAL_PROPOSITION') AND LegalFactPropositionId IS NOT NULL AND LegalEvidenceItemId IS NULL AND VerifiedLegalPropositionId IS NULL)
		OR (InputRoleCode = N'EVIDENCE' AND LegalEvidenceItemId IS NOT NULL AND LegalFactPropositionId IS NULL AND VerifiedLegalPropositionId IS NULL)
		OR (InputRoleCode = N'AUTHORITY' AND VerifiedLegalPropositionId IS NOT NULL AND LegalFactPropositionId IS NULL AND LegalEvidenceItemId IS NULL)
	)
);
GO

IF OBJECT_ID(N'POLOXI.Legal_MatterApplicationInferenceInput', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_MatterApplicationInferenceInput') AND name = N'IX_Legal_MatterApplicationInferenceInput_Inference')
	CREATE INDEX IX_Legal_MatterApplicationInferenceInput_Inference
		ON POLOXI.Legal_MatterApplicationInferenceInput (TenantId, LegalApplicationInferenceId, InputRoleCode)
		INCLUDE (RelationshipCode, LegalFactPropositionId, LegalEvidenceItemId, VerifiedLegalPropositionId) WHERE IsDeleted = 0;
GO
