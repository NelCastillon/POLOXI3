-- POLOXI Wide Semantic Proposal Engine prompt v1.1 - inline grounding.
-- Single-call semantic path: WIDE_SEMANTIC_PROPOSAL now emits BOTH the semantic forest AND the
-- enterprise grounding (capabilityCode + searchText + orderByRecency) on groundable leaf branches,
-- eliminating the separate WIDE_SEMANTIC_GROUNDING round-trip. The enforced JSON output schema lives
-- in IntelligenceWideService (proposalSchema); this migration updates the prompt TEXT so the model is
-- instructed to produce grounding inline. v1.0 body is reused verbatim and an inline-grounding section
-- is appended, so the rich semantic reasoning is preserved unchanged.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'AI.Legal_PromptDefinition',N'U') IS NOT NULL
BEGIN
	DECLARE @EffectiveFromUtc DATETIME2=SYSUTCDATETIME();
	DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000000';
	DECLARE @CapabilityId UNIQUEIDENTIFIER=(SELECT TOP(1) IntelligenceCapabilityId FROM AI.Legal_IntelligenceCapability WHERE TenantId IS NULL AND IsDeleted=0 ORDER BY CASE WHEN CapabilityCode LIKE N'%SEARCH%' THEN 0 ELSE 1 END,SortOrder);

	-- Reuse the approved v1.0 semantic body verbatim so no semantic reasoning is lost.
	DECLARE @BaseInstructions NVARCHAR(MAX)=(SELECT TOP(1) SystemInstructions FROM AI.Legal_PromptDefinition
		WHERE TenantId IS NULL AND PromptCode=N'WIDE_SEMANTIC_PROPOSAL' AND VersionLabel=N'v1.0' AND IsDeleted=0
		ORDER BY EffectiveFromUtc DESC);

	DECLARE @GroundingAddendum NVARCHAR(MAX)=N'

==================================================
INLINE ENTERPRISE GROUNDING (SINGLE-CALL CONTRACT)
==================================================

You are ALSO given an Approved capability catalog in the user message. Each
catalog line has: capabilityCode, a description, approved search terms, and a
recency flag.

In ADDITION to the semantic proposal, ground the branches against this catalog
so POLOXI Core can retrieve deterministic enterprise evidence WITHOUT a second
model call.

For every branch you emit, set:

- capabilityCode: the single approved capabilityCode that can genuinely ground
  this branch against enterprise data, or null when no approved capability
  legitimately applies. Never invent a capabilityCode that is not in the
  supplied catalog.
- searchText: a short retrieval query built ONLY from that capability''s
  approved search terms (plus, when helpful, verbatim entity names from the
  original query). Use null when capabilityCode is null.
- orderByRecency: true only when the branch is inherently time-sensitive AND the
  chosen capability supports recency; otherwise false.

Grounding rules:

- Do NOT force grounding. A branch that no approved capability can support MUST
  have capabilityCode=null and searchText=null. Purely interpretive / LLM-
  knowledge branches legitimately remain ungrounded.
- Grounding never overrides semantics. Never drop, merge, or distort a
  materially distinct branch just because it cannot be grounded.
- POLOXI Core still owns evidence admission, weighting, narrowing, and ranking.
  You only propose the capability + search term mapping.

Return capabilityCode, searchText, and orderByRecency on each branch object in
the structured result, alongside the existing semantic fields.';

	IF @CapabilityId IS NOT NULL AND @BaseInstructions IS NOT NULL AND NOT EXISTS
	(
		SELECT 1 FROM AI.Legal_PromptDefinition
		WHERE TenantId IS NULL AND PromptCode=N'WIDE_SEMANTIC_PROPOSAL' AND VersionLabel=N'v1.1' AND IsDeleted=0
	)
	BEGIN
		DECLARE @SystemInstructions NVARCHAR(MAX)=@BaseInstructions+@GroundingAddendum;

		UPDATE AI.Legal_PromptDefinition
		SET EffectiveToUtc=@EffectiveFromUtc,ModifiedDateUtc=@EffectiveFromUtc,ModifiedByUserId=@SystemUserId
		WHERE TenantId IS NULL AND PromptCode=N'WIDE_SEMANTIC_PROPOSAL' AND StatusCode=N'APPROVED' AND IsDeleted=0
		  AND VersionLabel<>N'v1.1' AND EffectiveFromUtc<=@EffectiveFromUtc
		  AND (EffectiveToUtc IS NULL OR EffectiveToUtc>@EffectiveFromUtc);

		INSERT AI.Legal_PromptDefinition(TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(NULL,@CapabilityId,N'WIDE_SEMANTIC_PROPOSAL',N'v1.1',N'Wide semantic proposal engine (inline grounding)',@SystemInstructions,N'{}',N'{"schema_version":"POLOXI_SEMANTIC_PROPOSAL_1.1"}',N'APPROVED',@SystemUserId,@EffectiveFromUtc,@EffectiveFromUtc,@EffectiveFromUtc,@SystemUserId,0);
	END;
END;

COMMIT TRANSACTION;
