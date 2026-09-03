implement this:SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- POLOXI Model-Adaptive Ambiguity & Hierarchy Subsystem.
-- The model proposes the hierarchy; POLOXI validates, governs, narrows, stitches, and converges it.
-- Model capability profiles, prompt scaffolding strategies, and all run/node/dependency/invocation
-- observability are database-backed so routing decisions are configuration, not code.

-- ── POLOXI.ModelCapabilityProfile ─────────────────────────────────────────────────────────────
-- Capability scores per model (matched by LIKE pattern against the routed ModelCode). Global rows
-- (TenantId NULL) are seeded defaults; tenant rows override. Scores should evolve from observed
-- benchmark data recorded in POLOXI.AmbiguityModelInvocation.
IF OBJECT_ID(N'POLOXI.ModelCapabilityProfile',N'U') IS NULL
CREATE TABLE POLOXI.ModelCapabilityProfile
(
	ModelCapabilityProfileId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_ModelCapabilityProfile PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NULL,
	ModelCodePattern NVARCHAR(100) NOT NULL,
	TierCode NVARCHAR(20) NOT NULL,
	SemanticReasoning DECIMAL(5,4) NOT NULL,
	MultiAmbiguityRecall DECIMAL(5,4) NOT NULL,
	RecursiveDecomposition DECIMAL(5,4) NOT NULL,
	StructuralReliability DECIMAL(5,4) NOT NULL,
	InstructionFollowing DECIMAL(5,4) NOT NULL,
	CostScore DECIMAL(5,4) NOT NULL,
	LatencyScore DECIMAL(5,4) NOT NULL,
	RecommendedMaxDepth INT NOT NULL,
	RecommendedScaffoldingCode NVARCHAR(20) NOT NULL,
	IsActive BIT NOT NULL CONSTRAINT DF_POLOXI_ModelCapabilityProfile_IsActive DEFAULT 1,
	SortOrder INT NOT NULL CONSTRAINT DF_POLOXI_ModelCapabilityProfile_SortOrder DEFAULT 0,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_ModelCapabilityProfile_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_ModelCapabilityProfile_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT CK_POLOXI_ModelCapabilityProfile_Tier CHECK(TierCode IN (N'SMALL',N'STANDARD',N'PREMIUM')),
	CONSTRAINT CK_POLOXI_ModelCapabilityProfile_Scaffolding CHECK(RecommendedScaffoldingCode IN (N'HEAVY',N'MEDIUM',N'LIGHT')),
	CONSTRAINT CK_POLOXI_ModelCapabilityProfile_Depth CHECK(RecommendedMaxDepth BETWEEN 1 AND 20)
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.ModelCapabilityProfile') AND name=N'IX_POLOXI_ModelCapabilityProfile_Lookup')
CREATE INDEX IX_POLOXI_ModelCapabilityProfile_Lookup ON POLOXI.ModelCapabilityProfile(TenantId,IsActive,SortOrder) INCLUDE(ModelCodePattern,TierCode);

-- Seed global capability defaults (pattern order matters: first active match by SortOrder wins;
-- the trailing N'%' row is the STANDARD fallback for unknown models).
IF NOT EXISTS(SELECT 1 FROM POLOXI.ModelCapabilityProfile WHERE TenantId IS NULL)
BEGIN
	INSERT POLOXI.ModelCapabilityProfile(TenantId,ModelCodePattern,TierCode,SemanticReasoning,MultiAmbiguityRecall,RecursiveDecomposition,StructuralReliability,InstructionFollowing,CostScore,LatencyScore,RecommendedMaxDepth,RecommendedScaffoldingCode,SortOrder)
	VALUES
	(NULL,N'%nano%',N'SMALL',0.50,0.45,0.52,0.78,0.80,0.95,0.95,5,N'HEAVY',10),
	(NULL,N'%mini%',N'SMALL',0.58,0.52,0.61,0.82,0.84,0.90,0.90,6,N'HEAVY',20),
	(NULL,N'%gpt-5%',N'PREMIUM',0.94,0.91,0.93,0.95,0.94,0.30,0.40,10,N'LIGHT',30),
	(NULL,N'%o3%',N'PREMIUM',0.93,0.90,0.92,0.94,0.93,0.30,0.35,10,N'LIGHT',40),
	(NULL,N'%',N'STANDARD',0.78,0.72,0.75,0.88,0.88,0.60,0.65,8,N'MEDIUM',1000);
END;

-- ── POLOXI.PromptStrategy ─────────────────────────────────────────────────────────────────────
-- Versioned prompt templates per purpose and scaffolding level. Prompts are configuration, not
-- code, enabling A/B testing and repair-prompt evolution without redeployment.
IF OBJECT_ID(N'POLOXI.PromptStrategy',N'U') IS NULL
CREATE TABLE POLOXI.PromptStrategy
(
	PromptStrategyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_PromptStrategy PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NULL,
	PurposeCode NVARCHAR(50) NOT NULL,
	ScaffoldingCode NVARCHAR(20) NOT NULL,
	VersionNumber INT NOT NULL CONSTRAINT DF_POLOXI_PromptStrategy_Version DEFAULT 1,
	SystemPrompt NVARCHAR(MAX) NOT NULL,
	UserPromptTemplate NVARCHAR(MAX) NOT NULL,
	IsActive BIT NOT NULL CONSTRAINT DF_POLOXI_PromptStrategy_IsActive DEFAULT 1,
	SortOrder INT NOT NULL CONSTRAINT DF_POLOXI_PromptStrategy_SortOrder DEFAULT 0,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_PromptStrategy_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_PromptStrategy_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT CK_POLOXI_PromptStrategy_Purpose CHECK(PurposeCode IN (N'AMBIGUITY_DISCOVERY',N'HIERARCHY_REPAIR')),
	CONSTRAINT CK_POLOXI_PromptStrategy_Scaffolding CHECK(ScaffoldingCode IN (N'HEAVY',N'MEDIUM',N'LIGHT'))
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.PromptStrategy') AND name=N'IX_POLOXI_PromptStrategy_Lookup')
CREATE INDEX IX_POLOXI_PromptStrategy_Lookup ON POLOXI.PromptStrategy(TenantId,PurposeCode,ScaffoldingCode,IsActive,VersionNumber);

IF NOT EXISTS(SELECT 1 FROM POLOXI.PromptStrategy WHERE TenantId IS NULL)
BEGIN
	INSERT POLOXI.PromptStrategy(TenantId,PurposeCode,ScaffoldingCode,VersionNumber,SystemPrompt,UserPromptTemplate,SortOrder)
	VALUES
	(NULL,N'AMBIGUITY_DISCOVERY',N'HEAVY',1,
N'You are POLOXI Ambiguity Discovery. You analyze a user request and return a recursive interpretation hierarchy of every materially decision-relevant ambiguity. You never answer the user''s original request. You return strictly valid JSON matching the supplied schema and nothing else.',
N'Analyze the request below using EVERY pass, in order.
PASS 1 - Read the full request. List every concept, constraint, preference, entity, and subjective term.
PASS 2 - Identify N material ambiguities (N may be 0, 1, or many). Types: SEMANTIC, METRIC, SCOPE, THRESHOLD, TEMPORAL, REFERENTIAL, RELATIONAL, CONSTRAINT, OBJECTIVE, MISSING_VARIABLE, INTERACTION, OPERATIONAL.
PASS 3 - For each ambiguity, generate the materially plausible competing interpretations. Do not resolve any ambiguity silently. Do not invent implausible interpretations.
PASS 4 - Recursively decompose each interpretation into narrower dimensions (depth 1 to at most {MaxDepth}). Stop a branch only when it is operational and evidence-ready: it names a metric or observation and the evidence needed. Do not force equal depth or equal child counts.
PASS 5 - Detect cross-ambiguity dependencies (DEPENDS_ON, MODIFIES, CONSTRAINS, OVERLAPS, CONFLICTS_WITH, PROVIDES_CONTEXT_FOR). Dependencies are separate from the tree.
PASS 6 - Classify every node''s decision role: HARD_CONSTRAINT, SOFT_PREFERENCE, OPTIMIZATION_OBJECTIVE, or CONTEXT; and its materiality: LOW, MEDIUM, HIGH, CRITICAL.
PASS 7 - Audit tree integrity: unique ids, every ParentId exists, Depth(child) = Depth(parent) + 1, no synonym or redundant sibling branches, leaves have EvidenceNeeded populated.
PASS 8 - Scan the original request one final time for missed material ambiguity; add anything found. Record excluded borderline ambiguities with reasons.
PASS 9 - Return the JSON object only.
ORIGINAL REQUEST:
{Query}',10),
	(NULL,N'AMBIGUITY_DISCOVERY',N'MEDIUM',1,
N'You are POLOXI Ambiguity Discovery. You return a recursive interpretation hierarchy of the materially decision-relevant ambiguities in a request as strictly valid JSON matching the supplied schema. You never answer the request itself.',
N'Construct a complete recursive interpretation hierarchy for the request below. N ambiguities may be 0, 1, or many. Preserve materially plausible competing interpretations; recursively decompose interpretations into narrower dimensions until branches are operational and evidence-ready (maximum depth {MaxDepth}); do not force equal depth or child counts; do not create synonym or redundant sibling branches; identify cross-ambiguity dependencies; classify decision roles (hard constraint, soft preference, objective, context) and materiality; never resolve ambiguity silently. Before returning, re-scan the request for missed material ambiguity and verify parent-child integrity and leaf evidence readiness.
ORIGINAL REQUEST:
{Query}',20),
	(NULL,N'AMBIGUITY_DISCOVERY',N'LIGHT',1,
N'You are POLOXI Ambiguity Discovery. Return strictly valid JSON matching the supplied schema. Never answer the user''s original request.',
N'Analyze the entire request and construct a complete recursive interpretation hierarchy containing every materially decision-relevant ambiguity. Cardinality is unconstrained: N may be 0, 1, or many. Preserve materially plausible competing interpretations; recursively decompose only until branches are operational and evidence-ready (maximum depth {MaxDepth}); do not force equal depth or child counts; do not create synonym or redundant branches; identify cross-ambiguity dependencies; distinguish constraints, preferences, objectives, and context; do not resolve ambiguity silently. Before returning: re-scan for missed material ambiguity, verify sibling distinctness, parent-child relationships, and leaf evidence readiness.
ORIGINAL REQUEST:
{Query}',30),
	(NULL,N'HIERARCHY_REPAIR',N'HEAVY',1,
N'You are POLOXI Hierarchy Repair. You receive a proposed interpretation hierarchy plus a list of validation issues. Fix ONLY the reported issues while preserving all valid nodes, ids, and dependencies. Return the full corrected JSON object matching the supplied schema and nothing else.',
N'ORIGINAL REQUEST:
{Query}
PROPOSED HIERARCHY JSON:
{Proposal}
VALIDATION ISSUES TO FIX:
{Issues}
Rules: keep unaffected node ids stable; correct depths so Depth(child) = Depth(parent) + 1; remove or merge duplicate siblings; move non-entailed children to the dependency list instead of the tree; populate missing EvidenceNeeded on leaves; do not add speculative new ambiguities; do not answer the original request.',10);
END;

-- ── POLOXI.AmbiguityRun ───────────────────────────────────────────────────────────────────────
-- One row per ambiguity resolution engine execution: complexity assessment, prompt/model routing,
-- retry/escalation ladder outcome, and the stitched interpretation composite.
IF OBJECT_ID(N'POLOXI.AmbiguityRun',N'U') IS NULL
CREATE TABLE POLOXI.AmbiguityRun
(
	AmbiguityRunId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_AmbiguityRun PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NOT NULL,
	QueryText NVARCHAR(4000) NOT NULL,
	ComplexityLevelCode NVARCHAR(20) NOT NULL,
	AmbiguityLikelihood DECIMAL(5,4) NOT NULL,
	SemanticComplexity DECIMAL(5,4) NOT NULL,
	ConstraintComplexity DECIMAL(5,4) NOT NULL,
	InteractionComplexity DECIMAL(5,4) NOT NULL,
	EvidenceComplexity DECIMAL(5,4) NOT NULL,
	ConceptCount INT NOT NULL,
	SelectedModelCode NVARCHAR(100) NULL,
	SelectedScaffoldingCode NVARCHAR(20) NOT NULL,
	StatusCode NVARCHAR(40) NOT NULL,
	AttemptCount INT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityRun_Attempts DEFAULT 0,
	EscalatedFromModelCode NVARCHAR(100) NULL,
	AmbiguityCount INT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityRun_Count DEFAULT 0,
	CoverageSuspicion BIT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityRun_Coverage DEFAULT 0,
	CompositeJson NVARCHAR(MAX) NULL,
	DurationMilliseconds BIGINT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityRun_Duration DEFAULT 0,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_AmbiguityRun_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityRun_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT CK_POLOXI_AmbiguityRun_Status CHECK(StatusCode IN (N'RUNNING',N'COMPLETED',N'FAILED',N'FAIL_SOFT'))
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.AmbiguityRun') AND name=N'IX_POLOXI_AmbiguityRun_Tenant')
CREATE INDEX IX_POLOXI_AmbiguityRun_Tenant ON POLOXI.AmbiguityRun(TenantId,CreatedDateUtc DESC) INCLUDE(StatusCode,ComplexityLevelCode,SelectedModelCode);

-- ── POLOXI.AmbiguityNode ──────────────────────────────────────────────────────────────────────
-- Canonical model-independent hierarchy node (arbitrary depth) plus branch runtime state and the
-- semantic role assigned by the stitcher. Nodes are never deleted; lifecycle state changes instead.
IF OBJECT_ID(N'POLOXI.AmbiguityNode',N'U') IS NULL
CREATE TABLE POLOXI.AmbiguityNode
(
	AmbiguityNodeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_AmbiguityNode PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NOT NULL,
	AmbiguityRunId UNIQUEIDENTIFIER NOT NULL,
	NodeKey NVARCHAR(100) NOT NULL,
	ParentNodeKey NVARCHAR(100) NULL,
	Depth INT NOT NULL,
	DisplayName NVARCHAR(300) NOT NULL,
	SourceText NVARCHAR(1000) NULL,
	NodeTypeCode NVARCHAR(30) NOT NULL,
	AmbiguityTypeCode NVARCHAR(30) NULL,
	MaterialityCode NVARCHAR(20) NOT NULL,
	DecisionRoleCode NVARCHAR(30) NOT NULL,
	OperationalDefinition NVARCHAR(1000) NULL,
	MetricOrObservation NVARCHAR(500) NULL,
	EvidenceNeeded NVARCHAR(1000) NULL,
	EvidenceTypeCode NVARCHAR(40) NULL,
	PreferenceDirectionCode NVARCHAR(30) NOT NULL,
	IsLeaf BIT NOT NULL,
	ProposedConfidence DECIMAL(5,4) NULL,
	BranchStatusCode NVARCHAR(20) NOT NULL,
	Priority DECIMAL(9,6) NOT NULL CONSTRAINT DF_POLOXI_AmbiguityNode_Priority DEFAULT 0,
	EvidenceSupport DECIMAL(5,4) NOT NULL CONSTRAINT DF_POLOXI_AmbiguityNode_Evidence DEFAULT 0,
	InformationGain DECIMAL(5,4) NOT NULL CONSTRAINT DF_POLOXI_AmbiguityNode_Gain DEFAULT 0,
	DecisionImpact DECIMAL(5,4) NOT NULL CONSTRAINT DF_POLOXI_AmbiguityNode_Impact DEFAULT 0,
	ResidualUncertainty DECIMAL(5,4) NOT NULL CONSTRAINT DF_POLOXI_AmbiguityNode_Uncertainty DEFAULT 1,
	ResolutionReason NVARCHAR(1000) NULL,
	SemanticRoleCode NVARCHAR(40) NOT NULL CONSTRAINT DF_POLOXI_AmbiguityNode_Role DEFAULT N'COMPETING_INTERPRETATION',
	LeafReadinessScore DECIMAL(5,4) NULL,
	SortOrder INT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityNode_SortOrder DEFAULT 0,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_AmbiguityNode_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityNode_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT FK_POLOXI_AmbiguityNode_Run FOREIGN KEY(AmbiguityRunId) REFERENCES POLOXI.AmbiguityRun(AmbiguityRunId),
	CONSTRAINT CK_POLOXI_AmbiguityNode_Type CHECK(NodeTypeCode IN (N'ROOT',N'AMBIGUITY',N'INTERPRETATION',N'DIMENSION',N'SUB_DIMENSION',N'EVIDENCE_LEAF')),
	CONSTRAINT CK_POLOXI_AmbiguityNode_Status CHECK(BranchStatusCode IN (N'PROPOSED',N'ACTIVE',N'DORMANT',N'RESOLVED',N'REOPENED',N'INVALIDATED'))
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.AmbiguityNode') AND name=N'IX_POLOXI_AmbiguityNode_Run')
CREATE INDEX IX_POLOXI_AmbiguityNode_Run ON POLOXI.AmbiguityNode(TenantId,AmbiguityRunId,SortOrder) INCLUDE(NodeKey,ParentNodeKey,Depth,BranchStatusCode);

-- ── POLOXI.AmbiguityNodeDependency ────────────────────────────────────────────────────────────
-- Cross-tree relationships (the tree handles parent->child only). Foundation for stitching and
-- cross-dimension interaction scoring: only registered dependencies participate in interactions.
IF OBJECT_ID(N'POLOXI.AmbiguityNodeDependency',N'U') IS NULL
CREATE TABLE POLOXI.AmbiguityNodeDependency
(
	AmbiguityNodeDependencyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_AmbiguityNodeDependency PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NOT NULL,
	AmbiguityRunId UNIQUEIDENTIFIER NOT NULL,
	SourceNodeKey NVARCHAR(100) NOT NULL,
	TargetNodeKey NVARCHAR(100) NOT NULL,
	DependencyTypeCode NVARCHAR(30) NOT NULL,
	Reason NVARCHAR(1000) NULL,
	Strength DECIMAL(5,4) NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_AmbiguityNodeDependency_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityNodeDependency_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT FK_POLOXI_AmbiguityNodeDependency_Run FOREIGN KEY(AmbiguityRunId) REFERENCES POLOXI.AmbiguityRun(AmbiguityRunId),
	CONSTRAINT CK_POLOXI_AmbiguityNodeDependency_Type CHECK(DependencyTypeCode IN (N'DEPENDS_ON',N'MODIFIES',N'CONSTRAINS',N'OVERLAPS',N'CONFLICTS_WITH',N'PROVIDES_CONTEXT_FOR'))
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.AmbiguityNodeDependency') AND name=N'IX_POLOXI_AmbiguityNodeDependency_Run')
CREATE INDEX IX_POLOXI_AmbiguityNodeDependency_Run ON POLOXI.AmbiguityNodeDependency(TenantId,AmbiguityRunId);

-- ── POLOXI.AmbiguityValidationIssue ───────────────────────────────────────────────────────────
-- Deterministic validator findings per run/attempt for observability and repair prompting.
IF OBJECT_ID(N'POLOXI.AmbiguityValidationIssue',N'U') IS NULL
CREATE TABLE POLOXI.AmbiguityValidationIssue
(
	AmbiguityValidationIssueId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_AmbiguityValidationIssue PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NOT NULL,
	AmbiguityRunId UNIQUEIDENTIFIER NOT NULL,
	AttemptNumber INT NOT NULL,
	IssueCode NVARCHAR(50) NOT NULL,
	SeverityCode NVARCHAR(20) NOT NULL,
	NodeKey NVARCHAR(100) NULL,
	IssueMessage NVARCHAR(1000) NOT NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_AmbiguityValidationIssue_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityValidationIssue_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT FK_POLOXI_AmbiguityValidationIssue_Run FOREIGN KEY(AmbiguityRunId) REFERENCES POLOXI.AmbiguityRun(AmbiguityRunId),
	CONSTRAINT CK_POLOXI_AmbiguityValidationIssue_Severity CHECK(SeverityCode IN (N'ERROR',N'WARNING'))
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.AmbiguityValidationIssue') AND name=N'IX_POLOXI_AmbiguityValidationIssue_Run')
CREATE INDEX IX_POLOXI_AmbiguityValidationIssue_Run ON POLOXI.AmbiguityValidationIssue(TenantId,AmbiguityRunId,AttemptNumber);

-- ── POLOXI.AmbiguityModelInvocation ───────────────────────────────────────────────────────────
-- Per-LLM-call observability: model, task, prompt strategy/version, tokens, latency, validity,
-- retry position, and escalation lineage. This is the raw data for capability learning.
IF OBJECT_ID(N'POLOXI.AmbiguityModelInvocation',N'U') IS NULL
CREATE TABLE POLOXI.AmbiguityModelInvocation
(
	AmbiguityModelInvocationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_POLOXI_AmbiguityModelInvocation PRIMARY KEY DEFAULT NEWID(),
	TenantId UNIQUEIDENTIFIER NOT NULL,
	AmbiguityRunId UNIQUEIDENTIFIER NOT NULL,
	TaskTypeCode NVARCHAR(40) NOT NULL,
	ModelCode NVARCHAR(100) NULL,
	PromptPurposeCode NVARCHAR(50) NOT NULL,
	PromptScaffoldingCode NVARCHAR(20) NOT NULL,
	PromptVersionNumber INT NOT NULL,
	InputTokenCount INT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityModelInvocation_In DEFAULT 0,
	OutputTokenCount INT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityModelInvocation_Out DEFAULT 0,
	DurationMilliseconds BIGINT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityModelInvocation_Duration DEFAULT 0,
	IsSuccess BIT NOT NULL,
	IsSchemaValid BIT NOT NULL,
	RetryNumber INT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityModelInvocation_Retry DEFAULT 0,
	EscalatedFromModelCode NVARCHAR(100) NULL,
	FailureMessage NVARCHAR(1000) NULL,
	CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_POLOXI_AmbiguityModelInvocation_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc DATETIME2 NULL,
	ModifiedByUserId UNIQUEIDENTIFIER NULL,
	IsDeleted BIT NOT NULL CONSTRAINT DF_POLOXI_AmbiguityModelInvocation_IsDeleted DEFAULT 0,
	RowVersion ROWVERSION NOT NULL,
	CONSTRAINT FK_POLOXI_AmbiguityModelInvocation_Run FOREIGN KEY(AmbiguityRunId) REFERENCES POLOXI.AmbiguityRun(AmbiguityRunId)
);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'POLOXI.AmbiguityModelInvocation') AND name=N'IX_POLOXI_AmbiguityModelInvocation_Run')
CREATE INDEX IX_POLOXI_AmbiguityModelInvocation_Run ON POLOXI.AmbiguityModelInvocation(TenantId,AmbiguityRunId) INCLUDE(TaskTypeCode,ModelCode,IsSuccess,IsSchemaValid);

COMMIT TRANSACTION;
