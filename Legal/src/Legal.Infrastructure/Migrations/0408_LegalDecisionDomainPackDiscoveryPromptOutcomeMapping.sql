SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ──────────────────────────────────────────────────────────────────────────────────────────────
-- DECISION_DISCOVERY_DOMAIN_PACK_V2 (idempotent).
--
-- Adds inline SEMANTIC outcome-to-canonical mapping to the Domain Pack discovery prefix. For every
-- outcome the model proposes, it also emits an advisory mappedOutcomeCodes[] array drawn ONLY from
-- the SELECTED_DOMAIN_PACK OutcomeCandidates (the canonical C1–C9 for Personal Injury), chosen by
-- legal MEANING (Name + Description + RoleCode), never by surface keywords, empty when none fit,
-- never invented. The mapping is advisory/presentation + routing ONLY: it never eliminates, selects,
-- scores, ranks, or establishes eligibility, and never overrides POLOXI Core competition,
-- normalization, or the canonical verification gates (asserted-historical / factual-predicate remain
-- owned by the platform via IsCanonicalOutcomeEligible). Every proposed outcome is preserved
-- regardless of mapping — an unmapped outcome stays a first-class candidate.
--
-- To guarantee V2 never diverges from V1, this migration DERIVES V2's text from the stored V1 row
-- (copying SystemPrompt verbatim and inserting the mapping section into a verbatim copy of V1's
-- UserPromptTemplate immediately before its closing ORIGINAL_PROMPT paragraph). The original
-- DECISION_DISCOVERY / DECISION_DISCOVERY_V2 prompt is NOT touched. POLOXI Core scoring, competition,
-- normalization, output schema, and canonical outcome injection are all unchanged.
-- ──────────────────────────────────────────────────────────────────────────────────────────────

-- ── Source V1 text (must exist from migration 0406) ──────────────────────────────────────────────
DECLARE @V1System   NVARCHAR(MAX) =
	(SELECT TOP 1 SystemPrompt        FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = N'DECISION_DISCOVERY_DOMAIN_PACK_V1');
DECLARE @V1Template NVARCHAR(MAX) =
	(SELECT TOP 1 UserPromptTemplate  FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = N'DECISION_DISCOVERY_DOMAIN_PACK_V1');
DECLARE @V1Stage    NVARCHAR(100)  =
	(SELECT TOP 1 StageCode           FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = N'DECISION_DISCOVERY_DOMAIN_PACK_V1');

IF @V1Template IS NOT NULL
BEGIN
	-- ── Mapping section (inserted before V1's closing ORIGINAL_PROMPT paragraph) ──────────────────
	DECLARE @MappingSection NVARCHAR(MAX) = N'OUTCOME-TO-CANONICAL SEMANTIC MAPPING

For every outcome candidate you propose, add an advisory semantic mapping to
the canonical OutcomeCandidates supplied in SELECTED_DOMAIN_PACK.

Emit, per proposed outcome, a field named mappedOutcomeCodes: an array of zero
or more OutcomeCode values drawn ONLY from the SELECTED_DOMAIN_PACK
OutcomeCandidates list. Choose by MEANING — the candidate''s Name, Description,
and RoleCode — not by surface keywords.

Rules:
- Use only codes that appear in SELECTED_DOMAIN_PACK.OutcomeCandidates. Never
  invent, abbreviate, or alter a code.
- An outcome may map to several canonical codes when it genuinely spans them,
  or to none. If no canonical candidate fits, return an empty array. An empty
  mapping is correct and is preferred over a forced or approximate match.
- Map by legal CATEGORY, not lexical similarity. In particular distinguish:
  a merits-based defense judgment or dismissal; a non-merits procedural or
  jurisdictional bar / dismissal; and a plaintiff-initiated voluntary dismissal
  or withdrawal are DIFFERENT canonical outcomes even though each reads as a
  "dismissal". Map each to the canonical code whose Description matches its
  actual legal character.
- This mapping is advisory and for presentation/routing ONLY. It does NOT
  eliminate, select, score, rank, or establish eligibility for any candidate,
  and it never overrides POLOXI Core competition, normalization, or the
  canonical verification obligations. Asserted-historical and factual-predicate
  gates remain owned by the platform; a mapping never satisfies them.
- Preserve every proposed outcome regardless of mapping. An unmapped outcome
  remains a first-class candidate; never drop it.

';

	-- Anchor: the closing paragraph that V1 ends with. Insert the mapping section immediately before it
	-- so the mapping instruction sits inside the pack prefix, ahead of the ORIGINAL_PROMPT handoff.
	DECLARE @Anchor NVARCHAR(MAX) = N'The ORIGINAL_PROMPT (exact stored text), the SELECTED_DOMAIN_PACK';

	DECLARE @V2Template NVARCHAR(MAX) =
		CASE
			WHEN CHARINDEX(@Anchor, @V1Template) > 0
				THEN REPLACE(@V1Template, @Anchor, @MappingSection + @Anchor)
			ELSE @V1Template + NCHAR(10) + NCHAR(10) + @MappingSection
		END;

	-- ── Upsert V2 (idempotent; refresh text on re-run so it tracks any V1 edits) ──────────────────
	MERGE POLOXI.Legal_DecisionPrompt AS target
	USING (VALUES
		(N'DECISION_DISCOVERY_DOMAIN_PACK_V2', @V1Stage, @V1System, @V2Template, CAST(NULL AS NVARCHAR(MAX)))
	) AS source (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
	ON target.PromptCode = source.PromptCode
	WHEN MATCHED THEN
		UPDATE SET
			StageCode          = source.StageCode,
			SystemPrompt       = source.SystemPrompt,
			UserPromptTemplate = source.UserPromptTemplate,
			OutputSchemaJson   = source.OutputSchemaJson
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson)
		VALUES (source.PromptCode, source.StageCode, source.SystemPrompt, source.UserPromptTemplate, source.OutputSchemaJson);
END

COMMIT TRANSACTION;
