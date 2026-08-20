SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @GlobalProfileId UNIQUEIDENTIFIER=(SELECT TOP(1) MatchProfileId FROM Search.MatchProfile WHERE TenantId IS NULL AND ProfileCode=N'GLOBAL_ENTERPRISE_SEARCH' AND IsDeleted=0 ORDER BY CreatedDateUtc);
DECLARE @SoundexAlgorithmId UNIQUEIDENTIFIER=(SELECT TOP(1) MatchAlgorithmId FROM Search.MatchAlgorithm WHERE TenantId IS NULL AND AlgorithmCode=N'SOUNDEX' AND IsActive=1 AND IsDeleted=0 ORDER BY CreatedDateUtc);

IF @GlobalProfileId IS NOT NULL AND @SoundexAlgorithmId IS NOT NULL
BEGIN
	MERGE Search.MatchFieldRule target
	USING(SELECT @GlobalProfileId MatchProfileId,N'DisplayName' FieldCode,N'Display Name Phonetic' DisplayName,@SoundexAlgorithmId MatchAlgorithmId,CONVERT(DECIMAL(7,4),10) Weight,CONVERT(DECIMAL(5,2),100) MinimumSimilarity,CONVERT(BIT,0) IsRequired,CONVERT(BIT,0) IsCriticalIdentifier,CONVERT(BIT,0) ExactMatchOnly,CONVERT(BIT,0) IsSensitive,15 SortOrder) source
	ON target.TenantId IS NULL AND target.MatchProfileId=source.MatchProfileId AND target.FieldCode=source.FieldCode AND target.MatchAlgorithmId=source.MatchAlgorithmId AND target.IsDeleted=0
	WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,Weight=source.Weight,MinimumSimilarity=source.MinimumSimilarity,IsRequired=source.IsRequired,IsCriticalIdentifier=source.IsCriticalIdentifier,ExactMatchOnly=source.ExactMatchOnly,IsSensitive=source.IsSensitive,SortOrder=source.SortOrder,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(MatchFieldRuleId,TenantId,MatchProfileId,FieldCode,DisplayName,MatchAlgorithmId,Weight,MinimumSimilarity,IsRequired,IsCriticalIdentifier,ExactMatchOnly,IsSensitive,SortOrder,IsActive,CreatedDateUtc,IsDeleted)
	VALUES(NEWID(),NULL,source.MatchProfileId,source.FieldCode,source.DisplayName,source.MatchAlgorithmId,source.Weight,source.MinimumSimilarity,source.IsRequired,source.IsCriticalIdentifier,source.ExactMatchOnly,source.IsSensitive,source.SortOrder,1,SYSUTCDATETIME(),0);
END;
