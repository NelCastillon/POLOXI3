SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0448: Post-L2 evidence-aware routing, matter classification proposals, zero-score cleanup.
--   Audit of the latest Domain Pack run (354 targets, 352 LLM-scored, 146 IN_SCOPE / 146 PENDING_REVIEW /
--   62 OUT_OF_SCOPE, 8/8 mandatory concepts retained, 1 low-score admission) found:
--     * PENDING_REVIEW had become a low-score holding band (LLM 0.00-0.25) rather than a genuine ambiguity state;
--     * IN_SCOPE conflated scope relevance, source availability, verified support and execution priority;
--     * 88 IN_SCOPE targets had neither a present document nor a mapped passage yet all received an L3 pass;
--     * a saved "Breach of Fiduciary Duty" subtype / IncidentTypeCode "Other" conflicted with a collision narrative.
--   This migration is the TABLE half (code follows the same contracts). The batched L2 reviewer is unchanged.
--     1) Dials: zero-score ceiling, immediate-relevance threshold, evidence-aware L3 routing switch.
--     2) Routing code lookup (availability / verification / execution priority) - DB-backed, no hardcoded lists.
--     3) Independent routing columns on Legal_MatterEvidenceScope (ScopeStatusCode semantics are NOT changed).
--     4) Legal_DecisionMatterClassificationProposal: confidence-qualified PROPOSED/ACCEPTED/REJECTED proposals.
--        The authoritative Legal_DecisionMatter / PI profile fields are NEVER mutated by the system.
--   Idempotent and additive. Nothing is deleted; USER scope rows are untouched.
-- ============================================================================
BEGIN TRANSACTION;

DECLARE @Now DATETIME2=SYSUTCDATETIME();
DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000000';

