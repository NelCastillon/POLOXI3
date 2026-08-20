SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'EPH') EXEC(N'CREATE SCHEMA EPH');

-- Cached live external knowledge snippets used to ground time-sensitive interpretive
-- results in the Wide search pipeline (retrieved via the configured provider, e.g. Tavily).
IF OBJECT_ID(N'EPH.ExternalKnowledge',N'U') IS NULL
BEGIN
	CREATE TABLE EPH.ExternalKnowledge
	(
		ExternalKnowledgeId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EphExternalKnowledge PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		NormalizedQuery NVARCHAR(400) NOT NULL,
		Title NVARCHAR(500) NOT NULL,
		Url NVARCHAR(2000) NOT NULL,
		Snippet NVARCHAR(MAX) NOT NULL,
		Score DECIMAL(5,4) NOT NULL CONSTRAINT DF_EphExternalKnowledge_Score DEFAULT 0,
		RetrievedDateUtc DATETIME2(3) NOT NULL,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_EphExternalKnowledge_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_EphExternalKnowledge_Deleted DEFAULT 0
	);
	CREATE INDEX IX_EphExternalKnowledge_TenantQuery ON EPH.ExternalKnowledge(TenantId,NormalizedQuery,RetrievedDateUtc DESC) WHERE IsDeleted=0;
END;

-- External grounding configuration settings (Platform scope, tenant-overridable).
IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Settings TABLE
	(
		SettingKey NVARCHAR(200) NOT NULL,
		SettingValue NVARCHAR(2000) NOT NULL,
		DataTypeCode NVARCHAR(50) NOT NULL,
		Description NVARCHAR(1000) NOT NULL,
		IsEncrypted BIT NOT NULL
	);

	INSERT @Settings(SettingKey,SettingValue,DataTypeCode,Description,IsEncrypted)
	VALUES
		(N'Intelligence.SearchWide.ExternalGrounding.Enabled',N'false',N'Boolean',N'Enables live external web grounding for time-sensitive Wide interpretive results.',0),
		(N'Intelligence.SearchWide.ExternalGrounding.ProviderCode',N'TAVILY',N'String',N'External knowledge provider code used for Wide grounding (e.g. TAVILY).',0),
		(N'Intelligence.SearchWide.ExternalGrounding.ApiKey',N'',N'String',N'API key for the configured external knowledge provider.',1),
		(N'Intelligence.SearchWide.ExternalGrounding.MaximumQueriesPerExecution',N'3',N'Integer',N'Maximum external search queries per Wide execution (cost circuit breaker).',0),
		(N'Intelligence.SearchWide.ExternalGrounding.MaximumSnippetsPerQuery',N'5',N'Integer',N'Maximum snippets retained per external search query.',0),
		(N'Intelligence.SearchWide.ExternalGrounding.CacheHours',N'24',N'Integer',N'Hours a cached external knowledge snippet remains fresh before re-retrieval.',0),
		(N'Intelligence.SearchWide.ExternalGrounding.TimeoutSeconds',N'10',N'Integer',N'HTTP timeout in seconds for external knowledge provider calls.',0);

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
			target.IsEncrypted=source.IsEncrypted,
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
			source.SettingValue,source.DataTypeCode,source.Description,source.IsEncrypted,0,
			SYSUTCDATETIME(),0
		);
END;

COMMIT TRANSACTION;
