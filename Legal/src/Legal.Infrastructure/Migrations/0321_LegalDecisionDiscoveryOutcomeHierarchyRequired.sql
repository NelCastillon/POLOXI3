SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal: make outcomeProposalHierarchy a MANDATORY, non-empty first-class artifact for
-- DECISION_DISCOVERY_V2 (LEGAL_DECISION / EVALUATE). Idempotent, schema-only.
--
-- Root cause this closes: the R2 prompt REQUIRES the outcome hierarchy, but the strict OutputSchemaJson
-- still declared it as ["object","null"] with an unbounded "nodes" array, so a compliant model could
-- legally emit outcomeProposalHierarchy:null (or nodes:[]) and satisfy the schema. Combined with the
-- shadow-default proposal-integrity gate, a missing hierarchy was silently accepted and zero outcome
-- nodes were persisted. Runtime enforcement is handled separately in LegalDecisionService (the
-- dual-hierarchy contract now always repairs/hard-fails a violation regardless of the recovery flag);
-- this migration removes the schema-level loophole so absence is a schema violation, not a legal value.
--
-- Changes (guarded so the patch applies exactly once):
--   • outcomeProposalHierarchy.type : ["object","null"]  ->  "object"  (may no longer be null)
--   • outcomeProposalHierarchy.nodes : add "minItems":1                 (must contain at least one node)
--
-- Does NOT touch POLOXI Core, scoring, the R2 prompt wording, DECISION_DISCOVERY (v1), or POLOXI Wide.
-- ───────────────────────────────────────────────────────────────────────────────────────────────

UPDATE POLOXI.Legal_DecisionPrompt
SET OutputSchemaJson =
		REPLACE(
			REPLACE(
				OutputSchemaJson,
				N'"outcomeProposalHierarchy":{"type":["object","null"],"additionalProperties":false,"required":["nodes"],"properties":{"nodes":{"type":"array","items"',
				N'"outcomeProposalHierarchy":{"type":"object","additionalProperties":false,"required":["nodes"],"properties":{"nodes":{"type":"array","minItems":1,"items"'),
			-- Also promote outcomeProposalHierarchy into the top-level required list if it is not already there.
			N'"required":["schemaVersion","decisionIntent","queryUnderstanding","semanticRoots","candidates","candidateBranchRelations","unresolvedPropositions","factProvenance","proposalSummary"]',
			N'"required":["schemaVersion","decisionIntent","queryUnderstanding","semanticRoots","candidates","outcomeProposalHierarchy","candidateBranchRelations","unresolvedPropositions","factProvenance","proposalSummary"]')
  , ModifiedDateUtc = SYSUTCDATETIME()
WHERE PromptCode = N'DECISION_DISCOVERY_V2'
  AND OutputSchemaJson LIKE N'%"outcomeProposalHierarchy":{"type":["object","null"]%';

COMMIT TRANSACTION;
