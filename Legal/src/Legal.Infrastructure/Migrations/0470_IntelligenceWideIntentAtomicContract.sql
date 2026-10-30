SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0470: Move the ATOMIC-FACTOR + MECE + GRAPH + CATALOG + MATTER-ANCHORING contract
--       onto WIDE_INTENT (v-next), now that WIDE_HIERARCHY_STEP is retired (0469).
--
-- Scope: /legal/personalinjury_decision2 (Wide2 pipeline, IntelligenceWide2Service).
-- WIDE_HIERARCHY_STEP was the generator of the atomic (L3) propositions and has been
-- retired. The hardened contract authored in 0468 (v3.23) therefore sits on a prompt that
-- no longer runs. This migration ports that same contract onto WIDE_INTENT so the SINGLE
-- surviving hierarchy prompt emits the atomic propositions directly. ProposeIntentAsync
-- already injects the catalog (BuildCatalog) and the matter context (BuildMatterContextSection),
-- so the catalog + matter passages the contract references are already available to this call.
--
-- Supersede/upsert pattern matches 0324/0436/0468. Idempotent via the v4.0 existence check.
-- WIDE_ANSWER is untouched. No schema/engine change; the runtime resolves WIDE_INTENT by
-- latest approved version.
-- ============================================================================
BEGIN TRANSACTION;

IF OBJECT_ID(N'AI.Legal_PromptDefinition',N'U') IS NOT NULL
BEGIN
	DECLARE @EffectiveFromUtc DATETIME2=SYSUTCDATETIME();
	DECLARE @SystemUserId UNIQUEIDENTIFIER=N'00000000-0000-0000-0000-000000000000';

	DECLARE @CapabilityId UNIQUEIDENTIFIER=(SELECT TOP(1) IntelligenceCapabilityId FROM AI.Legal_PromptDefinition
		WHERE TenantId IS NULL AND PromptCode=N'WIDE_INTENT' AND StatusCode=N'APPROVED' AND IsDeleted=0
		  AND EffectiveFromUtc<=@EffectiveFromUtc AND (EffectiveToUtc IS NULL OR EffectiveToUtc>@EffectiveFromUtc)
		ORDER BY EffectiveFromUtc DESC);

	DECLARE @Base NVARCHAR(MAX)=(SELECT TOP(1) SystemInstructions FROM AI.Legal_PromptDefinition
		WHERE TenantId IS NULL AND PromptCode=N'WIDE_INTENT' AND StatusCode=N'APPROVED' AND IsDeleted=0
		  AND EffectiveFromUtc<=@EffectiveFromUtc AND (EffectiveToUtc IS NULL OR EffectiveToUtc>@EffectiveFromUtc)
		ORDER BY EffectiveFromUtc DESC);

	-- Same contract as 0468 v3.23, re-anchored for the intent stage: there are no assigned parent
	-- branches here, so the CATALOG concepts/evidence types supplied in the prompt ARE the frame the
	-- atomic propositions attach to, and each returned branch must itself BE an atomic proposition.
	DECLARE @Addendum NVARCHAR(MAX)=N' ATOMIC PROPOSITION CONTRACT (STRICT - governs the branches you return): every branch you return MUST itself be an atomic proposition; apply ALL of the following together and never relax any of them. ATOMIC-FACTOR: each branch is exactly ONE concrete, independently resolvable legal or factual proposition that could be answered true, false, or unresolved by a specific matter value, a specific document passage, or a specific legal authority, without first splitting it into further questions; never return a broad dimension label (for example Liability, Damages, Causation, Settlement enforceability, Coverage, Procedural posture) as a branch, and never use an outcome name, a candidate name, a decision-frame instruction, or a generic answer heading as a branch. MECE: the branches must be Mutually Exclusive (no two can both be established or defeated by the same single fact) and Collectively Exhaustive for everything the matter must prove or rebut; when a necessary component has no matter evidence yet, still return the branch, anchor nothing to it, and let the gap be explicit rather than dropping it. GRAPH: when a branch can only be resolved after another returned branch is resolved, list that exact code in dependsOn; when a branch states the same proposition as an earlier returned branch, return it once and reference the earlier code in equivalentTo on the later one instead of duplicating; use only codes that appear in this response and never invent a code. CATALOG: the Concepts and Evidence Types supplied in this prompt ARE the configured Domain Pack catalog for this matter; never introduce, rename, substitute, or merge an evidence type or concept that is not in the supplied catalog, and attach every evidence-related branch to the catalog evidence type it belongs to. MATTER-EVIDENCE ANCHORING: a branch that asserts the matter supports or contradicts a proposition MUST cite the stable passage id(s) shown in the supplied matter context; never cite a passage id that is not shown, never fabricate a value, and never mark a branch verified, satisfied, or established - grounding, admission, and Branch x Candidate scoring are performed later by POLOXI Core. Distinguish jointly applicable dimensions from mutually exclusive alternatives via the branch role, but the PROPOSITION TEXT of every branch must remain atomic. Named candidates remain candidates, not branches.';

	IF @Base IS NOT NULL AND @CapabilityId IS NOT NULL
	   AND NOT EXISTS(SELECT 1 FROM AI.Legal_PromptDefinition
			WHERE TenantId IS NULL AND PromptCode=N'WIDE_INTENT' AND VersionLabel=N'v4.0' AND IsDeleted=0)
	BEGIN
		UPDATE prompt
		SET EffectiveToUtc=@EffectiveFromUtc,ModifiedDateUtc=@EffectiveFromUtc,ModifiedByUserId=@SystemUserId
		FROM AI.Legal_PromptDefinition prompt
		WHERE prompt.TenantId IS NULL AND prompt.PromptCode=N'WIDE_INTENT' AND prompt.StatusCode=N'APPROVED' AND prompt.IsDeleted=0
		  AND prompt.VersionLabel<>N'v4.0'
		  AND prompt.EffectiveFromUtc<=@EffectiveFromUtc AND (prompt.EffectiveToUtc IS NULL OR prompt.EffectiveToUtc>@EffectiveFromUtc);

		INSERT AI.Legal_PromptDefinition(TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(NULL,@CapabilityId,N'WIDE_INTENT',N'v4.0',N'Wide intent',@Base+@Addendum,N'{}',N'{}',N'APPROVED',@SystemUserId,@EffectiveFromUtc,@EffectiveFromUtc,@EffectiveFromUtc,@SystemUserId,0);
	END;
END;

COMMIT TRANSACTION;
GO