-- ---------------------------------------------------------------------------
-- 1) Platform configuration dials (upsert; existing tenant overrides untouched).
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200) NOT NULL, Value NVARCHAR(100) NOT NULL, DataTypeCode NVARCHAR(40) NOT NULL, Description NVARCHAR(1000) NOT NULL);
	INSERT @Settings(SettingKey,Value,DataTypeCode,Description) VALUES
	(N'Intelligence.SearchWide.MatterScopeZeroScoreCeiling',N'0.05',N'Decimal',
	 N'Zero-score cleanup: a target whose L2 relevance-review score is at or below this value is OUT_OF_SCOPE unless a concrete reopening trigger exists (PRIORITIZE rule, matter-type match, required coverage, directly classified document or directly classified passage). Lexical/keyword passage hits and the blended score are NOT reopening triggers. USER decisions are never overwritten.'),
	(N'Intelligence.SearchWide.MatterScopeImmediateRelevance',N'0.60',N'Decimal',
	 N'Evidence-aware L3 routing: an IN_SCOPE Evidence Type with ABSENT evidence (no classified document, no classified passage) still receives an L3 atomic-proposition pass when its L2 relevance score is at or above this value (IMMEDIATE priority). Below it the target is DEFERRED to acquisition (no model call this run; reopened automatically when evidence arrives). Concepts, required coverage and PRIORITIZE targets are never deferred.'),
	(N'Intelligence.SearchWide.EnableEvidenceAwareL3Routing',N'true',N'Boolean',
	 N'When true, Domain Pack L3 generation skips IN_SCOPE Evidence Types routed DEFERRED (absent evidence, relevance below MatterScopeImmediateRelevance) and records them as DEFERRED_ACQUISITION in the processing ledger. When false, every participating target receives an L3 pass (0445 behaviour). Routing attributes are computed and persisted either way.');

	UPDATE c SET c.DefaultValue=s.Value, c.Description=s.Description, c.ModifiedDateUtc=@Now
	  FROM Core.ConfigurationSetting c JOIN @Settings s ON s.SettingKey=c.SettingKey
	 WHERE c.TenantId IS NULL AND c.ScopeCode=N'Platform' AND c.IsDeleted=0;

	INSERT Core.ConfigurationSetting(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
	SELECT NEWID(),NULL,N'Platform',N'Intelligence',s.SettingKey,s.Value,s.Value,s.DataTypeCode,s.Description,0,0,@Now,0
	  FROM @Settings s
	 WHERE NOT EXISTS(SELECT 1 FROM Core.ConfigurationSetting c WHERE c.SettingKey=s.SettingKey AND c.TenantId IS NULL AND c.ScopeCode=N'Platform' AND c.IsDeleted=0);
END;

-- ---------------------------------------------------------------------------
-- 2) Routing code lookup. CodeKind: EVIDENCE_AVAILABILITY | VERIFICATION_STATUS | EXECUTION_PRIORITY | CLASSIFICATION_PROPOSAL_STATUS
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_MatterEvidenceRoutingCode',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_MatterEvidenceRoutingCode
	(
		MatterEvidenceRoutingCodeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_MatterEvidenceRoutingCode PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		CodeKind NVARCHAR(40) NOT NULL,
		Code NVARCHAR(40) NOT NULL,
		Name NVARCHAR(120) NOT NULL,
		Description NVARCHAR(1000) NULL,
		RunsL3 BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceRoutingCode_RunsL3 DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceRoutingCode_Sort DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceRoutingCode_Active DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_MatterEvidenceRoutingCode_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceRoutingCode_IsDeleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_Legal_MatterEvidenceRoutingCode_Code ON POLOXI.Legal_MatterEvidenceRoutingCode(CodeKind,Code) WHERE IsDeleted=0 AND TenantId IS NULL;
END;

DECLARE @Codes TABLE(CodeKind NVARCHAR(40) NOT NULL, Code NVARCHAR(40) NOT NULL, Name NVARCHAR(120) NOT NULL, Description NVARCHAR(1000) NOT NULL, RunsL3 BIT NOT NULL, SortOrder INT NOT NULL);
INSERT @Codes VALUES
(N'EVIDENCE_AVAILABILITY',N'PRESENT',N'Present',N'A document directly classified as this Evidence Type exists in the matter corpus.',1,10),
(N'EVIDENCE_AVAILABILITY',N'REFERENCED',N'Referenced',N'No classified document, but at least one passage is directly classified to this target (another document references or embeds it).',1,20),
(N'EVIDENCE_AVAILABILITY',N'ABSENT',N'Absent',N'No classified document and no directly classified passage. Lexical/keyword passage matches do NOT establish availability.',1,30),
(N'EVIDENCE_AVAILABILITY',N'NOT_APPLICABLE',N'Not applicable',N'Concept targets: availability is not a document property.',1,40),
(N'VERIFICATION_STATUS',N'VERIFIED',N'Verified',N'At least one VERIFIED source assertion is anchored to evidence of this type. Only this state may ever contribute LPI support.',1,10),
(N'VERIFICATION_STATUS',N'UNVERIFIED',N'Unverified',N'Evidence is present or referenced but no source assertion has been verified yet.',1,20),
(N'VERIFICATION_STATUS',N'CONTRADICTED',N'Contradicted',N'A verified assertion contradicts the evidence content for this target.',1,30),
(N'VERIFICATION_STATUS',N'UNKNOWN',N'Unknown',N'No evidence available to verify.',1,40),
(N'EXECUTION_PRIORITY',N'IMMEDIATE',N'Immediate',N'Enters L3 atomic-proposition generation this run: concept, required coverage, PRIORITIZE rule, present/referenced evidence, or relevance at/above MatterScopeImmediateRelevance.',1,10),
(N'EXECUTION_PRIORITY',N'NORMAL',N'Normal',N'Enters L3 this run with standard batching.',1,20),
(N'EXECUTION_PRIORITY',N'DEFERRED',N'Deferred',N'Relevant but evidence is ABSENT and relevance is below MatterScopeImmediateRelevance: routed to acquisition. No L3 model call this run; reopened automatically when a classified document or passage arrives.',0,30),
(N'EXECUTION_PRIORITY',N'DORMANT',N'Dormant',N'Scope status does not participate in L3 (PENDING_REVIEW, REVIEW_FAILED, OUT_OF_SCOPE).',0,40),
(N'CLASSIFICATION_PROPOSAL_STATUS',N'PROPOSED',N'Proposed',N'System-proposed classification awaiting attorney decision. Source fields unchanged.',1,10),
(N'CLASSIFICATION_PROPOSAL_STATUS',N'ACCEPTED',N'Accepted',N'Attorney accepted the proposal.',1,20),
(N'CLASSIFICATION_PROPOSAL_STATUS',N'REJECTED',N'Rejected',N'Attorney rejected the proposal; the saved classification stands.',1,30);

MERGE POLOXI.Legal_MatterEvidenceRoutingCode AS t
USING @Codes AS s ON t.CodeKind=s.CodeKind AND t.Code=s.Code AND t.TenantId IS NULL AND t.IsDeleted=0
WHEN MATCHED THEN UPDATE SET Name=s.Name, Description=s.Description, RunsL3=s.RunsL3, SortOrder=s.SortOrder, ModifiedDateUtc=@Now, ModifiedByUserId=@SystemUserId
WHEN NOT MATCHED THEN INSERT(TenantId,CodeKind,Code,Name,Description,RunsL3,SortOrder,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted)
	VALUES(NULL,s.CodeKind,s.Code,s.Name,s.Description,s.RunsL3,s.SortOrder,1,@Now,@SystemUserId,0);

-- ---------------------------------------------------------------------------
-- 3) Independent routing attributes on the per-matter scope ledger.
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_MatterEvidenceScope',N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'POLOXI.Legal_MatterEvidenceScope',N'EvidenceAvailabilityCode') IS NULL
		ALTER TABLE POLOXI.Legal_MatterEvidenceScope ADD EvidenceAvailabilityCode NVARCHAR(40) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_MatterEvidenceScope',N'VerificationStatusCode') IS NULL
		ALTER TABLE POLOXI.Legal_MatterEvidenceScope ADD VerificationStatusCode NVARCHAR(40) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_MatterEvidenceScope',N'ExecutionPriorityCode') IS NULL
		ALTER TABLE POLOXI.Legal_MatterEvidenceScope ADD ExecutionPriorityCode NVARCHAR(40) NULL;
	IF COL_LENGTH(N'POLOXI.Legal_MatterEvidenceScope',N'DirectPassageCount') IS NULL
		ALTER TABLE POLOXI.Legal_MatterEvidenceScope ADD DirectPassageCount INT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScope_DirectPassages DEFAULT 0;
	IF COL_LENGTH(N'POLOXI.Legal_MatterEvidenceScope',N'VerifiedAssertionCount') IS NULL
		ALTER TABLE POLOXI.Legal_MatterEvidenceScope ADD VerifiedAssertionCount INT NOT NULL CONSTRAINT DF_Legal_MatterEvidenceScope_VerifiedAssertions DEFAULT 0;
	IF COL_LENGTH(N'POLOXI.Legal_MatterEvidenceScope',N'RoutingReason') IS NULL
		ALTER TABLE POLOXI.Legal_MatterEvidenceScope ADD RoutingReason NVARCHAR(400) NULL;
