-- Phase 1 (VNext): Externalize the information-value criterion weights (previously hardcoded in
-- IntelligenceWideService information-round scoring) into DB-backed platform settings.
-- Defaults preserve the exact prior behavior:
-- raw = clamp(.20*uncertainty + .25*rankingImpact + .25*discrimination + .15*availability + .10*novelty - .05*redundancy, 0, 1).
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(400),DataTypeCode NVARCHAR(30),Description NVARCHAR(500));

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description)
	VALUES
		(N'Intelligence.SearchWide.CriterionUncertaintyWeight',N'0.20',N'Decimal',N'Information-round criterion weight: how unresolved the branch dimension is (LLM categorical judgment).'),
		(N'Intelligence.SearchWide.CriterionRankingImpactWeight',N'0.25',N'Decimal',N'Information-round criterion weight: how likely new evidence changes the final answer ranking.'),
		(N'Intelligence.SearchWide.CriterionDiscriminationWeight',N'0.25',N'Decimal',N'Information-round criterion weight: how well evidence here separates currently close candidates.'),
		(N'Intelligence.SearchWide.CriterionEvidenceAvailabilityWeight',N'0.15',N'Decimal',N'Information-round criterion weight: how likely useful public evidence exists for this target.'),
		(N'Intelligence.SearchWide.CriterionNoveltyWeight',N'0.10',N'Decimal',N'Information-round criterion weight: how different this target is from evidence already retrieved.'),
		(N'Intelligence.SearchWide.CriterionRedundancyPenalty',N'0.05',N'Decimal',N'Information-round criterion penalty: overlap with evidence already retrieved (subtracted from the raw score).');

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
			NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,
			source.SettingValue,source.DataTypeCode,source.Description,0,0,
			SYSUTCDATETIME(),0
		);
END;
