-- V3.17 Deliverable Synthesis for the POLOXI Wide pipeline.
-- POLOXI could produce strong grounded reasoning but then fall back to bare interpretive prose
-- for compute/adjudicate queries (for example "calculate the exact payable amount and give the
-- adjustment reason"), because there was no answer kind that captured "decide a specific outcome"
-- and no deterministic final-answer path for the no-candidate case.
--
-- This migration:
--   1) Seeds the new RESOLUTION answer kind (compute/adjudicate/determine a concrete deliverable).
--      RESOLUTION runs the full pipeline (0 depth = default) but does NOT run entity Candidate
--      Competition, so it never collapses into an empty named-entity ranking.
--   2) Adds the DB-backed EnableDeliverableSynthesis gate (default true = new behavior on).
--      Fail-soft: set to false to restore the prior raw-prose fallback.
--   3) Adds DB-backed deliverable indicators used to recognize resolution-like grouped ambiguity
--      branches when an upstream model misclassifies a compute/adjudication request as ranking.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- 1) Seed the RESOLUTION answer kind (idempotent global default).
IF OBJECT_ID(N'POLOXI.AnswerKind',N'U') IS NOT NULL
BEGIN
	MERGE POLOXI.AnswerKind AS target
	USING(VALUES
		(N'RESOLUTION',N'Resolution',N'Compute/adjudicate/decide a specific outcome (an exact amount, an eligibility or approval determination, a classification, an adjustment/denial reason, or a yes/no decision with justification) from supplied facts and rules. Full budgets; entity Candidate Competition is a category error, so the deterministic Deliverable Synthesis stage assembles the final answer.',0,NULL,CAST(0 AS BIT),4)
	)AS source(AnswerKindCode,DisplayName,Description,DepthCeiling,MaxInformationRounds,RunsCandidateCompetition,SortOrder)
	ON target.AnswerKindCode=source.AnswerKindCode AND target.TenantId IS NULL AND target.IsDeleted=0
	WHEN NOT MATCHED THEN INSERT(TenantId,AnswerKindCode,DisplayName,Description,DepthCeiling,MaxInformationRounds,RunsCandidateCompetition,SortOrder)
	VALUES(NULL,source.AnswerKindCode,source.DisplayName,source.Description,source.DepthCeiling,source.MaxInformationRounds,source.RunsCandidateCompetition,source.SortOrder);
END;

-- 2) Seed the EnableDeliverableSynthesis gate (default true, idempotent global default).
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(4000),DataTypeCode NVARCHAR(30),Description NVARCHAR(500));

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.EnableDeliverableSynthesis',N'true',N'Boolean',N'V3.17 Deliverable Synthesis: when true, RESOLUTION answers and no-candidate fallbacks receive a deterministic structured deliverable (determinacy verdict, blocking inputs, best-supported reason, citations) instead of bare interpretive prose. Fail-soft; set to false to restore the raw-prose fallback.'),
		(N'Intelligence.SearchWide.DeliverableSynthesisIndicators',N'compute|calculate|adjudicate|determine|decide|resolve|payable|owed|payment|amount|eligibility|eligible|approval|approved|denial|deny|adjustment|reason|classification|verdict|determination|yes/no',N'DelimitedList',N'V3.17 Deliverable Synthesis: DB-backed indicators for recognizing resolution-like grouped ambiguity branches when the answer kind is misclassified. Pipe-delimited; used only as an eligibility signal and never as evidence.');

	MERGE Core.ConfigurationSetting AS target
	USING @Settings AS source
	   ON target.TenantId IS NULL
	  AND target.ScopeCode=N'Platform'
	  AND target.SettingKey=source.SettingKey
	  AND target.IsDeleted=0
	WHEN MATCHED THEN
		UPDATE SET
			target.ModuleCode=N'Intelligence',
			target.SettingValue=COALESCE(NULLIF(target.SettingValue,N''),source.SettingValue),
			target.DefaultValue=source.SettingValue,
			target.DataTypeCode=source.DataTypeCode,
			target.Description=source.Description,
			target.IsEncrypted=0,
			target.IsReadOnly=0,
			target.ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN
		INSERT
		(
			SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,
			DataTypeCode,Description,IsEncrypted,IsReadOnly,CreatedDateUtc,IsDeleted
		)
		VALUES
		(
			NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,source.SettingValue,
			source.DataTypeCode,source.Description,0,0,SYSUTCDATETIME(),0
		);
END;

COMMIT TRANSACTION;
