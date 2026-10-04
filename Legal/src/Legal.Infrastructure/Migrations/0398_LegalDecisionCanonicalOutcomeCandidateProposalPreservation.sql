SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ─────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal: DECISION_DISCOVERY_V2 candidate-proposal PRESERVATION patch (idempotent).
--
-- Purpose: strengthen preservation of LLM-proposed outcome candidates WITHOUT restructuring the
-- prompt, changing the output schema, altering canonical-candidate injection, or weakening the
-- existing advisory-score instructions. Three independent, separately-guarded insertions are appended
-- to the EFFECTIVE runtime prompt. The effective DECISION_DISCOVERY_V2 SystemPrompt is the R2
-- dual-hierarchy contract written WHOLESALE by migration 0320 (later migrations 0321/0323 only patch
-- the output schema and Section 4; they do not touch the anchors below). All anchors are taken from
-- that R2 text — NOT from the superseded 0317 wording.
--
--   • Insertion A — role/temporal preservation of each proposed candidate (R2 §3 candidate anchor).
--   • Insertion B — preserve candidate identity; record gaps in unresolvedPropositions and keep
--                   candidate-to-factor links in candidateBranchRelations (R2 §6 unresolvedPropositions anchor).
--   • Insertion C — advisory score is never a leader/winner/ranking signal (R2 authority-boundaries anchor).
--
-- Actual legal schema names are preserved exactly: unresolvedPropositions, candidateBranchRelations.
-- The Wide schema names are NOT introduced. Each insertion uses its OWN marker phrase so reruns and
-- partially-applied patches can never duplicate or skip an insertion. Every anchor is verified BEFORE
-- the update; a missing anchor fails the migration explicitly (RAISERROR) instead of silently no-oping.
-- ─────────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @PromptCode NVARCHAR(100) = N'DECISION_DISCOVERY_V2';

-- The migration targets exactly one effective prompt row.
IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = @PromptCode)
BEGIN
	RAISERROR(N'0398: DECISION_DISCOVERY_V2 prompt row not found; cannot apply candidate-preservation patch.', 16, 1);
	ROLLBACK TRANSACTION;
	RETURN;
END;

-- ── Anchor + marker definitions ──────────────────────────────────────────────────────────────────
DECLARE @AnchorA NVARCHAR(MAX) = N'Do not mix ultimate claim resolutions, intermediate procedural states, and next operational actions as mutually exclusive answers to one decision.';
DECLARE @InsertA NVARCHAR(MAX) = N' Preserve the semantic role and temporal scope of each proposed candidate. Do not force ultimate resolutions, intermediate dispositions, next actions, or asserted historical outcomes into one mutually exclusive comparison. POLOXI Core governs competition eligibility and evaluation.';
DECLARE @MarkerA NVARCHAR(MAX) = N'%POLOXI Core governs competition eligibility and evaluation.%';

DECLARE @AnchorB NVARCHAR(MAX) = N'Do not invent documents, rulings, holdings, citations, verified facts, or private case records.';
DECLARE @InsertB NVARCHAR(MAX) = N' Preserve every genuinely distinct proposed candidate even when its supporting hierarchy, dependencies, or evidence are incomplete; do not drop or merge a candidate solely because its support is unresolved. Record missing support and verification needs in unresolvedPropositions, and preserve candidate-to-factor links in candidateBranchRelations.';
DECLARE @MarkerB NVARCHAR(MAX) = N'%preserve candidate-to-factor links in candidateBranchRelations.%';

DECLARE @AnchorC NVARCHAR(MAX) = N'Do not return narrative answer prose in place of candidate, outcome-node, dependency, or relationship objects.';
DECLARE @InsertC NVARCHAR(MAX) = N' The advisory candidate score is a proposal-stage signal only; it never establishes a leader, winner, or ranking. When competition eligibility is not satisfied, present candidates as proposed and unscored rather than ranked. POLOXI Core alone produces competition scores and any leading outcome.';
DECLARE @MarkerC NVARCHAR(MAX) = N'%POLOXI Core alone produces competition scores and any leading outcome.%';

DECLARE @Current NVARCHAR(MAX);

-- ── Insertion A ──────────────────────────────────────────────────────────────────────────────────
SELECT @Current = SystemPrompt FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = @PromptCode;
IF @Current NOT LIKE @MarkerA
BEGIN
	IF CHARINDEX(@AnchorA, @Current) = 0
	BEGIN
		RAISERROR(N'0398: Insertion A anchor not found in effective DECISION_DISCOVERY_V2 prompt; aborting.', 16, 1);
		ROLLBACK TRANSACTION;
		RETURN;
	END;

	UPDATE POLOXI.Legal_DecisionPrompt
	SET SystemPrompt = REPLACE(SystemPrompt, @AnchorA, @AnchorA + @InsertA)
	  , ModifiedDateUtc = SYSUTCDATETIME()
	WHERE PromptCode = @PromptCode
	  AND SystemPrompt NOT LIKE @MarkerA;
END;

-- ── Insertion B ──────────────────────────────────────────────────────────────────────────────────
SELECT @Current = SystemPrompt FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = @PromptCode;
IF @Current NOT LIKE @MarkerB
BEGIN
	IF CHARINDEX(@AnchorB, @Current) = 0
	BEGIN
		RAISERROR(N'0398: Insertion B anchor not found in effective DECISION_DISCOVERY_V2 prompt; aborting.', 16, 1);
		ROLLBACK TRANSACTION;
		RETURN;
	END;

	UPDATE POLOXI.Legal_DecisionPrompt
	SET SystemPrompt = REPLACE(SystemPrompt, @AnchorB, @AnchorB + @InsertB)
	  , ModifiedDateUtc = SYSUTCDATETIME()
	WHERE PromptCode = @PromptCode
	  AND SystemPrompt NOT LIKE @MarkerB;
END;

-- ── Insertion C ──────────────────────────────────────────────────────────────────────────────────
SELECT @Current = SystemPrompt FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = @PromptCode;
IF @Current NOT LIKE @MarkerC
BEGIN
	IF CHARINDEX(@AnchorC, @Current) = 0
	BEGIN
		RAISERROR(N'0398: Insertion C anchor not found in effective DECISION_DISCOVERY_V2 prompt; aborting.', 16, 1);
		ROLLBACK TRANSACTION;
		RETURN;
	END;

	UPDATE POLOXI.Legal_DecisionPrompt
	SET SystemPrompt = REPLACE(SystemPrompt, @AnchorC, @AnchorC + @InsertC)
	  , ModifiedDateUtc = SYSUTCDATETIME()
	WHERE PromptCode = @PromptCode
	  AND SystemPrompt NOT LIKE @MarkerC;
END;

-- ── Post-condition: confirm all three insertions are present in the effective runtime prompt. ──────
SELECT @Current = SystemPrompt FROM POLOXI.Legal_DecisionPrompt WHERE PromptCode = @PromptCode;
IF @Current NOT LIKE @MarkerA OR @Current NOT LIKE @MarkerB OR @Current NOT LIKE @MarkerC
BEGIN
	RAISERROR(N'0398: Post-condition failed; one or more candidate-preservation insertions are missing after patch.', 16, 1);
	ROLLBACK TRANSACTION;
	RETURN;
END;

COMMIT TRANSACTION;