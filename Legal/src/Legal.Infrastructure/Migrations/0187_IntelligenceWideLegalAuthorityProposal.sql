-- V3.20 Legal Authority Proposal feature flag for the wide POLOXI pipeline.
-- Root cause fixed: external retrieval only ever derived authorities from citations already present in
-- a branch's text (ExtractLegalAuthorities regex) or resolved by the DB concept map (ResolveConceptAuthorities).
-- The model's own proposed authorities were produced only in the final composer stage (AFTER retrieval)
-- and used solely to validate/echo already-verified authorities, never fed back into retrieval. As a
-- result, interpretive / result-analysis legal branches that named no citation and were not covered by
-- the concept map retrieved ZERO external sources, so those branches surfaced no evidence at all.
-- This setting enables a single fail-soft LLM call (WIDE_LEGAL_AUTHORITY_PROPOSAL prompt) that proposes
-- the primary authorities most likely to govern the question. Those authorities are used ONLY as
-- retrieval LEADS for legal branches lacking a cited/concept-resolved authority. Each proposed authority
-- is re-classified with the same deterministic regexes and routed through the UNCHANGED mandatory
-- identity + proposition-support admission gates (RetrieveStampedAsync verify path), so an unverifiable
-- or hallucinated authority simply fails verification and never becomes evidence. This is a
-- retrieval-coverage change only; it does not alter what counts as evidence or any scoring.
--   EnableLegalAuthorityProposal (default true) - master switch for the authority-proposal retrieval bridge.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(400),DataTypeCode NVARCHAR(30),Description NVARCHAR(500));

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.EnableLegalAuthorityProposal',N'true',N'Boolean',N'V3.20 When enabled, a single fail-soft LLM call proposes primary legal authorities that are used only as retrieval leads for legal branches lacking a cited or concept-resolved authority. Proposed authorities still pass the unchanged identity + proposition-support admission gates, so unverifiable authorities never become evidence.');

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
END
