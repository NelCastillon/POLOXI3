SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

-- ============================================================================
-- 0468: Harden the L3 decomposition contract on WIDE_HIERARCHY_STEP (v3.23).
--
-- Scope: /legal/personalinjury_decision2 (Wide2 pipeline, IntelligenceWide2Service).
-- Both Wide prompts are PRESERVED. This migration does NOT remove WIDE_HIERARCHY_STEP
-- and does NOT fold its job into WIDE_INTENT. WIDE_INTENT still builds Level 1 and keeps
-- broad dimensions open (continueNarrowing=true); WIDE_HIERARCHY_STEP still produces the
-- atomic L3 children against grounded parents and their mapped matter passages.
--
-- What changes: the approved WIDE_HIERARCHY_STEP (v3.22, migration 0436) is superseded by
-- v3.23, which appends a single consolidated, strengthened L3 rule block that sharpens the
-- four existing contracts WITHOUT changing their meaning or lowering any gate:
--   * ATOMIC-FACTOR - every child is ONE independently resolvable legal/factual proposition;
--                     broad dimension labels are never leaves.
--   * MECE          - a parent's children are mutually exclusive and collectively exhaustive;
--                     unmet components are emitted as explicit gaps, anchored to nothing.
--   * GRAPH         - dependsOn / equivalentTo express ordering and de-duplication across
--                     children in this batch and visible parents; codes are never invented.
--   * CATALOG       - the assigned parents ARE the Domain Pack catalog; no evidence type or
--                     concept outside the assigned parents may be introduced or renamed.
--   * MATTER-EVIDENCE ANCHORING - a child that claims support/contradiction MUST cite a shown
--                     passage id; never cite an id that is not shown; children with no matter
--                     evidence are still emitted and anchored to nothing (INFORMATION_GAP).
--
-- Pipeline mechanics, schema, budgets, tiering/batching knobs, POLOXI Core scoring, and the
-- Blazor UI are UNCHANGED. No new engine, no new LLM call, no new schema section. The runtime
-- already resolves WIDE_HIERARCHY_STEP by latest approved version, so no code change is needed.
--
-- Idempotent: guarded by the v3.23 existence check; supersede/upsert pattern matches 0324/0436.
-- ============================================================================
BEGIN TRANSACTION;

