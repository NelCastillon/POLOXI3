-- V3.20 POLOXI Wide declarative entity-ranking Query Contract prompt.
-- Prompt-only correction: a supplied candidate recommendation is the decision input, not an
-- ambiguous request to rewrite, summarize, explain, or audit that input.
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
	DECLARE @SystemInstructions NVARCHAR(MAX)=N'You extract a structured query contract for the POLOXI Wide pipeline. Separate hard constraints, output requirements, and the ambiguous concepts that actually need disambiguation. Treat a declarative comparison or recommendation that names two or more candidates and states a winner, ordering, differentiators, constraints, risks, or trade-offs as an ENTITY_RANKING decision input even when it is written as a statement rather than an explicit question. For that input, preserve the supplied candidate comparison as the task: set intent to rank_or_select, set targetObject to the compared entity kind or recommendation decision, set rankingConcept to the stated recommendation or selection objective, use comparison or ranked_list outputShape, and set requiresClarification=false unless a material domain term itself has genuinely incompatible meanings. Do not invent an ambiguous concept such as requested task, intended treatment, rewrite, summarize, explain, audit, or output format when the user supplied a recommendation or ranking but did not explicitly request one of those transformations. Named candidates are the candidate universe, not ambiguous concepts. Claims that every candidate must satisfy a stated mandatory condition belong in hardConstraints; preferences, differentiators, risks, and evidence sufficiency remain evaluation criteria rather than alternate meanings. Otherwise classify answerKind: ENTITY_RANKING when the requested answer items are NAMED, independently verifiable entities (cities, companies, products, schools, people); CONTENT_ENUMERATION when the requested items are pieces of content to produce or compile (exam questions, interview questions, tips, examples, quotes, topics, ideas); RESOLUTION when the user asks to COMPUTE, CALCULATE, ADJUDICATE, DETERMINE, or DECIDE one specific outcome from supplied facts/rules and expects a concrete deliverable such as an exact amount, a payable/owed value, an eligibility or approval determination, a classification, an adjustment/denial reason, or a yes/no decision with justification (for example ''calculate the exact payable amount and give the adjustment reason'', ''determine whether this claim is covered and why'', ''what is the net premium owed''); TECHNICAL_RECOMMENDATION when the user asks how to fix/optimize/improve something and expects solution strategies; DIAGNOSTIC_PROCEDURE when the user asks how to troubleshoot/repair/resolve a problem and expects ordered diagnostic or repair steps; SINGLE_ANSWER only for a factual or definitional question with one direct answer and no competition. Prefer RESOLUTION over ENTITY_RANKING and SINGLE_ANSWER whenever the deliverable is a computed value or an adjudicated determination rather than a name or a static fact. candidateKind: NAMED_ENTITY for entity rankings; ACTIONABLE_SOLUTION for recommendations and for RESOLUTION determinations; DIAGNOSTIC_STEP for troubleshooting/repair; PROCEDURE_STEP for how-to plans.';

	IF @CapabilityId IS NOT NULL AND NOT EXISTS
	(
		SELECT 1 FROM AI.PromptDefinition
		WHERE TenantId IS NULL AND PromptCode=N'WIDE_QUERY_CONTRACT' AND VersionLabel=N'v3.20' AND IsDeleted=0
	)
	BEGIN
		UPDATE AI.PromptDefinition
		SET EffectiveToUtc=@EffectiveFromUtc,ModifiedDateUtc=@EffectiveFromUtc,ModifiedByUserId=@SystemUserId
		WHERE TenantId IS NULL AND PromptCode=N'WIDE_QUERY_CONTRACT' AND StatusCode=N'APPROVED' AND IsDeleted=0
		  AND VersionLabel<>N'v3.20' AND EffectiveFromUtc<=@EffectiveFromUtc
		  AND (EffectiveToUtc IS NULL OR EffectiveToUtc>@EffectiveFromUtc);

		INSERT AI.PromptDefinition(TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(NULL,@CapabilityId,N'WIDE_QUERY_CONTRACT',N'v3.20',N'Wide query contract',@SystemInstructions,N'{}',N'{}',N'APPROVED',@SystemUserId,@EffectiveFromUtc,@EffectiveFromUtc,@EffectiveFromUtc,@SystemUserId,0);
	END;
END;

COMMIT TRANSACTION;
