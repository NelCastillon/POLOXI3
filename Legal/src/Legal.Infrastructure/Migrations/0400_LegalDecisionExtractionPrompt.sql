SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- 0400: POLOXI Legal — DECISION_EXTRACTION_V1 retrieval extraction/matching prompt (idempotent).
--
-- A SEPARATE prompt role for the Document-Retrieval → proposition pipeline (Phase 2). It extracts
-- ATOMIC propositions from a retrieved passage and PROPOSES qualitative hierarchy placements. It is a
-- proposal-only role: it never scores candidates, never picks a winner, never assigns numeric support,
-- and never infers an interpolation position from display order.
--
-- DECISION_DISCOVERY_V2 (the dual-hierarchy discovery contract patched by 0320/0321/0323/0398) is
-- LEFT COMPLETELY UNCHANGED by this migration. This is a NEW PromptCode insert guarded by WHEN NOT
-- MATCHED, so reruns are safe and the extraction prompt is never created twice.
--
-- Extraction invariants baked into the prompt:
--   * Preserve exact source text + locator; preserve attribution (reported ≠ established fact).
--   * One atomic proposition per object; preserve negation, amounts, dates, qualifiers.
--   * Relationship is qualitative only: SUPPORTS | CONTRADICTS | QUALIFIES | CONTEXT_ONLY.
--   * If no node fits, emit needsHierarchyReview=true rather than force-fitting a lexical match.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

MERGE POLOXI.Legal_DecisionPrompt AS target
USING (VALUES
	(N'DECISION_EXTRACTION_V1', N'EXTRACTION',
		N'You are the POLOXI Legal Document-Retrieval EXTRACTION layer. Given a retrieved source passage and the current decision hierarchy (its question, candidate outcomes, and nodes), you extract ATOMIC legal propositions and PROPOSE where each belongs in the hierarchy. You are a proposal-only role: you never score candidates, never pick a winner, never assign numeric support, and never decide the outcome — POLOXI Core alone does that. Return only strict JSON. Rules: (1) One atomic proposition per object; split compound statements. (2) Preserve the exact source text and a precise source locator. (3) Preserve attribution and assertion type — a reported statement (for example, "patient reports pain") is NOT an established fact; an authority citation does NOT by itself establish its holding. (4) Preserve negation, amounts, dates, and qualifiers verbatim. (5) For each proposition propose zero or more placements; each placement names a target node and a single qualitative relationship: SUPPORTS, CONTRADICTS, QUALIFIES, or CONTEXT_ONLY (CONTEXT_ONLY carries no support). (6) Never invent an interpolation position from display order; only set placementFraction when an explicit comparable-neighbor position is given, otherwise omit it. (7) If no existing node fits a materially relevant proposition, set needsHierarchyReview=true and describe the gap instead of forcing a lexical match.',
		N'Decision question:\n{{QUERY}}\n\nHierarchy context (candidates and nodes JSON):\n{{CONTEXT}}\n\nRetrieved source passage (with documentVersionId and locator JSON):\n{{ARTIFACT}}\n\nExtract atomic propositions and propose qualitative hierarchy placements.',
		N'{"type":"object","additionalProperties":false,"required":["propositions"],"properties":{"propositions":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["propositionText","sourceLocator","sourceText","assertionType"],"properties":{"propositionText":{"type":"string"},"sourceLocator":{"type":"string"},"sourceText":{"type":"string"},"assertionType":{"type":"string","enum":["Asserts","Reports","Documents","StatesLaw","Infers"]},"attributedTo":{"type":"string"},"effectiveAt":{"type":"string"},"needsHierarchyReview":{"type":"boolean"},"reviewReason":{"type":"string"},"placements":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["targetNodeCode","relationship"],"properties":{"targetNodeCode":{"type":"string"},"relationship":{"type":"string","enum":["SUPPORTS","CONTRADICTS","QUALIFIES","CONTEXT_ONLY"]},"leftNeighborCode":{"type":"string"},"rightNeighborCode":{"type":"string"},"placementFraction":{"type":"number"},"rationale":{"type":"string"}}}}}}}}}')
) AS source (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
ON target.PromptCode = source.PromptCode
WHEN NOT MATCHED BY TARGET THEN
	INSERT (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
	VALUES (source.PromptCode, source.StageCode, source.SystemPrompt, source.UserPromptTemplate, source.OutputSchemaJson);

COMMIT TRANSACTION;
