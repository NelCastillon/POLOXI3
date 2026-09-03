-- V3.18 POLOXI Wide explicit branch-role prompt versioning.
-- Runtime role enforcement is ineffective when approved registry prompts do not request branchRole,
-- because AI.PromptDefinition overrides embedded defaults. Add a new approved global version while
-- preserving tenant-specific prompt ownership and historical global versions.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'AI.PromptDefinition',N'U') IS NOT NULL
BEGIN
	DECLARE @EffectiveFromUtc DATETIME2=SYSUTCDATETIME();
	DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000000';
	DECLARE @CapabilityId UNIQUEIDENTIFIER=(SELECT TOP(1) IntelligenceCapabilityId FROM AI.IntelligenceCapability WHERE TenantId IS NULL AND IsDeleted=0 ORDER BY CASE WHEN CapabilityCode LIKE N'%SEARCH%' THEN 0 ELSE 1 END,SortOrder);

	DECLARE @Prompts TABLE(PromptCode NVARCHAR(120),DisplayName NVARCHAR(200),SystemInstructions NVARCHAR(MAX));
	INSERT @Prompts(PromptCode,DisplayName,SystemInstructions)
	VALUES
	(N'WIDE_INTENT',N'Wide intent proposal',N'You disambiguate an ambiguous enterprise question by dynamically constructing a problem-specific hierarchy. Propose the top level: distinct interpretation branches of the question. Branches are NOT limited to the supplied capability catalog - general, industry, and conceptual interpretations are allowed. Map capabilityCode only when the catalog can genuinely ground the branch against enterprise data; otherwise use null. For each branch set continueNarrowing=true when a meaningfully narrower sub-level exists, otherwise false with a stopReason of FULLY_DISAMBIGUATED, NO_FURTHER_RELEVANT_SUBDIVISION, EVIDENCE_SUFFICIENT, or INTERPRETATION_EXHAUSTED. Confidence per branch must be CALIBRATED, not defaulted. For each branch set semanticType to DIMENSION when siblings can be jointly relevant and ALTERNATIVE only when siblings are mutually exclusive interpretations. Also set branchRole: HARD_CONSTRAINT when failure makes a candidate ineligible; GUARDRAIL when weak performance should apply a non-compensatory penalty but is not automatic ineligibility; PREFERENCE for an ordinary criterion where stronger performance improves ranking; CONTEXT for ambiguity interpretation, process, methodology, evidence policy, output format, or other meta reasoning that must not score candidates. Ranking criteria are usually PREFERENCE or GUARDRAIL; never label a branch CONTEXT merely because it lacks enterprise grounding. Never claim records exist and never produce SQL.'),
	(N'WIDE_HIERARCHY_STEP',N'Wide hierarchy step',N'Continue a dynamic problem-specific disambiguation hierarchy. For each surviving parent branch, propose narrower child branches that progressively move toward a more specific subset of the parent interpretation. Set parentBranchCode to the exact parent branchCode. Map capabilityCode only when the catalog genuinely grounds the child, otherwise null. Set continueNarrowing=false with a stopReason when no meaningfully narrower relevant subdivision remains. Confidence per child must be calibrated and may not exceed its parent confidence. Set semanticType to DIMENSION when siblings can be jointly relevant and ALTERNATIVE only when siblings are mutually exclusive interpretations. Also set branchRole: HARD_CONSTRAINT when failure makes a candidate ineligible; GUARDRAIL when weak performance should apply a non-compensatory penalty but is not automatic ineligibility; PREFERENCE for an ordinary criterion where stronger performance improves ranking; CONTEXT for ambiguity interpretation, process, methodology, evidence policy, output format, or other meta reasoning that must not score candidates. A child role must describe the child actual function rather than automatically inheriting its parent role. Never claim records exist and never produce SQL.');

	IF @CapabilityId IS NOT NULL
	BEGIN
		UPDATE prompt
		SET EffectiveToUtc=@EffectiveFromUtc,ModifiedDateUtc=@EffectiveFromUtc,ModifiedByUserId=@SystemUserId
		FROM AI.PromptDefinition prompt
		JOIN @Prompts source ON source.PromptCode=prompt.PromptCode
		WHERE prompt.TenantId IS NULL AND prompt.StatusCode=N'APPROVED' AND prompt.IsDeleted=0
		  AND prompt.EffectiveFromUtc<=@EffectiveFromUtc AND (prompt.EffectiveToUtc IS NULL OR prompt.EffectiveToUtc>@EffectiveFromUtc);

		INSERT AI.PromptDefinition(TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
		SELECT NULL,@CapabilityId,source.PromptCode,N'v3.18',source.DisplayName,source.SystemInstructions,N'{}',N'{}',N'APPROVED',@SystemUserId,@EffectiveFromUtc,@EffectiveFromUtc,@EffectiveFromUtc,@SystemUserId,0
		FROM @Prompts source
		WHERE NOT EXISTS
		(
			SELECT 1 FROM AI.PromptDefinition existing
			WHERE existing.TenantId IS NULL AND existing.PromptCode=source.PromptCode AND existing.VersionLabel=N'v3.18' AND existing.IsDeleted=0
		);
	END;
END;

COMMIT TRANSACTION;