END;

-- ---------------------------------------------------------------------------
-- 4) Matter classification proposals (never mutate the source record).
-- ---------------------------------------------------------------------------
IF OBJECT_ID(N'POLOXI.Legal_DecisionMatterClassificationProposal',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_DecisionMatterClassificationProposal
	(
		DecisionMatterClassificationProposalId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_DecisionMatterClassificationProposal PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		DecisionMatterId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_DecisionMatterClassificationProposal_Matter REFERENCES POLOXI.Legal_DecisionMatter(DecisionMatterId),
		FieldCode NVARCHAR(60) NOT NULL,              -- SUBTYPE | INCIDENT_TYPE_CODE | INCIDENT_FAMILY | DECISION_CONTEXT
		CurrentValue NVARCHAR(400) NULL,              -- snapshot of the authoritative value at proposal time (read-only copy)
		ProposedValue NVARCHAR(400) NULL,
		Confidence DECIMAL(5,4) NOT NULL CONSTRAINT CK_Legal_DecisionMatterClassificationProposal_Confidence CHECK (Confidence BETWEEN 0 AND 1),
		StructuredSignals NVARCHAR(1000) NULL,        -- what the saved fields said
		NarrativeSignals NVARCHAR(1000) NULL,         -- what the narrative/evidence said
		Rationale NVARCHAR(2000) NOT NULL,
		StatusCode NVARCHAR(40) NOT NULL,             -- Legal_MatterEvidenceRoutingCode CLASSIFICATION_PROPOSAL_STATUS
		DecidedByUserId UNIQUEIDENTIFIER NULL,
		DecidedDateUtc DATETIME2 NULL,
		DecisionNote NVARCHAR(1000) NULL,
		LastProposedExecutionId UNIQUEIDENTIFIER NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_DecisionMatterClassificationProposal_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_DecisionMatterClassificationProposal_IsDeleted DEFAULT 0
	);
	-- One OPEN proposal per matter/field; decided proposals are kept as history.
	CREATE UNIQUE INDEX UX_Legal_DecisionMatterClassificationProposal_Open ON POLOXI.Legal_DecisionMatterClassificationProposal(TenantId,DecisionMatterId,FieldCode) WHERE IsDeleted=0 AND StatusCode=N'PROPOSED';
	CREATE INDEX IX_Legal_DecisionMatterClassificationProposal_Matter ON POLOXI.Legal_DecisionMatterClassificationProposal(TenantId,DecisionMatterId,StatusCode) WHERE IsDeleted=0;
END;

COMMIT TRANSACTION;
