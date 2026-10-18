SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0436: Matter-funneled Domain Pack L2 -> L3 (accuracy-first).
--   * Legal_WideBranchScope.RelevanceReasons: auditable, deterministic matter-relevance reasons per L2
--     (PriorityScore/TierCode were already present but never populated).
--   * Legal_WideBranchPassageMap: persisted per-execution top-N passage -> L2 mapping, computed ONCE by
--     bounded workers before batching so a 5,000-page matter is never rescanned per L3 batch.
--   * Settings: relevance tiering (context density only; NEVER an inclusion gate - every in-scope L2 still
--     receives a model pass), passage-map concurrency/limits.
--   * WIDE_HIERARCHY_STEP v3.22: explicit MECE decomposition + graph relationship (dependsOn/equivalentTo)
--     contract appended to the approved v3.21 text.
-- ============================================================================
BEGIN TRANSACTION;

IF OBJECT_ID(N'POLOXI.Legal_WideBranchScope',N'U') IS NOT NULL AND COL_LENGTH(N'POLOXI.Legal_WideBranchScope',N'RelevanceReasons') IS NULL
	ALTER TABLE POLOXI.Legal_WideBranchScope ADD RelevanceReasons NVARCHAR(2000) NULL;

IF OBJECT_ID(N'POLOXI.Legal_WideBranchPassageMap',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_WideBranchPassageMap
	(
		WideBranchPassageMapId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_WideBranchPassageMap PRIMARY KEY,
		WideExecutionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_WideBranchPassageMap_Execution REFERENCES POLOXI.Legal_WideExecution(WideExecutionId),
		WideBranchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_WideBranchPassageMap_Branch REFERENCES POLOXI.Legal_WideBranch(WideBranchId),
		LegalDocumentPassageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Legal_WideBranchPassageMap_Passage REFERENCES POLOXI.Legal_DocumentPassage(LegalDocumentPassageId),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		MatchMethodCode NVARCHAR(30) NOT NULL,        -- EVIDENCE_TYPE | KEYWORD | LEXICAL | EMBEDDING
		Score DECIMAL(7,4) NOT NULL CONSTRAINT DF_Legal_WideBranchPassageMap_Score DEFAULT 0,
		RankOrder INT NOT NULL CONSTRAINT DF_Legal_WideBranchPassageMap_Rank DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Legal_WideBranchPassageMap_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_WideBranchPassageMap_IsDeleted DEFAULT 0
	);
	CREATE INDEX IX_Legal_WideBranchPassageMap_Branch ON POLOXI.Legal_WideBranchPassageMap(TenantId,WideExecutionId,WideBranchId,RankOrder) INCLUDE(LegalDocumentPassageId,MatchMethodCode,Score) WHERE IsDeleted=0;
END;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200) NOT NULL,SettingValue NVARCHAR(2000) NOT NULL,DataTypeCode NVARCHAR(50) NOT NULL,Description NVARCHAR(1000) NOT NULL);
	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
	(N'Intelligence.SearchWide.L3TierAThreshold',N'0.35',N'Decimal',N'Matter-relevance score at or above which an L2 parent is Tier A (richer passage/factor context, smaller batches). Context density only - never an inclusion gate.'),
	(N'Intelligence.SearchWide.L3TierABatchSize',N'4',N'Integer',N'Batch size for Tier A (high matter-relevance) L2 parents.'),
	(N'Intelligence.SearchWide.L3TierAPassagesPerParent',N'8',N'Integer',N'Mapped passages shown per Tier A parent.'),
	(N'Intelligence.SearchWide.L3PassageMapConcurrency',N'4',N'Integer',N'Bounded worker count for the one-time passage -> L2 mapping pre-stage.'),
	(N'Intelligence.SearchWide.L3PassageMapTopN',N'12',N'Integer',N'Maximum persisted passage mappings per L2 parent.'),
	(N'Intelligence.SearchWide.L3FactorSliceMaxItems',N'6',N'Integer',N'Maximum Factor Inventory items injected per L2 parent in L3 prompts.'),
	(N'Intelligence.SearchWide.EnableL2RelevanceReview',N'true',N'Boolean',N'Optional single LLM matter-relevance review over the configured L2 catalog. Adjusts tier/order/context density only; validated against the Domain Pack Evidence Type/Concept catalog; never adds or removes factors.');
	MERGE Core.ConfigurationSetting AS target
	USING @Settings AS source
	   ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey AND target.IsDeleted=0
	WHEN NOT MATCHED BY TARGET THEN
		INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
		VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,source.SettingValue,source.DataTypeCode,source.Description,0,0,SYSUTCDATETIME(),0);
END;

