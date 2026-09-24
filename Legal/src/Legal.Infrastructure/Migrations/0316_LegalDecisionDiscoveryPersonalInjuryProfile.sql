SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal branch-first discovery (v2): make the Personal Injury Profile a FIRST-CLASS structured
-- input to the proposal stage. Extends DECISION_DISCOVERY_V2 — and ONLY that prompt — so Astra reads the
-- Personal Injury Profile as an explicit part of the immutable Matter Context Snapshot (not free text),
-- and uses it to identify incident-specific legal/factual dependencies (liability, defenses, injury
-- causation, damages, recovery). PI fields are SUPPLIED ALLEGATIONS/CONTEXT, not verified evidence; they
-- indicate propositions that still need investigation. Governing law, jurisdiction, forum, and incident
-- location remain distinct. POLOXI Core still owns ALL scoring, evidence admission, competition, and
-- readiness. Idempotent: uses REPLACE so re-running does not duplicate the added language. Does NOT touch
-- DECISION_DISCOVERY (v1) or the general-purpose POLOXI Wide (/legal/search) engine.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

-- 1) Reference the Personal Injury Profile inside the Matter Context Snapshot input line.
UPDATE POLOXI.Legal_DecisionPrompt
SET SystemPrompt = REPLACE(
		SystemPrompt,
		N'Given the Decision Contract, Matter Context Snapshot, and applicable Legal Domain Pack:',
		N'Given the Decision Contract, Matter Context Snapshot (including the Personal Injury Profile when present), and applicable Legal Domain Pack:')
WHERE PromptCode = N'DECISION_DISCOVERY_V2'
  AND CHARINDEX(N'Given the Decision Contract, Matter Context Snapshot, and applicable Legal Domain Pack:', SystemPrompt) > 0;

-- 2) Add an explicit PI Profile instruction to the SystemPrompt (before the closing summary line).
UPDATE POLOXI.Legal_DecisionPrompt
SET SystemPrompt = REPLACE(
		SystemPrompt,
		N'Produce a SHARED, decision-independent semantic forest and ONE global candidate universe.',
		N'When a Personal Injury Profile is supplied, use its incident type, incident date, incident state/location, liability, injury, and damages fields to identify incident-specific legal and factual dependencies (liability, defenses, injury causation, damages, and recovery considerations). Treat those fields as SUPPLIED ALLEGATIONS/CONTEXT, not independently verified evidence: they tell you which propositions need investigation. Preserve the distinction between governing law, jurisdiction, forum, and incident location, and do not assume any claimed injury, liability, or damage is established.

Produce a SHARED, decision-independent semantic forest and ONE global candidate universe.')
WHERE PromptCode = N'DECISION_DISCOVERY_V2'
  AND CHARINDEX(N'When a Personal Injury Profile is supplied,', SystemPrompt) = 0;

COMMIT TRANSACTION;
