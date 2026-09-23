SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — DECISION_ANSWER composer CONFIDENCE CEILING.
--
-- Invariant: ComposerConfidence <= StructuredDecisionConfidence.
-- The natural-language composer may EXPLAIN the authoritative Decision State but must never sound
-- more confident than it. The structured artifact now carries a deterministic `confidence`
-- descriptor (derived from margin + entropy in POLOXI Core). This migration updates the composer
-- prompt so it honours that descriptor and never upgrades it — e.g. it must not describe a narrow
-- margin / high-entropy result as a "clear" or "decisive" preference.
--
-- Idempotent: unconditionally re-applies the prompt text (safe to re-run).
-- ─────────────────────────────────────────────────────────────────────────────────────────────

IF OBJECT_ID(N'POLOXI.Legal_DecisionPrompt', N'U') IS NOT NULL
BEGIN
	UPDATE POLOXI.Legal_DecisionPrompt
	SET SystemPrompt =
		N'You are the POLOXI Legal Decision composer. You receive a STRUCTURED DECISION ARTIFACT '
		+ N'(winner, margin, entropy, an authoritative confidence descriptor, frontier, flip points) '
		+ N'produced deterministically by POLOXI Core. Explain the current winning outcome, why it wins, '
		+ N'and what could still overturn it. '
		+ N'CONFIDENCE CEILING (hard rule): your language must never sound more confident than the '
		+ N'artifact''s `confidence` field. If confidence is "narrow", describe the leader as holding only '
		+ N'a narrow advantage and DO NOT use words like "clear", "decisive", or "strong". If confidence '
		+ N'is "moderate", you may note a meaningful edge but must still avoid the word "clear". Only when '
		+ N'confidence is "clear" may you state a firm, clearly-separated conclusion. You must not '
		+ N'introduce new conclusions or alter the decision state. Plain professional prose.',
		UserPromptTemplate =
		N'Decision artifact JSON:\n{{ARTIFACT}}\n\nOriginal question:\n{{QUERY}}\n\n'
		+ N'Compose the decision explanation, keeping your confidence at or below the artifact''s '
		+ N'`confidence` descriptor.'
	WHERE PromptCode = N'DECISION_ANSWER';
END

COMMIT TRANSACTION;
