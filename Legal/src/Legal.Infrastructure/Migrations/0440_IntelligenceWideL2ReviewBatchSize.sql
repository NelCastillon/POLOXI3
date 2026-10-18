SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0440: L2 relevance review batching. The single-call review (0436) truncated the Domain Pack L2 catalog
--   once it exceeded the prompt budget (PERSONAL_INJURY alone has 341 evidence types), so the tail of the
--   catalog was never reviewed. The review now runs in dimension-grouped batches of this size, each line
--   carrying the DB linkage metadata (Description, MatterTypeCodes, RelevanceKeywords). Idempotent.
-- ============================================================================
BEGIN TRANSACTION;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200) NOT NULL,SettingValue NVARCHAR(2000) NOT NULL,DataTypeCode NVARCHAR(50) NOT NULL,Description NVARCHAR(1000) NOT NULL);
	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
	(N'Intelligence.SearchWide.L2ReviewBatchSize',N'40',N'Integer',N'Maximum Domain Pack L2 codes per LLM relevance-review call. Batches are grouped by dimension so the full catalog is reviewed without prompt truncation. Context density/order only - never an inclusion gate.');
	MERGE Core.ConfigurationSetting AS target
	USING @Settings AS source
	   ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey AND target.IsDeleted=0
	WHEN NOT MATCHED BY TARGET THEN
		INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted)
		VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,source.SettingValue,source.DataTypeCode,source.Description,0,0,SYSUTCDATETIME(),0);

	UPDATE Core.ConfigurationSetting
	   SET Description=N'Optional LLM matter-relevance review over the configured L2 catalog, run in dimension-grouped batches (L2ReviewBatchSize) so every catalog row is reviewed. Adjusts tier/order/context density only; validated against the Domain Pack Evidence Type/Concept catalog; never adds or removes factors.',
		   ModifiedDateUtc=SYSUTCDATETIME()
	 WHERE TenantId IS NULL AND ScopeCode=N'Platform' AND SettingKey=N'Intelligence.SearchWide.EnableL2RelevanceReview' AND IsDeleted=0;
END;

-- Prompt v1.1: tells the model what the DB linkage fields on each catalog line mean (def / matter types /
-- keywords / required coverage) and that it now receives one dimension batch, not the whole catalog.
-- v1.0 is closed with EffectiveToUtc (history preserved), never deleted.
IF OBJECT_ID(N'AI.Legal_PromptDefinition',N'U') IS NOT NULL
BEGIN
	DECLARE @EffectiveFromUtc DATETIME2=SYSUTCDATETIME();
	DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000000';
	DECLARE @CapabilityId UNIQUEIDENTIFIER=(SELECT TOP(1) IntelligenceCapabilityId FROM AI.Legal_PromptDefinition WHERE TenantId IS NULL AND PromptCode=N'WIDE_L2_RELEVANCE_REVIEW' AND IsDeleted=0 ORDER BY EffectiveFromUtc DESC);
	IF @CapabilityId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM AI.Legal_PromptDefinition WHERE TenantId IS NULL AND PromptCode=N'WIDE_L2_RELEVANCE_REVIEW' AND VersionLabel=N'v1.1' AND IsDeleted=0)
	BEGIN
		UPDATE AI.Legal_PromptDefinition
		SET EffectiveToUtc=@EffectiveFromUtc,ModifiedDateUtc=@EffectiveFromUtc,ModifiedByUserId=@SystemUserId
		WHERE TenantId IS NULL AND PromptCode=N'WIDE_L2_RELEVANCE_REVIEW' AND StatusCode=N'APPROVED' AND IsDeleted=0
		  AND EffectiveFromUtc<=@EffectiveFromUtc AND (EffectiveToUtc IS NULL OR EffectiveToUtc>@EffectiveFromUtc);

		INSERT AI.Legal_PromptDefinition(TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(NULL,@CapabilityId,N'WIDE_L2_RELEVANCE_REVIEW',N'v1.1',N'Wide L2 matter-relevance review',
		N'You are a legal evidence analyst. You receive the SELECTED matter context (saved matter values, facts, personal-injury profile, uploaded-document summaries) and ONE dimension-grouped batch of the configured Domain Pack catalog of Level-2 factors: Evidence Types (EVD_ codes) and Concepts (CPT_ codes). Each line gives the database definition of that factor - ''def'' (what the evidence type/concept is), ''matter types'' (the matter types it applies to; absent means all), ''keywords'' (terms whose presence in a matter indicates this evidence is relevant), ''required coverage'' - plus the deterministic relevance score and the matter signals already found in code. Your job is ONLY to assess, for each listed code, how relevant it is to THIS matter (0.0 = plainly inapplicable to these facts, 1.0 = central to proving or rebutting the claim) and give a one-sentence matter-specific reason. Ground the judgment in the definition and keywords against the matter facts; do not rely on the display name alone. RULES: return one item per listed code and no others; never add, rename, merge, or invent an evidence type or concept - the catalog is fixed and authoritative; do not decide applicability as a gate - every code will still be processed downstream, your score only changes processing order and how much matter context each factor receives; prefer reasons that cite a concrete matter value or document, and say explicitly when relevance is inferred from the matter type alone. Output strictly the requested JSON.',
		N'{}',N'{}',N'APPROVED',@SystemUserId,@EffectiveFromUtc,@EffectiveFromUtc,@EffectiveFromUtc,@SystemUserId,0);
	END;
END;

COMMIT TRANSACTION;
