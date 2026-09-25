SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ───────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal: Atomic L3 Factor Propositions for DECISION_DISCOVERY_V2, idempotent, prompt-only.
--
-- Root cause this closes: the R2 dual-hierarchy prompt (migration 0320) asks Section 4 (semanticRoots)
-- for "major shared decision dependencies" (L1) and "substantive subdependencies" (L2), but it never
-- REQUIRES the model to decompose each material L2 dimension into ATOMIC, INDEPENDENTLY TESTABLE L3
-- factor propositions. As a result the model stops at broad evaluation dimensions (for example
-- "Liability", "Damages", "Settlement enforceability"), and the shared-dependency normalizer registers
-- those broad dimensions as the Universal Factor Inventory factors. That is the Mendoza/Harper defect:
-- a handful of broad dimensions become "factors" instead of the concrete legal/factual questions that
-- actually distinguish the candidates.
--
-- Fix: additively strengthen Section 4 so the shared dependency forest ALWAYS carries L3 atomic factor
-- propositions under each material L2 dimension whenever the decision turns on more than one concrete
-- question, with an explicit ATOMIC-FACTOR RULE and worked examples. The parser (AdaptSemanticBranch),
-- the shared-dependency normalizer (NormalizeSharedDependencies), and the schema ($defs/branch already
-- recurses "children" to arbitrary depth) ALREADY support L3+ — this migration only closes the prompt
-- instruction gap so the model actually emits it.
--
-- POLOXI Core, the Light Evidence Graph, the Typed Legal Dependency Graph, all scoring formulas, the
-- OutputSchemaJson shape, DECISION_DISCOVERY (v1), and POLOXI Wide are UNCHANGED. No new engine, no new
-- LLM call, no new schema section. Only the DECISION_DISCOVERY_V2 SystemPrompt is patched, once.
--
-- Idempotent: guarded by a unique marker ("ATOMIC-FACTOR RULE") so re-running is a no-op, and anchored
-- on stable R2 Section 4 text so it only applies to the R2 dual-hierarchy prompt.
-- ───────────────────────────────────────────────────────────────────────────────────────────────

-- ── Additively insert the atomic-L3 guidance + ATOMIC-FACTOR RULE immediately after the existing
--    Section 4 depth sentence ("Use L1 for major shared decision dependencies ..."). One guarded
--    UPDATE; the whole patch applies exactly once. ──
UPDATE POLOXI.Legal_DecisionPrompt
SET SystemPrompt = REPLACE(
		SystemPrompt,
		N'Use L1 for major shared decision dependencies, L2 for substantive subdependencies, and L3+ when further decomposition is justified.',
		N'Use L1 for major shared decision dependencies, L2 for substantive subdependencies, and L3+ when further decomposition is justified.

Do not stop at broad evaluation dimensions. A broad dimension such as "Liability", "Damages", "Settlement enforceability", "Coverage", or "Procedural posture" is a GROUPING (L1/L2), never itself an atomic factor. Whenever the decision turns on more than one concrete question inside a dimension, decompose that dimension into ATOMIC, INDEPENDENTLY TESTABLE L3 factor propositions.

ATOMIC-FACTOR RULE

An L3 factor node must be a single, concrete, independently resolvable legal or factual proposition — one that could be answered true, false, or unresolved by specific evidence or a specific legal authority, without first splitting it into further questions.

- Each L3 node states ONE testable question (its semanticQuestion) and ONE interpretation of why resolving it changes candidate support (its whyMaterial).
- Do NOT register a broad dimension label as a leaf factor. If a dimension has only one genuine underlying question, that single L3 question — not the dimension heading — is the factor.
- Do NOT use outcome names, candidate names, candidate categories, decision-frame instructions, or generic answer headings as L3 factors (this remains governed by the SHARED-DEPENDENCY EXCLUSION RULE).
- Propose each atomic factor ONCE and connect every relevant candidate to its stable branchId through candidateBranchRelations; never duplicate the same atomic factor under different dimensions or candidates.
- You may propose the relationType and rationale for a factor, but you must never mark a factor as verified, satisfied, or established; grounding, admission, and Branch x Candidate scoring are performed later by POLOXI Core.

For example, under a "Settlement enforceability" L2 dimension, do NOT return "Settlement enforceability" as the factor. Return atomic L3 factors such as: "Was a definite offer communicated and unequivocally accepted?"; "Were all material terms (amount, releases, payors) agreed?"; "Was acceptance timely under the applicable deadline or statute?"; "Is the agreement barred by any statute of frauds or writing requirement?". Under a "Damages" L2 dimension, return atomic L3 factors such as: "Are the claimed economic damages (medical, wage-loss) documented?"; "Do the injuries meet the applicable threshold?"; rather than the single word "Damages".'),
	ModifiedDateUtc = SYSUTCDATETIME()
WHERE PromptCode = N'DECISION_DISCOVERY_V2'
  AND SystemPrompt LIKE N'%produce TWO DISTINCT semantic hierarchies%'
  AND SystemPrompt NOT LIKE N'%ATOMIC-FACTOR RULE%';

COMMIT TRANSACTION;
