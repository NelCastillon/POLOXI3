SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'AI') EXEC(N'CREATE SCHEMA AI');

IF OBJECT_ID(N'AI.IntelligencePillar', N'U') IS NULL
BEGIN
	CREATE TABLE AI.IntelligencePillar
	(
		IntelligencePillarId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_IntelligencePillar PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NULL,
		PillarCode NVARCHAR(100) NOT NULL,
		DisplayName NVARCHAR(200) NOT NULL,
		Description NVARCHAR(2000) NOT NULL,
		SortOrder INT NOT NULL,
		IsActive BIT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_IntelligencePillar_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_IntelligencePillar_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL
	);
	CREATE UNIQUE INDEX UX_AI_IntelligencePillar_Code ON AI.IntelligencePillar(PillarCode) WHERE TenantId IS NULL AND IsDeleted = 0;
END;

IF OBJECT_ID(N'AI.IntelligenceCapability', N'U') IS NULL
BEGIN
	CREATE TABLE AI.IntelligenceCapability
	(
		IntelligenceCapabilityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_IntelligenceCapability PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NULL,
		IntelligencePillarId UNIQUEIDENTIFIER NOT NULL,
		CapabilityCode NVARCHAR(100) NOT NULL,
		DisplayName NVARCHAR(200) NOT NULL,
		Description NVARCHAR(2000) NOT NULL,
		EngineKindCode NVARCHAR(50) NOT NULL,
		OwningModuleCode NVARCHAR(100) NOT NULL,
		IsAdvisory BIT NOT NULL,
		RequiresHumanReview BIT NOT NULL,
		SortOrder INT NOT NULL,
		IsActive BIT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_IntelligenceCapability_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_IntelligenceCapability_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_AI_IntelligenceCapability_Pillar FOREIGN KEY(IntelligencePillarId) REFERENCES AI.IntelligencePillar(IntelligencePillarId)
	);
	CREATE UNIQUE INDEX UX_AI_IntelligenceCapability_Code ON AI.IntelligenceCapability(CapabilityCode) WHERE TenantId IS NULL AND IsDeleted = 0;
	CREATE INDEX IX_AI_IntelligenceCapability_Pillar ON AI.IntelligenceCapability(IntelligencePillarId, SortOrder) INCLUDE(CapabilityCode, DisplayName, IsActive);
END;

IF OBJECT_ID(N'AI.EnginePolicy', N'U') IS NULL
BEGIN
	CREATE TABLE AI.EnginePolicy
	(
		EnginePolicyId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_EnginePolicy PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		IntelligenceCapabilityId UNIQUEIDENTIFIER NOT NULL,
		PolicyCode NVARCHAR(120) NOT NULL,
		DisplayName NVARCHAR(200) NOT NULL,
		Description NVARCHAR(2000) NOT NULL,
		ExecutionModeCode NVARCHAR(30) NOT NULL,
		ConfigurationJson NVARCHAR(MAX) NOT NULL,
		MinimumConfidence DECIMAL(5,4) NOT NULL,
		RequiresHumanReview BIT NOT NULL,
		FailClosed BIT NOT NULL,
		EffectiveFromUtc DATETIME2 NOT NULL,
		EffectiveToUtc DATETIME2 NULL,
		VersionNumber INT NOT NULL,
		IsActive BIT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_EnginePolicy_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_EnginePolicy_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_AI_EnginePolicy_Capability FOREIGN KEY(IntelligenceCapabilityId) REFERENCES AI.IntelligenceCapability(IntelligenceCapabilityId),
		CONSTRAINT CK_AI_EnginePolicy_Confidence CHECK(MinimumConfidence BETWEEN 0 AND 1),
		CONSTRAINT CK_AI_EnginePolicy_Version CHECK(VersionNumber > 0),
		CONSTRAINT CK_AI_EnginePolicy_Dates CHECK(EffectiveToUtc IS NULL OR EffectiveToUtc > EffectiveFromUtc),
		CONSTRAINT CK_AI_EnginePolicy_Json CHECK(ISJSON(ConfigurationJson) = 1)
	);
	CREATE UNIQUE INDEX UX_AI_EnginePolicy_CodeVersion ON AI.EnginePolicy(PolicyCode, VersionNumber) WHERE TenantId IS NULL AND IsDeleted = 0;
	CREATE INDEX IX_AI_EnginePolicy_Active ON AI.EnginePolicy(TenantId, IntelligenceCapabilityId, IsActive, EffectiveFromUtc DESC) INCLUDE(PolicyCode, ExecutionModeCode, MinimumConfidence, RequiresHumanReview);
END;

IF OBJECT_ID(N'AI.PromptDefinition', N'U') IS NULL
BEGIN
	CREATE TABLE AI.PromptDefinition
	(
		PromptDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_PromptDefinition PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		IntelligenceCapabilityId UNIQUEIDENTIFIER NOT NULL,
		PromptCode NVARCHAR(120) NOT NULL,
		VersionLabel NVARCHAR(30) NOT NULL,
		DisplayName NVARCHAR(200) NOT NULL,
		SystemInstructions NVARCHAR(MAX) NOT NULL,
		InputSchemaJson NVARCHAR(MAX) NOT NULL,
		OutputSchemaJson NVARCHAR(MAX) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		ApprovedByUserId UNIQUEIDENTIFIER NULL,
		ApprovedDateUtc DATETIME2 NULL,
		EffectiveFromUtc DATETIME2 NULL,
		EffectiveToUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_PromptDefinition_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_PromptDefinition_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_AI_PromptDefinition_Capability FOREIGN KEY(IntelligenceCapabilityId) REFERENCES AI.IntelligenceCapability(IntelligenceCapabilityId),
		CONSTRAINT CK_AI_PromptDefinition_Status CHECK(StatusCode IN(N'DRAFT', N'APPROVED', N'RETIRED')),
		CONSTRAINT CK_AI_PromptDefinition_InputJson CHECK(ISJSON(InputSchemaJson) = 1),
		CONSTRAINT CK_AI_PromptDefinition_OutputJson CHECK(ISJSON(OutputSchemaJson) = 1),
		CONSTRAINT CK_AI_PromptDefinition_Approval CHECK(StatusCode <> N'APPROVED' OR (ApprovedByUserId IS NOT NULL AND ApprovedDateUtc IS NOT NULL))
	);
	CREATE UNIQUE INDEX UX_AI_PromptDefinition_Version ON AI.PromptDefinition(PromptCode, VersionLabel) WHERE TenantId IS NULL AND IsDeleted = 0;
