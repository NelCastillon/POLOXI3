-- Separate DB-backed prompt definitions for the gpt-6-astra Wide pipeline variant.
-- The Prompt registry on /legal/configuration is driven by AI.Legal_PromptDefinition
-- (PromptCode grouping via PromptMeta), NOT Core.ConfigurationSetting. To give Astra its own
-- editable prompt set under "Intelligent Search Wide", this migration clones every current
-- APPROVED Wide (Sol) prompt into a new '<CODE>_ASTRA' prompt code, seeding the Astra
-- instructions/schema FROM the current Sol prompt. Purely additive and idempotent:
-- existing '_ASTRA' prompt rows are left untouched so operator edits survive re-runs.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'AI.Legal_PromptDefinition',N'U') IS NOT NULL
BEGIN
	DECLARE @NowUtc DATETIME2=SYSUTCDATETIME();

	-- Astra reasoning-model directive prepended to the FULL Sol specification. This is the
	-- "best fit for Astra" translation: it optimizes how gpt-6-astra consumes the task without
	-- altering any output contract, identity/evidence-support admission gate, branch role,
	-- candidate identity, scoring/narrowing logic, or JSON schema defined by the Sol prompt.
	DECLARE @AstraDirective NVARCHAR(MAX)=
N'[ASTRA REASONING MODEL DIRECTIVE — gpt-6-astra]
You are running as the gpt-6-astra reasoning model. Reason privately and thoroughly before answering; never expose chain-of-thought. Follow the complete specification below EXACTLY as written — every output contract, JSON schema, identity and proposition-support admission gate, branch role (HARD_CONSTRAINT / GUARDRAIL / PREFERENCE / CONTEXT), semanticType rule, calibrated-confidence requirement, candidate-identity rule, and narrowing/stop logic remains fully authoritative and must not be relaxed, reordered, or reinterpreted. The directive only changes HOW you reason, not WHAT you must produce:
- Think step by step internally, then emit only the required final output. Do not add commentary, preamble, or extra keys.
- When the specification requires JSON, return strictly valid JSON matching the schema with no markdown fences or trailing prose.
- Prefer precision and calibration over verbosity; differentiate confidences and never default them.
- If evidence is missing or unverifiable, obey the specification''s gates rather than inventing support; never claim records exist and never fabricate citations, authorities, or SQL.
- Resolve ambiguity using the fixed Query Contract and each branch''s semantic function exactly as specified below.
--- BEGIN CANONICAL SPECIFICATION ---
';

	-- Latest APPROVED version per Sol Wide prompt code (the row the engine currently uses).
	;WITH SolWidePrompts AS
	(
		SELECT prompt.*,
			ROW_NUMBER() OVER
			(
				PARTITION BY prompt.PromptCode
				ORDER BY CASE WHEN prompt.EffectiveToUtc IS NULL THEN 0 ELSE 1 END,
						 prompt.EffectiveFromUtc DESC,prompt.CreatedDateUtc DESC
			) rn
		FROM AI.Legal_PromptDefinition prompt
		WHERE prompt.TenantId IS NULL
		  AND prompt.IsDeleted=0
		  AND prompt.StatusCode=N'APPROVED'
		  AND prompt.PromptCode LIKE N'WIDE[_]%'
		  AND prompt.PromptCode NOT LIKE N'%[_]ASTRA'
	)
	INSERT AI.Legal_PromptDefinition
	(
		PromptDefinitionId,TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,
		SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,
		EffectiveFromUtc,EffectiveToUtc,CreatedDateUtc,CreatedByUserId,IsDeleted
	)
	SELECT
		NEWID(),
		NULL,
		sol.IntelligenceCapabilityId,
		sol.PromptCode + N'_ASTRA',
		sol.VersionLabel,
		sol.DisplayName + N' (Astra)',
		@AstraDirective + sol.SystemInstructions,
		sol.InputSchemaJson,
		sol.OutputSchemaJson,
		sol.StatusCode,
		sol.ApprovedByUserId,
		sol.ApprovedDateUtc,
		@NowUtc,
		sol.EffectiveToUtc,
		@NowUtc,
		sol.CreatedByUserId,
		0
	FROM SolWidePrompts sol
	WHERE sol.rn=1
	  AND NOT EXISTS
	  (
		SELECT 1 FROM AI.Legal_PromptDefinition astra
		WHERE astra.TenantId IS NULL
		  AND astra.IsDeleted=0
		  AND astra.PromptCode=sol.PromptCode + N'_ASTRA'
	  );
END;

COMMIT TRANSACTION;