IF OBJECT_ID(N'AI.Legal_PromptDefinition',N'U') IS NOT NULL
BEGIN
	DECLARE @EffectiveFromUtc DATETIME2=SYSUTCDATETIME();
	DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000000';
	DECLARE @Addendum NVARCHAR(MAX)=N' MATTER-FUNNELED DECOMPOSITION: each assigned parent arrives with its matter-bound context (saved matter values, Factor Inventory items with availability/verification/contradiction state, and mapped evidence passages with stable ids). Decompose the parent USING that context: a child proposition must be testable against a specific matter value, document passage, or an explicitly named missing item - never a generic textbook restatement. MECE RULE: the children of one parent must be Mutually Exclusive (no two children can be established or defeated by the same single fact) and Collectively Exhaustive for what the matter must prove or rebut under that parent; if a necessary component has no matter evidence, still emit it and anchor nothing, so the gap is explicit. GRAPH RULE: when a child can only be resolved after another child (in this batch or a visible parent) is resolved, list that code in dependsOn; when a child states the same proposition as a child under another assigned parent, emit it once and reference the first code in equivalentTo on the later one instead of duplicating. Use only codes that appear in this response or in the assigned parent list; never invent codes. CATALOG RULE: the assigned parents ARE the Domain Pack catalog (configured Concepts and Evidence Types) for this matter; never introduce, rename, or substitute an evidence type that is not an assigned parent - a child proposition about evidence must be attached to the assigned evidence-type parent it belongs to.';
	DECLARE @Base NVARCHAR(MAX)=(SELECT TOP(1) SystemInstructions FROM AI.Legal_PromptDefinition WHERE TenantId IS NULL AND PromptCode=N'WIDE_HIERARCHY_STEP' AND StatusCode=N'APPROVED' AND IsDeleted=0 AND EffectiveFromUtc<=@EffectiveFromUtc AND (EffectiveToUtc IS NULL OR EffectiveToUtc>@EffectiveFromUtc) ORDER BY EffectiveFromUtc DESC);
	DECLARE @CapabilityId UNIQUEIDENTIFIER=(SELECT TOP(1) IntelligenceCapabilityId FROM AI.Legal_PromptDefinition WHERE TenantId IS NULL AND PromptCode=N'WIDE_HIERARCHY_STEP' AND IsDeleted=0 ORDER BY EffectiveFromUtc DESC);
	IF @CapabilityId IS NULL SET @CapabilityId=(SELECT TOP(1) IntelligenceCapabilityId FROM AI.Legal_IntelligenceCapability WHERE TenantId IS NULL AND IsDeleted=0 ORDER BY CASE WHEN CapabilityCode LIKE N'%SEARCH%' THEN 0 ELSE 1 END,SortOrder);
	IF @Base IS NOT NULL AND @CapabilityId IS NOT NULL AND CHARINDEX(N'MECE RULE',@Base)=0
	   AND NOT EXISTS(SELECT 1 FROM AI.Legal_PromptDefinition WHERE TenantId IS NULL AND PromptCode=N'WIDE_HIERARCHY_STEP' AND VersionLabel=N'v3.22' AND IsDeleted=0)
	BEGIN
		UPDATE AI.Legal_PromptDefinition
		SET EffectiveToUtc=@EffectiveFromUtc,ModifiedDateUtc=@EffectiveFromUtc,ModifiedByUserId=@SystemUserId
		WHERE TenantId IS NULL AND PromptCode=N'WIDE_HIERARCHY_STEP' AND StatusCode=N'APPROVED' AND IsDeleted=0
		  AND EffectiveFromUtc<=@EffectiveFromUtc AND (EffectiveToUtc IS NULL OR EffectiveToUtc>@EffectiveFromUtc);

		INSERT AI.Legal_PromptDefinition(TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(NULL,@CapabilityId,N'WIDE_HIERARCHY_STEP',N'v3.22',N'Wide hierarchy step',@Base+@Addendum,N'{}',N'{}',N'APPROVED',@SystemUserId,@EffectiveFromUtc,@EffectiveFromUtc,@EffectiveFromUtc,@SystemUserId,0);
	END;

	-- Optional matter-relevance review over the configured L2 catalog. ORDERING/CONTEXT-DENSITY ONLY:
	-- the model never adds or removes catalog rows; code validates every returned code against the
	-- Domain Pack Evidence Type / Concept catalog and drops anything else.
	IF @CapabilityId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM AI.Legal_PromptDefinition WHERE TenantId IS NULL AND PromptCode=N'WIDE_L2_RELEVANCE_REVIEW' AND IsDeleted=0)
	BEGIN
		INSERT AI.Legal_PromptDefinition(TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(NULL,@CapabilityId,N'WIDE_L2_RELEVANCE_REVIEW',N'v1.0',N'Wide L2 matter-relevance review',
		N'You are a legal evidence analyst. You receive the SELECTED matter context (saved matter values, facts, personal-injury profile, uploaded-document summaries) and the COMPLETE configured Domain Pack catalog of Level-2 factors: Evidence Types (EVD_ codes) and Concepts (CPT_ codes), each already given a deterministic relevance score and reasons. Your job is ONLY to assess, for each listed code, how relevant it is to THIS matter (0.0 = plainly inapplicable to these facts, 1.0 = central to proving or rebutting the claim) and give a one-sentence matter-specific reason. RULES: return one item per listed code and no others; never add, rename, merge, or invent an evidence type or concept - the catalog is fixed and authoritative; do not decide applicability as a gate - every code will still be processed downstream, your score only changes processing order and how much matter context each factor receives; prefer reasons that cite a concrete matter value or document, and say explicitly when relevance is inferred from the matter type alone. Output strictly the requested JSON.',
		N'{}',N'{}',N'APPROVED',@SystemUserId,@EffectiveFromUtc,@EffectiveFromUtc,@EffectiveFromUtc,@SystemUserId,0);
	END;
END;

COMMIT TRANSACTION;
GO
