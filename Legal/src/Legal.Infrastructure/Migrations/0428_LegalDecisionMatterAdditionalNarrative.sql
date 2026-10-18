SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────────
-- Matter.AdditionalNarrative (idempotent).
--
-- Persists the free-text prompt the user submits from a matter decision cockpit (e.g.
-- /legal/personalinjury_decision2) onto the Matter BEFORE any LLM prompt executes. The value is
-- SUPPLIED user context (not verified evidence) and is projected into the Matter Context Snapshot so
-- every authorized-context consumer sees it. The persisted DECISION_DISCOVERY_DOMAIN_PACK_V4 prompt's
-- FULL CONTEXT BOUNDARY is extended to name it explicitly. No other prompt rows are touched.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────────

IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'AdditionalNarrative') IS NULL
BEGIN
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD AdditionalNarrative NVARCHAR(MAX) NULL;
END;

-- The stored row may use LF, CRLF, or CR CR LF line endings (0427 re-expanded an already-CRLF
-- literal). Detect the separator actually present so the anchor matches instead of silently no-oping.
DECLARE @Template NVARCHAR(MAX) =
	(SELECT TOP 1 UserPromptTemplate FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = N'DECISION_DISCOVERY_DOMAIN_PACK_V4');
DECLARE @Sep NVARCHAR(3) =
	CASE
		WHEN CHARINDEX(NCHAR(13) + NCHAR(13) + NCHAR(10), @Template) > 0 THEN NCHAR(13) + NCHAR(13) + NCHAR(10)
		WHEN CHARINDEX(NCHAR(13) + NCHAR(10), @Template) > 0 THEN NCHAR(13) + NCHAR(10)
		ELSE NCHAR(10)
	END;

DECLARE @Old NVARCHAR(400) =
	N'matter' + @Sep +
	N'description, narrative and incident information;';
DECLARE @New NVARCHAR(400) =
	N'matter' + @Sep +
	N'description, narrative and incident information; the user-supplied' + @Sep +
	N'Additional Narrative saved on the matter for this execution (SUPPLIED' + @Sep +
	N'context, not verified evidence);';

UPDATE POLOXI.Legal_DecisionPrompt
SET UserPromptTemplate = REPLACE(UserPromptTemplate, @Old, @New)
WHERE PromptCode = N'DECISION_DISCOVERY_DOMAIN_PACK_V4'
  AND CHARINDEX(@Old, UserPromptTemplate) > 0
  AND CHARINDEX(N'Additional Narrative', UserPromptTemplate) = 0;

COMMIT TRANSACTION;
