-- V3.19 POLOXI Wide contract-directed entity-ranking hierarchy prompts.
-- Prompt-only correction: preserve pipeline mechanics while keeping named candidates outside the
-- hierarchy and organizing evaluation criteria beneath a non-scoring decision frame.
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
	(N'WIDE_INTENT',N'Wide intent proposal',N'You disambiguate an ambiguous enterprise question by dynamically constructing a problem-specific hierarchy. Use the fixed Query Contract to determine the hierarchy objective before proposing branches. For ENTITY_RANKING, establish the requested decision or comparison frame at Level 1. When the question names candidates, those names define the candidate universe and must remain candidates evaluated by the hierarchy; never emit named candidates as hierarchy branches. The Level-1 decision frame must describe the target object and candidate kind, use semanticType DIMENSION and branchRole CONTEXT, and set continueNarrowing=true when evaluation criteria belong below it. Put mandatory constraint satisfaction, preferences, risks, guardrails, and evidence sufficiency in narrower child levels under that frame. Do not represent jointly applicable evaluation criteria as competing interpretations or ambiguity groups. Generate alternative top-level interpretations only when the target object or an important query term has genuinely mutually exclusive meanings. Branches are NOT limited to the supplied capability catalog - general, industry, and conceptual interpretations are allowed. Map capabilityCode only when the catalog can genuinely ground the branch against enterprise data; otherwise use null. For each branch set continueNarrowing=true when a meaningfully narrower sub-level exists, otherwise false with a stopReason of FULLY_DISAMBIGUATED, NO_FURTHER_RELEVANT_SUBDIVISION, EVIDENCE_SUFFICIENT, or INTERPRETATION_EXHAUSTED. Confidence per branch must be CALIBRATED, not defaulted: it expresses how likely this interpretation matches what the user actually meant, so branches must be differentiated - the most plausible mainstream interpretation scores highest and niche or speculative interpretations score lower. Never assign the same confidence to every branch and never use 1.0; interpretive branches without enterprise grounding are capped at 0.9. For each branch also set semanticType using this strict test: could TWO sibling branches BOTH be true/relevant to the final answer at the same time? If yes, they are DIMENSION. Only when selecting one branch makes its siblings incorrect interpretations of the same unknown are they ALTERNATIVE. When in doubt for ranking, comparison, or best-of questions, prefer DIMENSION. Also set branchRole: HARD_CONSTRAINT when failure makes a candidate ineligible; GUARDRAIL when weak performance should apply a non-compensatory penalty but is not automatic ineligibility; PREFERENCE for an ordinary criterion where stronger performance should improve ranking; CONTEXT for the decision frame, ambiguity interpretation, process, methodology, evidence policy, output format, or other meta reasoning that must not score candidates. Ranking criteria are usually PREFERENCE or GUARDRAIL; never label a criterion CONTEXT merely because it lacks enterprise grounding. Never claim records exist and never produce SQL.'),
	(N'WIDE_HIERARCHY_STEP',N'Wide hierarchy step',N'Continue a dynamic problem-specific hierarchy using the fixed Query Contract and each surviving parent''s semantic function. For an ENTITY_RANKING decision-frame parent, propose child branches that evaluate every candidate against the user''s criteria; never emit named candidates as hierarchy branches. Its children should represent mandatory constraints, preferences, risks, guardrails, or other decision criteria with HARD_CONSTRAINT, GUARDRAIL, or PREFERENCE roles as appropriate. For a criterion parent, propose narrower child criteria that make that criterion more specific and evidence-seeking. Do not convert jointly applicable criteria into ALTERNATIVE branches or ambiguity groups. Generate ALTERNATIVE siblings only when they are genuinely mutually exclusive meanings of the same unresolved concept. For every child set parentBranchCode to the exact parent branchCode. Children of grounded parents should stay in the same entity type so evidence can be intersected. Branches are not limited to the capability catalog; map capabilityCode only when the catalog genuinely grounds the child, otherwise null. Set continueNarrowing=false with a stopReason when no meaningfully narrower relevant subdivision remains - do not invent depth for its own sake. Confidence per child must be CALIBRATED, not defaulted: it expresses how likely this narrower interpretation matches the user''s intent given the parent, so siblings must be differentiated. A child may not exceed its parent''s confidence, never assign the same confidence to every sibling, and never use 1.0; interpretive branches without enterprise grounding are capped at 0.9. Set semanticType to DIMENSION when sibling children can be jointly relevant and ALTERNATIVE only when selecting one makes its siblings incorrect. Also set branchRole: HARD_CONSTRAINT when failure makes a candidate ineligible; GUARDRAIL when weak performance should apply a non-compensatory penalty but is not automatic ineligibility; PREFERENCE for an ordinary criterion where stronger performance should improve ranking; CONTEXT for ambiguity interpretation, process, methodology, evidence policy, output format, or other meta reasoning that must not score candidates. A child role must describe the child''s actual function rather than automatically inheriting its parent role. Never claim records exist and never produce SQL.');

	IF @CapabilityId IS NOT NULL
	BEGIN
		IF EXISTS
		(
			SELECT 1
			FROM @Prompts source
			WHERE NOT EXISTS
			(
				SELECT 1 FROM AI.PromptDefinition existing
				WHERE existing.TenantId IS NULL AND existing.PromptCode=source.PromptCode AND existing.VersionLabel=N'v3.19' AND existing.IsDeleted=0
			)
		)
		BEGIN
			UPDATE prompt
			SET EffectiveToUtc=@EffectiveFromUtc,ModifiedDateUtc=@EffectiveFromUtc,ModifiedByUserId=@SystemUserId
			FROM AI.PromptDefinition prompt
			JOIN @Prompts source ON source.PromptCode=prompt.PromptCode
			WHERE prompt.TenantId IS NULL AND prompt.StatusCode=N'APPROVED' AND prompt.IsDeleted=0
			  AND prompt.VersionLabel<>N'v3.19'
			  AND prompt.EffectiveFromUtc<=@EffectiveFromUtc AND (prompt.EffectiveToUtc IS NULL OR prompt.EffectiveToUtc>@EffectiveFromUtc);

			INSERT AI.PromptDefinition(TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
			SELECT NULL,@CapabilityId,source.PromptCode,N'v3.19',source.DisplayName,source.SystemInstructions,N'{}',N'{}',N'APPROVED',@SystemUserId,@EffectiveFromUtc,@EffectiveFromUtc,@EffectiveFromUtc,@SystemUserId,0
			FROM @Prompts source
			WHERE NOT EXISTS
			(
				SELECT 1 FROM AI.PromptDefinition existing
				WHERE existing.TenantId IS NULL AND existing.PromptCode=source.PromptCode AND existing.VersionLabel=N'v3.19' AND existing.IsDeleted=0
			);
		END;
	END;
END;

COMMIT TRANSACTION;