END;

IF OBJECT_ID(N'AI.SafetyControl', N'U') IS NULL
BEGIN
	CREATE TABLE AI.SafetyControl
	(
		SafetyControlId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_SafetyControl PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NULL,
		ControlCode NVARCHAR(120) NOT NULL,
		DisplayName NVARCHAR(200) NOT NULL,
		Description NVARCHAR(2000) NOT NULL,
		ControlTypeCode NVARCHAR(50) NOT NULL,
		EnforcementStageCode NVARCHAR(50) NOT NULL,
		ConfigurationJson NVARCHAR(MAX) NOT NULL,
		ViolationActionCode NVARCHAR(50) NOT NULL,
		RequiresHumanReview BIT NOT NULL,
		SortOrder INT NOT NULL,
		IsActive BIT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_SafetyControl_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_SafetyControl_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_AI_SafetyControl_Json CHECK(ISJSON(ConfigurationJson) = 1)
	);
	CREATE UNIQUE INDEX UX_AI_SafetyControl_Code ON AI.SafetyControl(ControlCode) WHERE TenantId IS NULL AND IsDeleted = 0;
END;

IF OBJECT_ID(N'AI.IntelligenceFinding', N'U') IS NULL
BEGIN
	CREATE TABLE AI.IntelligenceFinding
	(
		IntelligenceFindingId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_IntelligenceFinding PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntelligenceCapabilityId UNIQUEIDENTIFIER NOT NULL,
		EnginePolicyId UNIQUEIDENTIFIER NULL,
		ExecutionId UNIQUEIDENTIFIER NULL,
		EntityTypeCode NVARCHAR(100) NOT NULL,
		EntityId UNIQUEIDENTIFIER NOT NULL,
		FindingTypeCode NVARCHAR(100) NOT NULL,
		SeverityCode NVARCHAR(30) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		Title NVARCHAR(300) NOT NULL,
		Summary NVARCHAR(2000) NOT NULL,
		Explanation NVARCHAR(MAX) NOT NULL,
		Score DECIMAL(18,6) NULL,
		Confidence DECIMAL(5,4) NULL,
		RuleVersion NVARCHAR(50) NULL,
		IdempotencyKey NVARCHAR(240) NOT NULL,
		DetectedDateUtc DATETIME2 NOT NULL,
		DueDateUtc DATETIME2 NULL,
		ResolvedDateUtc DATETIME2 NULL,
		ResolvedByUserId UNIQUEIDENTIFIER NULL,
		ResolutionCode NVARCHAR(50) NULL,
		ResolutionNotes NVARCHAR(2000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_IntelligenceFinding_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_IntelligenceFinding_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_AI_IntelligenceFinding_Capability FOREIGN KEY(IntelligenceCapabilityId) REFERENCES AI.IntelligenceCapability(IntelligenceCapabilityId),
		CONSTRAINT FK_AI_IntelligenceFinding_Policy FOREIGN KEY(EnginePolicyId) REFERENCES AI.EnginePolicy(EnginePolicyId),
		CONSTRAINT FK_AI_IntelligenceFinding_Execution FOREIGN KEY(ExecutionId) REFERENCES AI.Execution(ExecutionId),
		CONSTRAINT CK_AI_IntelligenceFinding_Confidence CHECK(Confidence IS NULL OR Confidence BETWEEN 0 AND 1)
	);
	CREATE UNIQUE INDEX UX_AI_IntelligenceFinding_Idempotency ON AI.IntelligenceFinding(TenantId, IdempotencyKey) WHERE IsDeleted = 0;
	CREATE INDEX IX_AI_IntelligenceFinding_Entity ON AI.IntelligenceFinding(TenantId, EntityTypeCode, EntityId, StatusCode, DetectedDateUtc DESC) INCLUDE(IntelligenceCapabilityId, SeverityCode, Title, Score, Confidence);
END;

IF OBJECT_ID(N'AI.FindingEvidence', N'U') IS NULL
BEGIN
	CREATE TABLE AI.FindingEvidence
	(
		FindingEvidenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_FindingEvidence PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntelligenceFindingId UNIQUEIDENTIFIER NOT NULL,
		EvidenceTypeCode NVARCHAR(50) NOT NULL,
		SourceModuleCode NVARCHAR(100) NOT NULL,
		SourceEntityTypeCode NVARCHAR(100) NULL,
		SourceEntityId UNIQUEIDENTIFIER NULL,
		SourceReference NVARCHAR(2000) NOT NULL,
		Description NVARCHAR(2000) NOT NULL,
		EvidenceValueJson NVARCHAR(MAX) NULL,
		RelevanceScore DECIMAL(5,4) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_FindingEvidence_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_FindingEvidence_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_AI_FindingEvidence_Finding FOREIGN KEY(IntelligenceFindingId) REFERENCES AI.IntelligenceFinding(IntelligenceFindingId),
		CONSTRAINT CK_AI_FindingEvidence_Json CHECK(EvidenceValueJson IS NULL OR ISJSON(EvidenceValueJson) = 1),
		CONSTRAINT CK_AI_FindingEvidence_Relevance CHECK(RelevanceScore IS NULL OR RelevanceScore BETWEEN 0 AND 1)
	);
	CREATE INDEX IX_AI_FindingEvidence_Finding ON AI.FindingEvidence(TenantId, IntelligenceFindingId, CreatedDateUtc) INCLUDE(EvidenceTypeCode, SourceModuleCode, SourceEntityTypeCode, SourceEntityId);
END;

IF OBJECT_ID(N'AI.EntityRelationship', N'U') IS NULL
BEGIN
	CREATE TABLE AI.EntityRelationship
	(
		EntityRelationshipId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_EntityRelationship PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		SourceEntityTypeCode NVARCHAR(100) NOT NULL,
		SourceEntityId UNIQUEIDENTIFIER NOT NULL,
		RelationshipTypeCode NVARCHAR(100) NOT NULL,
		TargetEntityTypeCode NVARCHAR(100) NOT NULL,
		TargetEntityId UNIQUEIDENTIFIER NOT NULL,
		SourceModuleCode NVARCHAR(100) NOT NULL,
		SourceReference NVARCHAR(2000) NOT NULL,
		Strength DECIMAL(5,4) NOT NULL,
		EffectiveFromUtc DATETIME2 NULL,
		EffectiveToUtc DATETIME2 NULL,
		LastSynchronizedDateUtc DATETIME2 NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_EntityRelationship_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_EntityRelationship_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_AI_EntityRelationship_Strength CHECK(Strength BETWEEN 0 AND 1),
		CONSTRAINT CK_AI_EntityRelationship_Dates CHECK(EffectiveToUtc IS NULL OR EffectiveFromUtc IS NULL OR EffectiveToUtc > EffectiveFromUtc)
	);
	CREATE UNIQUE INDEX UX_AI_EntityRelationship_Edge ON AI.EntityRelationship(TenantId, SourceEntityTypeCode, SourceEntityId, RelationshipTypeCode, TargetEntityTypeCode, TargetEntityId) WHERE IsDeleted = 0;
	CREATE INDEX IX_AI_EntityRelationship_Target ON AI.EntityRelationship(TenantId, TargetEntityTypeCode, TargetEntityId, RelationshipTypeCode) INCLUDE(SourceEntityTypeCode, SourceEntityId, Strength);
END;

IF OBJECT_ID(N'AI.EntitySimilarity', N'U') IS NULL
BEGIN
	CREATE TABLE AI.EntitySimilarity
	(
		EntitySimilarityId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_EntitySimilarity PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode NVARCHAR(100) NOT NULL,
		SourceEntityId UNIQUEIDENTIFIER NOT NULL,
		SimilarEntityId UNIQUEIDENTIFIER NOT NULL,
		SimilarityModelCode NVARCHAR(100) NOT NULL,
		SimilarityModelVersion NVARCHAR(50) NOT NULL,
		SimilarityScore DECIMAL(5,4) NOT NULL,
		FeatureEvidenceJson NVARCHAR(MAX) NOT NULL,
		CalculatedDateUtc DATETIME2 NOT NULL,
		ExpiresDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_EntitySimilarity_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_EntitySimilarity_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT CK_AI_EntitySimilarity_Score CHECK(SimilarityScore BETWEEN 0 AND 1),
		CONSTRAINT CK_AI_EntitySimilarity_Json CHECK(ISJSON(FeatureEvidenceJson) = 1),
		CONSTRAINT CK_AI_EntitySimilarity_Different CHECK(SourceEntityId <> SimilarEntityId)
	);
	CREATE UNIQUE INDEX UX_AI_EntitySimilarity_Pair ON AI.EntitySimilarity(TenantId, EntityTypeCode, SourceEntityId, SimilarEntityId, SimilarityModelCode, SimilarityModelVersion) WHERE IsDeleted = 0;
	CREATE INDEX IX_AI_EntitySimilarity_Rank ON AI.EntitySimilarity(TenantId, EntityTypeCode, SourceEntityId, SimilarityScore DESC) INCLUDE(SimilarEntityId, SimilarityModelCode, CalculatedDateUtc);
END;

IF OBJECT_ID(N'AI.BusinessSignal', N'U') IS NULL
BEGIN
	CREATE TABLE AI.BusinessSignal
	(
		BusinessSignalId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_BusinessSignal PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntelligenceCapabilityId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode NVARCHAR(100) NOT NULL,
		EntityId UNIQUEIDENTIFIER NOT NULL,
		SignalTypeCode NVARCHAR(100) NOT NULL,
		SignalDateUtc DATETIME2 NOT NULL,
		SeverityCode NVARCHAR(30) NOT NULL,
		Score DECIMAL(18,6) NULL,
		Confidence DECIMAL(5,4) NULL,
		Title NVARCHAR(300) NOT NULL,
		Summary NVARCHAR(2000) NOT NULL,
		EvidenceJson NVARCHAR(MAX) NOT NULL,
		RecommendedActionCode NVARCHAR(100) NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		AssignedToUserId UNIQUEIDENTIFIER NULL,
		DueDateUtc DATETIME2 NULL,
		IdempotencyKey NVARCHAR(240) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_BusinessSignal_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_BusinessSignal_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_AI_BusinessSignal_Capability FOREIGN KEY(IntelligenceCapabilityId) REFERENCES AI.IntelligenceCapability(IntelligenceCapabilityId),
		CONSTRAINT CK_AI_BusinessSignal_Confidence CHECK(Confidence IS NULL OR Confidence BETWEEN 0 AND 1),
		CONSTRAINT CK_AI_BusinessSignal_Json CHECK(ISJSON(EvidenceJson) = 1)
	);
	CREATE UNIQUE INDEX UX_AI_BusinessSignal_Idempotency ON AI.BusinessSignal(TenantId, IdempotencyKey) WHERE IsDeleted = 0;
	CREATE INDEX IX_AI_BusinessSignal_Queue ON AI.BusinessSignal(TenantId, StatusCode, SeverityCode, SignalDateUtc DESC) INCLUDE(IntelligenceCapabilityId, EntityTypeCode, EntityId, Title, Score, AssignedToUserId);
END;

IF OBJECT_ID(N'AI.ReasoningSession', N'U') IS NULL
BEGIN
	CREATE TABLE AI.ReasoningSession
	(
		ReasoningSessionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_ReasoningSession PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		RequestedByUserId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode NVARCHAR(100) NOT NULL,
		EntityId UNIQUEIDENTIFIER NOT NULL,
		Question NVARCHAR(2000) NOT NULL,
		IntentCode NVARCHAR(100) NOT NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		CorrelationId NVARCHAR(120) NOT NULL,
		EnginePolicyId UNIQUEIDENTIFIER NULL,
		ExecutionId UNIQUEIDENTIFIER NULL,
		PermissionSnapshotHash CHAR(64) NOT NULL,
		StartedDateUtc DATETIME2 NOT NULL,
		CompletedDateUtc DATETIME2 NULL,
		Confidence DECIMAL(5,4) NULL,
		RequiresHumanReview BIT NOT NULL,
		FailureCode NVARCHAR(100) NULL,
		FailureMessage NVARCHAR(4000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_ReasoningSession_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_ReasoningSession_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_AI_ReasoningSession_Policy FOREIGN KEY(EnginePolicyId) REFERENCES AI.EnginePolicy(EnginePolicyId),
		CONSTRAINT FK_AI_ReasoningSession_Execution FOREIGN KEY(ExecutionId) REFERENCES AI.Execution(ExecutionId),
		CONSTRAINT CK_AI_ReasoningSession_Confidence CHECK(Confidence IS NULL OR Confidence BETWEEN 0 AND 1)
	);
	CREATE UNIQUE INDEX UX_AI_ReasoningSession_Correlation ON AI.ReasoningSession(TenantId, CorrelationId) WHERE IsDeleted = 0;
	CREATE INDEX IX_AI_ReasoningSession_Entity ON AI.ReasoningSession(TenantId, EntityTypeCode, EntityId, CreatedDateUtc DESC) INCLUDE(StatusCode, IntentCode, RequestedByUserId, Confidence, RequiresHumanReview);
END;

IF OBJECT_ID(N'AI.ReasoningEvidence', N'U') IS NULL
BEGIN
	CREATE TABLE AI.ReasoningEvidence
	(
		ReasoningEvidenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_ReasoningEvidence PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ReasoningSessionId UNIQUEIDENTIFIER NOT NULL,
		EvidenceTypeCode NVARCHAR(50) NOT NULL,
		SourceModuleCode NVARCHAR(100) NOT NULL,
		SourceEntityTypeCode NVARCHAR(100) NULL,
		SourceEntityId UNIQUEIDENTIFIER NULL,
		SourceReference NVARCHAR(2000) NOT NULL,
		Title NVARCHAR(500) NOT NULL,
		Summary NVARCHAR(2000) NOT NULL,
		EvidenceValueJson NVARCHAR(MAX) NULL,
		RelevanceScore DECIMAL(5,4) NOT NULL,
		IsAuthoritative BIT NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_ReasoningEvidence_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_ReasoningEvidence_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_AI_ReasoningEvidence_Session FOREIGN KEY(ReasoningSessionId) REFERENCES AI.ReasoningSession(ReasoningSessionId),
		CONSTRAINT CK_AI_ReasoningEvidence_Relevance CHECK(RelevanceScore BETWEEN 0 AND 1),
		CONSTRAINT CK_AI_ReasoningEvidence_Json CHECK(EvidenceValueJson IS NULL OR ISJSON(EvidenceValueJson) = 1)
	);
	CREATE INDEX IX_AI_ReasoningEvidence_Session ON AI.ReasoningEvidence(TenantId, ReasoningSessionId, RelevanceScore DESC) INCLUDE(EvidenceTypeCode, SourceModuleCode, IsAuthoritative);
END;

IF OBJECT_ID(N'AI.ReasoningConclusion', N'U') IS NULL
BEGIN
	CREATE TABLE AI.ReasoningConclusion
	(
		ReasoningConclusionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_ReasoningConclusion PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ReasoningSessionId UNIQUEIDENTIFIER NOT NULL,
		ConclusionCode NVARCHAR(100) NOT NULL,
		SequenceNumber INT NOT NULL,
		Title NVARCHAR(300) NOT NULL,
		Explanation NVARCHAR(MAX) NOT NULL,
		RuleCode NVARCHAR(120) NULL,
		RuleVersion NVARCHAR(50) NULL,
		Confidence DECIMAL(5,4) NOT NULL,
		IsBlocking BIT NOT NULL,
		CanBeWaived BIT NOT NULL,
		WaiverPermissionCode NVARCHAR(150) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_ReasoningConclusion_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_ReasoningConclusion_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_AI_ReasoningConclusion_Session FOREIGN KEY(ReasoningSessionId) REFERENCES AI.ReasoningSession(ReasoningSessionId),
		CONSTRAINT CK_AI_ReasoningConclusion_Sequence CHECK(SequenceNumber > 0),
		CONSTRAINT CK_AI_ReasoningConclusion_Confidence CHECK(Confidence BETWEEN 0 AND 1)
	);
	CREATE UNIQUE INDEX UX_AI_ReasoningConclusion_Sequence ON AI.ReasoningConclusion(TenantId, ReasoningSessionId, SequenceNumber) WHERE IsDeleted = 0;
END;

IF OBJECT_ID(N'AI.ReasoningAction', N'U') IS NULL
BEGIN
	CREATE TABLE AI.ReasoningAction
	(
		ReasoningActionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_ReasoningAction PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ReasoningSessionId UNIQUEIDENTIFIER NOT NULL,
		SequenceNumber INT NOT NULL,
		ActionCode NVARCHAR(100) NOT NULL,
		DisplayName NVARCHAR(300) NOT NULL,
		Description NVARCHAR(2000) NOT NULL,
		TargetRoute NVARCHAR(1000) NULL,
		RequiredPermissionCode NVARCHAR(150) NULL,
		RequiresConfirmation BIT NOT NULL,
		IsAvailable BIT NOT NULL,
		UnavailableReason NVARCHAR(1000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_ReasoningAction_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_ReasoningAction_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_AI_ReasoningAction_Session FOREIGN KEY(ReasoningSessionId) REFERENCES AI.ReasoningSession(ReasoningSessionId),
		CONSTRAINT CK_AI_ReasoningAction_Sequence CHECK(SequenceNumber > 0)
	);
	CREATE UNIQUE INDEX UX_AI_ReasoningAction_Sequence ON AI.ReasoningAction(TenantId, ReasoningSessionId, SequenceNumber) WHERE IsDeleted = 0;
END;

IF OBJECT_ID(N'AI.IntelligenceWorkItem', N'U') IS NULL
BEGIN
	CREATE TABLE AI.IntelligenceWorkItem
	(
		IntelligenceWorkItemId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_IntelligenceWorkItem PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		IntelligenceCapabilityId UNIQUEIDENTIFIER NOT NULL,
		WorkTypeCode NVARCHAR(100) NOT NULL,
		EntityTypeCode NVARCHAR(100) NULL,
		EntityId UNIQUEIDENTIFIER NULL,
		StatusCode NVARCHAR(30) NOT NULL,
		Priority INT NOT NULL,
		IdempotencyKey NVARCHAR(240) NOT NULL,
		PayloadJson NVARCHAR(MAX) NOT NULL,
		AttemptCount INT NOT NULL,
		MaximumAttempts INT NOT NULL,
		AvailableDateUtc DATETIME2 NOT NULL,
		LeaseOwner NVARCHAR(200) NULL,
		LeaseExpiresDateUtc DATETIME2 NULL,
		CompletedDateUtc DATETIME2 NULL,
		LastError NVARCHAR(4000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_IntelligenceWorkItem_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_IntelligenceWorkItem_Deleted DEFAULT 0,
		RowVersion ROWVERSION NOT NULL,
		CONSTRAINT FK_AI_IntelligenceWorkItem_Capability FOREIGN KEY(IntelligenceCapabilityId) REFERENCES AI.IntelligenceCapability(IntelligenceCapabilityId),
		CONSTRAINT CK_AI_IntelligenceWorkItem_Payload CHECK(ISJSON(PayloadJson) = 1),
		CONSTRAINT CK_AI_IntelligenceWorkItem_Attempts CHECK(AttemptCount >= 0 AND MaximumAttempts > 0),
		CONSTRAINT CK_AI_IntelligenceWorkItem_Priority CHECK(Priority BETWEEN 1 AND 100)
	);
	CREATE UNIQUE INDEX UX_AI_IntelligenceWorkItem_Idempotency ON AI.IntelligenceWorkItem(TenantId, IdempotencyKey) WHERE IsDeleted = 0;
	CREATE INDEX IX_AI_IntelligenceWorkItem_Queue ON AI.IntelligenceWorkItem(StatusCode, AvailableDateUtc, Priority DESC) INCLUDE(TenantId, IntelligenceCapabilityId, WorkTypeCode, AttemptCount, MaximumAttempts, LeaseExpiresDateUtc);
END;

DECLARE @Pillars TABLE(PillarId UNIQUEIDENTIFIER, PillarCode NVARCHAR(100), DisplayName NVARCHAR(200), Description NVARCHAR(2000), SortOrder INT);
INSERT @Pillars VALUES
('91000000-0000-0000-0000-000000000001',N'KNOWLEDGE_UNDERSTANDING',N'Knowledge & Understanding',N'Canonical insurance knowledge, semantic interoperability, grounded context, document understanding, and governed ontology management.',1),
('91000000-0000-0000-0000-000000000002',N'DECISION_INTELLIGENCE',N'Decision Intelligence',N'Deterministic, explainable, advisory risk, compliance, validation, and recommendation capabilities.',2),
('91000000-0000-0000-0000-000000000003',N'SEARCH_DISCOVERY',N'Search & Discovery',N'Permission-aware hybrid discovery across indexed entities, relationships, and evidence-based similarity.',3),
('91000000-0000-0000-0000-000000000004',N'AI_OPERATIONS',N'AI Operations',N'Governed prompts, providers, executions, evaluations, safety controls, approvals, and audit evidence.',4),
('91000000-0000-0000-0000-000000000005',N'BUSINESS_INTELLIGENCE',N'Business Intelligence',N'Advisory workflow, renewal, claims, producer, and customer signals derived from authoritative AMS activity.',5);
MERGE AI.IntelligencePillar AS target USING @Pillars AS source ON target.IntelligencePillarId = source.PillarId
WHEN MATCHED THEN UPDATE SET PillarCode=source.PillarCode,DisplayName=source.DisplayName,Description=source.Description,SortOrder=source.SortOrder,IsActive=1,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(IntelligencePillarId,TenantId,PillarCode,DisplayName,Description,SortOrder,IsActive,CreatedDateUtc,IsDeleted) VALUES(source.PillarId,NULL,source.PillarCode,source.DisplayName,source.Description,source.SortOrder,1,SYSUTCDATETIME(),0);

DECLARE @Capabilities TABLE(CapabilityId UNIQUEIDENTIFIER, PillarId UNIQUEIDENTIFIER, CapabilityCode NVARCHAR(100), DisplayName NVARCHAR(200), Description NVARCHAR(2000), EngineKindCode NVARCHAR(50), ModuleCode NVARCHAR(100), IsAdvisory BIT, RequiresReview BIT, SortOrder INT);
INSERT @Capabilities VALUES
('92000000-0000-0000-0000-000000000001','91000000-0000-0000-0000-000000000001',N'KNOWLEDGE_REPOSITORY',N'Knowledge Repository',N'Canonical insurance concepts, labels, versions, hierarchies, relationships, provenance, publication, and governance.',N'KNOWLEDGE',N'Knowledge',0,1,1),
('92000000-0000-0000-0000-000000000002','91000000-0000-0000-0000-000000000001',N'SEMANTIC_MAPPING',N'Semantic Mapping Engine',N'Maps approved external and carrier terminology to canonical AgencyBinder concepts.',N'DETERMINISTIC',N'Knowledge',0,1,2),
('92000000-0000-0000-0000-000000000003','91000000-0000-0000-0000-000000000001',N'AI_GROUNDING',N'AI Grounding Engine',N'Builds permission-filtered, source-linked context for governed AI operations.',N'HYBRID',N'AI',1,0,3),
('92000000-0000-0000-0000-000000000004','91000000-0000-0000-0000-000000000001',N'DOCUMENT_INTELLIGENCE',N'Document Intelligence Engine',N'OCR, classification, extraction, normalization, exception handling, and human-reviewed document understanding.',N'HYBRID',N'DMS',1,1,4),
('92000000-0000-0000-0000-000000000005','91000000-0000-0000-0000-000000000001',N'ONTOLOGY_MANAGER',N'Ontology Manager',N'Governs insurance concept schemes, hierarchy closure, semantic rules, versions, and publication.',N'KNOWLEDGE',N'Knowledge',0,1,5),
('92000000-0000-0000-0000-000000000006','91000000-0000-0000-0000-000000000002',N'DETERMINISTIC_RULES',N'Deterministic Rules Engine',N'Evaluates versioned business rules for validation, eligibility, calculations, and workflow decisions.',N'DETERMINISTIC',N'Rules',0,0,1),
('92000000-0000-0000-0000-000000000007','91000000-0000-0000-0000-000000000002',N'RECOMMENDATION',N'Recommendation Engine',N'Produces evidence-linked advisory coverage, cross-sell, document, endorsement, and next-step recommendations.',N'HYBRID',N'AI',1,1,2),
('92000000-0000-0000-0000-000000000008','91000000-0000-0000-0000-000000000002',N'RISK_INTELLIGENCE',N'Risk Intelligence Engine',N'Analyzes risk characteristics, completeness, losses, policy trends, and account risk without replacing underwriting.',N'HYBRID',N'Risk',1,1,3),
('92000000-0000-0000-0000-000000000009','91000000-0000-0000-0000-000000000002',N'COMPLIANCE_INTELLIGENCE',N'Compliance Intelligence Engine',N'Checks approved state, carrier, licensing, disclosure, and internal compliance requirements.',N'DETERMINISTIC',N'Compliance',1,1,4),
('92000000-0000-0000-0000-000000000010','91000000-0000-0000-0000-000000000002',N'EXPLAINABILITY',N'Explainability Engine',N'Explains triggered rules, evidence, thresholds, versions, approvals, waivers, and next actions.',N'DETERMINISTIC',N'AI',1,0,5),
('92000000-0000-0000-0000-000000000011','91000000-0000-0000-0000-000000000003',N'SEARCH_INTELLIGENCE',N'Search Intelligence',N'Permission-aware keyword, semantic, natural-language, module, entity, and recency-aware search.',N'HYBRID',N'AI',0,0,1),
('92000000-0000-0000-0000-000000000012','91000000-0000-0000-0000-000000000003',N'RELATIONSHIP_ENGINE',N'Relationship Engine',N'Projects source-linked relationships among customers, accounts, policies, claims, risks, documents, and workflow records.',N'DETERMINISTIC',N'AI',0,0,2),
('92000000-0000-0000-0000-000000000013','91000000-0000-0000-0000-000000000003',N'SIMILARITY_ENGINE',N'Similarity Engine',N'Finds evidence-based similar submissions, claims, accounts, and losses using versioned models.',N'HYBRID',N'AI',1,0,3),
('92000000-0000-0000-0000-000000000014','91000000-0000-0000-0000-000000000004',N'PROMPT_REGISTRY',N'Prompt Registry',N'Version-controls schemas and approved prompts with effective dates and approvals.',N'GOVERNANCE',N'AI',0,1,1),
('92000000-0000-0000-0000-000000000015','91000000-0000-0000-0000-000000000004',N'AI_CONFIGURATION',N'AI Configuration Center',N'Controls providers, deployments, routes, limits, confidence thresholds, timeouts, and review policy.',N'GOVERNANCE',N'AI',0,1,2),
('92000000-0000-0000-0000-000000000016','91000000-0000-0000-0000-000000000004',N'AI_EXECUTION',N'AI Execution Center',N'Tracks governed requests, model use, duration, cost, confidence, grounding, status, and approval evidence.',N'GOVERNANCE',N'AI',0,1,3),
('92000000-0000-0000-0000-000000000017','91000000-0000-0000-0000-000000000004',N'AI_EVALUATION',N'AI Evaluation Platform',N'Measures configured accuracy, precision, recall, correction, hallucination proxy, and success metrics.',N'GOVERNANCE',N'AI',0,1,4),
('92000000-0000-0000-0000-000000000018','91000000-0000-0000-0000-000000000004',N'AI_SAFETY_GOVERNANCE',N'AI Safety & Governance',N'Enforces prompt-injection, sensitive-data, output-schema, authorization, approval, and audit controls.',N'GOVERNANCE',N'AI',0,1,5),
('92000000-0000-0000-0000-000000000019','91000000-0000-0000-0000-000000000005',N'WORKFLOW_INTELLIGENCE',N'Workflow Intelligence',N'Highlights bottlenecks, delays, handoff patterns, and approval concentration using authoritative workflow activity.',N'DETERMINISTIC',N'Workflow',1,0,1),
('92000000-0000-0000-0000-000000000020','91000000-0000-0000-0000-000000000005',N'RENEWAL_INTELLIGENCE',N'Renewal Intelligence',N'Prioritizes renewal readiness, missing items, retention risk, and service actions from policy and renewal evidence.',N'HYBRID',N'Policy',1,1,2),
('92000000-0000-0000-0000-000000000021','91000000-0000-0000-0000-000000000005',N'CLAIMS_INTELLIGENCE',N'Claims Intelligence',N'Highlights large-loss patterns, rule-assisted indicators, similar claims, and missing documentation without adjudication.',N'HYBRID',N'Claims',1,1,3),
('92000000-0000-0000-0000-000000000022','91000000-0000-0000-0000-000000000005',N'PRODUCER_INTELLIGENCE',N'Producer Intelligence',N'Provides permission-scoped pipeline, follow-up, cross-sell, renewal priority, and productivity signals.',N'DETERMINISTIC',N'CRM',1,0,4),
('92000000-0000-0000-0000-000000000023','91000000-0000-0000-0000-000000000005',N'CUSTOMER_INTELLIGENCE',N'Customer Intelligence',N'Builds an evidence-linked customer 360 with relationship timeline, coverage gaps, communications, and policy trends.',N'HYBRID',N'Client',1,1,5),
('92000000-0000-0000-0000-000000000024','91000000-0000-0000-0000-000000000002',N'INSURANCE_REASONING',N'Insurance Reasoning Engine',N'Combines authorized transaction evidence, approved concepts, deterministic rules, carrier requirements, documents, history, and permissions into explainable guidance.',N'HYBRID',N'AI',1,1,6);
MERGE AI.IntelligenceCapability AS target USING @Capabilities AS source ON target.IntelligenceCapabilityId = source.CapabilityId
WHEN MATCHED THEN UPDATE SET IntelligencePillarId=source.PillarId,CapabilityCode=source.CapabilityCode,DisplayName=source.DisplayName,Description=source.Description,EngineKindCode=source.EngineKindCode,OwningModuleCode=source.ModuleCode,IsAdvisory=source.IsAdvisory,RequiresHumanReview=source.RequiresReview,SortOrder=source.SortOrder,IsActive=1,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(IntelligenceCapabilityId,TenantId,IntelligencePillarId,CapabilityCode,DisplayName,Description,EngineKindCode,OwningModuleCode,IsAdvisory,RequiresHumanReview,SortOrder,IsActive,CreatedDateUtc,IsDeleted) VALUES(source.CapabilityId,NULL,source.PillarId,source.CapabilityCode,source.DisplayName,source.Description,source.EngineKindCode,source.ModuleCode,source.IsAdvisory,source.RequiresReview,source.SortOrder,1,SYSUTCDATETIME(),0);

MERGE AI.EnginePolicy AS target
USING (SELECT IntelligenceCapabilityId, CapabilityCode + N'_DEFAULT' AS PolicyCode, DisplayName + N' default policy' AS DisplayName, Description, CASE WHEN EngineKindCode=N'GOVERNANCE' THEN N'ON_DEMAND' ELSE N'BACKGROUND' END AS ExecutionModeCode, RequiresHumanReview FROM AI.IntelligenceCapability WHERE TenantId IS NULL AND IsDeleted=0) AS source
ON target.TenantId IS NULL AND target.PolicyCode=source.PolicyCode AND target.VersionNumber=1
WHEN MATCHED THEN UPDATE SET IntelligenceCapabilityId=source.IntelligenceCapabilityId,DisplayName=source.DisplayName,Description=source.Description,ExecutionModeCode=source.ExecutionModeCode,RequiresHumanReview=source.RequiresHumanReview,IsActive=1,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(TenantId,IntelligenceCapabilityId,PolicyCode,DisplayName,Description,ExecutionModeCode,ConfigurationJson,MinimumConfidence,RequiresHumanReview,FailClosed,EffectiveFromUtc,VersionNumber,IsActive,CreatedDateUtc,IsDeleted) VALUES(NULL,source.IntelligenceCapabilityId,source.PolicyCode,source.DisplayName,source.Description,source.ExecutionModeCode,N'{"batchSize":25,"evidenceRequired":true}',0.7000,source.RequiresHumanReview,1,CONVERT(DATETIME2,N'2026-01-01T00:00:00'),1,1,SYSUTCDATETIME(),0);

DECLARE @Safety TABLE(ControlCode NVARCHAR(120),DisplayName NVARCHAR(200),Description NVARCHAR(2000),ControlTypeCode NVARCHAR(50),StageCode NVARCHAR(50),ActionCode NVARCHAR(50),RequiresReview BIT,SortOrder INT);
INSERT @Safety VALUES
(N'PROMPT_INJECTION_DETECTION',N'Prompt injection detection',N'Detect and block instructions that attempt to bypass approved system policy or retrieve restricted context.',N'INPUT_VALIDATION',N'PRE_EXECUTION',N'BLOCK',1,1),
(N'PERMISSION_SCOPED_GROUNDING',N'Permission-scoped grounding',N'Only sources authorized for the requesting tenant and user may enter grounding context.',N'AUTHORIZATION',N'GROUNDING',N'BLOCK',0,2),
(N'SENSITIVE_DATA_MINIMIZATION',N'Sensitive data minimization',N'Minimize sensitive data sent to providers and reject prohibited data classes according to tenant policy.',N'DATA_PROTECTION',N'GROUNDING',N'REDACT',1,3),
(N'STRUCTURED_OUTPUT_VALIDATION',N'Structured output validation',N'Validate model output against the approved prompt output schema before persistence or display.',N'OUTPUT_VALIDATION',N'POST_EXECUTION',N'REJECT',1,4),
(N'GROUNDED_CLAIM_VALIDATION',N'Grounded claim validation',N'Require every material conclusion to cite authoritative evidence or be labeled as an unverified suggestion.',N'GROUNDING',N'POST_EXECUTION',N'REVIEW',1,5),
(N'REGULATED_DECISION_GUARD',N'Regulated decision guard',N'Prevent intelligence output from autonomously binding, denying, adjudicating, or changing regulated records.',N'ACTION_GUARD',N'PRE_ACTION',N'BLOCK',1,6),
(N'HUMAN_APPROVAL_GATE',N'Human approval gate',N'Require authorized human approval when capability, policy, confidence, or safety controls require review.',N'APPROVAL',N'PRE_ACTION',N'REVIEW',1,7),
(N'AUDIT_EVIDENCE_REQUIRED',N'Audit evidence required',N'Require correlation, configuration version, execution, evidence, and decision records for governed operations.',N'AUDIT',N'PERSISTENCE',N'BLOCK',0,8);
MERGE AI.SafetyControl AS target USING @Safety AS source ON target.TenantId IS NULL AND target.ControlCode=source.ControlCode
WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,Description=source.Description,ControlTypeCode=source.ControlTypeCode,EnforcementStageCode=source.StageCode,ViolationActionCode=source.ActionCode,RequiresHumanReview=source.RequiresReview,SortOrder=source.SortOrder,IsActive=1,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(TenantId,ControlCode,DisplayName,Description,ControlTypeCode,EnforcementStageCode,ConfigurationJson,ViolationActionCode,RequiresHumanReview,SortOrder,IsActive,CreatedDateUtc,IsDeleted) VALUES(NULL,source.ControlCode,source.DisplayName,source.Description,source.ControlTypeCode,source.StageCode,N'{"enabled":true}',source.ActionCode,source.RequiresReview,source.SortOrder,1,SYSUTCDATETIME(),0);

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Config TABLE(SettingKey NVARCHAR(200),Value NVARCHAR(2000),DataType NVARCHAR(50),Name NVARCHAR(200),Description NVARCHAR(1000));
	INSERT @Config VALUES
	(N'Intelligence.Platform.WorkerBatchSize',N'25',N'Integer',N'Platform worker batch size',N'Maximum cross-pillar work items leased per processing cycle.'),
	(N'Intelligence.Platform.WorkerIntervalSeconds',N'30',N'Integer',N'Platform worker interval',N'Cross-pillar projection and engine evaluation polling interval.'),
	(N'Intelligence.Platform.LeaseSeconds',N'300',N'Integer',N'Platform work lease',N'Exclusive lease duration for cross-pillar work items.'),
	(N'Intelligence.Relationship.MaximumDepth',N'5',N'Integer',N'Relationship traversal depth',N'Maximum relationship traversal depth for permission-aware discovery.'),
	(N'Intelligence.Similarity.MinimumScore',N'0.70',N'Decimal',N'Minimum similarity score',N'Minimum configured similarity score returned to users.'),
	(N'Intelligence.Reasoning.MinimumConfidence',N'0.70',N'Decimal',N'Reasoning confidence threshold',N'Below this threshold reasoning output requires human review.'),
	(N'Intelligence.Reasoning.MaximumEvidenceItems',N'50',N'Integer',N'Reasoning evidence limit',N'Maximum authorized evidence items assembled for one reasoning session.');
	MERGE Core.ConfigurationSetting AS target USING @Config AS source ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey
	WHEN MATCHED THEN UPDATE SET ModuleCode=N'Intelligence',DefaultValue=source.Value,DataTypeCode=source.DataType,Description=source.Name+N'. '+source.Description,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,IsDeleted,CreatedDateUtc) VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.Value,source.Value,source.DataType,source.Name+N'. '+source.Description,0,0,0,SYSUTCDATETIME());
END;

IF OBJECT_ID(N'Master.PermissionAction',N'U') IS NOT NULL AND OBJECT_ID(N'IAM.Permission',N'U') IS NOT NULL
BEGIN
	DECLARE @Permissions TABLE(PermissionCode NVARCHAR(150),PermissionName NVARCHAR(200),ResourceCode NVARCHAR(100),ActionCode NVARCHAR(50),Description NVARCHAR(500));
	INSERT @Permissions VALUES
	(N'Intelligence.Analyze',N'Run intelligence analysis',N'AI.Analysis',N'WRITE',N'Queue configured risk, compliance, relationship, similarity, and business intelligence analysis.'),
	(N'Intelligence.Reason',N'Use insurance reasoning',N'AI.Reasoning',N'WRITE',N'Run permission-aware, evidence-linked insurance reasoning sessions.'),
	(N'Intelligence.Findings.Read',N'View intelligence findings',N'AI.Finding',N'READ',N'View authorized risk, compliance, workflow, renewal, claims, producer, and customer findings.'),
	(N'Intelligence.Findings.Review',N'Review intelligence findings',N'AI.Finding',N'APPROVE',N'Review, resolve, waive, or dismiss intelligence findings with audit evidence.'),
	(N'Intelligence.Relationships.Read',N'View entity relationships',N'AI.Relationship',N'READ',N'Explore permission-aware entity relationships and similarity results.'),
	(N'Intelligence.Governance.Manage',N'Manage intelligence governance',N'AI.Governance',N'MANAGE',N'Manage pillar capabilities, engine policies, prompts, and safety controls.');
	UPDATE existing SET PermissionName=source.PermissionName,ResourceCode=source.ResourceCode,ActionCode=source.ActionCode,ModuleCode=N'AI',Description=source.Description,PermissionActionId=COALESCE(actionRow.PermissionActionId,existing.PermissionActionId),IsBuiltIn=1,IsActive=1,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
	FROM IAM.Permission existing JOIN @Permissions source ON source.PermissionCode=existing.PermissionCode
	OUTER APPLY(SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE UPPER(ActionCode)=source.ActionCode OR UPPER(ActionName)=source.ActionCode ORDER BY PermissionActionId) actionRow;
	INSERT IAM.Permission(PermissionId,TenantId,PermissionCode,PermissionActionId,PermissionName,ResourceCode,ActionCode,ModuleCode,Description,IsBuiltIn,IsActive,CreatedDateUtc,IsDeleted)
	SELECT NEWID(),seedTenant.TenantId,source.PermissionCode,COALESCE(actionRow.PermissionActionId,readAction.PermissionActionId,1),source.PermissionName,source.ResourceCode,source.ActionCode,N'AI',source.Description,1,1,SYSUTCDATETIME(),0
	FROM @Permissions source CROSS APPLY(SELECT TOP 1 TenantId FROM Core.Tenant ORDER BY TenantId) seedTenant
	OUTER APPLY(SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE UPPER(ActionCode)=source.ActionCode OR UPPER(ActionName)=source.ActionCode ORDER BY PermissionActionId) actionRow
	OUTER APPLY(SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE UPPER(ActionCode)=N'READ' OR UPPER(ActionName)=N'READ' ORDER BY PermissionActionId) readAction
	WHERE NOT EXISTS(SELECT 1 FROM IAM.Permission existing WHERE existing.PermissionCode=source.PermissionCode);
	INSERT IAM.RolePermission(RolePermissionId,TenantId,RoleId,PermissionId,PermissionCode,GrantedDateUtc,CreatedDateUtc,IsDeleted)
	SELECT NEWID(),role.TenantId,role.RoleId,permission.PermissionId,permission.PermissionCode,SYSUTCDATETIME(),SYSUTCDATETIME(),0
	FROM IAM.Role role CROSS JOIN IAM.Permission permission
	WHERE permission.PermissionCode IN(SELECT PermissionCode FROM @Permissions) AND permission.IsDeleted=0 AND role.RoleCode IN(N'SYSTEM_ADMIN',N'TENANT_ADMIN',N'ADMINISTRATOR') AND role.IsDeleted=0
	AND NOT EXISTS(SELECT 1 FROM IAM.RolePermission existing WHERE existing.TenantId=role.TenantId AND existing.RoleId=role.RoleId AND (existing.PermissionId=permission.PermissionId OR existing.PermissionCode=permission.PermissionCode) AND existing.IsDeleted=0);
END;
