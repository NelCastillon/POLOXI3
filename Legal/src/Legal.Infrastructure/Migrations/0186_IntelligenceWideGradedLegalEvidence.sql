-- V3.19 Graded Legal Evidence for the wide POLOXI pipeline.
-- Root cause fixed: the legal retrievers (CourtListener / GovInfo / eCFR / Cornell LII) return no
-- provider relevance score, so every retrieved legal snippet carried Score=0. In ComputeEvidenceSupport
-- the external contribution was Score * PropositionSupport, which is 0 for any legal snippet regardless
-- of how well it supported the branch claim. That collapsed external evidence support to 0, drove
-- EvidenceCoverage / DecisionEvidenceCoverage to 0%, and reduced POLOXI confidence to prior*0.45 (~41%)
-- even when authoritative statutes were admitted. These settings replace the missing provider score with
-- a deterministic, GRADED legal relevance that still honors the mandatory identity + proposition-support
-- admission gates (merely-retrieved sources are NOT counted):
--   LegalRetrievalRelevanceFloor (default .60) - base relevance for an admitted (identity-verified)
--                                                legal snippet; proposition support scales the headroom
--                                                above this floor so on-point sources approach 1.
--   LegalSupportFloor            (default .50) - minimum relevance for any admitted legal snippet so a
--                                                verified authority always counts as evidence.
--   AuthoritativeSourceBonus     (default .15) - promotion for primary law (Statute/Regulation) over
--                                                persuasive case-law snippets.
-- Non-legal (web) snippets are unaffected: they keep their real provider Score. This never lowers the
-- identity/support admission gates - it only weights an already-admitted snippet. Defaults restore graded
-- support rather than the previous binary 0.
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(400),DataTypeCode NVARCHAR(30),Description NVARCHAR(500));

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.LegalRetrievalRelevanceFloor',N'0.60',N'Decimal',N'V3.19 Base relevance for an admitted (identity-verified) legal snippet whose retriever returns no provider score. Proposition support scales the headroom above this floor. Non-legal web snippets keep their real provider score.'),
		(N'Intelligence.SearchWide.LegalSupportFloor',N'0.50',N'Decimal',N'V3.19 Minimum relevance for any admitted legal snippet so a verified authority always counts as evidence instead of contributing zero.'),
		(N'Intelligence.SearchWide.AuthoritativeSourceBonus',N'0.15',N'Decimal',N'V3.19 Relevance bonus promoting authoritative primary law (Statute/Regulation) over persuasive case-law snippets when computing external evidence support.');

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