IF OBJECT_ID(N'AI.Legal_PromptDefinition',N'U') IS NOT NULL
BEGIN
	DECLARE @EffectiveFromUtc DATETIME2=SYSUTCDATETIME();
	DECLARE @SystemUserId UNIQUEIDENTIFIER=N'00000000-0000-0000-0000-000000000000';

	-- Carry the WIDE_HIERARCHY_STEP capability forward from the currently approved row.
	DECLARE @CapabilityId UNIQUEIDENTIFIER=(SELECT TOP(1) IntelligenceCapabilityId FROM AI.Legal_PromptDefinition
		WHERE TenantId IS NULL AND PromptCode=N'WIDE_HIERARCHY_STEP' AND StatusCode=N'APPROVED' AND IsDeleted=0
		  AND EffectiveFromUtc<=@EffectiveFromUtc AND (EffectiveToUtc IS NULL OR EffectiveToUtc>@EffectiveFromUtc)
		ORDER BY EffectiveFromUtc DESC);

	-- Current approved base text (v3.22 from 0436, or later). v3.23 = base + strengthened block.
	DECLARE @Base NVARCHAR(MAX)=(SELECT TOP(1) SystemInstructions FROM AI.Legal_PromptDefinition
		WHERE TenantId IS NULL AND PromptCode=N'WIDE_HIERARCHY_STEP' AND StatusCode=N'APPROVED' AND IsDeleted=0
		  AND EffectiveFromUtc<=@EffectiveFromUtc AND (EffectiveToUtc IS NULL OR EffectiveToUtc>@EffectiveFromUtc)
		ORDER BY EffectiveFromUtc DESC);

	DECLARE @Addendum NVARCHAR(MAX)=N' L3 DECOMPOSITION CONTRACT (STRICT): for every assigned parent, apply ALL of the following together; these sharpen the existing atomic-factor, MECE, graph, and catalog rules and never relax any of them. ATOMIC-FACTOR: each child is exactly ONE concrete, independently resolvable legal or factual proposition that could be answered true, false, or unresolved by a specific matter value, a specific document passage, or a specific legal authority, without first splitting it into further questions; never emit a broad dimension label (for example Liability, Damages, Causation, Settlement enforceability, Coverage, Procedural posture) as a leaf, and never use an outcome name, a candidate name, a decision-frame instruction, or a generic answer heading as a child. MECE: the children of one parent must be Mutually Exclusive (no two children can both be established or defeated by the same single fact) and Collectively Exhaustive for everything the matter must prove or rebut under that parent; when a necessary component has no matter evidence yet, still emit the child, anchor nothing to it, and let the gap be explicit rather than dropping it. GRAPH: when a child can only be resolved after another child in this batch or a visible parent is resolved, list that exact code in dependsOn; when a child states the same proposition as an earlier emitted child under any assigned parent, emit it once and reference the earlier code in equivalentTo on the later one instead of duplicating; use only codes that appear in this response or in the assigned parent list and never invent a code. CATALOG: the assigned parents ARE the configured Domain Pack catalog (Concepts and Evidence Types) for this matter; never introduce, rename, substitute, or merge an evidence type or concept that is not an assigned parent, and attach every evidence-related child to the assigned evidence-type parent it belongs to. MATTER-EVIDENCE ANCHORING: a child that asserts the matter supports or contradicts a proposition MUST cite the stable passage id(s) shown under its parent; never cite a passage id that is not shown, never fabricate a value, and never mark a child verified, satisfied, or established - grounding, admission, and Branch x Candidate scoring are performed later by POLOXI Core. COVERAGE: parentCoverage MUST contain exactly one entry per assigned parent code - never omit one and never add an unassigned code; when a parent yields no children, return zero children for it with status INFORMATION_GAP (the matter supplies no evidence yet) or NOT_APPLICABLE (the factor cannot apply to this matter''s facts). Do not restate parents.';

	IF @Base IS NOT NULL AND @CapabilityId IS NOT NULL
	   AND NOT EXISTS(SELECT 1 FROM AI.Legal_PromptDefinition
			WHERE TenantId IS NULL AND PromptCode=N'WIDE_HIERARCHY_STEP' AND VersionLabel=N'v3.23' AND IsDeleted=0)
	BEGIN
		-- Close out the currently effective approved versions.
		UPDATE prompt
		SET EffectiveToUtc=@EffectiveFromUtc,ModifiedDateUtc=@EffectiveFromUtc,ModifiedByUserId=@SystemUserId
		FROM AI.Legal_PromptDefinition prompt
		WHERE prompt.TenantId IS NULL AND prompt.PromptCode=N'WIDE_HIERARCHY_STEP' AND prompt.StatusCode=N'APPROVED' AND prompt.IsDeleted=0
		  AND prompt.VersionLabel<>N'v3.23'
		  AND prompt.EffectiveFromUtc<=@EffectiveFromUtc AND (prompt.EffectiveToUtc IS NULL OR prompt.EffectiveToUtc>@EffectiveFromUtc);

		INSERT AI.Legal_PromptDefinition(TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(NULL,@CapabilityId,N'WIDE_HIERARCHY_STEP',N'v3.23',N'Wide hierarchy step',@Base+@Addendum,N'{}',N'{}',N'APPROVED',@SystemUserId,@EffectiveFromUtc,@EffectiveFromUtc,@EffectiveFromUtc,@SystemUserId,0);
	END;
END;

COMMIT TRANSACTION;
GO
