SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Config TABLE(SettingKey NVARCHAR(200),SettingValue NVARCHAR(2000),DataTypeCode NVARCHAR(50),Description NVARCHAR(1000));
	INSERT @Config VALUES
	(N'Intelligence.Search.KeywordWeight',N'0.25',N'Decimal',N'Unified Intelligence Search keyword score weight.'),
	(N'Intelligence.Search.SemanticWeight',N'0.30',N'Decimal',N'Unified Intelligence Search concept and semantic score weight.'),
	(N'Intelligence.Search.FuzzyWeight',N'0.25',N'Decimal',N'Unified Intelligence Search shared fuzzy matching score weight.'),
	(N'Intelligence.Search.RelationshipWeight',N'0.10',N'Decimal',N'Unified Intelligence Search relationship score weight.'),
	(N'Intelligence.Search.RecencyWeight',N'0.05',N'Decimal',N'Unified Intelligence Search source recency score weight.'),
	(N'Intelligence.Search.BusinessPriorityWeight',N'0.05',N'Decimal',N'Unified Intelligence Search rules-based business priority weight.'),
	(N'Intelligence.Search.RecencyWindowDays',N'365',N'Integer',N'Days over which source recency decays to zero.'),
	(N'Intelligence.Search.MaximumRelationshipResults',N'20',N'Integer',N'Maximum permission-aware related candidates per search.'),
	(N'Intelligence.Search.MinimumUnifiedScore',N'0.05',N'Decimal',N'Minimum unified score returned to the caller.'),
	(N'Intelligence.Search.EnableRules',N'true',N'Boolean',N'Enables constrained search result rules.'),
	(N'Intelligence.Search.EnableRelationships',N'true',N'Boolean',N'Enables permission-aware relationship expansion.'),
	(N'Intelligence.Search.EnableAiSummary',N'true',N'Boolean',N'Allows request-controlled grounded search summaries when an AI route is configured.');
	MERGE Core.ConfigurationSetting target USING @Config source
	  ON ((target.TenantId IS NULL)) AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey
	WHEN MATCHED THEN UPDATE SET ModuleCode=N'Intelligence',DefaultValue=source.SettingValue,DataTypeCode=source.DataTypeCode,Description=source.Description,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,IsDeleted,CreatedDateUtc)
	  VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.SettingValue,source.SettingValue,source.DataTypeCode,source.Description,0,0,0,SYSUTCDATETIME());
END;

IF COL_LENGTH(N'AI.SearchQuery',N'ScoringWeightsJson') IS NULL ALTER TABLE AI.SearchQuery ADD ScoringWeightsJson NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'AI.SearchQuery',N'SummaryStatusCode') IS NULL ALTER TABLE AI.SearchQuery ADD SummaryStatusCode NVARCHAR(30) NULL;
IF COL_LENGTH(N'AI.SearchQuery',N'SummaryExecutionId') IS NULL ALTER TABLE AI.SearchQuery ADD SummaryExecutionId UNIQUEIDENTIFIER NULL;
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_AI_SearchQuery_ScoringWeightsJson') EXEC(N'ALTER TABLE AI.SearchQuery ADD CONSTRAINT CK_AI_SearchQuery_ScoringWeightsJson CHECK(ScoringWeightsJson IS NULL OR ISJSON(ScoringWeightsJson)=1);');

IF OBJECT_ID(N'AI.SearchResultEvidence',N'U') IS NULL
BEGIN
	CREATE TABLE AI.SearchResultEvidence
	(
		SearchResultEvidenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AI_SearchResultEvidence PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		SearchQueryId UNIQUEIDENTIFIER NOT NULL,
		SearchDocumentId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode NVARCHAR(100) NOT NULL,
		EntityId UNIQUEIDENTIFIER NOT NULL,
		RankNumber INT NOT NULL,
		KeywordScore DECIMAL(9,6) NOT NULL,
		SemanticScore DECIMAL(9,6) NOT NULL,
		FuzzyScore DECIMAL(9,6) NOT NULL,
		RelationshipScore DECIMAL(9,6) NOT NULL,
		RecencyScore DECIMAL(9,6) NOT NULL,
		BusinessPriorityScore DECIMAL(9,6) NOT NULL,
		UnifiedScore DECIMAL(9,6) NOT NULL,
		ExplanationsJson NVARCHAR(MAX) NOT NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AI_SearchResultEvidence_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_AI_SearchResultEvidence_Deleted DEFAULT 0,
		CONSTRAINT FK_AI_SearchResultEvidence_Query FOREIGN KEY(SearchQueryId) REFERENCES AI.SearchQuery(SearchQueryId),
		CONSTRAINT FK_AI_SearchResultEvidence_Document FOREIGN KEY(SearchDocumentId) REFERENCES AI.SearchDocument(SearchDocumentId),
		CONSTRAINT CK_AI_SearchResultEvidence_Explanations CHECK(ISJSON(ExplanationsJson)=1)
	);
	CREATE INDEX IX_AI_SearchResultEvidence_Query ON AI.SearchResultEvidence(TenantId,SearchQueryId,RankNumber) INCLUDE(SearchDocumentId,UnifiedScore);
END;

IF OBJECT_ID(N'Rules.RuleDefinition',N'U') IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Rules.RuleDefinition WHERE TenantId IS NULL AND RuleCode=N'INTELLIGENCE_SEARCH.OPEN_WORK_PRIORITY' AND VersionNumber=1 AND IsDeleted=0)
	INSERT Rules.RuleDefinition(RuleDefinitionId,TenantId,RuleCode,DisplayName,Description,RuleCategoryCode,EntityTypeCode,SourceModuleCode,ConditionJson,OutcomeJson,SeverityCode,StopsProcessing,EffectiveFromUtc,EffectiveToUtc,VersionNumber,IsActive,CreatedDateUtc,IsDeleted)
	VALUES(NEWID(),NULL,N'INTELLIGENCE_SEARCH.OPEN_WORK_PRIORITY',N'Open work search priority',N'Provides a small business-priority signal for active or open search records.',N'SEARCH',N'INTELLIGENCE_SEARCH_RESULT',N'Intelligence',N'{"field":"statusCode","operator":"IN","value":["OPEN","ACTIVE","PENDING","IN_REVIEW"]}',N'{"businessPriorityScore":0.75,"explanation":"Active business work received a configured priority signal."}',N'LOW',0,SYSUTCDATETIME(),NULL,1,1,SYSUTCDATETIME(),0);
