SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'POLOXI') EXEC(N'CREATE SCHEMA POLOXI');

-- Database-backed search context lookup for the Wide search "Context" dropdown.
-- GENERAL keeps the existing pipeline behavior; LEGAL routes external grounding through
-- legal sources (CourtListener case law + GovInfo/eCFR statutes and regulations).
IF OBJECT_ID(N'POLOXI.Legal_SearchContext',N'U') IS NULL
BEGIN
	CREATE TABLE POLOXI.Legal_SearchContext
	(
		SearchContextId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_SearchContext PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NULL,
		ContextCode NVARCHAR(50) NOT NULL,
		DisplayName NVARCHAR(120) NOT NULL,
		Description NVARCHAR(500) NULL,
		IsDefault BIT NOT NULL CONSTRAINT DF_Legal_SearchContext_IsDefault DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_Legal_SearchContext_IsActive DEFAULT 1,
		SortOrder INT NOT NULL CONSTRAINT DF_Legal_SearchContext_SortOrder DEFAULT 0,
		CreatedDateUtc DATETIME2(3) NOT NULL CONSTRAINT DF_Legal_SearchContext_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2(3) NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_Legal_SearchContext_Deleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_Legal_SearchContext_TenantCode ON POLOXI.Legal_SearchContext(TenantId,ContextCode) WHERE IsDeleted=0;
END;

-- Seed the two platform (tenant-null) context values.
MERGE POLOXI.Legal_SearchContext AS target
USING (VALUES
	(N'GENERAL',N'General',N'Standard Wide search. External grounding uses the configured general web provider.',1,1,1),
	(N'LEGAL',N'Legal',N'Legal research context. External grounding uses CourtListener case law and GovInfo/eCFR statutes and regulations.',0,1,2)
) AS source(ContextCode,DisplayName,Description,IsDefault,IsActive,SortOrder)
   ON target.TenantId IS NULL AND target.ContextCode=source.ContextCode AND target.IsDeleted=0
WHEN MATCHED THEN
	UPDATE SET
		target.DisplayName=source.DisplayName,
		target.Description=source.Description,
		target.IsDefault=source.IsDefault,
		target.IsActive=source.IsActive,
		target.SortOrder=source.SortOrder,
		target.ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN
	INSERT(SearchContextId,TenantId,ContextCode,DisplayName,Description,IsDefault,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
	VALUES(NEWID(),NULL,source.ContextCode,source.DisplayName,source.Description,source.IsDefault,source.IsActive,source.SortOrder,SYSUTCDATETIME(),0);

-- Legal external-grounding configuration (Platform scope, tenant-overridable). API keys are supplied
-- via configuration/DB, never hardcoded; a blank ApiKey or Enabled=false disables that source.
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
		(N'Intelligence.SearchWide.LegalGrounding.Enabled',N'true',N'Boolean',N'Enables legal-source external grounding when the Legal context is selected.',0),
		(N'Intelligence.SearchWide.LegalGrounding.MaximumQueriesPerExecution',N'3',N'Integer',N'Maximum legal-source search queries per Wide execution (cost circuit breaker).',0),
		(N'Intelligence.SearchWide.LegalGrounding.MaximumSnippetsPerQuery',N'5',N'Integer',N'Maximum snippets retained per legal-source search query.',0),
		(N'Intelligence.SearchWide.LegalGrounding.CacheHours',N'24',N'Integer',N'Hours a cached legal knowledge snippet remains fresh before re-retrieval.',0),
		(N'Intelligence.SearchWide.LegalGrounding.TimeoutSeconds',N'15',N'Integer',N'HTTP timeout in seconds for legal-source provider calls.',0),
		-- CourtListener (case law)
		(N'Intelligence.SearchWide.LegalGrounding.CourtListener.Enabled',N'true',N'Boolean',N'Enables CourtListener case-law retrieval for the Legal context.',0),
		(N'Intelligence.SearchWide.LegalGrounding.CourtListener.BaseUrl',N'https://www.courtlistener.com',N'String',N'CourtListener API base URL.',0),
		(N'Intelligence.SearchWide.LegalGrounding.CourtListener.ApiToken',N'',N'String',N'CourtListener API token (optional; anonymous access is rate-limited).',1),
		-- GovInfo (statutes/regulations) + eCFR (current regulation text)
		(N'Intelligence.SearchWide.LegalGrounding.GovInfo.Enabled',N'true',N'Boolean',N'Enables GovInfo/eCFR statutory and regulatory retrieval for the Legal context.',0),
		(N'Intelligence.SearchWide.LegalGrounding.GovInfo.BaseUrl',N'https://api.govinfo.gov',N'String',N'GovInfo API base URL.',0),
		(N'Intelligence.SearchWide.LegalGrounding.GovInfo.ApiKey',N'',N'String',N'GovInfo api.data.gov key.',1),
		(N'Intelligence.SearchWide.LegalGrounding.Ecfr.BaseUrl',N'https://www.ecfr.gov',N'String',N'eCFR API base URL for current regulation text.',0);

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
