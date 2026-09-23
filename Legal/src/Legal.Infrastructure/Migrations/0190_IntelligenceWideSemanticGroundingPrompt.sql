-- POLOXI Wide Semantic Grounding prompt (WIDE_SEMANTIC_GROUNDING v1.0).
--
-- Stage 2 of the feature-flagged semantic Wide path. The WIDE_SEMANTIC_PROPOSAL engine produces a rich
-- semantic forest + global candidate universe but intentionally omits enterprise grounding. This prompt
-- maps each flattened semantic branch to an approved capability + approved search term so the adapted
-- branch tree can retrieve deterministic evidence. Runtime use is gated behind the
-- Intelligence.Poloxi.EnableSemanticProposal configuration flag (default off), so registering this prompt
-- does not change default behavior.
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

	DECLARE @SystemInstructions NVARCHAR(MAX)=N'You ground abstract semantic branches against an approved enterprise capability catalog so they can retrieve deterministic evidence.

You are given a list of semantic branches (each with a branchCode, label, and semantic meaning) and a catalog of approved capabilities (each with a capabilityCode, description, and approved search terms).

For EACH supplied branch, decide whether an approved capability can genuinely ground it against enterprise data:

- When a capability applies, set capabilityCode to that exact catalog code and set searchText to the single most relevant approved term drawn from that capability''s approved-terms list.
- When no approved capability can genuinely ground the branch, set both capabilityCode and searchText to null.
- Set orderByRecency=true only when the branch is explicitly time-oriented AND the chosen capability supports recency; otherwise false.

Rules:

- Never invent capability codes or search terms that are not present in the supplied catalog.
- Echo each branchCode exactly as supplied.
- Do not add branches that were not supplied, and do not omit any supplied branch.
- Prefer null over a weak or speculative mapping: an ungrounded branch is better than a wrongly grounded one.

Return VALID JSON ONLY with a top-level branches array. Do not include prose outside the JSON.';

	IF @CapabilityId IS NOT NULL AND NOT EXISTS
	(
		SELECT 1 FROM AI.Legal_PromptDefinition
		WHERE TenantId IS NULL AND PromptCode=N'WIDE_SEMANTIC_GROUNDING' AND VersionLabel=N'v1.0' AND IsDeleted=0
	)
	BEGIN
		INSERT AI.Legal_PromptDefinition(TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(NULL,@CapabilityId,N'WIDE_SEMANTIC_GROUNDING',N'v1.0',N'Wide semantic grounding',@SystemInstructions,N'{}',N'{"schema_version":"POLOXI_SEMANTIC_GROUNDING_1.0"}',N'APPROVED',@SystemUserId,@EffectiveFromUtc,@EffectiveFromUtc,@EffectiveFromUtc,@SystemUserId,0);
	END;
END;

COMMIT TRANSACTION;
