-- ============================================================================
-- 0452: WIDE_HIERARCHY_STEP v3.23 - Six-Factor-Group Generation + Evidence-Type Anchoring
--
-- WHY THIS EXISTS:
--   WIDE_HIERARCHY_STEP is the MAIN L3/L4 atomic-proposition GENERATION prompt
--   for the Decision2 pipeline (IntelligenceWide2Service.SearchDynamicAsync,
--   behind /legal/personalinjury_decision2). It is invoked once per L3 batch by
--   ProposeBatchAsync (IntelligenceWide2Service.CoverageBatches.cs) via
--   GetWideSystemPromptAsync(..., WIDE_HIERARCHY_STEP). This is the prompt that
--   turns each assigned Level-2 parent into the matter's atomic propositions.
--
--   Two matter channels already FEED this prompt but the prompt text (v3.22,
--   migration 0436) never binds generation to them explicitly:
--     * The SELECTED MATTER CONTEXT block (BuildMatterContextSection) carries
--       ALL SIX matter factor groups - Decision, Legal Scope, Personal-Injury
--       Profile, Facts, per-entry Evidence, and the uploaded-document evidence
--       graph - but the prompt never names them, so the model tends to decompose
--       only the richest group instead of the complete matter picture.
--     * The assigned parents ARE the DB-saved, per-matter IN_SCOPE Domain Pack
--       Evidence Types (EVD_) and Concepts (CPT_) produced by the matter funnel
--       (Legal_MatterEvidenceScope). The prompt never requires each generated
--       atomic proposition to be ANCHORED back to its Evidence Type parent, so
--       the Legal_MatterAtomicProposition -> Evidence Type link
--       (ParentTargetKindCode=EVIDENCE_TYPE + ParentTargetCode) is weaker than
--       it should be.
--
-- WHAT THIS ADDS (additive prompt text ONLY; append-and-supersede pattern
--   identical to migrations 0324/0365/0436; idempotent via the v3.23 guard and
--   the SIX-FACTOR ANCHORING marker):
--     v3.23 appends a SIX-FACTOR ANCHORING directive that (1) names all six
--     matter factor groups as the generation source, (2) requires each atomic
--     proposition to be anchored to its assigned parent and, when evidentiary,
--     to the EVIDENCE TYPE (EVD_) parent it belongs to (non-evidentiary legal/
--     scope facts may anchor to a CONCEPT (CPT_) parent instead), and (3)
--     requires an explicit GAP proposition when a factor group that the parent
--     depends on supplies no saved matter value, so missing coverage stays
--     visible rather than silently dropped.
--
--   Pipeline mechanics, JSON schema, stopReason enum, token budgets, calibration
--   and role rules, POLOXI Core scoring, and the Blazor UI are UNCHANGED. The C#
--   seed/fallback (IntelligencePromptDefaults.WideHierarchyStep) is intentionally
--   left as the byte-identical base; the DB layer is the authoritative runtime
--   prompt and this migration extends it in place.
--
-- Depends on 0436 (WIDE_HIERARCHY_STEP v3.22 base + matter funnel). Idempotent /
-- safe to re-run.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'AI.Legal_PromptDefinition',N'U') IS NOT NULL
BEGIN
	DECLARE @EffectiveFromUtc DATETIME2=SYSUTCDATETIME();
	DECLARE @SystemUserId UNIQUEIDENTIFIER='00000000-0000-0000-0000-000000000000';

	DECLARE @Addendum NVARCHAR(MAX)=N' SIX-FACTOR ANCHORING: the SELECTED MATTER CONTEXT supplied with this batch is organized into six matter factor groups - (1) Decision (the decision/matter record and additional narrative), (2) Legal Scope (jurisdiction, governing law, venue, procedural posture), (3) Personal-Injury Profile (policies, injuries, treatments, bills, damages, liens, demands, settlements, witnesses), (4) Facts (the saved matter facts), (5) Evidence (each per-entry evidence row), and (6) Uploaded-Document Evidence (document summaries, passages, propositions, and their classified evidence-type codes). When you decompose an assigned parent you MUST derive its atomic children from the COMPLETE matter picture across ALL SIX groups that bear on that parent - never only the single richest group; a child must be testable against a concrete value, passage, or named missing item drawn from one of these groups. ANCHORING RULE: every atomic child must set parentBranchCode to the exact assigned parent it belongs to. An EVIDENTIARY child (one established, corroborated, or defeated by a document, record, or evidence item) MUST be anchored to the assigned EVIDENCE TYPE parent (an EVD_ code) that owns that evidence category - never float an evidentiary proposition under an unrelated parent and never invent an evidence type that is not an assigned parent. A NON-EVIDENTIARY legal, jurisdictional, or scope proposition (for example a governing-law or venue predicate from the Legal Scope group) may instead be anchored to the assigned CONCEPT parent (a CPT_ code) it refines when no evidence-type parent applies. GAP RULE: when a factor group that an assigned parent depends on supplies NO saved matter value, still emit the corresponding atomic child, anchor it to that parent, cite no passage, and state the missing item in parentCoverage with status INFORMATION_GAP so the uncovered factor is explicit rather than silently omitted. This anchoring is what links each generated atomic proposition to its Evidence Type in the saved matter decision; preserve it on every child.';

	DECLARE @Base NVARCHAR(MAX)=(SELECT TOP(1) SystemInstructions FROM AI.Legal_PromptDefinition WHERE TenantId IS NULL AND PromptCode=N'WIDE_HIERARCHY_STEP' AND StatusCode=N'APPROVED' AND IsDeleted=0 AND EffectiveFromUtc<=@EffectiveFromUtc AND (EffectiveToUtc IS NULL OR EffectiveToUtc>@EffectiveFromUtc) ORDER BY EffectiveFromUtc DESC);
	DECLARE @CapabilityId UNIQUEIDENTIFIER=(SELECT TOP(1) IntelligenceCapabilityId FROM AI.Legal_PromptDefinition WHERE TenantId IS NULL AND PromptCode=N'WIDE_HIERARCHY_STEP' AND IsDeleted=0 ORDER BY EffectiveFromUtc DESC);
	IF @CapabilityId IS NULL SET @CapabilityId=(SELECT TOP(1) IntelligenceCapabilityId FROM AI.Legal_IntelligenceCapability WHERE TenantId IS NULL AND IsDeleted=0 ORDER BY CASE WHEN CapabilityCode LIKE N'%SEARCH%' THEN 0 ELSE 1 END,SortOrder);

	IF @Base IS NOT NULL AND @CapabilityId IS NOT NULL AND CHARINDEX(N'SIX-FACTOR ANCHORING',@Base)=0
	   AND NOT EXISTS(SELECT 1 FROM AI.Legal_PromptDefinition WHERE TenantId IS NULL AND PromptCode=N'WIDE_HIERARCHY_STEP' AND VersionLabel=N'v3.23' AND IsDeleted=0)
	BEGIN
		UPDATE AI.Legal_PromptDefinition
		SET EffectiveToUtc=@EffectiveFromUtc,ModifiedDateUtc=@EffectiveFromUtc,ModifiedByUserId=@SystemUserId
		WHERE TenantId IS NULL AND PromptCode=N'WIDE_HIERARCHY_STEP' AND StatusCode=N'APPROVED' AND IsDeleted=0
		  AND EffectiveFromUtc<=@EffectiveFromUtc AND (EffectiveToUtc IS NULL OR EffectiveToUtc>@EffectiveFromUtc);

		INSERT AI.Legal_PromptDefinition(TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(NULL,@CapabilityId,N'WIDE_HIERARCHY_STEP',N'v3.23',N'Wide hierarchy step',@Base+@Addendum,N'{}',N'{}',N'APPROVED',@SystemUserId,@EffectiveFromUtc,@EffectiveFromUtc,@EffectiveFromUtc,@SystemUserId,0);
	END;
END;

COMMIT TRANSACTION;
GO
